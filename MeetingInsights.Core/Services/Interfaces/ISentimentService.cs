using MeetingInsights.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingInsights.Core.Services.Interfaces
{
    public interface ISentimentService
    {
        Task<TeamSentimentResponse> GetTeamSentimentAsync(string teamId);
        Task<MeetingSentimentResponse> GetMeetingSentimentAsync(string teamId, string meetingId);
        Task<SentimentConcernsResponse> GetSentimentConcernsAsync(string teamId);
    }
}
