using MeetingInsights.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static MeetingInsights.Core.Models.QueryModels;

namespace MeetingInsights.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QueryController : ControllerBase
    {

        private readonly IQueryService _queryService;
        private readonly ILogger<QueryController> _logger;

        public QueryController(
            IQueryService queryService,
            ILogger<QueryController> logger)
        {
            _queryService = queryService;
            _logger = logger;
        }

        /// <summary>
        /// POST /api/query
        /// Process a question through the full pipeline
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<QueryResponse>> Query([FromBody] QueryRequest request)
        {

            if(string.IsNullOrWhiteSpace(request.TeamId))
            {
                return BadRequest(new { error = "TeamId is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest(new { error = "Question is required" });
            }

            _logger.LogInformation("Received query: {Question}", request.Question);
            var response = await _queryService.ProcessQueryAsync(request);
            return Ok(response);
        }

        /// <summary>
        /// POST /api/query/search
        /// Search only - returns relevant chunks without AI answer
        /// </summary>

        [HttpPost("search")]
        public async Task<ActionResult> SearchOnly([FromBody] QueryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
            {
                return BadRequest(new { error = "Question is required" });
            }

            var results = await _queryService.SearchChunksAsync(request.Question, request.TeamId, request.MaxResults);

            return Ok(new
            {
                teamId = request.TeamId,
                query = request.Question,
                resultCount = results.Count,
                results = results.Select(r => new
                {
                    r.ChunkId,
                    r.MeetingId,
                    r.MeetingDate,
                    r.SimilarityScore,
                    textPreview = r.EnrichedText.Length > 300
                        ? r.EnrichedText.Substring(0, 300) + "..."
                        : r.EnrichedText
                })
            });
        }
    }

}
