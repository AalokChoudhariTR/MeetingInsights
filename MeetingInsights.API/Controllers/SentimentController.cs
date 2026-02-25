using MeetingInsights.Core.Models;
using MeetingInsights.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MeetingInsights.API.Controllers
{
    [ApiController]
    [Route("api/meetings")]
    public class SentimentController : ControllerBase
    {
        private readonly ISentimentService _sentimentService;
        private readonly ILogger<SentimentController> _logger;

        public SentimentController(
            ISentimentService sentimentService,
            ILogger<SentimentController> logger)
        {
            _sentimentService = sentimentService;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/sentiment
        /// Get overall team sentiment trends
        /// </summary>
        [HttpGet("{teamId}/sentiment")]
        public async Task<ActionResult<TeamSentimentResponse>> GetTeamSentiment(string teamId)
        {
            _logger.LogInformation("Getting team sentiment for {TeamId}", teamId);

            var response = await _sentimentService.GetTeamSentimentAsync(teamId);

            if (!response.Success)
            {
                return StatusCode(500, new { error = "Failed to analyze team sentiment" });
            }

            return Ok(response);
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/{meetingId}/sentiment
        /// Get detailed sentiment for a specific meeting
        /// </summary>
        [HttpGet("{teamId}/{meetingId}/sentiment")]
        public async Task<ActionResult<MeetingSentimentResponse>> GetMeetingSentiment(string teamId, string meetingId)
        {
            _logger.LogInformation("Getting sentiment for meeting {MeetingId}", meetingId);

            var response = await _sentimentService.GetMeetingSentimentAsync(teamId, meetingId);

            if (!response.Success)
            {
                return NotFound(new { error = $"Meeting {meetingId} not found" });
            }

            return Ok(response);
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/sentiment/concerns
        /// Get meetings/topics with concerning sentiment
        /// </summary>
        [HttpGet("{teamId}/sentiment/concerns")]
        public async Task<ActionResult<SentimentConcernsResponse>> GetSentimentConcerns(string teamId)
        {
            _logger.LogInformation("Getting sentiment concerns for team {TeamId}", teamId);

            var response = await _sentimentService.GetSentimentConcernsAsync(teamId);

            if (!response.Success)
            {
                return StatusCode(500, new { error = "Failed to analyze sentiment concerns" });
            }

            return Ok(response);
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/sentiment/trend
        /// Get sentiment trend visualization data
        /// </summary>
        [HttpGet("{teamId}/sentiment/trend")]
        public async Task<ActionResult> GetSentimentTrend(string teamId)
        {
            _logger.LogInformation("Getting sentiment trend for team {TeamId}", teamId);

            var fullResponse = await _sentimentService.GetTeamSentimentAsync(teamId);

            // Format for easy visualization
            var trend = fullResponse.Trend.Select(t => new
            {
                meetingId = t.MeetingId,
                date = t.Date.ToString("yyyy-MM-dd"),
                score = Math.Round(t.Score, 2),
                category = t.Category.ToString(),
                keyDriver = t.KeyDriver,
                color = GetSentimentColor(t.Category)
            }).ToList();

            return Ok(new
            {
                teamId = teamId,
                overallScore = Math.Round(fullResponse.OverallTeamSentiment, 2),
                overallCategory = fullResponse.OverallCategory.ToString(),
                trend = trend,
                summary = fullResponse.TeamMoodSummary
            });
        }

        /// <summary>
        /// GET /api/meetings/{teamId}/sentiment/by-speaker
        /// Get sentiment breakdown by speaker
        /// </summary>
        [HttpGet("{teamId}/sentiment/by-speaker")]
        public async Task<ActionResult> GetSentimentBySpeaker(string teamId)
        {
            _logger.LogInformation("Getting sentiment by speaker for team {TeamId}", teamId);

            var fullResponse = await _sentimentService.GetTeamSentimentAsync(teamId);

            var bySpeaker = fullResponse.SpeakerAverages.Select(s => new
            {
                speaker = s.SpeakerName,
                averageScore = Math.Round(s.AverageScore, 2),
                category = s.Category.ToString(),
                statementCount = s.StatementCount,
                color = GetSentimentColor(s.Category)
            }).ToList();

            return Ok(new
            {
                teamId = teamId,
                speakerCount = bySpeaker.Count,
                speakers = bySpeaker,
                mostPositive = bySpeaker.FirstOrDefault(),
                mostNegative = bySpeaker.LastOrDefault()
            });
        }

        /// <summary>
        /// Get color for sentiment visualization
        /// </summary>
        private string GetSentimentColor(SentimentCategory category)
        {
            return category switch
            {
                SentimentCategory.VeryPositive => "#22c55e",  // Green
                SentimentCategory.Positive => "#86efac",      // Light green
                SentimentCategory.Neutral => "#fbbf24",       // Yellow
                SentimentCategory.Negative => "#fb923c",      // Orange
                SentimentCategory.VeryNegative => "#ef4444",  // Red
                _ => "#9ca3af"                                // Gray
            };
        }
    }
}
