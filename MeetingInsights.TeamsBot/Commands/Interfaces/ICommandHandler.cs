using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace MeetingInsights.TeamsBot.Commands.Interfaces
{
    public interface ICommandHandler
    {
        string Command { get; }
        string Description { get; }
        Task<IMessageActivity> HandleAsync(ITurnContext context, string teamId, string[] args);
    }
}
