using MeetingInsights.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using static MeetingInsights.Core.Models.QueryModels;

namespace MeetingInsights.Core.Services.Interfaces
{
    public interface ISnowflakeService
    {
        Task<List<MeetingTranscript>> GetAllTranscriptsAsync();
        Task<int> SaveProcessedChunksAsync(List<ProcessedChunk> chunks);
        Task<List<SearchResult>> SearchWithCortexAsync(string query, string teamId, int maxResults = 5);
        Task<string> GenerateAnswerWithCortexAsync(string prompt);
        Task<string> GetMeetingTranscriptTextAsync(string teamId, string meetingId);
        Task<List<MeetingTranscript>> GetMeetingsByTeamAsync(string teamId);
        Task<string> SummarizeWithCortexAsync(string text);
    }
}
