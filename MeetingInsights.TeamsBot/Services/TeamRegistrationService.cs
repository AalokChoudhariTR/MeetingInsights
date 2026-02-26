using MeetingInsights.TeamsBot.Models;
using MeetingInsights.TeamsBot.Services.Interfaces;
using System.Collections.Concurrent;

namespace MeetingInsights.TeamsBot.Services
{
    /// <summary>
    /// Service to manage team registrations
    /// For hackathon, using in-memory + file storage.
    /// </summary>
    public class TeamRegistrationService : ITeamRegistrationService
    {
        private readonly ILogger<TeamRegistrationService> _logger;
        private readonly ConcurrentDictionary<string, TeamRegistration> _registrations;
        private readonly string _storageFile = "team_registrations.json";
        private static int _teamCounter = 0;

        public TeamRegistrationService(ILogger<TeamRegistrationService> logger)
        {
            _logger = logger;
            _registrations = new ConcurrentDictionary<string, TeamRegistration>();
            LoadRegistrations();
        }

        public Task<TeamRegistration?> GetTeamByChannelIdAsync(string teamsChannelId)
        {
            _registrations.TryGetValue(teamsChannelId, out var registration);
            return Task.FromResult(registration);
        }

        public async Task<TeamRegistration> RegisterTeamAsync(string teamsChannelId, string teamName, string registeredBy)
        {
            // Check if already registered
            if (_registrations.TryGetValue(teamsChannelId, out var existing))
            {
                _logger.LogInformation("Team already registered: {TeamId}", existing.TeamId);
                return existing;
            }

            // Create new registration
            var registration = new TeamRegistration
            {
                TeamId = await GenerateTeamIdAsync(),
                TeamsChannelId = teamsChannelId,
                TeamName = teamName,
                RegisteredAt = DateTime.UtcNow,
                RegisteredBy = registeredBy
            };

            _registrations[teamsChannelId] = registration;
            await SaveRegistrationsAsync();

            _logger.LogInformation("New team registered: {TeamId} for channel {ChannelId}",
                registration.TeamId, teamsChannelId);

            return registration;
        }

        public Task<bool> IsTeamRegisteredAsync(string teamsChannelId)
        {
            return Task.FromResult(_registrations.ContainsKey(teamsChannelId));
        }

        public Task<string> GenerateTeamIdAsync()
        {
            var id = Interlocked.Increment(ref _teamCounter);
            return Task.FromResult($"TEAM_{id:D3}");
        }

        private void LoadRegistrations()
        {
            try
            {
                if (File.Exists(_storageFile))
                {
                    var json = File.ReadAllText(_storageFile);
                    var registrations = System.Text.Json.JsonSerializer.Deserialize<List<TeamRegistration>>(json);

                    if (registrations != null)
                    {
                        foreach (var reg in registrations)
                        {
                            _registrations[reg.TeamsChannelId] = reg;
                        }

                        // Set counter to max existing
                        if (registrations.Any())
                        {
                            var maxId = registrations
                                .Select(r => int.TryParse(r.TeamId.Replace("TEAM_", ""), out var id) ? id : 0)
                                .Max();
                            _teamCounter = maxId;
                        }
                    }

                    _logger.LogInformation("Loaded {Count} team registrations", _registrations.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load team registrations, starting fresh");
            }
        }

        private async Task SaveRegistrationsAsync()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(
                    _registrations.Values.ToList(),
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                await File.WriteAllTextAsync(_storageFile, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving team registrations");
            }
        }
    }
}
