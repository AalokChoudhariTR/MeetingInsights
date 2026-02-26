using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingInsights.Core.Models
{
    /// <summary>
    /// Type of decision change
    /// </summary>
    public enum DecisionChangeType
    {
        None,           // No change - consistent
        Evolved,        // Natural evolution/refinement
        Reversed,       // Complete reversal
        Expanded,       // Scope expanded
        Reduced,        // Scope reduced
        Clarified,      // Previous ambiguity clarified
        Contradicted    // Direct contradiction
    }

    /// <summary>
    /// Category of decision
    /// </summary>
    public enum DecisionCategory
    {
        Budget,
        Timeline,
        Staffing,
        Technical,
        Vendor,
        Process,
        Scope,
        Other
    }

    /// <summary>
    /// Represents a decision made in a meeting
    /// </summary>
    public class Decision
    {
        public string DecisionId { get; set; } = string.Empty;           // DEC-001
        public string Topic { get; set; } = string.Empty;                // What the decision is about
        public string Description { get; set; } = string.Empty;          // The actual decision
        public string MeetingId { get; set; } = string.Empty;            // Where it was made
        public DateTime DecisionDate { get; set; }                       // When
        public string DecisionMaker { get; set; } = string.Empty;        // Who made/announced it
        public DecisionCategory Category { get; set; }                   // Type of decision
        public string OriginalQuote { get; set; } = string.Empty;        // Exact quote
        public bool HasConflict { get; set; }                            // Does this conflict with another?
        public string? ConflictsWith { get; set; }                       // Which decision it conflicts with
        public DecisionChangeType ChangeType { get; set; }               // Type of change
        public List<DecisionEvolution> Evolution { get; set; } = new();  // How it evolved
    }

    /// <summary>
    /// Tracks how a decision evolved across meetings
    /// </summary>
    public class DecisionEvolution
    {
        public string MeetingId { get; set; } = string.Empty;
        public DateTime MeetingDate { get; set; }
        public string PreviousState { get; set; } = string.Empty;
        public string NewState { get; set; } = string.Empty;
        public DecisionChangeType ChangeType { get; set; }
        public string ChangeDescription { get; set; } = string.Empty;
        public string Quote { get; set; } = string.Empty;
    }

    /// <summary>
    /// A topic that had conflicting or changing decisions
    /// </summary>
    public class ConflictingDecision
    {
        public string Topic { get; set; } = string.Empty;
        public DecisionCategory Category { get; set; }
        public DecisionChangeType OverallChangeType { get; set; }
        public string ConflictSummary { get; set; } = string.Empty;
        public List<DecisionPoint> Timeline { get; set; } = new();
        public string CurrentState { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
    }

    /// <summary>
    /// A point in the decision timeline
    /// </summary>
    public class DecisionPoint
    {
        public string MeetingId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Decision { get; set; } = string.Empty;
        public string DecisionMaker { get; set; } = string.Empty;
        public DecisionChangeType? ChangeFromPrevious { get; set; }
    }

    /// <summary>
    /// Response for all decisions
    /// </summary>
    public class DecisionsResponse
    {
        public bool Success { get; set; }
        public string TeamId { get; set; } = string.Empty;
        public int TotalDecisions { get; set; }
        public int DecisionsWithConflicts { get; set; }
        public Dictionary<string, int> DecisionsByCategory { get; set; } = new();
        public Dictionary<string, int> DecisionsByMeeting { get; set; } = new();
        public List<Decision> Decisions { get; set; } = new();
        public List<ConflictingDecision> Conflicts { get; set; } = new();
        public int ProcessingTimeMs { get; set; }
    }

    /// <summary>
    /// Response for conflicts only
    /// </summary>
    public class ConflictsResponse
    {
        public bool Success { get; set; }
        public string TeamId { get; set; } = string.Empty;
        public int TotalConflicts { get; set; }
        public string OverallAssessment { get; set; } = string.Empty;
        public List<ConflictingDecision> Conflicts { get; set; } = new();
        public int ProcessingTimeMs { get; set; }
    }
}
