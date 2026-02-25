using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingInsights.Core.Models
{
    public class QueryModels
    {
        public class QueryRequest
        {
            public string Question {  get; set; } = string.Empty;
            public string TeamId { get; set; } = string.Empty;
            public int MaxResults { get; set; } = 5;
        }

        public class SearchResult
        {
            public string ChunkId { get; set; } = string.Empty;
            public string MeetingId { get; set; } = string.Empty;
            public DateTime MeetingDate { get; set; }
            public string EnrichedText { get; set; } = string.Empty;
            public double SimilarityScore { get; set; }
        }

        public class QueryResponse
        {
            public bool Success { get; set; }
            public string Question { get; set; } = string.Empty;
            public string Answer { get; set; } = string.Empty;
            public bool HasConflicts { get; set; }
            public string? ConflictSummary { get; set; } = string.Empty;
            public List<TimelineEntry> Timeline { get; set; } = new();
            public List<SourceReference> Sources { get; set; } = new();
            public int ProcessingTimeMs { get; set; }
        }

        public class TimelineEntry
        {
            public DateTime Date { get; set; }
            public string MeetingId { get; set; } = string.Empty;
            public string Summary {  get; set; } = string.Empty;
        }

        public class SourceReference
        {
            public string ChunkId { get; set; } = string.Empty;
            public string MeetingId { get; set; } = string.Empty;
            public DateTime MeetingDate { get; set; }
            public string TextPreview { get; set; } = string.Empty;
        }

    }
}
