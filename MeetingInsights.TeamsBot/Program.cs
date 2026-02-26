using MeetingInsights.TeamsBot;
using MeetingInsights.TeamsBot.Bots;
using MeetingInsights.TeamsBot.Commands;
using MeetingInsights.TeamsBot.Commands.Interfaces;
using MeetingInsights.TeamsBot.Models;
using MeetingInsights.TeamsBot.Services;
using MeetingInsights.TeamsBot.Services.Interfaces;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;

var builder = WebApplication.CreateBuilder(args);


// Add Bot Settings
builder
    .Services.Configure<BotSettings>(builder.Configuration.GetSection("BotSettings"));

// Add Bot Framework Authentication
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

// Add Bot Adapter
builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();

// Add Services
builder.Services.AddSingleton<ITeamRegistrationService, TeamRegistrationService>();
builder.Services.AddHttpClient<IBackendAPIService, BackendAPIService>();

// Add Command Handlers
builder.Services.AddSingleton<ICommandHandler, AskCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, DecisionsCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, ConflictsCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, ActionItemsCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, PendingCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, SentimentCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, SummaryCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, HelpCommandHandler>();

// Add Command Router
builder.Services.AddSingleton<CommandRouter>();
// Add Bot
builder.Services.AddTransient<IBot, MeetingInsightsBot>();

var app = builder.Build();

// Bot endpoint
app.MapPost("/api/messages", async (IBotFrameworkHttpAdapter adapter, IBot bot, HttpContext context) =>
{
    await adapter.ProcessAsync(context.Request, context.Response, bot);
});

// Health check
app.MapGet("/health", () => "OK");

app.Run();