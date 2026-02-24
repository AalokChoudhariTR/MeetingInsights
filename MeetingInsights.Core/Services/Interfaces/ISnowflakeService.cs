using MeetingInsights.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingInsights.Core.Services.Interfaces
{
    public interface ISnowflakeService
    {
        Task<List<MeetingTranscript>> GetAllTranscriptsAsync();
        Task<int> SaveProcessedChunksAsync(List<ProcessedChunk> chunks);
    }
}
