using MeetingInsights.Core.Models;
using MeetingInsights.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MeetingInsights.API.Controllers
{
    [ApiController]
    [Route("api/meetings")]
    public class DecisionsController : ControllerBase
    {
        private readonly IDecisionService _decisionService;
        private readonly ILogger<DecisionsController> _logger;

        public DecisionsController(
            IDecisionService decisionService,
            ILogger<DecisionsController> logger)
        {
            _decisionService = decisionService;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/decisions
        /// Get ALL decisions across all meetings with conflict detection
        /// </summary>
        [HttpGet("{teamId}/decisions")]
        public async Task<ActionResult<DecisionsResponse>> GetAllDecisions(string teamId)
        {
            _logger.LogInformation("Getting all decisions for team {TeamId}", teamId);

            var response = await _decisionService.GetAllDecisionsAsync(teamId);

            if (!response.Success)
            {
                return StatusCode(500, new { error = "Failed to extract decisions" });
            }

            return Ok(response);
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/decisions/conflicts
        /// Get ONLY decisions that have conflicts or changes
        /// </summary>
        [HttpGet("{teamId}/decisions/conflicts")]
        public async Task<ActionResult<ConflictsResponse>> GetConflicts(string teamId)
        {
            _logger.LogInformation("Getting decision conflicts for team {TeamId}", teamId);

            var response = await _decisionService.GetConflictsAsync(teamId);

            if (!response.Success)
            {
                return StatusCode(500, new { error = "Failed to analyze conflicts" });
            }

            return Ok(response);
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/{meetingId}/decisions
        /// Get decisions from a specific meeting
        /// </summary>
        [HttpGet("{teamId}/{meetingId}/decisions")]
        public async Task<ActionResult> GetMeetingDecisions(string teamId, string meetingId)
        {
            _logger.LogInformation("Getting decisions for meeting {MeetingId}", meetingId);

            var decisions = await _decisionService.GetMeetingDecisionsAsync(teamId, meetingId);

            return Ok(new
            {
                meetingId = meetingId,
                count = decisions.Count,
                decisions = decisions
            });
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/decisions/by-category/{category}
        /// Get decisions filtered by category
        /// </summary>
        [HttpGet("{teamId}/decisions/by-category/{category}")]
        public async Task<ActionResult> GetDecisionsByCategory(string teamId, string category)
        {
            _logger.LogInformation("Getting {Category} decisions for team {TeamId}", category, teamId);

            var allDecisions = await _decisionService.GetAllDecisionsAsync(teamId);

            var filteredDecisions = allDecisions.Decisions
                .Where(d => d.Category.ToString().Equals(category, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.DecisionDate)
                .ToList();

            // Get related conflicts
            var relatedConflicts = allDecisions.Conflicts
                .Where(c => c.Category.ToString().Equals(category, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Ok(new
            {
                teamId = teamId,
                category = category,
                count = filteredDecisions.Count,
                conflictsInCategory = relatedConflicts.Count,
                decisions = filteredDecisions,
                conflicts = relatedConflicts
            });
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/decisions/timeline
        /// Get a visual timeline of all decisions
        /// </summary>
        [HttpGet("{teamId}/decisions/timeline")]
        public async Task<ActionResult> GetDecisionTimeline(string teamId)
        {
            _logger.LogInformation("Getting decision timeline for team {TeamId}", teamId);

            var allDecisions = await _decisionService.GetAllDecisionsAsync(teamId);

            // Group by meeting date
            var timeline = allDecisions.Decisions
                .GroupBy(d => d.MeetingId)
                .OrderBy(g => g.First().DecisionDate)
                .Select(g => new
                {
                    meetingId = g.Key,
                    date = g.First().DecisionDate,
                    decisionCount = g.Count(),
                    decisions = g.Select(d => new
                    {
                        d.DecisionId,
                        d.Topic,
                        d.Description,
                        d.Category,
                        d.DecisionMaker,
                        d.HasConflict,
                        d.ChangeType
                    })
                })
                .ToList();

            return Ok(new
            {
                teamId = teamId,
                totalMeetings = timeline.Count,
                totalDecisions = allDecisions.TotalDecisions,
                totalConflicts = allDecisions.DecisionsWithConflicts,
                timeline = timeline
            });
        }
    }
}
