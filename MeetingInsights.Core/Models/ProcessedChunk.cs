using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingInsights.Core.Models
{
    public class ProcessedChunk
    {
        public string ChunkId { get; set; } = string.Empty;
        public string MeetingId { get; set; } = string.Empty;
        public DateTime MeetingDate { get; set; }
        public string TeamId { get; set; } = string.Empty;
        public int ChunkSequence { get; set; }
        public string ChunkText { get; set; } = string.Empty;
        public string EnrichedText { get; set; } = string.Empty;
        public int CharacterCount { get; set; }
    }
}
