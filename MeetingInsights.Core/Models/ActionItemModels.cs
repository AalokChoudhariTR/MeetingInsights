using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingInsights.Core.Models
{
    public enum ActionItemStatus
    {
        Assigned,
        InProgress,
        Completed,
        Blocked,
        Unknown
    }
    /// <summary>
    /// Represents an extracted action item
    /// </summary>
    public class ActionItem
    {
        public string ActionItemId { get; set; } = string.Empty;       // AI-001, AI-002
        public string AssignedTo { get; set; } = string.Empty;         // Person's name
        public string Task { get; set; } = string.Empty;               // What needs to be done
        public string AssignedInMeetingId { get; set; } = string.Empty;// MTG-001
        public DateTime AssignedDate { get; set; }                     // When assigned
        public ActionItemStatus Status { get; set; }                   // Current status
        public string? JiraTicket { get; set; }                        // JIRA reference if mentioned
        public string? DueDate { get; set; }                           // If a deadline was mentioned
        public List<ActionItemUpdate> Updates { get; set; } = new();   // Status updates from later meetings
        public string? CompletedInMeetingId { get; set; }              // Which meeting marked it complete
        public string OriginalQuote { get; set; } = string.Empty;      // Original text where assigned
    }

    /// <summary>
    /// Status update for an action item found in a later meeting
    /// </summary>
    public class ActionItemUpdate
    {
        public string MeetingId { get; set; } = string.Empty;
        public DateTime MeetingDate { get; set; }
        public string UpdateText { get; set; } = string.Empty;
        public ActionItemStatus NewStatus { get; set; }
    }

    /// <summary>
    /// Response for action items endpoint
    /// </summary>
    public class ActionItemsResponse
    {
        public bool Success { get; set; }
        public string TeamId { get; set; } = string.Empty;
        public int TotalActionItems { get; set; }
        public int CompletedCount { get; set; }
        public int InProgressCount { get; set; }
        public int AssignedCount { get; set; }
        public int BlockedCount { get; set; }
        public Dictionary<string, int> ItemsByPerson { get; set; } = new();
        public List<ActionItem> ActionItems { get; set; } = new();
        public int ProcessingTimeMs { get; set; }
    }

    /// <summary>
    /// Single meeting action items response
    /// </summary>
    public class MeetingActionItemsResponse
    {
        public bool Success { get; set; }
        public string MeetingId { get; set; } = string.Empty;
        public DateTime MeetingDate { get; set; }
        public int ActionItemCount { get; set; }
        public List<ActionItem> ActionItems { get; set; } = new();
        public int ProcessingTimeMs { get; set; }
    }
}
