using MeetingInsights.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingInsights.Core.Services.Interfaces
{
    public interface IAtomizerService
    {
        List<ProcessedChunk> ProcessTranscripts(List<MeetingTranscript> meetingTranscripts);
        string CleanText(string rawText);
        List<string> ChunkText(string text, int chunkSize = 500);
    }
}
