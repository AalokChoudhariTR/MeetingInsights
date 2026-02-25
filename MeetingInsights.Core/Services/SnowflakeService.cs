using MeetingInsights.Core.Configuration;
using MeetingInsights.Core.Models;
using MeetingInsights.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Snowflake.Data.Client;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.Json.Nodes;
using static MeetingInsights.Core.Models.QueryModels;

namespace MeetingInsights.Core.Services
{
    // <summary>
    /// Handles all Snowflake database operations
    /// </summary>
    public class SnowflakeService : ISnowflakeService
    {
        private readonly SnowflakeSettings _settings;
        private readonly ILogger<SnowflakeService> _logger;

        public SnowflakeService(
            IOptions<SnowflakeSettings> settings,
            ILogger<SnowflakeService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<List<MeetingTranscript>> GetAllTranscriptsAsync()
        {
            var transcripts = new List<MeetingTranscript>();

            try
            {
                using var connection = new SnowflakeDbConnection();
                connection.ConnectionString = _settings.ConnectionString;
                _logger.LogInformation("Connecting to Snowflake...");
                await connection.OpenAsync();
                _logger.LogInformation("Connected successfully!");

                using var command = connection.CreateCommand();
                command.CommandText = @"
                SELECT 
                    MEETING_ID,
                    MEETING_DATE,
                    TEAM_ID,
                    ORIGINAL_ID,
                    TRANSCRIPT_TEXT,
                    SOURCE_SYSTEM
                FROM MEETING_TRANSCRIPTS
                ORDER BY MEETING_DATE";

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    transcripts.Add(new MeetingTranscript
                    {
                        MeetingId = reader.GetString(0),
                        MeetingDate = reader.GetDateTime(1),
                        TeamId = reader.GetString(2),
                        OriginalId = reader.GetInt32(3),
                        TranscriptText = reader.GetString(4),
                        SourceSystem = reader.GetString(5)
                    });
                }

                _logger.LogInformation("Fetched {Count} transcripts from Snowflake", transcripts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transcripts from Snowflake");
                throw;
            }

            return transcripts;
        }

        /// <summary>
        /// Saves processed chunks to the PROCESSED_CHUNKS table
        /// </summary>
        public async Task<int> SaveProcessedChunksAsync(List<ProcessedChunk> chunks)
        {
            if (chunks == null || chunks.Count == 0)
            {
                _logger.LogWarning("No chunks to save");
                return 0;
            }

            int savedCount = 0;

            try
            {
                using var connection = new SnowflakeDbConnection();
                connection.ConnectionString = _settings.ConnectionString;
                await connection.OpenAsync();

                // First, create the table if it doesn't exist
                using var createTableCmd = connection.CreateCommand();
                createTableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS PROCESSED_CHUNKS (
                    CHUNK_ID        VARCHAR(100) PRIMARY KEY,
                    MEETING_ID      VARCHAR(50),
                    MEETING_DATE    DATE,
                    TEAM_ID         VARCHAR(200),
                    CHUNK_SEQUENCE  INT,
                    CHUNK_TEXT      VARCHAR(16777216),
                    ENRICHED_TEXT   VARCHAR(16777216),
                    CHARACTER_COUNT INT,
                    CREATED_AT      TIMESTAMP DEFAULT CURRENT_TIMESTAMP()
                )";
                await createTableCmd.ExecuteNonQueryAsync();
                _logger.LogInformation("PROCESSED_CHUNKS table ready");

                // Clear existing data (for re-processing)
                using var clearCmd = connection.CreateCommand();
                clearCmd.CommandText = "TRUNCATE TABLE PROCESSED_CHUNKS";
                await clearCmd.ExecuteNonQueryAsync();
                _logger.LogInformation("Cleared existing chunks");

                // Insert chunks in batches
                foreach (var chunk in chunks)
                {
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = @"
                    INSERT INTO PROCESSED_CHUNKS 
                    (CHUNK_ID, MEETING_ID, MEETING_DATE, TEAM_ID, CHUNK_SEQUENCE, CHUNK_TEXT, ENRICHED_TEXT, CHARACTER_COUNT)
                    VALUES 
                    (:chunkId, :meetingId, :meetingDate, :teamId, :chunkSequence, :chunkText, :enrichedText, :charCount)";

                    insertCmd.Parameters.Add(new SnowflakeDbParameter { ParameterName = "chunkId", Value = chunk.ChunkId, DbType = DbType.String });
                    insertCmd.Parameters.Add(new SnowflakeDbParameter { ParameterName = "meetingId", Value = chunk.MeetingId, DbType = DbType.String });
                    insertCmd.Parameters.Add(new SnowflakeDbParameter { ParameterName = "meetingDate", Value = chunk.MeetingDate, DbType = DbType.Date });
                    insertCmd.Parameters.Add(new SnowflakeDbParameter { ParameterName = "teamId", Value = chunk.TeamId, DbType = DbType.String });
                    insertCmd.Parameters.Add(new SnowflakeDbParameter { ParameterName = "chunkSequence", Value = chunk.ChunkSequence, DbType = DbType.Int32 });
                    insertCmd.Parameters.Add(new SnowflakeDbParameter { ParameterName = "chunkText", Value = chunk.ChunkText, DbType = DbType.String });
                    insertCmd.Parameters.Add(new SnowflakeDbParameter { ParameterName = "enrichedText", Value = chunk.EnrichedText, DbType = DbType.String });
                    insertCmd.Parameters.Add(new SnowflakeDbParameter { ParameterName = "charCount", Value = chunk.CharacterCount, DbType = DbType.Int32 });

                    await insertCmd.ExecuteNonQueryAsync();
                    savedCount++;
                }

                _logger.LogInformation("Saved {Count} chunks to Snowflake", savedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving chunks to Snowflake");
                throw;
            }
            return savedCount;
        }


        /// <summary>
        /// Search chunks using Cortex Search Service
        /// </summary>
        public async Task<List<SearchResult>> SearchWithCortexAsync(string query,string teamId, int maxResults = 5)
        {
            var results = new List<SearchResult>();
            try
            {
                using var connection = new SnowflakeDbConnection();
                connection.ConnectionString = _settings.ConnectionString;
                await connection.OpenAsync();
                using var command = connection.CreateCommand();

                // Call Cortex Search Service
                command.CommandText = $@"
            SELECT PARSE_JSON(
                SNOWFLAKE.CORTEX.SEARCH_PREVIEW(
                    'MEETING_SEARCH_SERVICE',
                    '{{
                        ""query"": ""{EscapeJsonString(query)}"",
                        ""columns"": [""CHUNK_ID"", ""MEETING_ID"", ""MEETING_DATE"", ""TEAM_ID"", ""ENRICHED_TEXT""],
                        ""filter"" : {{""@eq"": {{""TEAM_ID"": ""{EscapeJsonString(teamId)}""}}}},
                        ""limit"": {maxResults}
                    }}'
                )
            ):results AS search_results";

                _logger.LogInformation("Searching for team {TeamId} with query: {Query}", teamId, query);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var jsonResult = reader.GetString(0);
                    if (!string.IsNullOrEmpty(jsonResult))
                    {
                        var jsonArray = JsonNode.Parse(jsonResult)?.AsArray();
                        if (jsonArray != null)
                        {
                            foreach (var item in jsonArray)
                            {
                                if (item == null) continue;
                                results.Add(new SearchResult
                                {
                                    ChunkId = item["CHUNK_ID"]?.GetValue<string>() ?? "",
                                    MeetingId = item["MEETING_ID"]?.GetValue<string>() ??
                                               ExtractMeetingIdFromChunkId(item["CHUNK_ID"]?.GetValue<string>() ?? ""),
                                    MeetingDate = DateTime.TryParse(item["MEETING_DATE"]?.GetValue<string>(), out var date)
                                                 ? date : DateTime.MinValue,
                                    EnrichedText = item["ENRICHED_TEXT"]?.GetValue<string>() ?? "",
                                    SimilarityScore = item["@scores"]?["cosine_similarity"]?.GetValue<double>() ?? 0
                                });
                            }
                        }
                    }
                }
                _logger.LogInformation("Cortex Search returned {Count} for team {TeamId} results for query: {Query}",
                    results.Count, teamId, query);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching with Cortex for query: {Query}", query);
                throw;
            }
            return results;
        }

