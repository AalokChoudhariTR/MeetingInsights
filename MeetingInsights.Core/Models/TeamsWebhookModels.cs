using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingInsights.Core.Models
{
    public class TeamsWebhookRequest
    {
        public string? Type { get; set; }
        public string? Text { get; set; }
        public string? ChannelId { get; set; }
        public string? TeamName { get; set; }
        public TeamsConversation? Conversation { get; set; }
        public TeamsFrom? From { get; set; }
    }

    public class TeamsConversation
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public class TeamsFrom
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    public class TeamsWebhookResponse
    {
        public string Type { get; set; } = "message";
        public string Text { get; set; } = "";
    }

    public class BotCommand
    {
        public string Name { get; set; } = "";
        public string? Argument { get; set; }
    }
}
