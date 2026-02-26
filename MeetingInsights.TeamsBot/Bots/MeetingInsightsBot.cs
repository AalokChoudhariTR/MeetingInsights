using MeetingInsights.TeamsBot.Commands;
using MeetingInsights.TeamsBot.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;

namespace MeetingInsights.TeamsBot.Bots
{

    /// <summary>
    /// Main Teams Bot class
    /// </summary>
    public class MeetingInsightsBot : TeamsActivityHandler
    {
        private readonly ITeamRegistrationService _registrationService;
        private readonly CommandRouter _commandRouter;
        private readonly ILogger<MeetingInsightsBot> _logger;

        public MeetingInsightsBot(
            ITeamRegistrationService registrationService,
            CommandRouter commandRouter,
            ILogger<MeetingInsightsBot> logger)
        {
            _registrationService = registrationService;
            _commandRouter = commandRouter;
            _logger = logger;
        }

        /// <summary>
        /// Called when bot is added to a team
        /// </summary>
        protected override async Task OnTeamsMembersAddedAsync(
            IList<TeamsChannelAccount> membersAdded,
            TeamInfo teamInfo,
            ITurnContext<IConversationUpdateActivity> turnContext,
            CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                // Check if the bot itself was added
                if (member.Id == turnContext.Activity.Recipient.Id)
                {
                    var channelId = turnContext.Activity.Conversation.Id;
                    var teamName = teamInfo?.Name ?? "Unknown Team";
                    var addedBy = turnContext.Activity.From?.Name ?? "Unknown";

                    // Register the team
                    var registration = await _registrationService.RegisterTeamAsync(channelId, teamName, addedBy);

                    var welcomeMessage = $@"👋 **Hello! I'm the Meeting Insights Bot**

I've registered this team with ID: **{registration.TeamId}**

I'll help you:
• 📝 Search and query meeting transcripts
• 📋 Track action items and decisions
• 📊 Analyze team sentiment
• ⚠️ Detect conflicting decisions

**Quick Start:**
• Ask me anything: `@MeetingInsightsBot What was discussed about budget?`
• View commands: `@MeetingInsightsBot /help`

I'm ready to help! 🚀";

                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeMessage), cancellationToken);
                }
            }
        }

        /// <summary>
        /// Called when a message is received
        /// </summary>
        protected override async Task OnMessageActivityAsync(
            ITurnContext<IMessageActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var text = turnContext.Activity.Text?.Trim() ?? "";

            // Remove bot mention from text
            text = RemoveBotMention(text, turnContext.Activity);

            if (string.IsNullOrEmpty(text))
            {
                await turnContext.SendActivityAsync(
                    _commandRouter.GetHelpMessage(),
                    cancellationToken);
                return;
            }

            // Get team ID for this channel
            var channelId = turnContext.Activity.Conversation.Id;
            var registration = await _registrationService.GetTeamByChannelIdAsync(channelId);

            if (registration == null)
            {
                // Auto-register if not registered
                var teamName = "Auto-registered Team";
                var addedBy = turnContext.Activity.From?.Name ?? "System";
                registration = await _registrationService.RegisterTeamAsync(channelId, teamName, addedBy);

                await turnContext.SendActivityAsync(
                    MessageFactory.Text($"✅ Team registered with ID: **{registration.TeamId}**"),
                    cancellationToken);
            }

            _logger.LogInformation("Processing message from team {TeamId}: {Text}", registration.TeamId, text);

            try
            {
                // Route to appropriate command handler
                var response = await _commandRouter.RouteCommandAsync(turnContext, registration.TeamId, text);
                await turnContext.SendActivityAsync(response, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing command");
                await turnContext.SendActivityAsync(
                    MessageFactory.Text("❌ Sorry, something went wrong. Please try again."),
                    cancellationToken);
            }
        }

        /// <summary>
        /// Remove bot @mention from message text
        /// </summary>
        private string RemoveBotMention(string text, IMessageActivity activity)
        {
            if (activity.Entities == null) return text;

            foreach (var entity in activity.Entities)
            {
                if (entity.Type == "mention")
                {
                    var mention = entity.GetAs<Mention>();
                    if (mention?.Mentioned?.Id == activity.Recipient.Id)
                    {
                        text = text.Replace(mention.Text, "").Trim();
                    }
                }
            }

            return text;
        }
    }
}
