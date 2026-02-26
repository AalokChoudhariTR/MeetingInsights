using MeetingInsights.Core.Models;
using MeetingInsights.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace MeetingInsights.Core.Services
{
    /// <summary>
    /// Service for analyzing sentiment across meetings
    /// </summary>
    public class SentimentService : ISentimentService
    {
        private readonly ISnowflakeService _snowflakeService;
        private readonly ILogger<SentimentService> _logger;

        public SentimentService(
            ISnowflakeService snowflakeService,
            ILogger<SentimentService> logger)
        {
            _snowflakeService = snowflakeService;
            _logger = logger;
        }

        /// <summary>
        /// Get overall team sentiment across all meetings
        /// </summary>
        public async Task<TeamSentimentResponse> GetTeamSentimentAsync(string teamId)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new TeamSentimentResponse { TeamId = teamId, Success = true };

            try
            {
                _logger.LogInformation("Analyzing team sentiment for {TeamId}", teamId);

                // Step 1: Get all meetings
                var meetings = await _snowflakeService.GetMeetingsByTeamAsync(teamId);
                meetings = meetings.OrderBy(m => m.MeetingDate).ToList();

                if (meetings.Count == 0)
                {
                    response.TeamMoodSummary = "No meetings found for analysis.";
                    return response;
                }

                response.MeetingsAnalyzed = meetings.Count;

                // Step 2: Analyze each meeting
                var meetingSentiments = new List<MeetingSentiment>();
                var allSpeakerSentiments = new Dictionary<string, List<double>>();
                double? previousScore = null;

                foreach (var meeting in meetings)
                {
                    var transcript = await _snowflakeService.GetMeetingTranscriptTextAsync(teamId, meeting.MeetingId);
                    var sentiment = await AnalyzeMeetingSentimentAsync(
                        transcript,
                        meeting.MeetingId,
                        meeting.MeetingDate);

                    // Track change from previous
                    if (previousScore.HasValue)
                    {
                        sentiment.ChangeFromPrevious = sentiment.OverallScore - previousScore.Value;
                    }
                    previousScore = sentiment.OverallScore;

                    meetingSentiments.Add(sentiment);

                    // Aggregate speaker sentiments
                    foreach (var speaker in sentiment.BySpeaker)
                    {
                        if (!allSpeakerSentiments.ContainsKey(speaker.SpeakerName))
                        {
                            allSpeakerSentiments[speaker.SpeakerName] = new List<double>();
                        }
                        allSpeakerSentiments[speaker.SpeakerName].Add(speaker.AverageScore);
                    }
                }

                // Step 3: Calculate overall metrics
                response.OverallTeamSentiment = meetingSentiments.Average(m => m.OverallScore);
                response.OverallCategory = GetSentimentCategory(response.OverallTeamSentiment);

                // Build trend
                response.Trend = meetingSentiments.Select(m => new SentimentTrend
                {
                    MeetingId = m.MeetingId,
                    Date = m.MeetingDate,
                    Score = m.OverallScore,
                    Category = m.Category,
                    KeyDriver = m.Highlights.FirstOrDefault()?.Topic ?? "General discussion"
                }).ToList();

                // Speaker averages
                response.SpeakerAverages = allSpeakerSentiments.Select(kvp => new SpeakerSentiment
                {
                    SpeakerName = kvp.Key,
                    AverageScore = kvp.Value.Average(),
                    Category = GetSentimentCategory(kvp.Value.Average()),
                    StatementCount = kvp.Value.Count
                }).OrderByDescending(s => s.AverageScore).ToList();

                // Most positive/negative meetings
                response.MostPositiveMeeting = meetingSentiments.OrderByDescending(m => m.OverallScore).FirstOrDefault();
                response.MostNegativeMeeting = meetingSentiments.OrderBy(m => m.OverallScore).FirstOrDefault();

                // Identify concerns
                response.Concerns = await IdentifyConcernsAsync(meetingSentiments);

                // Generate team mood summary
                response.TeamMoodSummary = await GenerateTeamMoodSummaryAsync(response);

                stopwatch.Stop();
                response.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("Team sentiment analysis complete. Overall: {Score:F2} ({Category})",
                    response.OverallTeamSentiment, response.OverallCategory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing team sentiment for {TeamId}", teamId);
                response.Success = false;
            }

            return response;
        }

        /// <summary>
        /// Get detailed sentiment for a specific meeting
        /// </summary>
        public async Task<MeetingSentimentResponse> GetMeetingSentimentAsync(string teamId, string meetingId)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new MeetingSentimentResponse { Success = true };

            try
            {
                _logger.LogInformation("Analyzing sentiment for meeting {MeetingId}", meetingId);

                var meetings = await _snowflakeService.GetMeetingsByTeamAsync(teamId);
                var meeting = meetings.FirstOrDefault(m => m.MeetingId == meetingId);

                if (meeting == null)
                {
                    response.Success = false;
                    return response;
                }

                var transcript = await _snowflakeService.GetMeetingTranscriptTextAsync(teamId, meetingId);
                response.Sentiment = await AnalyzeMeetingSentimentAsync(transcript, meetingId, meeting.MeetingDate);

                stopwatch.Stop();
                response.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing sentiment for meeting {MeetingId}", meetingId);
                response.Success = false;
            }

            return response;
        }

        /// <summary>
        /// Get meetings/topics with concerning sentiment
        /// </summary>
        public async Task<SentimentConcernsResponse> GetSentimentConcernsAsync(string teamId)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new SentimentConcernsResponse { TeamId = teamId, Success = true };

            try
            {
                var fullSentiment = await GetTeamSentimentAsync(teamId);
                response.Concerns = fullSentiment.Concerns;
                response.TotalConcerns = fullSentiment.Concerns.Count;

                if (response.TotalConcerns > 0)
                {
                    response.OverallAssessment = await GenerateConcernAssessmentAsync(fullSentiment.Concerns);
                    response.RecommendedActions = GenerateRecommendedActions(fullSentiment.Concerns);
                }
                else
                {
                    response.OverallAssessment = "No significant sentiment concerns detected across the analyzed meetings. Team morale appears stable.";
                }

                stopwatch.Stop();
                response.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sentiment concerns for {TeamId}", teamId);
                response.Success = false;
            }

            return response;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // PRIVATE METHODS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Analyze sentiment for a single meeting
        /// </summary>
        private async Task<MeetingSentiment> AnalyzeMeetingSentimentAsync(
            string transcript,
            string meetingId,
            DateTime meetingDate)
        {
            var sentiment = new MeetingSentiment
            {
                MeetingId = meetingId,
                MeetingDate = meetingDate
            };

            // Step 1: Get overall meeting sentiment
            sentiment.OverallScore = await _snowflakeService.AnalyzeSentimentAsync(transcript);
            sentiment.Category = GetSentimentCategory(sentiment.OverallScore);

            // Step 2: Parse by speaker and analyze each
            var speakerStatements = ParseSpeakerStatements(transcript);
            var speakerSentiments = new List<SpeakerSentiment>();

            foreach (var speaker in speakerStatements)
            {
                var statements = speaker.Value;
                if (statements.Count == 0) continue;

                // Analyze each statement
                var scores = new List<(string Statement, double Score)>();
                foreach (var statement in statements.Take(10)) // Limit for performance
                {
                    if (statement.Length > 20) // Skip very short statements
                    {
                        var score = await _snowflakeService.AnalyzeSentimentAsync(statement);
                        scores.Add((statement, score));
                    }
                }

                if (scores.Count > 0)
                {
                    var avgScore = scores.Average(s => s.Score);
                    var mostPositive = scores.OrderByDescending(s => s.Score).First();
                    var mostNegative = scores.OrderBy(s => s.Score).First();

                    speakerSentiments.Add(new SpeakerSentiment
                    {
                        SpeakerName = speaker.Key,
                        AverageScore = avgScore,
                        Category = GetSentimentCategory(avgScore),
                        StatementCount = statements.Count,
                        MostPositiveStatement = TruncateText(mostPositive.Statement, 150),
                        MostNegativeStatement = TruncateText(mostNegative.Statement, 150)
                    });
                }
            }

            sentiment.BySpeaker = speakerSentiments.OrderByDescending(s => s.AverageScore).ToList();

            // Step 3: Identify key topics and their sentiment
            sentiment.ByTopic = await AnalyzeTopicSentimentsAsync(transcript, meetingId);

            // Step 4: Extract highlights (notable positive/negative moments)
            sentiment.Highlights = ExtractSentimentHighlights(speakerSentiments, sentiment.ByTopic);

            // Step 5: Generate mood summary
            sentiment.MoodSummary = await GenerateMeetingMoodSummaryAsync(sentiment);

            return sentiment;
        }

        /// <summary>
        /// Parse transcript into speaker statements
        /// </summary>
        private Dictionary<string, List<string>> ParseSpeakerStatements(string transcript)
        {
            var result = new Dictionary<string, List<string>>();

            // Pattern: "Name Name: statement"
            var pattern = @"([A-Z][a-z]+(?:\s+[A-Z][a-z]+)?)\s*:\s*([^:]+?)(?=(?:[A-Z][a-z]+(?:\s+[A-Z][a-z]+)?)\s*:|$)";
            var matches = Regex.Matches(transcript, pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var speaker = match.Groups[1].Value.Trim();
                var statement = match.Groups[2].Value.Trim();

                if (!string.IsNullOrWhiteSpace(speaker) && !string.IsNullOrWhiteSpace(statement))
                {
                    if (!result.ContainsKey(speaker))
                    {
                        result[speaker] = new List<string>();
                    }
                    result[speaker].Add(statement);
                }
            }

            return result;
        }

        /// <summary>
        /// Analyze sentiment of key topics
        /// </summary>
        private async Task<List<TopicSentiment>> AnalyzeTopicSentimentsAsync(string transcript, string meetingId)
        {
            var topicSentiments = new List<TopicSentiment>();

            // Key topics to check for
            var topics = new[]
            {
            ("budget", "budget|cost|expense|funding|money"),
            ("timeline", "timeline|deadline|schedule|date|when"),
            ("staffing", "staff|team|hire|job|position|workload"),
            ("quality", "quality|error|defect|issue|problem"),
            ("AI/automation", "AI|automation|system|technology|tool"),
            ("training", "training|learn|skill|knowledge")
        };

            foreach (var (topic, pattern) in topics)
            {
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                var matches = regex.Matches(transcript);

                if (matches.Count > 0)
                {
                    // Extract context around topic mentions
                    var contexts = new List<string>();
                    foreach (Match match in matches.Take(3))
                    {
                        var start = Math.Max(0, match.Index - 100);
                        var length = Math.Min(300, transcript.Length - start);
                        contexts.Add(transcript.Substring(start, length));
                    }

                    // Analyze sentiment of topic context
                    var combinedContext = string.Join(" ", contexts);
                    var score = await _snowflakeService.AnalyzeSentimentAsync(combinedContext);

                    topicSentiments.Add(new TopicSentiment
                    {
                        Topic = topic,
                        Score = score,
                        Category = GetSentimentCategory(score),
                        Context = TruncateText(contexts.FirstOrDefault() ?? "", 150)
                    });
                }
            }

            return topicSentiments.OrderBy(t => t.Score).ToList();
        }

        /// <summary>
        /// Extract notable sentiment highlights
        /// </summary>
        private List<SentimentHighlight> ExtractSentimentHighlights(
            List<SpeakerSentiment> speakerSentiments,
            List<TopicSentiment> topicSentiments)
        {
            var highlights = new List<SentimentHighlight>();

            // Add speaker highlights
            foreach (var speaker in speakerSentiments)
            {
                if (speaker.AverageScore < -0.3)
                {
                    highlights.Add(new SentimentHighlight
                    {
                        Type = speaker.AverageScore < -0.5 ? "Significant Concern" : "Concern",
                        Speaker = speaker.SpeakerName,
                        Quote = speaker.MostNegativeStatement,
                        Score = speaker.AverageScore,
                        Topic = "General"
                    });
                }
                else if (speaker.AverageScore > 0.5)
                {
                    highlights.Add(new SentimentHighlight
                    {
                        Type = "Enthusiasm",
                        Speaker = speaker.SpeakerName,
                        Quote = speaker.MostPositiveStatement,
                        Score = speaker.AverageScore,
                        Topic = "General"
                    });
                }
            }

            // Add topic highlights
            foreach (var topic in topicSentiments.Where(t => Math.Abs(t.Score) > 0.3))
            {
                highlights.Add(new SentimentHighlight
                {
                    Type = topic.Score < 0 ? "Topic Concern" : "Topic Positive",
                    Speaker = "Team",
                    Quote = topic.Context,
                    Score = topic.Score,
                    Topic = topic.Topic
                });
            }

            return highlights.OrderBy(h => h.Score).Take(10).ToList();
        }

        /// <summary>
        /// Identify sentiment concerns across meetings
        /// </summary>
        private async Task<List<SentimentConcern>> IdentifyConcernsAsync(List<MeetingSentiment> meetingSentiments)
        {
            var concerns = new List<SentimentConcern>();

            foreach (var meeting in meetingSentiments)
            {
                // Flag meetings with negative overall sentiment
                if (meeting.OverallScore < -0.2)
                {
                    concerns.Add(new SentimentConcern
                    {
                        MeetingId = meeting.MeetingId,
                        MeetingDate = meeting.MeetingDate,
                        ConcernType = meeting.OverallScore < -0.5 ? "High Stress" : "Elevated Concern",
                        Description = $"Meeting showed {meeting.Category} sentiment ({meeting.OverallScore:F2})",
                        AffectedSpeakers = meeting.BySpeaker.Where(s => s.AverageScore < -0.2).Select(s => s.SpeakerName).ToList(),
                        Topic = meeting.ByTopic.OrderBy(t => t.Score).FirstOrDefault()?.Topic ?? "General",
                        SentimentScore = meeting.OverallScore,
                        RecommendedAction = GetRecommendedAction(meeting.OverallScore)
                    });
                }

                // Flag significant negative shifts
                if (meeting.ChangeFromPrevious.HasValue && meeting.ChangeFromPrevious < -0.3)
                {
                    concerns.Add(new SentimentConcern
                    {
                        MeetingId = meeting.MeetingId,
                        MeetingDate = meeting.MeetingDate,
                        ConcernType = "Sentiment Drop",
                        Description = $"Significant mood drop from previous meeting ({meeting.ChangeFromPrevious:F2})",
                        AffectedSpeakers = meeting.BySpeaker.Where(s => s.AverageScore < 0).Select(s => s.SpeakerName).ToList(),
                        Topic = meeting.Highlights.FirstOrDefault()?.Topic ?? "Unknown",
                        SentimentScore = meeting.OverallScore,
                        RecommendedAction = "Review meeting topics for sources of concern"
                    });
                }

                // Flag speakers with consistently negative sentiment
                foreach (var speaker in meeting.BySpeaker.Where(s => s.AverageScore < -0.4))
                {
                    concerns.Add(new SentimentConcern
                    {
                        MeetingId = meeting.MeetingId,
                        MeetingDate = meeting.MeetingDate,
                        ConcernType = "Individual Concern",
                        Description = $"{speaker.SpeakerName} showed negative sentiment ({speaker.AverageScore:F2})",
                        AffectedSpeakers = new List<string> { speaker.SpeakerName },
                        Topic = "Personal/Workload",
                        SentimentScore = speaker.AverageScore,
                        RecommendedAction = $"Consider follow-up with {speaker.SpeakerName}"
                    });
                }
            }

            return concerns.OrderBy(c => c.SentimentScore).ToList();
        }

        /// <summary>
        /// Generate team mood summary
        /// </summary>
        private async Task<string> GenerateTeamMoodSummaryAsync(TeamSentimentResponse response)
        {
            var trendDescription = response.Trend.Count > 1
                ? (response.Trend.Last().Score > response.Trend.First().Score ? "improving" :
                   response.Trend.Last().Score < response.Trend.First().Score ? "declining" : "stable")
                : "stable";

            var prompt = $@"Generate a brief (2-3 sentence) professional summary of team mood based on:
 
Overall Sentiment: {response.OverallTeamSentiment:F2} ({response.OverallCategory})
Trend: {trendDescription} over {response.MeetingsAnalyzed} meetings
Concerns: {response.Concerns.Count} flagged items
Most Positive Meeting: {response.MostPositiveMeeting?.MeetingId} (Score: {response.MostPositiveMeeting?.OverallScore:F2})
Most Negative Meeting: {response.MostNegativeMeeting?.MeetingId} (Score: {response.MostNegativeMeeting?.OverallScore:F2})
 
Write a professional HR/management summary:";

            try
            {
                return await _snowflakeService.GenerateAnswerWithCortexAsync(prompt);
            }
            catch
            {
                return $"Team sentiment is {response.OverallCategory} with a {trendDescription} trend over {response.MeetingsAnalyzed} meetings.";
            }
        }

        /// <summary>
        /// Generate meeting mood summary
        /// </summary>
        private async Task<string> GenerateMeetingMoodSummaryAsync(MeetingSentiment sentiment)
        {
            var topicInfo = sentiment.ByTopic.Any()
                ? $"Topics discussed: {string.Join(", ", sentiment.ByTopic.Select(t => $"{t.Topic} ({t.Category})"))}"
                : "";

            return $"Meeting mood was {sentiment.Category} (score: {sentiment.OverallScore:F2}). " +
                   $"{sentiment.BySpeaker.Count} participants analyzed. {topicInfo}";
        }

        /// <summary>
        /// Generate concern assessment
        /// </summary>
        private async Task<string> GenerateConcernAssessmentAsync(List<SentimentConcern> concerns)
        {
            var concernSummary = string.Join("\n", concerns.Take(5).Select(c =>
                $"- {c.MeetingId}: {c.ConcernType} - {c.Description}"));

            var prompt = $@"Provide a brief professional assessment of these team sentiment concerns:
 
{concernSummary}
 
Write 2-3 sentences assessing the severity and potential causes.";

            try
            {
                return await _snowflakeService.GenerateAnswerWithCortexAsync(prompt);
            }
            catch
            {
                return $"Detected {concerns.Count} sentiment concerns requiring attention.";
            }
        }

        /// <summary>
        /// Generate recommended actions based on concerns
        /// </summary>
        private List<string> GenerateRecommendedActions(List<SentimentConcern> concerns)
        {
            var actions = new HashSet<string>();

            foreach (var concern in concerns)
            {
                switch (concern.ConcernType)
                {
                    case "High Stress":
                        actions.Add("Schedule team wellness check-in");
                        actions.Add("Review workload distribution");
                        break;
                    case "Sentiment Drop":
                        actions.Add("Investigate recent changes affecting team morale");
                        break;
                    case "Individual Concern":
                        actions.Add($"Schedule 1:1 with affected team members");
                        break;
                }
            }

            // Add general recommendations
            if (concerns.Any(c => c.Topic.Contains("staffing", StringComparison.OrdinalIgnoreCase)))
            {
                actions.Add("Address staffing concerns in next team meeting");
            }

            if (concerns.Any(c => c.Topic.Contains("workload", StringComparison.OrdinalIgnoreCase)))
            {
                actions.Add("Evaluate project timelines and resource allocation");
            }

            return actions.Take(5).ToList();
        }

        /// <summary>
        /// Get recommended action based on sentiment score
        /// </summary>
        private string GetRecommendedAction(double score)
        {
            return score switch
            {
                < -0.6 => "Immediate attention recommended - schedule team check-in",
                < -0.4 => "Monitor closely - address concerns in next meeting",
                < -0.2 => "Note for awareness - keep communication open",
                _ => "No immediate action needed"
            };
        }

        /// <summary>
        /// Get sentiment category from score
        /// </summary>
        private SentimentCategory GetSentimentCategory(double score)
        {
            return score switch
            {
                < -0.6 => SentimentCategory.VeryNegative,
                < -0.2 => SentimentCategory.Negative,
                < 0.2 => SentimentCategory.Neutral,
                < 0.6 => SentimentCategory.Positive,
                _ => SentimentCategory.VeryPositive
            };
        }

        /// <summary>
        /// Truncate text to specified length
        /// </summary>
        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= maxLength ? text : text.Substring(0, maxLength - 3) + "...";
        }
    }
}
