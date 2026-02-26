using MeetingInsights.Core.Models;
using MeetingInsights.TeamsBot.Models;
using MeetingInsights.TeamsBot.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using static MeetingInsights.Core.Models.QueryModels;

namespace MeetingInsights.TeamsBot.Services
{

    /// <summary>
    /// Service to communicate with the backend API
    /// </summary>
    public class BackendAPIService : IBackendAPIService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BackendAPIService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public BackendAPIService(
            HttpClient httpClient,
            IOptions<BotSettings> settings,
            ILogger<BackendAPIService> logger)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(settings.Value.BackendApiBaseUrl);
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        public async Task<QueryResponse> AskQuestionAsync(string teamId, string question)
        {
            try
            {
                var request = new { teamId, question, maxResults = 5 };
                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync("/api/query", content);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<QueryResponse>(json, _jsonOptions) ?? new QueryResponse();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling query API for team {TeamId}", teamId);
                return new QueryResponse { Success = false, Answer = "Error processing your question. Please try again." };
            }
        }

        public async Task<DecisionsResponse> GetDecisionsAsync(string teamId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/meetings/{teamId}/decisions");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<DecisionsResponse>(json, _jsonOptions) ?? new DecisionsResponse();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting decisions for team {TeamId}", teamId);
                return new DecisionsResponse { Success = false };
            }
        }

        public async Task<ConflictsResponse> GetConflictsAsync(string teamId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/meetings/{teamId}/decisions/conflicts");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ConflictsResponse>(json, _jsonOptions) ?? new ConflictsResponse();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting conflicts for team {TeamId}", teamId);
                return new ConflictsResponse { Success = false };
            }
        }

        public async Task<ActionItemsResponse> GetActionItemsAsync(string teamId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/meetings/{teamId}/action-items");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ActionItemsResponse>(json, _jsonOptions) ?? new ActionItemsResponse();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting action items for team {TeamId}", teamId);
                return new ActionItemsResponse { Success = false };
            }
        }

        public async Task<TeamSentimentResponse> GetSentimentAsync(string teamId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/meetings/{teamId}/sentiment");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<TeamSentimentResponse>(json, _jsonOptions) ?? new TeamSentimentResponse();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sentiment for team {TeamId}", teamId);
                return new TeamSentimentResponse { Success = false };
            }
        }

        public async Task<TeamMeetingsSummary> GetTeamSummaryAsync(string teamId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/meetings/{teamId}/summary");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<TeamMeetingsSummary>(json, _jsonOptions) ?? new TeamMeetingsSummary();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting team summary for {TeamId}", teamId);
                return new TeamMeetingsSummary();
            }
        }

        public async Task<MeetingSummaryResponse> GetMeetingSummaryAsync(string teamId, string meetingId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/meetings/{teamId}/{meetingId}/summary");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<MeetingSummaryResponse>(json, _jsonOptions) ?? new MeetingSummaryResponse();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting meeting summary for {MeetingId}", meetingId);
                return new MeetingSummaryResponse { Success = false };
            }
        }

        public async Task<bool> ProcessTranscriptAsync(string teamId, string meetingId, string transcript, DateTime meetingDate)
        {
            try
            {
                var request = new
                {
                    teamId,
                    meetingId,
                    transcript,
                    meetingDate = meetingDate.ToString("yyyy-MM-dd")
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync("/api/transcripts/process", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing transcript for meeting {MeetingId}", meetingId);
                return false;
            }
        }
    }
}
