using MeetingInsights.Core.Models;
using static MeetingInsights.Core.Models.QueryModels;

namespace MeetingInsights.TeamsBot.Services.Interfaces
{
    public interface IBackendAPIService
    {
        Task<QueryResponse> AskQuestionAsync(string teamId, string question);
        Task<DecisionsResponse> GetDecisionsAsync(string teamId);
        Task<ConflictsResponse> GetConflictsAsync(string teamId);
        Task<ActionItemsResponse> GetActionItemsAsync(string teamId);
        Task<TeamSentimentResponse> GetSentimentAsync(string teamId);
        Task<TeamMeetingsSummary> GetTeamSummaryAsync(string teamId);
        Task<MeetingSummaryResponse> GetMeetingSummaryAsync(string teamId, string meetingId);
        Task<bool> ProcessTranscriptAsync(string teamId, string meetingId, string transcript, DateTime meetingDate);
    }
}
