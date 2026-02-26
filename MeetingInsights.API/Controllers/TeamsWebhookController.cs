using MeetingInsights.Core.Models;
using MeetingInsights.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static MeetingInsights.Core.Models.QueryModels;

namespace MeetingInsights.API.Controllers
{

    [ApiController]
    [Route("api/teams")]
    public class TeamsWebhookController : ControllerBase
    {
        private readonly IQueryService _queryService;
        private readonly IMeetingService _meetingService;
        private readonly IActionItemService _actionItemService;
        private readonly IDecisionService _decisionService;
        private readonly ISentimentService _sentimentService;
        private readonly ILogger<TeamsWebhookController> _logger;
        private readonly IConfiguration _configuration;

        // Store team mappings (in production, use database)
        private static readonly Dictionary<string, string> _teamMappings = new();

        public TeamsWebhookController(
            IQueryService queryService,
            IMeetingService meetingService,
            IActionItemService actionItemService,
            IDecisionService decisionService,
            ISentimentService sentimentService,
            ILogger<TeamsWebhookController> logger,
            IConfiguration configuration)
        {
            _queryService = queryService;
            _meetingService = meetingService;
            _actionItemService = actionItemService;
            _decisionService = decisionService;
            _sentimentService = sentimentService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// POST /api/teams/webhook
        /// Receives messages from Teams Outgoing Webhook
        /// </summary>
        [HttpPost("webhook")]
        public async Task<ActionResult> HandleTeamsWebhook([FromBody] TeamsWebhookRequest request)
        {
            try
            {
                _logger.LogInformation("Received Teams webhook: {Text}", request.Text);

                // Validate HMAC (optional but recommended)
                // var isValid = ValidateHmac(Request);

                // Get or create team ID mapping
                var teamsChannelId = request.ChannelId ?? request.Conversation?.Id ?? "default";
                var teamId = GetOrCreateTeamId(teamsChannelId, request.TeamName);

                // Parse the command from message text
                var command = ParseCommand(request.Text);
                // Execute command and get response
                var response = await ExecuteCommandAsync(command, teamId);

                // Return Teams-formatted response
                return Ok(new TeamsWebhookResponse
                {
                    Type = "message",
                    Text = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Teams webhook");
                return Ok(new TeamsWebhookResponse
                {
                    Type = "message",
                    Text = $"❌ Error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Parse command from Teams message
        /// </summary>
        private BotCommand ParseCommand(string text)
        {
            // Remove bot mention (e.g., "<at>MeetingBot</at>")
            var cleanText = System.Text.RegularExpressions.Regex.Replace(
                text ?? "",
                @"<at>.*?</at>\s*",
                "").Trim();

            var parts = cleanText.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var commandName = parts.Length > 0 ? parts[0].ToLower() : "help";
            var argument = parts.Length > 1 ? parts[1] : null;

            return new BotCommand
            {
                Name = commandName,
                Argument = argument
            };
        }

        /// <summary>
        /// Execute bot command
        /// </summary>
        private async Task<string> ExecuteCommandAsync(BotCommand command, string teamId)
        {
            return command.Name switch
            {
                "help" => GetHelpText(),
                "status" => GetStatusText(teamId),
                "summary" => await GetSummaryAsync(teamId, command.Argument),
                "decisions" => await GetDecisionsAsync(teamId),
                "conflicts" => await GetConflictsAsync(teamId),
                "actions" or "action-items" => await GetActionItemsAsync(teamId),
                "pending" => await GetPendingActionsAsync(teamId),
                "sentiment" or "mood" => await GetSentimentAsync(teamId),
                "concerns" => await GetConcernsAsync(teamId),
                "ask" or "query" or "q" => await AskQuestionAsync(teamId, command.Argument),
                "meetings" => await GetMeetingsListAsync(teamId),
                _ => await AskQuestionAsync(teamId, $"{command.Name} {command.Argument}".Trim())
            };
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // COMMAND HANDLERS
        // ═══════════════════════════════════════════════════════════════════════════

        private string GetHelpText()
        {
            return @"🤖 **Meeting Insights Bot Commands**
 
📋 **Quick Commands:**
• `summary` - Get executive summary of all meetings
• `summary MTG-001` - Get summary of specific meeting
• `decisions` - View all decisions made
• `conflicts` - View decisions that changed over time
• `actions` - View all action items
• `pending` - View pending action items only
• `sentiment` - View team mood analysis
• `concerns` - View sentiment concerns
• `meetings` - List all meetings
 
❓ **Ask Questions:**
• `ask <your question>` - Ask any question about meetings
• Or just type your question directly!
 
**Examples:**
• `ask What was discussed about budget?`
• `ask Who is responsible for training?`
• `What are the timeline concerns?`";
        }

        private string GetStatusText(string teamId)
        {
            return $@"✅ **Bot Status**
• Team ID: `{teamId}`
• Status: Connected
• Backend: Online";
        }

        private async Task<string> GetSummaryAsync(string teamId, string? meetingId)
        {
            if (!string.IsNullOrEmpty(meetingId))
            {
                var response = await _meetingService.GetMeetingSummaryAsync(teamId, meetingId.ToUpper());
                if (!response.Success)
                    return $"❌ Meeting {meetingId} not found.";

                var s = response.Summary!;
                return $@"📝 **Meeting Summary: {s.MeetingId}**
📅 Date: {s.MeetingDate:MMM dd, yyyy}
 
{s.Summary}
 
**Key Points:**
{string.Join("\n", s.KeyPoints.Select(p => $"• {p}"))}
 
**Participants:** {string.Join(", ", s.Participants)}";
            }
            else
            {
                var response = await _meetingService.GetTeamMeetingsSummaryAsync(teamId);
                return $@"📊 **Team Executive Summary**
📅 {response.TotalMeetings} meetings ({response.FirstMeetingDate:MMM dd} - {response.LastMeetingDate:MMM dd, yyyy})
 
{response.ExecutiveSummary}
 
**Key Themes:** {string.Join(", ", response.OverallKeyThemes)}
**Participants:** {string.Join(", ", response.AllParticipants.Take(5))}{(response.AllParticipants.Count > 5 ? $" +{response.AllParticipants.Count - 5} more" : "")}";
            }
        }

        private async Task<string> GetDecisionsAsync(string teamId)
        {
            var response = await _decisionService.GetAllDecisionsAsync(teamId);
            var recentDecisions = response.Decisions
                .OrderByDescending(d => d.DecisionDate)
                .Take(5);

            return $@"📋 **Decision Log**
Total: {response.TotalDecisions} decisions | ⚠️ {response.DecisionsWithConflicts} with changes
 
**Recent Decisions:**
{string.Join("\n", recentDecisions.Select(d =>
        $"• [{d.MeetingId}] {d.Topic}: {d.Description}{(d.HasConflict ? " ⚠️" : "")}"))}
 
**By Category:**
{string.Join(" | ", response.DecisionsByCategory.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}
 
_Use `conflicts` to see decisions that changed over time._";
        }

        private async Task<string> GetConflictsAsync(string teamId)
        {
            var response = await _decisionService.GetConflictsAsync(teamId);

            if (response.TotalConflicts == 0)
                return "✅ **No Decision Conflicts**\nAll decisions have remained consistent across meetings.";

            var conflictList = string.Join("\n\n", response.Conflicts.Take(3).Select(c =>
                $@"**{c.Topic}** ({c.Category})
Type: {c.OverallChangeType}
{c.ConflictSummary}
Current: {c.CurrentState}"));

            return $@"⚠️ **Decision Conflicts Detected**
Found {response.TotalConflicts} topics with changes:
 
{conflictList}
 
**Assessment:** {response.OverallAssessment}";
        }

        private async Task<string> GetActionItemsAsync(string teamId)
        {
            var response = await _actionItemService.GetAllActionItemsAsync(teamId);

            var summary = $@"📋 **Action Items Summary**
Total: {response.TotalActionItems} | ✅ {response.CompletedCount} | 🔄 {response.InProgressCount} | 📌 {response.AssignedCount} | 🚫 {response.BlockedCount}
 
**By Person:**
{string.Join("\n", response.ItemsByPerson.Take(5).Select(kvp => $"• {kvp.Key}: {kvp.Value} items"))}";

            var recentItems = response.ActionItems
                .Where(a => a.Status != ActionItemStatus.Completed)
                .Take(5);

            if (recentItems.Any())
            {
                summary += $@"
 
**Active Items:**
{string.Join("\n", recentItems.Select(a =>
        $"• [{GetStatusEmoji(a.Status)}] {a.AssignedTo}: {a.Task}"))}";
            }

            return summary;
        }

        private async Task<string> GetPendingActionsAsync(string teamId)
        {
            var response = await _actionItemService.GetAllActionItemsAsync(teamId);
            var pending = response.ActionItems
                .Where(a => a.Status != ActionItemStatus.Completed)
                .OrderBy(a => a.AssignedDate)
                .ToList();

            if (pending.Count == 0)
                return "✅ **All Action Items Completed!**\nNo pending tasks.";

            return $@"📌 **Pending Action Items** ({pending.Count} items)
 
{string.Join("\n", pending.Take(10).Select(a =>
        $"• [{GetStatusEmoji(a.Status)}] **{a.AssignedTo}**: {a.Task}\n  _Assigned: {a.AssignedDate:MMM dd} in {a.AssignedInMeetingId}_"))}
 
{(pending.Count > 10 ? $"_... and {pending.Count - 10} more_" : "")}";
        }

        private async Task<string> GetSentimentAsync(string teamId)
        {
            var response = await _sentimentService.GetTeamSentimentAsync(teamId);

            var trendEmojis = response.Trend.Select(t => GetSentimentEmoji(t.Category));

            return $@"📊 **Team Sentiment Analysis**
Overall: {GetSentimentEmoji(response.OverallCategory)} {response.OverallCategory} ({response.OverallTeamSentiment:F2})
 
**Trend:** {string.Join("", trendEmojis)}
 
{response.TeamMoodSummary}
 
**By Speaker:**
{string.Join("\n", response.SpeakerAverages.Take(5).Select(s =>
        $"• {s.SpeakerName}: {GetSentimentEmoji(s.Category)} {s.Category}"))}
 
{(response.Concerns.Count > 0 ? $"\n⚠️ {response.Concerns.Count} concerns flagged. Use `concerns` for details." : "")}";
        }

        private async Task<string> GetConcernsAsync(string teamId)
        {
            var response = await _sentimentService.GetSentimentConcernsAsync(teamId);

            if (response.TotalConcerns == 0)
                return "✅ **No Sentiment Concerns**\nTeam morale appears stable across all meetings.";

            return $@"⚠️ **Sentiment Concerns** ({response.TotalConcerns} flagged)
 
{response.OverallAssessment}
 
**Flagged Items:**
{string.Join("\n", response.Concerns.Take(5).Select(c =>
        $"• [{c.MeetingId}] {c.ConcernType}: {c.Description}"))}
 
**Recommended Actions:**
{string.Join("\n", response.RecommendedActions.Select(a => $"• {a}"))}";
        }

        private async Task<string> AskQuestionAsync(string teamId, string? question)
        {
            if (string.IsNullOrWhiteSpace(question))
                return "❓ Please provide a question. Example: `ask What was the budget decision?`";

            var request = new QueryRequest
            {
                TeamId = teamId,
                Question = question,
                MaxResults = 5
            };

            var response = await _queryService.ProcessQueryAsync(request);

            var result = $@"💡 **Answer:**
{response.Answer}";

            if (response.Timeline?.Count > 0)
            {
                result += $@"
 
📅 **Timeline:**
{string.Join("\n", response.Timeline.Select(t =>
        $"• [{t.MeetingId}] {t.Date:MMM dd}: {t.Summary}"))}";
            }

            if (response.HasConflicts)
            {
                result += $"\n\n⚠️ **Note:** {response.ConflictSummary}";
            }

            result += $"\n\n_Sources: {string.Join(", ", response.Sources?.Select(s => s.MeetingId).Distinct() ?? Array.Empty<string>())}_";

            return result;
        }

        private async Task<string> GetMeetingsListAsync(string teamId)
        {
            var meetings = await _meetingService.ListMeetingsAsync(teamId);

            return $@"📅 **Meetings List** ({meetings.Count} meetings)
 
{string.Join("\n", meetings.Select(m =>
        $"• **{m.MeetingId}** - {m.MeetingDate:MMM dd, yyyy}"))}
 
_Use `summary {meetings.FirstOrDefault()?.MeetingId ?? "MTG-001"}` for details._";
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════════════════════════

        private string GetOrCreateTeamId(string channelId, string? teamName)
        {
            if (_teamMappings.TryGetValue(channelId, out var existingId))
                return existingId;

            // For demo, use TEAM_A. In production, generate unique ID
            var teamId = "TEAM_A"; // Or generate: $"TEAM_{Guid.NewGuid().ToString("N")[..8].ToUpper()}"
            _teamMappings[channelId] = teamId;
            _logger.LogInformation("Mapped channel {ChannelId} to team {TeamId}", channelId, teamId);
            return teamId;
        }

        private string GetStatusEmoji(ActionItemStatus status) => status switch
        {
            ActionItemStatus.Completed => "✅",
            ActionItemStatus.InProgress => "🔄",
            ActionItemStatus.Blocked => "🚫",
            ActionItemStatus.Assigned => "📌",
            _ => "❓"
        };

        private string GetSentimentEmoji(SentimentCategory category) => category switch
        {
            SentimentCategory.VeryPositive => "😊",
            SentimentCategory.Positive => "🙂",
            SentimentCategory.Neutral => "😐",
            SentimentCategory.Negative => "😟",
            SentimentCategory.VeryNegative => "😰",
            _ => "❓"
        };
    }
}
