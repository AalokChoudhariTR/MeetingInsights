using MeetingInsights.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingInsights.Core.Services.Interfaces
{
    public interface IMeetingService
    {
        Task<MeetingSummaryResponse> GetMeetingSummaryAsync(string teamId, string meetingId);
        Task<TeamMeetingsSummary> GetTeamMeetingsSummaryAsync(string teamId);
        Task<List<MeetingSummaryBrief>> ListMeetingsAsync(string teamId);
    }
}
