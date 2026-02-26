using MeetingInsights.TeamsBot.Cards;
using MeetingInsights.TeamsBot.Commands.Interfaces;
using MeetingInsights.TeamsBot.Services.Interfaces;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace MeetingInsights.TeamsBot.Commands
{
    public class SummaryCommandHandler : ICommandHandler
    {
        private readonly IBackendAPIService _apiService;
        public string Command => "summary";
        public string Description => "Get meeting summary (all or specific meeting)";

        public SummaryCommandHandler(IBackendAPIService apiService)
        {
            _apiService = apiService;
        }

        public async Task<IMessageActivity> HandleAsync(ITurnContext context, string teamId, string[] args)
        {
            await context.SendActivityAsync(new Activity { Type = ActivityTypes.Typing });

            // Check if specific meeting requested
            if (args.Length > 0)
            {
                var meetingId = args[0];
                var meetingResponse = await _apiService.GetMeetingSummaryAsync(teamId, meetingId);

                if (!meetingResponse.Success)
                {
                    return MessageFactory.Text($"❌ Meeting {meetingId} not found.");
                }

                var meetingCard = AdaptiveCardBuilder.BuildMeetingSummaryCard(meetingResponse);
                return MessageFactory.Attachment(meetingCard);
            }

            // Get team summary
            var response = await _apiService.GetTeamSummaryAsync(teamId);
            var card = AdaptiveCardBuilder.BuildTeamSummaryCard(response);
            return MessageFactory.Attachment(card);
        }
    }
}
