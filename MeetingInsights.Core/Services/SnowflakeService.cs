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
    }
}
