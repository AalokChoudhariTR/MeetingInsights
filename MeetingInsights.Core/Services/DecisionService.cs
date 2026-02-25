using MeetingInsights.Core.Models;
using MeetingInsights.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace MeetingInsights.Core.Services
{
    /// <summary>
    /// Service for extracting decisions and detecting conflicts across meetings
    /// </summary>
    public class DecisionService : IDecisionService
    {
        private readonly ISnowflakeService _snowflakeService;
        private readonly ILogger<DecisionService> _logger;

        public DecisionService(
            ISnowflakeService snowflakeService,
            ILogger<DecisionService> logger)
        {
            _snowflakeService = snowflakeService;
            _logger = logger;
        }

        /// <summary>
        /// Get all decisions with conflict detection
        /// </summary>
        public async Task<DecisionsResponse> GetAllDecisionsAsync(string teamId)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new DecisionsResponse { TeamId = teamId, Success = true };

            try
            {
                _logger.LogInformation("Extracting all decisions for team {TeamId}", teamId);

                // Step 1: Get all meetings chronologically
                var meetings = await _snowflakeService.GetMeetingsByTeamAsync(teamId);
                meetings = meetings.OrderBy(m => m.MeetingDate).ToList();

                if (meetings.Count == 0)
                {
                    return response;
                }

                // Step 2: Extract decisions from each meeting
                var allDecisions = new List<Decision>();
                var meetingTranscripts = new Dictionary<string, string>();
                int decisionCounter = 1;

                foreach (var meeting in meetings)
                {
                    var transcript = await _snowflakeService.GetMeetingTranscriptTextAsync(teamId, meeting.MeetingId);
                    meetingTranscripts[meeting.MeetingId] = transcript;

                    var decisions = await ExtractDecisionsFromMeetingAsync(
                        transcript,
                        meeting.MeetingId,
                        meeting.MeetingDate,
                        decisionCounter);

                    foreach (var decision in decisions)
                    {
                        decision.DecisionId = $"DEC-{decisionCounter:D3}";
                        decisionCounter++;
                        allDecisions.Add(decision);
                    }
                }

                _logger.LogInformation("Extracted {Count} decisions", allDecisions.Count);

                // Step 3: Detect conflicts and track evolution
                var conflicts = await DetectConflictsAsync(allDecisions, meetingTranscripts);

                // Step 4: Build response
                response.Decisions = allDecisions.OrderBy(d => d.DecisionDate).ToList();
                response.TotalDecisions = allDecisions.Count;
                response.DecisionsWithConflicts = conflicts.Count;
                response.Conflicts = conflicts;

                // Stats by category
                response.DecisionsByCategory = allDecisions
                    .GroupBy(d => d.Category.ToString())
                    .ToDictionary(g => g.Key, g => g.Count());

                // Stats by meeting
                response.DecisionsByMeeting = allDecisions
                    .GroupBy(d => d.MeetingId)
                    .ToDictionary(g => g.Key, g => g.Count());

                stopwatch.Stop();
                response.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("Decision extraction complete. {Total} decisions, {Conflicts} with conflicts",
                    response.TotalDecisions, response.DecisionsWithConflicts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting decisions for team {TeamId}", teamId);
                response.Success = false;
            }

            return response;
        }

        /// <summary>
        /// Get only conflicting decisions
        /// </summary>
        public async Task<ConflictsResponse> GetConflictsAsync(string teamId)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new ConflictsResponse { TeamId = teamId, Success = true };

            try
            {
                var fullResponse = await GetAllDecisionsAsync(teamId);
                response.Conflicts = fullResponse.Conflicts;
                response.TotalConflicts = fullResponse.Conflicts.Count;

                // Generate overall assessment
                if (response.TotalConflicts > 0)
                {
                    response.OverallAssessment = await GenerateConflictAssessmentAsync(fullResponse.Conflicts);
                }
                else
                {
                    response.OverallAssessment = "No conflicting decisions detected. All decisions across meetings appear consistent.";
                }

                stopwatch.Stop();
                response.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conflicts for team {TeamId}", teamId);
                response.Success = false;
            }

            return response;
        }

        /// <summary>
        /// Get decisions from a specific meeting
        /// </summary>
        public async Task<List<Decision>> GetMeetingDecisionsAsync(string teamId, string meetingId)
        {
            var meetings = await _snowflakeService.GetMeetingsByTeamAsync(teamId);
            var meeting = meetings.FirstOrDefault(m => m.MeetingId == meetingId);

            if (meeting == null)
                return new List<Decision>();

            var transcript = await _snowflakeService.GetMeetingTranscriptTextAsync(teamId, meetingId);
            return await ExtractDecisionsFromMeetingAsync(transcript, meetingId, meeting.MeetingDate, 1);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // PRIVATE METHODS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Extract decisions from a single meeting transcript
        /// </summary>
        private async Task<List<Decision>> ExtractDecisionsFromMeetingAsync(
            string transcript,
            string meetingId,
            DateTime meetingDate,
            int startingId)
        {
            var decisions = new List<Decision>();

            var prompt = $@"Analyze this meeting transcript and extract ALL decisions made.
 
Look for:
1. Explicit decisions: ""We've decided..."", ""The decision is..."", ""We're going with...""
2. Agreements: ""Agreed"", ""Let's do that"", ""That works""
3. Approvals: ""Approved"", ""Green light"", ""Go ahead""
4. Commitments: ""We will..."", ""We're going to...""
5. Conclusions: ""So we're..."", ""That settles it""
 
For EACH decision, extract:
- topic: What the decision is about (brief, 3-5 words)
- description: The actual decision made (one sentence)
- decisionMaker: Who announced/made the decision
- category: One of [BUDGET, TIMELINE, STAFFING, TECHNICAL, VENDOR, PROCESS, SCOPE, OTHER]
- originalQuote: The exact phrase from transcript
 
Meeting transcript:
{(transcript.Length > 8000 ? transcript.Substring(0, 8000) + "..." : transcript)}
 
Respond with a JSON array:
[
    {{
        ""topic"": ""Budget allocation"",
        ""description"": ""Approved $45,000 for the pilot program"",
        ""decisionMaker"": ""Sarah Chen"",
        ""category"": ""BUDGET"",
        ""originalQuote"": ""We're allocating $45,000 for the pilot""
    }}
]
 
Return ONLY the JSON array. If no decisions found, return [].";

            try
            {
                var response = await _snowflakeService.GenerateAnswerWithCortexAsync(prompt);

                var jsonStart = response.IndexOf('[');
                var jsonEnd = response.LastIndexOf(']');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var parsed = JsonSerializer.Deserialize<List<DecisionRaw>>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (parsed != null)
                    {
                        foreach (var item in parsed)
                        {
                            if (!string.IsNullOrWhiteSpace(item.Description))
                            {
                                decisions.Add(new Decision
                                {
                                    Topic = item.Topic ?? "Unknown",
                                    Description = item.Description,
                                    MeetingId = meetingId,
                                    DecisionDate = meetingDate,
                                    DecisionMaker = item.DecisionMaker ?? "Team",
                                    Category = ParseCategory(item.Category),
                                    OriginalQuote = item.OriginalQuote ?? "",
                                    HasConflict = false,
                                    ChangeType = DecisionChangeType.None
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing decisions from meeting {MeetingId}", meetingId);
            }

            return decisions;
        }

        /// <summary>
        /// Detect conflicts and changes across decisions
        /// </summary>
        private async Task<List<ConflictingDecision>> DetectConflictsAsync(
            List<Decision> allDecisions,
            Dictionary<string, string> meetingTranscripts)
        {
            var conflicts = new List<ConflictingDecision>();

            // Group decisions by similar topics
            var topicGroups = await GroupDecisionsByTopicAsync(allDecisions);

            foreach (var group in topicGroups.Where(g => g.Value.Count > 1))
            {
                var decisionsInGroup = group.Value.OrderBy(d => d.DecisionDate).ToList();

                // Check for conflicts within this topic group
                var conflict = await AnalyzeDecisionGroupForConflictsAsync(group.Key, decisionsInGroup);

                if (conflict != null && conflict.OverallChangeType != DecisionChangeType.None)
                {
                    conflicts.Add(conflict);

                    // Mark individual decisions as having conflicts
                    foreach (var decision in decisionsInGroup)
                    {
                        decision.HasConflict = true;
                    }
                }
            }

            return conflicts;
        }

        /// <summary>
        /// Group decisions by similar topics using LLM
        /// </summary>
        private async Task<Dictionary<string, List<Decision>>> GroupDecisionsByTopicAsync(List<Decision> decisions)
        {
            var groups = new Dictionary<string, List<Decision>>();

            // Simple grouping by category first
            foreach (var decision in decisions)
            {
                var key = $"{decision.Category}:{decision.Topic.ToLower()}";
                // Try to find existing similar group
                var existingKey = groups.Keys.FirstOrDefault(k =>
                    k.StartsWith($"{decision.Category}:") &&
                    IsSimilarTopic(k.Split(':')[1], decision.Topic.ToLower()));

                if (existingKey != null)
                {
                    groups[existingKey].Add(decision);
                }
                else
                {
                    groups[key] = new List<Decision> { decision };
                }
            }

            return groups;
        }

        /// <summary>
        /// Check if two topics are similar
        /// </summary>
        private bool IsSimilarTopic(string topic1, string topic2)
        {
            // Simple keyword matching
            var words1 = topic1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var words2 = topic2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var commonWords = words1.Intersect(words2, StringComparer.OrdinalIgnoreCase).Count();
            var totalWords = Math.Max(words1.Length, words2.Length);

            return totalWords > 0 && (double)commonWords / totalWords > 0.5;
        }

        /// <summary>
        /// Analyze a group of related decisions for conflicts
        /// </summary>
        private async Task<ConflictingDecision?> AnalyzeDecisionGroupForConflictsAsync(
            string topic,
            List<Decision> decisions)
        {
            if (decisions.Count < 2)
                return null;

            // Build timeline string
            var timelineText = string.Join("\n", decisions.Select(d =>
                $"- {d.DecisionDate:yyyy-MM-dd} ({d.MeetingId}): {d.Description}"));

            var prompt = $@"Analyze these decisions made over time about the same topic and detect any conflicts or changes.
 
TOPIC: {topic}
 
DECISION TIMELINE:
{timelineText}
 
Analyze and respond with JSON:
{{
    ""hasConflict"": true/false,
    ""changeType"": ""NONE"" or ""EVOLVED"" or ""REVERSED"" or ""EXPANDED"" or ""REDUCED"" or ""CONTRADICTED"",
    ""conflictSummary"": ""Brief description of what changed"",
    ""currentState"": ""The final/current decision state"",
    ""impact"": ""Potential impact of this change""
}}
 
Change types:
- NONE: Decisions are consistent
- EVOLVED: Natural refinement of the decision
- REVERSED: Complete reversal of previous decision
- EXPANDED: Scope or budget increased
- REDUCED: Scope or budget decreased
- CONTRADICTED: Direct contradiction without explanation
 
Return ONLY JSON.";

            try
            {
                var response = await _snowflakeService.GenerateAnswerWithCortexAsync(prompt);

                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var parsed = JsonSerializer.Deserialize<ConflictAnalysisResult>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (parsed != null && parsed.HasConflict)
                    {
                        var conflict = new ConflictingDecision
                        {
                            Topic = topic.Split(':').LastOrDefault() ?? topic,
                            Category = decisions.First().Category,
                            OverallChangeType = ParseChangeType(parsed.ChangeType),
                            ConflictSummary = parsed.ConflictSummary ?? "",
                            CurrentState = parsed.CurrentState ?? "",
                            Impact = parsed.Impact ?? "",
                            Timeline = decisions.Select((d, index) => new DecisionPoint
                            {
                                MeetingId = d.MeetingId,
                                Date = d.DecisionDate,
                                Decision = d.Description,
                                DecisionMaker = d.DecisionMaker,
                                ChangeFromPrevious = index == 0 ? null : DetermineChangeType(decisions[index - 1], d)
                            }).ToList()
                        };

                        return conflict;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing conflicts for topic {Topic}", topic);
            }

            return null;
        }

        /// <summary>
        /// Generate overall conflict assessment
        /// </summary>
        private async Task<string> GenerateConflictAssessmentAsync(List<ConflictingDecision> conflicts)
        {
            var conflictSummaries = string.Join("\n", conflicts.Select(c =>
                $"- {c.Topic}: {c.ConflictSummary} (Type: {c.OverallChangeType})"));

            var prompt = $@"Provide a brief executive assessment of these decision conflicts detected across meetings:
 
CONFLICTS DETECTED:
{conflictSummaries}
 
Write a 2-3 sentence professional assessment summarizing:
1. The nature of the conflicts
2. Whether they represent healthy project evolution or concerning inconsistencies
3. Any recommendations
 
Be concise and professional.";

            try
            {
                return await _snowflakeService.GenerateAnswerWithCortexAsync(prompt);
            }
            catch
            {
                return "Multiple decision changes detected across meetings. Review recommended.";
            }
        }

        /// <summary>
        /// Determine change type between two decisions
        /// </summary>
        private DecisionChangeType DetermineChangeType(Decision previous, Decision current)
        {
            // Simple heuristics
            var prevLower = previous.Description.ToLower();
            var currLower = current.Description.ToLower();

            if (prevLower == currLower)
                return DecisionChangeType.None;

            // Check for numeric changes (budget, timeline)
            if (ContainsNumber(prevLower) && ContainsNumber(currLower))
            {
                var prevNum = ExtractNumber(prevLower);
                var currNum = ExtractNumber(currLower);

                if (prevNum > 0 && currNum > 0)
                {
                    if (currNum > prevNum * 1.1m)
                        return DecisionChangeType.Expanded;
                    if (currNum < prevNum * 0.9m)
                        return DecisionChangeType.Reduced;
                }
            }

            // Check for reversal keywords
            var reversalKeywords = new[] { "instead", "no longer", "changed to", "switch", "reverse" };
            if (reversalKeywords.Any(k => currLower.Contains(k)))
                return DecisionChangeType.Reversed;

            return DecisionChangeType.Evolved;
        }

        private bool ContainsNumber(string text)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+");
        }

        private decimal ExtractNumber(string text)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, @"[\d,]+(?:\.\d+)?");
            if (match.Success)
            {
                var numStr = match.Value.Replace(",", "");
                if (decimal.TryParse(numStr, out var num))
                    return num;
            }
            return 0;
        }

        private DecisionCategory ParseCategory(string? category)
        {
            return category?.ToUpper() switch
            {
                "BUDGET" => DecisionCategory.Budget,
                "TIMELINE" => DecisionCategory.Timeline,
                "STAFFING" => DecisionCategory.Staffing,
                "TECHNICAL" => DecisionCategory.Technical,
                "VENDOR" => DecisionCategory.Vendor,
                "PROCESS" => DecisionCategory.Process,
                "SCOPE" => DecisionCategory.Scope,
                _ => DecisionCategory.Other
            };
        }

        private DecisionChangeType ParseChangeType(string? changeType)
        {
            return changeType?.ToUpper() switch
            {
                "EVOLVED" => DecisionChangeType.Evolved,
                "REVERSED" => DecisionChangeType.Reversed,
                "EXPANDED" => DecisionChangeType.Expanded,
                "REDUCED" => DecisionChangeType.Reduced,
                "CLARIFIED" => DecisionChangeType.Clarified,
                "CONTRADICTED" => DecisionChangeType.Contradicted,
                _ => DecisionChangeType.None
            };
        }

        // Helper classes for JSON parsing
        private class DecisionRaw
        {
            public string? Topic { get; set; }
            public string? Description { get; set; }
            public string? DecisionMaker { get; set; }
            public string? Category { get; set; }
            public string? OriginalQuote { get; set; }
        }

        private class ConflictAnalysisResult
        {
            public bool HasConflict { get; set; }
            public string? ChangeType { get; set; }
            public string? ConflictSummary { get; set; }
            public string? CurrentState { get; set; }
            public string? Impact { get; set; }
        }
    }
}
