using System;
using System.Collections.Generic;
using System.Text;
using static MeetingInsights.Core.Models.QueryModels;

namespace MeetingInsights.Core.Services.Interfaces
{
    public interface IQueryService
    {
        Task<QueryResponse> ProcessQueryAsync(QueryRequest request);
        Task<List<SearchResult>> SearchChunksAsync(string query, string teamId, int maxResults = 5);
    }
}
