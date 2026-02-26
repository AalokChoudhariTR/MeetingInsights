using MeetingInsights.Core.Models;
using MeetingInsights.Core.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace MeetingInsights.API.Controllers
{
    [ApiController]
    [Route("api/documents")]
    public class DocumentController : ControllerBase
    {
        private readonly IAtomizerService _atomizerService;
        private readonly ISnowflakeService _snowflakeService;
        private readonly ILogger<DocumentController> _logger;

        public DocumentController(
            IAtomizerService atomizerService,
            ISnowflakeService snowflakeService,
            ILogger<DocumentController> logger)
        {
            _atomizerService = atomizerService;
            _snowflakeService = snowflakeService;
            _logger = logger;
        }

        /// <summary>
        /// Upload up to 5 docx meeting transcripts and process them
        /// </summary>
        [HttpPost("upload")]
        [RequestSizeLimit(20_000_000)] // 20 MB limit
        public async Task<IActionResult> UploadDocuments(
            [FromForm] List<IFormFile> files,
            [FromForm] string teamId)
        {
            if (files == null || files.Count == 0)
                return BadRequest("No files uploaded.");
            if (files.Count > 5)
                return BadRequest("You can upload up to 5 files at a time.");
            if (string.IsNullOrWhiteSpace(teamId))
                return BadRequest("Team ID is required.");

            var transcripts = new List<MeetingTranscript>();
            foreach (var file in files)
            {
                if (!file.FileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Only .docx files are supported.");

                // Parse metadata from filename
                var (meetingName, meetingDate) = ParseFileName(file.FileName);

                // Read docx content using Open XML SDK
                string transcriptText;
                using (var stream = file.OpenReadStream())
                {
                    transcriptText = ExtractTextFromDocx(stream);
                }

                // Create MeetingTranscript object
                var meetingId = $"{meetingName}_{meetingDate:yyyyMMdd}";
                transcripts.Add(new MeetingTranscript
                {
                    MeetingId = meetingId,
                    MeetingDate = meetingDate,
                    TeamId = teamId,
                    TranscriptText = transcriptText,
                    SourceSystem = "DOCX_UPLOAD"
                });
            }

            // Process (clean, chunk, enrich)
            var chunks = _atomizerService.ProcessTranscripts(transcripts);

            // Save to Snowflake
            var savedCount = await _snowflakeService.SaveProcessedChunksAsync(chunks);

            return Ok(new
            {
                success = true,
                message = "Files processed and saved.",
                filesUploaded = files.Count,
                chunksCreated = chunks.Count,
                chunksSaved = savedCount,
                sampleChunks = chunks.Take(3).Select(c => new
                {
                    c.ChunkId,
                    c.MeetingId,
                    c.MeetingDate,
                    c.ChunkSequence,
                    c.CharacterCount,
                    preview = c.EnrichedText.Length > 200 ? c.EnrichedText.Substring(0, 200) + "..." : c.EnrichedText
                })
            });
        }

        // Helper to parse filename: AI_MEETING_IMPLEMENTATION_2024-01-08.docx
        private (string meetingName, DateTime meetingDate) ParseFileName(string fileName)
        {
            var name = Path.GetFileNameWithoutExtension(fileName);
            var parts = name.Split('_');
            // Assume last part is date
            var datePart = parts.Last();
            DateTime meetingDate = DateTime.TryParse(datePart, out var dt)
                ? dt
                : DateTime.ParseExact(datePart, "yyyy-MM-dd", null);
            var meetingName = string.Join("_", parts.Take(parts.Length - 1));
            return (meetingName, meetingDate);
        }

        // Helper to extract text from docx using Open XML SDK
        private string ExtractTextFromDocx(Stream stream)
        {
            using (var wordDoc = WordprocessingDocument.Open(stream, false))
            {
                var body = wordDoc.MainDocumentPart.Document.Body;
                return body == null ? "" : body.InnerText;
            }
        }
    }
}