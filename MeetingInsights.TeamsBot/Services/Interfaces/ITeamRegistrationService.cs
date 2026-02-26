using MeetingInsights.TeamsBot.Models;

namespace MeetingInsights.TeamsBot.Services.Interfaces
{
    public interface ITeamRegistrationService
    {
        Task<TeamRegistration?> GetTeamByChannelIdAsync(string teamsChannelId);
        Task<TeamRegistration> RegisterTeamAsync(string teamsChannelId, string teamName, string registeredBy);
        Task<bool> IsTeamRegisteredAsync(string teamsChannelId);
        Task<string> GenerateTeamIdAsync();
    }
}
