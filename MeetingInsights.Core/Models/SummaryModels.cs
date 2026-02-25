using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingInsights.Core.Models
{

    /// <summary>
    /// Summary of a single meeting
    /// </summary>
    public class MeetingSummary
    {
        public string MeetingId { get; set; } = string.Empty;
        public DateTime MeetingDate { get; set; }
        public string TeamId { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> KeyPoints { get; set; } = new();
        public List<string> Participants { get; set; } = new();
        public List<string> TopicsDiscussed { get; set; } = new();
        public int OriginalLength { get; set; }
        public int SummaryLength { get; set; }
    }

    /// <summary>
    /// Summary of all meetings for a team
    /// </summary>
    public class TeamMeetingsSummary
    {
        public string TeamId { get; set; } = string.Empty;
        public int TotalMeetings { get; set; }
        public DateTime FirstMeetingDate { get; set; }
        public DateTime LastMeetingDate { get; set; }
        public string ExecutiveSummary { get; set; } = string.Empty;
        public List<string> OverallKeyThemes { get; set; } = new();
        public List<string> AllParticipants { get; set; } = new();
        public List<MeetingSummaryBrief> MeetingBriefs { get; set; } = new();
        public int ProcessingTimeMs { get; set; }
    }

    /// <summary>
    /// Brief summary of a meeting (used in team overview)
    /// </summary>
    public class MeetingSummaryBrief
    {
        public string MeetingId { get; set; } = string.Empty;
        public DateTime MeetingDate { get; set; }
        public string OneLinerSummary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response wrapper for single meeting summary
    /// </summary>
    public class MeetingSummaryResponse
    {
        public bool Success { get; set; }
        public MeetingSummary? Summary { get; set; }
        public string? Error { get; set; }
        public int ProcessingTimeMs { get; set; }
    }
}
