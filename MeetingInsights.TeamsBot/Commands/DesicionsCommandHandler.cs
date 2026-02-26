using MeetingInsights.TeamsBot.Cards;
using MeetingInsights.TeamsBot.Commands.Interfaces;
using MeetingInsights.TeamsBot.Services.Interfaces;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace MeetingInsights.TeamsBot.Commands
{
    public class DecisionsCommandHandler : ICommandHandler
    {
        private readonly IBackendAPIService _apiService;
        public string Command => "decisions";
        public string Description => "View all decisions made across meetings";

        public DecisionsCommandHandler(IBackendAPIService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IMessageActivity> HandleAsync(ITurnContext context, string teamId, string[] args)
        {
            await context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing });

            var response = await _apiService.GetDecisionsAsync(teamId);

            if (!response.Success)
            {
                return MessageFactory.Text("❌ Unable to retrieve decisions. Please try again.");
            }

            var card = AdaptiveCardBuilder.BuildDecisionsCard(response);
            return MessageFactory.Attachment(card);
        }
    }
}
