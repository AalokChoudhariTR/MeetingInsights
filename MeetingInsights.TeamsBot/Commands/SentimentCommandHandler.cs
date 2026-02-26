using MeetingInsights.TeamsBot.Cards;
using MeetingInsights.TeamsBot.Commands.Interfaces;
using MeetingInsights.TeamsBot.Services.Interfaces;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace MeetingInsights.TeamsBot.Commands
{
    public class SentimentCommandHandler : ICommandHandler
    {
        private readonly IBackendAPIService _apiService;
        public string Command => "sentiment";
        public string Description => "View team sentiment analysis";

        public SentimentCommandHandler(IBackendAPIService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IMessageActivity> HandleAsync(ITurnContext context, string teamId, string[] args)
        {
            await context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing });

            var response = await _apiService.GetSentimentAsync(teamId);

            if (!response.Success)
            {
                return MessageFactory.Text("❌ Unable to retrieve sentiment analysis. Please try again.");
            }

            var card = AdaptiveCardBuilder.BuildSentimentCard(response);
            return MessageFactory.Attachment(card);
        }
    }
}
