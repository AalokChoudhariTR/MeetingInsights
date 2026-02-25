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
    public class MeetingService : IMeetingService
    {
        private readonly ISnowflakeService _snowflakeService;
        private readonly ILogger<MeetingService> _logger;

        public MeetingService(
            ISnowflakeService snowflakeService,
            ILogger<MeetingService> logger)
        {
            _snowflakeService = snowflakeService;
            _logger = logger;
        }

        /// <summary>
        /// Get summary of ALL meetings for a team - Executive Overview
        /// </summary>
        public async Task<TeamMeetingsSummary> GetTeamMeetingsSummaryAsync(string teamId)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new TeamMeetingsSummary { TeamId = teamId };

            try
            {
                _logger.LogInformation("Generating team summary for {TeamId}", teamId);

                // Step 1: Get all meetings for this team
                var meetings = await _snowflakeService.GetMeetingsByTeamAsync(teamId);
                if (meetings.Count == 0)
                {
                    result.ExecutiveSummary = "No meetings found for this team.";
                    return result;
                }

                result.TotalMeetings = meetings.Count;
                result.FirstMeetingDate = meetings.Min(m => m.MeetingDate);
                result.LastMeetingDate = meetings.Max(m => m.MeetingDate);

                // Step 2: Get brief summary for each meeting
                var allTranscriptText = new StringBuilder();
                var meetingBriefs = new List<MeetingSummaryBrief>();
                var allParticipants = new HashSet<string>();

                foreach (var meeting in meetings.OrderBy(m => m.MeetingDate))
                {
                    // Get transcript text
                    var transcriptText = await _snowflakeService.GetMeetingTranscriptTextAsync(teamId, meeting.MeetingId);
                    allTranscriptText.AppendLine($"=== Meeting {meeting.MeetingId} ({meeting.MeetingDate:yyyy-MM-dd}) ===");
                    allTranscriptText.AppendLine(transcriptText);
                    allTranscriptText.AppendLine();

                    // Extract participants from this meeting
                    var participants = ExtractParticipants(transcriptText);
                    foreach (var p in participants)
                    {
                        allParticipants.Add(p);
                    }

                    // Generate one-liner for this meeting
                    var oneLiner = await GenerateOneLinerAsync(transcriptText, meeting.MeetingId);
                    meetingBriefs.Add(new MeetingSummaryBrief
                    {
                        MeetingId = meeting.MeetingId,
                        MeetingDate = meeting.MeetingDate,
                        OneLinerSummary = oneLiner
                    });
                }

                result.MeetingBriefs = meetingBriefs;
                result.AllParticipants = allParticipants.ToList();

                // Step 3: Generate executive summary of all meetings combined
                var executiveSummaryPrompt = BuildExecutiveSummaryPrompt(
                    teamId,
                    meetings.Count,
                    result.FirstMeetingDate,
                    result.LastMeetingDate,
                    allTranscriptText.ToString());

                result.ExecutiveSummary = await _snowflakeService.GenerateAnswerWithCortexAsync(executiveSummaryPrompt);
                // Step 4: Extract overall themes
                result.OverallKeyThemes = ExtractKeyThemes(result.ExecutiveSummary);

                stopwatch.Stop();
                result.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("Generated team summary for {TeamId} in {Time}ms",
                    teamId, result.ProcessingTimeMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating team summary for {TeamId}", teamId);
                result.ExecutiveSummary = $"Error generating summary: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Get detailed summary of a specific meeting
        /// </summary>
        public async Task<MeetingSummaryResponse> GetMeetingSummaryAsync(string teamId, string meetingId)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new MeetingSummaryResponse();

            try
            {
                _logger.LogInformation("Generating summary for meeting {MeetingId}", meetingId);

                // Step 1: Get the full transcript
                var transcriptText = await _snowflakeService.GetMeetingTranscriptTextAsync(teamId, meetingId);
                if (string.IsNullOrEmpty(transcriptText))
                {
                    response.Success = false;
                    response.Error = $"Meeting {meetingId} not found for team {teamId}";
                    return response;
                }

                // Step 2: Get meeting date
                var meetings = await _snowflakeService.GetMeetingsByTeamAsync(teamId);
                var meeting = meetings.FirstOrDefault(m => m.MeetingId == meetingId);

                // Step 3: Use Cortex SUMMARIZE for base summary
                var cortexSummary = await _snowflakeService.SummarizeWithCortexAsync(transcriptText);

                // Step 4: Generate detailed summary with structure using Cortex Complete
                var detailedPrompt = BuildDetailedSummaryPrompt(meetingId, transcriptText);
                var detailedResponse = await _snowflakeService.GenerateAnswerWithCortexAsync(detailedPrompt);

                // Step 5: Parse and build the response
                var summary = new MeetingSummary
                {
                    MeetingId = meetingId,
                    MeetingDate = meeting?.MeetingDate ?? DateTime.MinValue,
                    TeamId = teamId,
                    Summary = cortexSummary,
                    Participants = ExtractParticipants(transcriptText),
                    KeyPoints = ExtractKeyPoints(detailedResponse),
                    TopicsDiscussed = ExtractTopics(detailedResponse),
                    OriginalLength = transcriptText.Length,
                    SummaryLength = cortexSummary.Length
                };

                response.Success = true;
                response.Summary = summary;
                stopwatch.Stop();
                response.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("Generated summary for meeting {MeetingId} in {Time}ms",
                    meetingId, response.ProcessingTimeMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating summary for meeting {MeetingId}", meetingId);
                response.Success = false;
                response.Error = ex.Message;
            }

            return response;
        }

        /// <summary>
        /// List all meetings for a team
        /// </summary>
        public async Task<List<MeetingSummaryBrief>> ListMeetingsAsync(string teamId)
        {
            var meetings = await _snowflakeService.GetMeetingsByTeamAsync(teamId);
            return meetings.OrderBy(m => m.MeetingDate).Select(m => new MeetingSummaryBrief
            {
                MeetingId = m.MeetingId,
                MeetingDate = m.MeetingDate,
                OneLinerSummary = "" // Will be populated if full summary is requested
            }).ToList();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Extract participant names from transcript text
        /// </summary>
        private List<string> ExtractParticipants(string text)
        {
            var participants = new HashSet<string>();
            // Pattern: "Name Name:" at the start of speech
            var pattern = @"([A-Z][a-z]+(?:\s+[A-Z][a-z]+)?)\s*:";
            var matches = Regex.Matches(text, pattern);
            foreach (Match match in matches)
            {
                var name = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(name) && name.Length > 2)
                {
                    participants.Add(name);
                }
            }

            return participants.ToList();
        }

        /// <summary>
        /// Generate a one-line summary for a meeting
        /// </summary>
        private async Task<string> GenerateOneLinerAsync(string transcriptText, string meetingId)
        {
            // Use first 2000 chars for efficiency
            var textSample = transcriptText.Length > 2000
                ? transcriptText.Substring(0, 2000)
                : transcriptText;

            var prompt = $@"Generate a single sentence (max 100 characters) summarizing this meeting excerpt. 
Be concise and capture the main topic or decision.
 
Meeting text:
{textSample}
 
One-line summary:";

            try
            {
                var result = await _snowflakeService.GenerateAnswerWithCortexAsync(prompt);
                // Clean up the result - take first sentence only
                var firstSentence = result.Split('.')[0].Trim();
                return firstSentence.Length > 150 ? firstSentence.Substring(0, 147) + "..." : firstSentence;
            }
            catch
            {
                return "Summary unavailable";
            }
        }

        /// <summary>
        /// Build prompt for executive summary
        /// </summary>
        private string BuildExecutiveSummaryPrompt(string teamId, int meetingCount, DateTime firstDate, DateTime lastDate, string allText)
        {
            // Limit text to avoid token limits
            var limitedText = allText.Length > 15000 ? allText.Substring(0, 15000) + "..." : allText;

            return $@"You are an executive assistant analyzing meeting transcripts for team {teamId}.
 
The team had {meetingCount} meetings from {firstDate:MMMM d, yyyy} to {lastDate:MMMM d, yyyy}.
 
Based on ALL the meeting content below, provide an executive summary that includes:
1. Overall project/initiative status
2. Key achievements and milestones
3. Main challenges or concerns raised
4. Critical decisions made
5. Next steps or pending items
 
Meeting transcripts (chronological order):
{limitedText}
 
Provide a clear, professional executive summary (300-500 words):";
        }

        /// <summary>
        /// Build prompt for detailed meeting summary
        /// </summary>
        private string BuildDetailedSummaryPrompt(string meetingId, string transcriptText)
        {
            var limitedText = transcriptText.Length > 10000 ? transcriptText.Substring(0, 10000) + "..." : transcriptText;

            return $@"Analyze this meeting transcript and extract:
 
1. KEY_POINTS: List 3-5 main discussion points (one line each)
2. TOPICS: List the main topics/themes discussed
3. DECISIONS: Any decisions that were made
4. ACTION_ITEMS: Any tasks assigned (format: [Person] - [Task])
 
Meeting transcript:
{limitedText}
 
Format your response as:
KEY_POINTS:
- point 1
- point 2
 
TOPICS:
- topic 1
- topic 2
 
DECISIONS:
- decision 1
 
ACTION_ITEMS:
- [Person] - Task description";
        }

        /// <summary>
        /// Extract key points from LLM response
        /// </summary>
        private List<string> ExtractKeyPoints(string llmResponse)
        {
            var keyPoints = new List<string>();
            try
            {
                var keyPointsSection = ExtractSection(llmResponse, "KEY_POINTS:", "TOPICS:");
                var lines = keyPointsSection.Split('\n')
                    .Where(l => l.Trim().StartsWith("-") || l.Trim().StartsWith("•"))
                    .Select(l => l.Trim().TrimStart('-', '•').Trim())
                    .Where(l => !string.IsNullOrEmpty(l));
                keyPoints.AddRange(lines);
            }
            catch
            {
                // Return empty list on failure
            }

            return keyPoints;
        }

        /// <summary>
        /// Extract topics from LLM response
        /// </summary>
        private List<string> ExtractTopics(string llmResponse)
        {
            var topics = new List<string>();
            try
            {
                var topicsSection = ExtractSection(llmResponse, "TOPICS:", "DECISIONS:");
                var lines = topicsSection.Split('\n')
                    .Where(l => l.Trim().StartsWith("-") || l.Trim().StartsWith("•"))
                    .Select(l => l.Trim().TrimStart('-', '•').Trim())
                    .Where(l => !string.IsNullOrEmpty(l));
                topics.AddRange(lines);
            }
            catch
            {
                // Return empty list on failure
            }

            return topics;
        }

        /// <summary>
        /// Extract section from text between two markers
        /// </summary>
        private string ExtractSection(string text, string startMarker, string endMarker)
        {
            var startIndex = text.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0) return "";
            startIndex += startMarker.Length;
            var endIndex = text.IndexOf(endMarker, startIndex, StringComparison.OrdinalIgnoreCase);
            if (endIndex < 0) endIndex = text.Length;
            return text.Substring(startIndex, endIndex - startIndex).Trim();
        }

        /// <summary>
        /// Extract key themes from executive summary
        /// </summary>
        private List<string> ExtractKeyThemes(string summary)
        {
            // Simple extraction - look for common theme patterns
            var themes = new List<string>();
            var themeKeywords = new[] { "AI implementation", "quality control", "budget", "staffing",
            "automation", "pilot program", "training", "timeline" };
            foreach (var keyword in themeKeywords)
            {
                if (summary.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    themes.Add(keyword);
                }
            }

            return themes;
        }
    }
}

