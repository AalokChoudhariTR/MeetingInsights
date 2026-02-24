using MeetingInsights.Core.Configuration;
using MeetingInsights.Core.Services;
using MeetingInsights.Core.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.Configure<SnowflakeSettings>(builder.Configuration.GetSection("Snowflake"));

builder.Services.AddScoped<ISnowflakeService, SnowflakeService>();
builder.Services.AddScoped<IAtomizerService, AtomizerService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Allowangular", policy =>
    {
        policy.WithOrigins("https://localhost:4200").AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngular");
app.UseAuthorization();
app.MapControllers();

app.Run();
