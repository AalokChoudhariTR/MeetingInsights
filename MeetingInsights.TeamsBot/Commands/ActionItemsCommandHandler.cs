using MeetingInsights.TeamsBot.Cards;
using MeetingInsights.TeamsBot.Commands.Interfaces;
using MeetingInsights.TeamsBot.Services.Interfaces;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace MeetingInsights.TeamsBot.Commands
{
    public class ActionItemsCommandHandler : ICommandHandler
    {
        private readonly IBackendAPIService _apiService;
        public string Command => "action-items";
        public string Description => "View all action items";

        public ActionItemsCommandHandler(IBackendAPIService apiService)
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

            var card = AdaptiveCardBuilder.BuildActionItemsCard(response);
            return MessageFactory.Attachment(card);
        }
    }
}
