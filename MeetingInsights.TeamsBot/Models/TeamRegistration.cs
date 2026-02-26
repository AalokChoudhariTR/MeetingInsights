namespace MeetingInsights.TeamsBot.Models
{
    /// <summary>
    /// Represents a registered team
    /// </summary>
    public class TeamRegistration
    {
        public string TeamId { get; set; } = string.Empty;           // Our internal ID (TEAM_001)
        public string TeamsChannelId { get; set; } = string.Empty;   // Microsoft Teams channel ID
        public string TeamName { get; set; } = string.Empty;         // Display name
        public DateTime RegisteredAt { get; set; }
        public string RegisteredBy { get; set; } = string.Empty;
    }
}
