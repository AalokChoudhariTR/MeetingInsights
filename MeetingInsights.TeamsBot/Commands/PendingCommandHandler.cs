using MeetingInsights.Core.Models;
using MeetingInsights.TeamsBot.Cards;
using MeetingInsights.TeamsBot.Commands.Interfaces;
using MeetingInsights.TeamsBot.Services.Interfaces;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace MeetingInsights.TeamsBot.Commands
{
    public class PendingCommandHandler : ICommandHandler
    {
        private readonly IBackendAPIService _apiService;
        public string Command => "pending";
        public string Description => "View pending action items only";

        public PendingCommandHandler(IBackendAPIService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IMessageActivity> HandleAsync(ITurnContext context, string teamId, string[] args)
        {
            await context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing });

            var response = await _apiService.GetActionItemsAsync(teamId);

            if (!response.Success)
            {
                return MessageFactory.Text("❌ Unable to retrieve action items. Please try again.");
            }

            // Filter to pending only
            var pendingItems = response.ActionItems
                .Where(a => a.Status != ActionItemStatus.Completed)
                .ToList();

            var card = AdaptiveCardBuilder.BuildPendingItemsCard(pendingItems, teamId);
            return MessageFactory.Attachment(card);
        }
    }
}
