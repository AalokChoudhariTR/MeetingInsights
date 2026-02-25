using MeetingInsights.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using static MeetingInsights.Core.Models.QueryModels;

namespace MeetingInsights.Core.Services
{ 
    public class QueryService : IQueryService
    {
        private readonly ISnowflakeService _snowflakeService;
        private readonly ILogger<QueryService> _logger;

        public QueryService(
            ISnowflakeService snowflakeService,
            ILogger<QueryService> logger)
        {
            _snowflakeService = snowflakeService;
            _logger = logger;
        }

        public async Task<QueryResponse> ProcessQueryAsync(QueryRequest request)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = new QueryResponse
            {
                Question = request.Question
            };

            try
            {
                _logger.LogInformation("Processing query for team {TeamId}: {Question}", request.TeamId,request.Question);
                // ═══════════════════════════════════════════════════════════════

                // STEP 1: Search for relevant chunks

                // ═══════════════════════════════════════════════════════════════

                _logger.LogInformation("Step 1: Searching for relevant chunks...");

                var searchResults = await _snowflakeService.SearchWithCortexAsync(

                    request.Question,
                    request.TeamId, 
                    request.MaxResults);

                if (searchResults.Count == 0)

                {

                    response.Success = true;

                    response.Answer = "I couldn't find any relevant information in the meeting transcripts for your question.";

                    response.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

                    return response;

                }

                _logger.LogInformation("Found {Count} relevant chunks", searchResults.Count);

                // ═══════════════════════════════════════════════════════════════

                // STEP 2: Sort chronologically

                // ═══════════════════════════════════════════════════════════════

                _logger.LogInformation("Step 2: Sorting chronologically...");

                var sortedResults = searchResults

                    .OrderBy(r => r.MeetingDate)

                    .ThenBy(r => r.ChunkId)

                    .ToList();

                // ═══════════════════════════════════════════════════════════════

                // STEP 3: Build context for LLM

                // ═══════════════════════════════════════════════════════════════

                _logger.LogInformation("Step 3: Building context for LLM...");

                var context = BuildContextFromChunks(sortedResults);

                // ═══════════════════════════════════════════════════════════════

                // STEP 4: Generate answer with Cortex Complete

                // ═══════════════════════════════════════════════════════════════

                _logger.LogInformation("Step 4: Generating answer with Cortex Complete...");

                var prompt = BuildPrompt(request.Question, context);

                var llmResponse = await _snowflakeService.GenerateAnswerWithCortexAsync(prompt);

                // ═══════════════════════════════════════════════════════════════

                // STEP 5: Parse LLM response

                // ═══════════════════════════════════════════════════════════════

                _logger.LogInformation("Step 5: Parsing LLM response...");

                ParseLLMResponse(llmResponse, response);

                // ═══════════════════════════════════════════════════════════════

                // STEP 6: Add source references

                // ═══════════════════════════════════════════════════════════════

                response.Sources = sortedResults.Select(r => new SourceReference

                {

                    ChunkId = r.ChunkId,

                    MeetingId = r.MeetingId,

                    MeetingDate = r.MeetingDate,

                    TextPreview = r.EnrichedText.Length > 200

                        ? r.EnrichedText.Substring(0, 200) + "..."

                        : r.EnrichedText

                }).ToList();

                response.Success = true;

                stopwatch.Stop();

                response.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

                _logger.LogInformation("Query processed successfully in {Time}ms",

                    response.ProcessingTimeMs);

            }

            catch (Exception ex)

            {

                _logger.LogError(ex, "Error processing query: {Question}", request.Question);

                response.Success = false;

                response.Answer = $"An error occurred while processing your question: {ex.Message}";

                response.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;

            }

            return response;

        }

        /// <summary>

        /// Search for relevant chunks without generating an answer

        /// </summary>

        public async Task<List<SearchResult>> SearchChunksAsync(string query, string teamId, int maxResults = 5)

