using MeetingInsights.TeamsBot.Commands.Interfaces;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace MeetingInsights.TeamsBot.Commands
{

    /// <summary>
    /// Routes commands to appropriate handlers
    /// </summary>
    public class CommandRouter
    {
        private readonly Dictionary<string, ICommandHandler> _handlers;
        private readonly ILogger<CommandRouter> _logger;

        public CommandRouter(
            IEnumerable<ICommandHandler> handlers,
            ILogger<CommandRouter> logger)
        {
            _handlers = handlers.ToDictionary(h => h.Command.ToLower(), h => h);
            _logger = logger;
        }

        public async Task<IMessageActivity> RouteCommandAsync(ITurnContext context, string teamId, string input)
        {
            var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
            {
                return GetHelpMessage();
            }

            var command = parts[0].ToLower().TrimStart('/');
            var args = parts.Skip(1).ToArray();

            _logger.LogInformation("Routing command: {Command} for team {TeamId}", command, teamId);

            // Check for registered handlers
            if (_handlers.TryGetValue(command, out var handler))
            {
                return await handler.HandleAsync(context, teamId, args);
            }

            // If not a known command, treat as a question (ask)
            if (_handlers.TryGetValue("ask", out var askHandler))
            {
                return await askHandler.HandleAsync(context, teamId, parts);
            }

            return GetHelpMessage();
        }

        public IMessageActivity GetHelpMessage()
        {
            var helpText = @"🤖 **Meeting Insights Bot - Commands**

**Ask a Question:**
`@bot ask What was discussed about budget?`
`@bot What are the main concerns?`

**Quick Commands:**
`@bot /summary` - Get summary of all meetings
`@bot /summary MTG-001` - Get specific meeting summary
`@bot /decisions` - View all decisions made
`@bot /conflicts` - View conflicting decisions
`@bot /action-items` - View action items
`@bot /pending` - View pending action items
`@bot /sentiment` - View team sentiment analysis
`@bot /help` - Show this help message

**Examples:**
• `@bot Who is responsible for the pilot program?`
• `@bot /action-items`
• `@bot /sentiment`";

            return MessageFactory.Text(helpText);
        }

        public IEnumerable<(string Command, string Description)> GetAvailableCommands()
        {
            return _handlers.Values.Select(h => (h.Command, h.Description));
        }
    }
}
