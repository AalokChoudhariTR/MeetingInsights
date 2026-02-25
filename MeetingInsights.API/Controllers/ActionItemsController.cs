using MeetingInsights.Core.Models;
using MeetingInsights.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MeetingInsights.API.Controllers
{
    [ApiController]
    [Route("api/meetings")]
    public class ActionItemsController : ControllerBase
    {
        private readonly IActionItemService _actionItemService;
        private readonly ILogger<ActionItemsController> _logger;

        public ActionItemsController(
            IActionItemService actionItemService,
            ILogger<ActionItemsController> logger)
        {
            _actionItemService = actionItemService;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/action-items
        /// Get ALL action items across all meetings for a team
        /// </summary>
        [HttpGet("{teamId}/action-items")]
        public async Task<ActionResult<ActionItemsResponse>> GetAllActionItems(string teamId)
        {
            _logger.LogInformation("Getting all action items for team {TeamId}", teamId);
            var response = await _actionItemService.GetAllActionItemsAsync(teamId);
            if (!response.Success)
            {
                return StatusCode(500, new { error = "Failed to extract action items" });
            }
            return Ok(response);
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/{meetingId}/action-items
        /// Get action items from a specific meeting
        /// </summary>
        [HttpGet("{teamId}/{meetingId}/action-items")]
        public async Task<ActionResult<MeetingActionItemsResponse>> GetMeetingActionItems(string teamId, string meetingId)
        {
            _logger.LogInformation("Getting action items for meeting {MeetingId}", meetingId);
            var response = await _actionItemService.GetMeetingActionItemsAsync(teamId, meetingId);
            if (!response.Success)
            {
                return NotFound(new { error = $"Meeting {meetingId} not found" });
            }
            return Ok(response);
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/action-items/by-person/{personName}
        /// Get action items assigned to a specific person
        /// </summary>
        [HttpGet("{teamId}/action-items/by-person/{personName}")]
        public async Task<ActionResult> GetActionItemsByPerson(string teamId, string personName)
        {
            _logger.LogInformation("Getting action items for {PersonName} in team {TeamId}", personName, teamId);
            var allItems = await _actionItemService.GetAllActionItemsAsync(teamId);
            var personItems = allItems.ActionItems
                .Where(a => a.AssignedTo.Contains(personName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return Ok(new
            {
                teamId = teamId,
                personName = personName,
                totalItems = personItems.Count,
                completedCount = personItems.Count(a => a.Status == ActionItemStatus.Completed),
                inProgressCount = personItems.Count(a => a.Status == ActionItemStatus.InProgress),
                actionItems = personItems
            });
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/action-items/pending
        /// Get only pending (not completed) action items
        /// </summary>
        [HttpGet("{teamId}/action-items/pending")]
        public async Task<ActionResult> GetPendingActionItems(string teamId)
        {
            _logger.LogInformation("Getting pending action items for team {TeamId}", teamId);
            var allItems = await _actionItemService.GetAllActionItemsAsync(teamId);
            var pendingItems = allItems.ActionItems
                .Where(a => a.Status != ActionItemStatus.Completed)
                .OrderBy(a => a.AssignedDate)
                .ToList();

            return Ok(new
            {
                teamId = teamId,
                totalPending = pendingItems.Count,
                byStatus = new
                {
                    assigned = pendingItems.Count(a => a.Status == ActionItemStatus.Assigned),
                    inProgress = pendingItems.Count(a => a.Status == ActionItemStatus.InProgress),
                    blocked = pendingItems.Count(a => a.Status == ActionItemStatus.Blocked)
                },
                actionItems = pendingItems
            });
        }
    }
}
