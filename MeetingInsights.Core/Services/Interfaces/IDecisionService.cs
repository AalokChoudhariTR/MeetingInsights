using MeetingInsights.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingInsights.Core.Services.Interfaces
{
    public interface IDecisionService
    {
        /// <summary>
        /// Get all decisions across all meetings with conflict detection
        /// </summary>
        Task<DecisionsResponse> GetAllDecisionsAsync(string teamId);
        /// <summary>
        /// Get only conflicting/changed decisions
        /// </summary>
        Task<ConflictsResponse> GetConflictsAsync(string teamId);
        /// <summary>
        /// Get decisions from a specific meeting
        /// </summary>
        Task<List<Decision>> GetMeetingDecisionsAsync(string teamId, string meetingId);
    }
}
