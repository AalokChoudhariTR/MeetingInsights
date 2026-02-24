using MeetingInsights.Core.Models;
using MeetingInsights.Core.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MeetingInsights.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TranscriptController : ControllerBase
    {
        private readonly ISnowflakeService _snowflakeService;
        private readonly IAtomizerService _atomizerService;
        private readonly ILogger<TranscriptController> _logger;

        public TranscriptController(
            ISnowflakeService snowflakeService,
            IAtomizerService atomizerService,
            ILogger<TranscriptController> logger)
        {
            _snowflakeService = snowflakeService;
            _atomizerService = atomizerService;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/transcript
        /// Fetches all raw transcripts from Snowflake
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<MeetingTranscript>>> GetTranscripts()
        {
            try
            {
                var transcripts = await _snowflakeService.GetAllTranscriptsAsync();
                return Ok(new
                {
                    success = true,
                    count = transcripts.Count,
                    data = transcripts.Select(t => new
                    {
                        t.MeetingId,
                        t.MeetingDate,
                        t.TeamId,
                        transcriptPreview = t.TranscriptText.Length > 200
                            ? t.TranscriptText.Substring(0, 200) + "..."
                            : t.TranscriptText
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching transcripts");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/transcript/process
        /// Fetches transcripts, processes them (clean, chunk, enrich), and saves to Snowflake
        /// </summary>
        [HttpPost("process")]
        public async Task<ActionResult> ProcessTranscripts()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("Starting transcript processing pipeline...");

                // Step 1: Fetch from Snowflake
                _logger.LogInformation("Step 1: Fetching transcripts from Snowflake...");
                var transcripts = await _snowflakeService.GetAllTranscriptsAsync();
                _logger.LogInformation("Fetched {Count} transcripts", transcripts.Count);

                // Step 2: Process (Clean, Chunk, Enrich)
                _logger.LogInformation("Step 2: Processing transcripts (clean, chunk, enrich)...");
                var chunks = _atomizerService.ProcessTranscripts(transcripts);
                _logger.LogInformation("Created {Count} chunks", chunks.Count);

                // Step 3: Save back to Snowflake
                _logger.LogInformation("Step 3: Saving processed chunks to Snowflake...");
                var savedCount = await _snowflakeService.SaveProcessedChunksAsync(chunks);
                _logger.LogInformation("Saved {Count} chunks", savedCount);

                stopwatch.Stop();

                return Ok(new
                {
                    success = true,
                    message = "Processing complete",
                    stats = new
                    {
                        transcriptsProcessed = transcripts.Count,
                        chunksCreated = chunks.Count,
                        chunksSaved = savedCount,
                        processingTimeMs = stopwatch.ElapsedMilliseconds,
                        averageChunksPerTranscript = transcripts.Count > 0
                            ? Math.Round((double)chunks.Count / transcripts.Count, 2)
                            : 0
                    },
                    sampleChunks = chunks.Take(3).Select(c => new
                    {
                        c.ChunkId,
                        c.MeetingDate,
                        c.ChunkSequence,
                        c.CharacterCount,
                        enrichedTextPreview = c.EnrichedText.Length > 300
                            ? c.EnrichedText.Substring(0, 300) + "..."
                            : c.EnrichedText
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing transcripts");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        /// <summary>
        /// POST /api/transcript/preview
        /// Preview processing without saving (for testing)
        /// </summary>
        [HttpPost("preview")]
        public async Task<ActionResult> PreviewProcessing()
        {
            try
            {
                // Fetch transcripts
                var transcripts = await _snowflakeService.GetAllTranscriptsAsync();
                // Process but don't save
                var chunks = _atomizerService.ProcessTranscripts(transcripts);

                return Ok(new
                {
                    success = true,
                    message = "Preview only - not saved to Snowflake",
                    stats = new
                    {
                        transcriptsProcessed = transcripts.Count,
                        chunksCreated = chunks.Count
                    },
                    chunks = chunks.Select(c => new
                    {
                        c.ChunkId,
                        c.MeetingId,
                        c.MeetingDate,
                        c.ChunkSequence,
                        c.CharacterCount,
                        c.ChunkText,
                        c.EnrichedText
                    })
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing processing");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}