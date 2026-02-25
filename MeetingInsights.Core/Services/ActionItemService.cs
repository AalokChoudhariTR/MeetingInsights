using MeetingInsights.Core.Models;
using MeetingInsights.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MeetingInsights.Core.Services
{
    public class ActionItemService : IActionItemService
    {
        private readonly ISnowflakeService _snowflakeService;
        private readonly ILogger<ActionItemService> _logger;

        public ActionItemService(
            ISnowflakeService snowflakeService,
            ILogger<ActionItemService> logger)
        {
            _snowflakeService = snowflakeService;
            _logger = logger;
        }

        /// <summary>
        /// Extract and track ALL action items across all team meetings
        /// </summary>
        public async Task<ActionItemsResponse> GetAllActionItemsAsync(string teamId)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new ActionItemsResponse { TeamId = teamId, Success = true };

            try
            {
                _logger.LogInformation("Extracting all action items for team {TeamId}", teamId);

                // Step 1: Get all meetings in chronological order
                var meetings = await _snowflakeService.GetMeetingsByTeamAsync(teamId);
                meetings = meetings.OrderBy(m => m.MeetingDate).ToList();

                if (meetings.Count == 0)
                {
                    response.TotalActionItems = 0;
                    return response;
                }

                // Step 2: Extract action items from each meeting
                var allActionItems = new List<ActionItem>();
                var meetingTranscripts = new Dictionary<string, string>();
                int actionItemCounter = 1;

                foreach (var meeting in meetings)
                {
                    var transcript = await _snowflakeService.GetMeetingTranscriptTextAsync(teamId, meeting.MeetingId);
                    meetingTranscripts[meeting.MeetingId] = transcript;

                    // Extract action items from this meeting
                    var items = await ExtractActionItemsFromMeetingAsync(
                        transcript,
                        meeting.MeetingId,
                        meeting.MeetingDate,
                        actionItemCounter);

                    foreach (var item in items)
                    {
                        item.ActionItemId = $"AI-{actionItemCounter:D3}";
                        actionItemCounter++;
                        allActionItems.Add(item);
                    }
                }

                _logger.LogInformation("Extracted {Count} raw action items", allActionItems.Count);

                // Step 3: Track status changes across meetings
                await TrackActionItemStatusesAsync(allActionItems, meetings, meetingTranscripts);

                // Step 4: Build response
                response.ActionItems = allActionItems.OrderBy(a => a.AssignedDate).ToList();
                response.TotalActionItems = allActionItems.Count;
                response.CompletedCount = allActionItems.Count(a => a.Status == ActionItemStatus.Completed);
                response.InProgressCount = allActionItems.Count(a => a.Status == ActionItemStatus.InProgress);
                response.AssignedCount = allActionItems.Count(a => a.Status == ActionItemStatus.Assigned);
                response.BlockedCount = allActionItems.Count(a => a.Status == ActionItemStatus.Blocked);

                // Items by person
                response.ItemsByPerson = allActionItems
                    .GroupBy(a => a.AssignedTo)
                    .ToDictionary(g => g.Key, g => g.Count());

                stopwatch.Stop();
                response.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("Completed action item extraction in {Time}ms. Found {Total} items ({Completed} completed)",
                    response.ProcessingTimeMs, response.TotalActionItems, response.CompletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting action items for team {TeamId}", teamId);
                response.Success = false;
            }

            return response;
        }

        /// <summary>
        /// Extract action items from a specific meeting
        /// </summary>
        public async Task<MeetingActionItemsResponse> GetMeetingActionItemsAsync(string teamId, string meetingId)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new MeetingActionItemsResponse { MeetingId = meetingId, Success = true };

            try
            {
                _logger.LogInformation("Extracting action items for meeting {MeetingId}", meetingId);

                // Get meeting info
                var meetings = await _snowflakeService.GetMeetingsByTeamAsync(teamId);
                var meeting = meetings.FirstOrDefault(m => m.MeetingId == meetingId);

                if (meeting == null)
                {
                    response.Success = false;
                    return response;
                }

                response.MeetingDate = meeting.MeetingDate;

                // Get transcript
                var transcript = await _snowflakeService.GetMeetingTranscriptTextAsync(teamId, meetingId);

                // Extract action items
                var items = await ExtractActionItemsFromMeetingAsync(transcript, meetingId, meeting.MeetingDate, 1);

                // Assign IDs
                for (int i = 0; i < items.Count; i++)
                {
                    items[i].ActionItemId = $"{meetingId}-AI-{(i + 1):D2}";
                }

                response.ActionItems = items;
                response.ActionItemCount = items.Count;

                stopwatch.Stop();
                response.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting action items for meeting {MeetingId}", meetingId);
                response.Success = false;
            }

            return response;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // PRIVATE METHODS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Extract action items from a single meeting transcript using LLM
        /// </summary>
        private async Task<List<ActionItem>> ExtractActionItemsFromMeetingAsync(
            string transcript,
            string meetingId,
            DateTime meetingDate,
            int startingId)
        {
            var actionItems = new List<ActionItem>();

            var prompt = $@"Analyze this meeting transcript and extract ALL action items, tasks, or assignments.
 
Look for:
1. Explicit assignments: ""[Name], can you..."", ""[Name] will..."", ""[Name] to do...""
2. Commitments: ""I'll..."", ""I will..."", ""I can take care of...""
3. Follow-up tasks: ""Let's follow up on..."", ""Need to check...""
4. Deadlines mentioned: ""by Friday"", ""next week"", ""before the next meeting""
5. JIRA tickets or ticket numbers if mentioned
 
For EACH action item found, extract:
- assignedTo: Person's name
- task: What needs to be done (concise description)
- dueDate: If mentioned (or null)
- jiraTicket: If a ticket/JIRA number is mentioned (or null)
- originalQuote: The exact phrase from transcript
 
Meeting transcript:
{(transcript.Length > 8000 ? transcript.Substring(0, 8000) + "..." : transcript)}
 
Respond with a JSON array of action items:
[
    {{
        ""assignedTo"": ""Person Name"",
        ""task"": ""Task description"",
        ""dueDate"": ""mentioned deadline or null"",
        ""jiraTicket"": ""JIRA-123 or null"",
        ""originalQuote"": ""exact quote from transcript""
    }}
]
 
Return ONLY the JSON array, no additional text. If no action items found, return [].";

            try
            {
                var response = await _snowflakeService.GenerateAnswerWithCortexAsync(prompt);
                // Parse JSON response
                var jsonStart = response.IndexOf('[');
                var jsonEnd = response.LastIndexOf(']');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var parsed = JsonSerializer.Deserialize<List<ActionItemRaw>>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (parsed != null)
                    {
                        foreach (var item in parsed)
                        {
                            if (!string.IsNullOrWhiteSpace(item.Task))
                            {
                                actionItems.Add(new ActionItem
                                {
                                    AssignedTo = item.AssignedTo ?? "Unassigned",
                                    Task = item.Task,
                                    AssignedInMeetingId = meetingId,
                                    AssignedDate = meetingDate,
                                    Status = ActionItemStatus.Assigned,
                                    DueDate = item.DueDate,
                                    JiraTicket = ExtractJiraTicket(item.JiraTicket ?? item.OriginalQuote ?? ""),
                                    OriginalQuote = item.OriginalQuote ?? ""
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing action items from LLM response for meeting {MeetingId}", meetingId);
            }

            return actionItems;
        }

        /// <summary>
        /// Track action item status changes across meetings
        /// </summary>
        private async Task TrackActionItemStatusesAsync(
            List<ActionItem> actionItems,
            List<MeetingTranscript> meetings,
            Dictionary<string, string> meetingTranscripts)
        {
            if (actionItems.Count == 0) return;

            // For each action item, look in subsequent meetings for status updates
            foreach (var item in actionItems)
            {
                var subsequentMeetings = meetings
                    .Where(m => m.MeetingDate > item.AssignedDate)
                    .OrderBy(m => m.MeetingDate)
                    .ToList();

                foreach (var meeting in subsequentMeetings)
                {
                    if (!meetingTranscripts.TryGetValue(meeting.MeetingId, out var transcript))
                        continue;

                    // Check if this action item is mentioned
                    var statusUpdate = await CheckActionItemStatusInMeetingAsync(
                        item,
                        transcript,
                        meeting.MeetingId,
                        meeting.MeetingDate);

                    if (statusUpdate != null)
                    {
                        item.Updates.Add(statusUpdate);
                        item.Status = statusUpdate.NewStatus;

                        if (statusUpdate.NewStatus == ActionItemStatus.Completed)
                        {
                            item.CompletedInMeetingId = meeting.MeetingId;
                            break; // Stop tracking once completed
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check if an action item is mentioned in a meeting and determine status
        /// </summary>
        private async Task<ActionItemUpdate?> CheckActionItemStatusInMeetingAsync(
            ActionItem item,
            string transcript,
            string meetingId,
            DateTime meetingDate)
        {
            // Quick check - does the transcript mention the assignee and any keywords from the task?
            var taskKeywords = item.Task.Split(' ')
                .Where(w => w.Length > 4)
                .Take(3)
                .ToList();

            bool possibleMention = transcript.Contains(item.AssignedTo, StringComparison.OrdinalIgnoreCase) &&
                                   taskKeywords.Any(k => transcript.Contains(k, StringComparison.OrdinalIgnoreCase));

            if (!possibleMention)
                return null;

            // Use LLM to check status
            var prompt = $@"Check if this action item is mentioned in the meeting and determine its status.
 
ACTION ITEM:
- Assigned to: {item.AssignedTo}
- Task: {item.Task}
- Originally assigned: {item.AssignedDate:yyyy-MM-dd}
 
MEETING TRANSCRIPT (from {meetingDate:yyyy-MM-dd}):
{(transcript.Length > 4000 ? transcript.Substring(0, 4000) : transcript)}
 
Is this action item mentioned? If yes, what is its status?
 
Respond with JSON:
{{
    ""mentioned"": true/false,
    ""status"": ""COMPLETED"" or ""IN_PROGRESS"" or ""BLOCKED"" or ""NOT_MENTIONED"",
    ""updateText"": ""brief quote or summary of what was said about it""
}}
 
Return ONLY JSON.";

            try
            {
                var response = await _snowflakeService.GenerateAnswerWithCortexAsync(prompt);
                var jsonStart = response.IndexOf('{');
                var jsonEnd = response.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var parsed = JsonSerializer.Deserialize<StatusCheckResult>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (parsed != null && parsed.Mentioned)
                    {
                        return new ActionItemUpdate
                        {
                            MeetingId = meetingId,
                            MeetingDate = meetingDate,
                            UpdateText = parsed.UpdateText ?? "",
                            NewStatus = ParseStatus(parsed.Status)
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking action item status in meeting {MeetingId}", meetingId);
            }

            return null;
        }

        /// <summary>
        /// Extract JIRA ticket from text
        /// </summary>
        private string? ExtractJiraTicket(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            // Common JIRA patterns: PROJ-123, ABC-1234, etc.
            var jiraPattern = @"[A-Z]{2,10}-\d{1,6}";
            var match = Regex.Match(text, jiraPattern);
            return match.Success ? match.Value : null;
        }

        /// <summary>
        /// Parse status string to enum
        /// </summary>
        private ActionItemStatus ParseStatus(string? status)
        {
            return status?.ToUpper() switch
            {
                "COMPLETED" => ActionItemStatus.Completed,
                "IN_PROGRESS" => ActionItemStatus.InProgress,
                "BLOCKED" => ActionItemStatus.Blocked,
                "ASSIGNED" => ActionItemStatus.Assigned,
                _ => ActionItemStatus.Unknown
            };
        }

        // Helper classes for JSON deserialization
        private class ActionItemRaw
        {
            public string? AssignedTo { get; set; }
            public string? Task { get; set; }
            public string? DueDate { get; set; }
            public string? JiraTicket { get; set; }
            public string? OriginalQuote { get; set; }
        }

        private class StatusCheckResult
        {
            public bool Mentioned { get; set; }
            public string? Status { get; set; }
            public string? UpdateText { get; set; }
        }
    }
}
