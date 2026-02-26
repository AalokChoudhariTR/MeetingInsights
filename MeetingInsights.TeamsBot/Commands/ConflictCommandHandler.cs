using MeetingInsights.TeamsBot.Cards;
using MeetingInsights.TeamsBot.Commands.Interfaces;
using MeetingInsights.TeamsBot.Services.Interfaces;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace MeetingInsights.TeamsBot.Commands
{
    public class ConflictsCommandHandler : ICommandHandler
    {
        private readonly IBackendAPIService _apiService;
        public string Command => "conflicts";
        public string Description => "View conflicting or changed decisions";

        public ConflictsCommandHandler(IBackendAPIService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IMessageActivity> HandleAsync(ITurnContext context, string teamId, string[] args)
        {
            await context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing });

            var response = await _apiService.GetConflictsAsync(teamId);

            if (!response.Success)
            {
                return MessageFactory.Text("❌ Unable to retrieve conflicts. Please try again.");
            }

            if (response.TotalConflicts == 0)
            {
                return MessageFactory.Text("✅ No conflicting decisions detected. All decisions appear consistent across meetings.");
            }

            var card = AdaptiveCardBuilder.BuildConflictsCard(response);
            return MessageFactory.Attachment(card);
        }
    }
}
