using MeetingInsights.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MeetingInsights.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MeetingsController : ControllerBase
    {
        private readonly IMeetingService _meetingService;
        private readonly ILogger<MeetingsController> _logger;

        public MeetingsController(
            IMeetingService meetingService,
            ILogger<MeetingsController> logger)
        {
            _meetingService = meetingService;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/meetings/{teamId}
        /// List all meetings for a team
        /// </summary>
        [HttpGet("{teamId}")]
        public async Task<ActionResult> ListMeetings(string teamId)
        {
            _logger.LogInformation("Listing meetings for team {TeamId}", teamId);
            var meetings = await _meetingService.ListMeetingsAsync(teamId);
            return Ok(new
            {
                teamId = teamId,
                count = meetings.Count,
                meetings = meetings
            });
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/summary
        /// Get executive summary of ALL meetings for a team
        /// </summary>
        [HttpGet("{teamId}/summary")]
        public async Task<ActionResult> GetTeamSummary(string teamId)
        {
            _logger.LogInformation("Generating team summary for {TeamId}", teamId);
            var summary = await _meetingService.GetTeamMeetingsSummaryAsync(teamId);
            return Ok(summary);
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/{meetingId}/summary
        /// Get detailed summary of a specific meeting
        /// </summary>
        [HttpGet("{teamId}/{meetingId}/summary")]
        public async Task<ActionResult> GetMeetingSummary(string teamId, string meetingId)
        {
            _logger.LogInformation("Generating summary for meeting {MeetingId}", meetingId);
            var response = await _meetingService.GetMeetingSummaryAsync(teamId, meetingId);
            if (!response.Success)
            {
                return NotFound(new { error = response.Error });
            }
            return Ok(response);
        }
    }
}
