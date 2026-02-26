using MeetingInsights.TeamsBot.Cards;
using MeetingInsights.TeamsBot.Commands.Interfaces;
using MeetingInsights.TeamsBot.Services.Interfaces;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace MeetingInsights.TeamsBot.Commands
{
    public class AskCommandHandler : ICommandHandler
    {
        private readonly IBackendAPIService _apiService;
        public string Command => "ask";
        public string Description => "Ask a question about meetings";

        public AskCommandHandler(IBackendAPIService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IMessageActivity> HandleAsync(ITurnContext context, string teamId, string[] args)
        {
            if (args.Length == 0)
            {
                return MessageFactory.Text("Please provide a question. Example: `@bot ask What was discussed about budget?`");
            }

            var question = string.Join(" ", args);

            // Send typing indicator
            await context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing });

            var response = await _apiService.AskQuestionAsync(teamId, question);

            if (!response.Success)
            {
                return MessageFactory.Text("❌ Sorry, I couldn't process your question. Please try again.");
            }

            // Build adaptive card response
            var card = AdaptiveCardBuilder.BuildQueryResponseCard(response);

            return MessageFactory.Attachment(card);
        }
    }
}
