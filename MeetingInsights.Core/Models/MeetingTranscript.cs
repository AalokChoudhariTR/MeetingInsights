using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MeetingInsights.Core.Models
{
    public class MeetingTranscript
    { 
        public string MeetingId { get; set; } = string.Empty;
        public DateTime MeetingDate { get; set; }
        public string TeamId { get; set; } = string.Empty;
        public int OriginalId { get; set; }
        public string TranscriptText { get; set; } = string.Empty;
        public string SourceSystem { get; set; } = string.Empty;
    }
}