        /// <summary>
        /// Generate an answer using Cortex Complete (LLM)
        /// </summary>
        public async Task<string> GenerateAnswerWithCortexAsync(string prompt)

        {
            try
            {
                using var connection = new SnowflakeDbConnection();

                connection.ConnectionString = _settings.ConnectionString;
                await connection.OpenAsync();
                using var command = connection.CreateCommand();

                // Use Cortex Complete with llama3-70b model
                command.CommandText = @"
            SELECT SNOWFLAKE.CORTEX.COMPLETE(
                'llama3-70b',
                :prompt
            ) AS response";
                command.Parameters.Add(new SnowflakeDbParameter
                {
                    ParameterName = "prompt",
                    Value = prompt,
                    DbType = DbType.String
                });

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    var response = reader.GetString(0);
                    _logger.LogInformation("Cortex Complete generated response of {Length} characters",
                        response?.Length ?? 0);
                    return response ?? "";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Cortex Complete");
                throw;
            }
            return "";
        }

        // Helper methods
        private string EscapeJsonString(string input)
        {
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private string ExtractMeetingIdFromChunkId(string chunkId)
        {
            // MTG-001-003 -> MTG-001
            var parts = chunkId.Split('-');
            if (parts.Length >= 2)
                return $"{parts[0]}-{parts[1]}";
            return chunkId;
        }

        /// <summary>
        /// Get full transcript text for a specific meeting (combined chunks)
        /// </summary>
        public async Task<string> GetMeetingTranscriptTextAsync(string teamId, string meetingId)
        {
            try
            {
                using var connection = new SnowflakeDbConnection();
                connection.ConnectionString = _settings.ConnectionString;
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
            SELECT LISTAGG(CHUNK_TEXT, ' ') WITHIN GROUP (ORDER BY CHUNK_SEQUENCE) AS full_text
            FROM PROCESSED_CHUNKS
            WHERE TEAM_ID = :teamId AND MEETING_ID = :meetingId
            GROUP BY MEETING_ID";

                command.Parameters.Add(new SnowflakeDbParameter { ParameterName = "teamId", Value = teamId, DbType = DbType.String });
                command.Parameters.Add(new SnowflakeDbParameter { ParameterName = "meetingId", Value = meetingId, DbType = DbType.String });

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return reader.GetString(0);
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transcript for meeting {MeetingId}", meetingId);
                throw;
            }
        }

        /// <summary>
        /// Get all meetings for a team
        /// </summary>
        public async Task<List<MeetingTranscript>> GetMeetingsByTeamAsync(string teamId)
        {
            var meetings = new List<MeetingTranscript>();

            try
            {
                using var connection = new SnowflakeDbConnection();
                connection.ConnectionString = _settings.ConnectionString;
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = @"
            SELECT DISTINCT
                MEETING_ID,
                MEETING_DATE,
                TEAM_ID
            FROM PROCESSED_CHUNKS
            WHERE TEAM_ID = :teamId
            ORDER BY MEETING_DATE";

                command.Parameters.Add(new SnowflakeDbParameter { ParameterName = "teamId", Value = teamId, DbType = DbType.String });

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    meetings.Add(new MeetingTranscript
                    {
                        MeetingId = reader.GetString(0),
                        MeetingDate = reader.GetDateTime(1),
                        TeamId = reader.GetString(2)
                    });
                }

                _logger.LogInformation("Found {Count} meetings for team {TeamId}", meetings.Count, teamId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting meetings for team {TeamId}", teamId);
                throw;
            }

            return meetings;
        }

        /// <summary>
        /// Summarize text using Snowflake Cortex SUMMARIZE function
        /// </summary>
        public async Task<string> SummarizeWithCortexAsync(string text)
        {
            try
            {
                using var connection = new SnowflakeDbConnection();
                connection.ConnectionString = _settings.ConnectionString;
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                // Use Cortex SUMMARIZE function
                command.CommandText = @"
            SELECT SNOWFLAKE.CORTEX.SUMMARIZE(:text) AS summary";

                command.Parameters.Add(new SnowflakeDbParameter
                {
                    ParameterName = "text",
                    Value = text,
                    DbType = DbType.String
                });

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var summary = reader.GetString(0);
                    _logger.LogInformation("Cortex SUMMARIZE generated {Length} character summary", summary?.Length ?? 0);
                    return summary ?? "";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Cortex SUMMARIZE");
                throw;
            }

            return "";
        }
        /// <summary>
        /// Analyze sentiment of text using Snowflake Cortex SENTIMENT function
        /// Returns a score from -1 (negative) to +1 (positive)
        /// </summary>
        public async Task<double> AnalyzeSentimentAsync(string text)
        {
            try
            {
                using var connection = new SnowflakeDbConnection();
                connection.ConnectionString = _settings.ConnectionString;
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                // Use Cortex SENTIMENT function
                command.CommandText = @"
            SELECT SNOWFLAKE.CORTEX.SENTIMENT(:text) AS sentiment_score";

                command.Parameters.Add(new SnowflakeDbParameter
                {
                    ParameterName = "text",
                    Value = text,
                    DbType = DbType.String
                });

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var score = reader.GetDouble(0);
                    return score;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing sentiment, returning neutral");
            }

            return 0.0; // Neutral on error
        }

        /// <summary>
        /// Batch analyze sentiment for multiple texts
        /// </summary>
        public async Task<List<double>> AnalyzeSentimentBatchAsync(List<string> texts)
        {
            var results = new List<double>();

            try
            {
                using var connection = new SnowflakeDbConnection();
                connection.ConnectionString = _settings.ConnectionString;
                await connection.OpenAsync();

                foreach (var text in texts)
                {
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        results.Add(0.0);
                        continue;
                    }

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
                SELECT SNOWFLAKE.CORTEX.SENTIMENT(:text) AS sentiment_score";

                    command.Parameters.Add(new SnowflakeDbParameter
                    {
                        ParameterName = "text",
                        Value = text.Length > 5000 ? text.Substring(0, 5000) : text,
                        DbType = DbType.String
                    });

                    using var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        results.Add(reader.GetDouble(0));
                    }
                    else
                    {
                        results.Add(0.0);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch sentiment analysis");
                // Fill remaining with neutral
                while (results.Count < texts.Count)
                {
                    results.Add(0.0);
                }
            }

            return results;
        }
    }
}
