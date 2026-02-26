namespace MeetingInsights.TeamsBot.Models
{
    // <summary>
    /// Bot configuration settings
    /// </summary>
    public class BotSettings
    {
        public string MicrosoftAppId { get; set; } = string.Empty;
        public string MicrosoftAppPassword { get; set; } = string.Empty;
        public string MicrosoftAppTenantId { get; set; } = string.Empty;
        public string BackendApiBaseUrl { get; set; } = "http://localhost:5000";
    }
}