        {

            return await _snowflakeService.SearchWithCortexAsync(query, teamId, maxResults);

        }

        /// <summary>

        /// Builds the context string from sorted chunks

        /// </summary>

        private string BuildContextFromChunks(List<SearchResult> chunks)

        {

            var sb = new StringBuilder();

            foreach (var chunk in chunks)

            {

                sb.AppendLine($"--- Meeting {chunk.MeetingId} ({chunk.MeetingDate:yyyy-MM-dd}) ---");

                // Remove the metadata header from enriched text for cleaner context

                var content = chunk.EnrichedText;

                var contentIndex = content.IndexOf("] Content:");

                if (contentIndex > 0)

                {

                    content = content.Substring(contentIndex + 10).Trim();

                }

                sb.AppendLine(content);

                sb.AppendLine();

            }

            return sb.ToString();

        }

        /// <summary>

        /// Builds the prompt for Cortex Complete

        /// </summary>

        private string BuildPrompt(string question, string context)

        {

            return $@"You are an AI assistant analyzing meeting transcripts for a team. 

Your task is to answer questions about what was discussed in the meetings.
 
IMPORTANT INSTRUCTIONS:

1. Answer based ONLY on the provided meeting context

2. Note the dates of meetings - information may evolve over time

3. DETECT CONFLICTS: If decisions or statements from different meetings contradict each other, flag this

4. Provide a chronological timeline if the topic was discussed across multiple meetings

5. Be specific and cite which meeting (by date) information comes from
 
MEETING CONTEXT (in chronological order):

{context}
 
USER QUESTION: {question}
 
Please respond in the following JSON format:

{{

    ""answer"": ""Your detailed answer here"",

    ""hasConflicts"": true/false,

    ""conflictSummary"": ""Description of any conflicts found, or null if none"",

    ""timeline"": [

        {{

            ""date"": ""YYYY-MM-DD"",

            ""meetingId"": ""MTG-XXX"",

            ""summary"": ""What was said/decided in this meeting""

        }}

    ]

}}
 
Respond ONLY with valid JSON, no additional text.";

        }

        /// <summary>

        /// Parses the LLM response and populates the QueryResponse

        /// </summary>

        private void ParseLLMResponse(string llmResponse, QueryResponse response)

        {

            try

            {

                // Try to parse as JSON

                var jsonStart = llmResponse.IndexOf('{');

                var jsonEnd = llmResponse.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)

                {

                    var jsonString = llmResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);

                    var parsed = JsonSerializer.Deserialize<LLMResponseFormat>(jsonString, new JsonSerializerOptions

                    {

                        PropertyNameCaseInsensitive = true

                    });

                    if (parsed != null)

                    {

                        response.Answer = parsed.Answer ?? llmResponse;

                        response.HasConflicts = parsed.HasConflicts;

                        response.ConflictSummary = parsed.ConflictSummary;

                        if (parsed.Timeline != null)

                        {

                            response.Timeline = parsed.Timeline.Select(t => new TimelineEntry

                            {

                                Date = DateTime.TryParse(t.Date, out var d) ? d : DateTime.MinValue,

                                MeetingId = t.MeetingId ?? "",

                                Summary = t.Summary ?? ""

                            }).ToList();

                        }

                        return;

                    }

                }

            }

            catch (Exception ex)

            {

                _logger.LogWarning(ex, "Could not parse LLM response as JSON, using raw response");

            }

            // Fallback: use raw response as answer

            response.Answer = llmResponse;

            response.HasConflicts = false;

        }

        // Helper class for JSON parsing

        private class LLMResponseFormat

        {

            public string? Answer { get; set; }

            public bool HasConflicts { get; set; }

            public string? ConflictSummary { get; set; }

            public List<TimelineEntryFormat>? Timeline { get; set; }

        }

        private class TimelineEntryFormat

        {

            public string? Date { get; set; }

            public string? MeetingId { get; set; }

            public string? Summary { get; set; }

        }

    }

}
