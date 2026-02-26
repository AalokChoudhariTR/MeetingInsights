using MeetingInsights.TeamsBot.Commands.Interfaces;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace MeetingInsights.TeamsBot.Commands
{
    public class HelpCommandHandler : ICommandHandler
    {
        private readonly CommandRouter _router;
        public string Command => "help";
        public string Description => "Show available commands";

        public HelpCommandHandler()
        {
            _router = null!; // Will use static help
        }

        public Task<IMessageActivity> HandleAsync(ITurnContext context, string teamId, string[] args)
        {
            var helpText = @"🤖 **Meeting Insights Bot - Help**

**💬 Ask Questions:**
Just type your question naturally:
• `@bot What was discussed about budget?`
• `@bot Who is handling the AI implementation?`
• `@bot What are the main concerns from the team?`

**📋 Quick Commands:**

| Command | Description |
|---------|-------------|
| `/summary` | Summary of all meetings |
| `/summary MTG-001` | Summary of specific meeting |
| `/decisions` | All decisions made |
| `/conflicts` | Conflicting/changed decisions |
| `/action-items` | All action items |
| `/pending` | Pending action items only |
| `/sentiment` | Team sentiment analysis |
| `/help` | This help message |

**💡 Tips:**
• The bot remembers your team context
• Ask follow-up questions naturally
• Use `/conflicts` to see decisions that changed over time";

            return Task.FromResult<IMessageActivity>(MessageFactory.Text(helpText));
        }
    }
}
