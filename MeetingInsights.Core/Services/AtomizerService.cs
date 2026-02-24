using MeetingInsights.Core.Models;
using MeetingInsights.Core.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MeetingInsights.Core.Services
{
    public class AtomizerService : IAtomizerService
    {
        private readonly ILogger<AtomizerService> _logger;
        private const int DEFAULT_CHUNK_SIZE = 500;

        public AtomizerService(ILogger<AtomizerService> logger)
        {
            _logger = logger;
        }
        public List<ProcessedChunk> ProcessTranscripts(List<MeetingTranscript> transcripts)
        {
            var allChunks = new List<ProcessedChunk>();

            _logger.LogInformation("Starting to process {Count} transcripts", transcripts.Count);

            foreach (var transcript in transcripts)
            {
                try
                {
                    // Step 1: Clean the text
                    var cleanedText = CleanText(transcript.TranscriptText);
                    _logger.LogDebug("Cleaned transcript {MeetingId}: {OriginalLength} -> {CleanedLength} chars",
                        transcript.MeetingId,
                        transcript.TranscriptText.Length,
                        cleanedText.Length);

                    // Step 2: Chunk the text
                    var textChunks = ChunkText(cleanedText, DEFAULT_CHUNK_SIZE);
                    _logger.LogDebug("Split transcript {MeetingId} into {ChunkCount} chunks",
                        transcript.MeetingId,
                        textChunks.Count);

                    // Step 3: Create ProcessedChunk objects with enrichment
                    for (int i = 0; i < textChunks.Count; i++)
                    {
                        var chunk = new ProcessedChunk
                        {
                            ChunkId = $"{transcript.MeetingId}-{(i + 1):D3}",  // MTG-001-001
                            MeetingId = transcript.MeetingId,
                            MeetingDate = transcript.MeetingDate,
                            TeamId = transcript.TeamId,
                            ChunkSequence = i + 1,
                            ChunkText = textChunks[i],
                            CharacterCount = textChunks[i].Length,
                            // Step 4: Enrich with metadata header
                            EnrichedText = CreateEnrichedText(
                                transcript.TeamId,
                                transcript.MeetingDate,
                                transcript.MeetingId,
                                i + 1,
                                textChunks[i])
                        };

                        allChunks.Add(chunk);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing transcript {MeetingId}", transcript.MeetingId);
                }
            }

            _logger.LogInformation("Processing complete. Created {TotalChunks} chunks from {TranscriptCount} transcripts",
                allChunks.Count, transcripts.Count);

            return allChunks;
        }

        /// <summary>
        /// Cleans transcript text by removing system noise
        /// </summary>
        public string CleanText(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return string.Empty;

            var cleanedText = rawText;

            // 1. Remove meeting header (Date, Time, Location, Attendees intro)
            //    Pattern: "AI Implementation Project Meeting Date: January 8, 2024 Time: 2:00 PM - 2:30 PM Location: Conference Room B, Nordic Prints HQ, Frost Valley, MN Attendees:"
            cleanedText = Regex.Replace(
                cleanedText,
                @"^.*?Attendees:\s*",
                "",
                RegexOptions.Singleline);

            // 2. Remove "User joined/left the meeting" messages
            cleanedText = Regex.Replace(
                cleanedText,
                @"\[?[A-Za-z\s]+ (joined|left|has joined|has left)( the meeting| the call)?\]?\.?\s*",
                "",
                RegexOptions.IgnoreCase);

            // 3. Remove system timestamps like "[10:30:25 AM]" or "(2:15 PM)"
            cleanedText = Regex.Replace(
                cleanedText,
                @"[\[\(]\d{1,2}:\d{2}(:\d{2})?\s*(AM|PM)?[\]\)]",
                "",
                RegexOptions.IgnoreCase);

            // 4. Remove recording started/stopped messages
            cleanedText = Regex.Replace(
                cleanedText,
                @"(Recording (started|stopped|paused|resumed)\.?\s*)",
                "",
                RegexOptions.IgnoreCase);

            // 5. Remove "is typing..." indicators
            cleanedText = Regex.Replace(
                cleanedText,
                @"[A-Za-z\s]+ is typing\.{3}\s*",
                "",
                RegexOptions.IgnoreCase);

            // 6. Remove multiple spaces and normalize whitespace
            cleanedText = Regex.Replace(cleanedText, @"\s+", " ");

            // 7. Remove leading/trailing whitespace
            cleanedText = cleanedText.Trim();

            return cleanedText;
        }

        public List<string> ChunkText(string text, int chunkSize = 500)
        {
            var chunks = new List<string>();

            if (string.IsNullOrWhiteSpace(text))
                return chunks;

            // If text is smaller than chunk size, return as single chunk
            if (text.Length <= chunkSize)
            {
                chunks.Add(text);
                return chunks;
            }

            var currentPosition = 0;

            while (currentPosition < text.Length)
            {
                // Calculate how much text remains
                var remainingLength = text.Length - currentPosition;
                // If remaining text fits in one chunk, add it and finish
                if (remainingLength <= chunkSize)
                {
                    chunks.Add(text.Substring(currentPosition).Trim());
                    break;
                }

                // Find the best break point (end of sentence) within chunk size
                var chunkEndPosition = FindBestBreakPoint(text, currentPosition, chunkSize);
                // Extract the chunk
                var chunkLength = chunkEndPosition - currentPosition;
                var chunk = text.Substring(currentPosition, chunkLength).Trim();
                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    chunks.Add(chunk);
                }

                // Move to next position
                currentPosition = chunkEndPosition;
                // Skip any leading whitespace for next chunk
                while (currentPosition < text.Length && char.IsWhiteSpace(text[currentPosition]))
                {
                    currentPosition++;
                }
            }

            return chunks;
        }

        private int FindBestBreakPoint(string text, int startPosition, int maxLength)
        {
            var endPosition = Math.Min(startPosition + maxLength, text.Length);
            var searchText = text.Substring(startPosition, endPosition - startPosition);

            // Look for sentence-ending punctuation from the end backwards
            // Prefer breaking after: . ! ? followed by space or at speaker change (Name:)
            // First, try to find a speaker change (e.g., "Sarah Chen:") - best break point
            var speakerPattern = new Regex(@"\s+[A-Z][a-z]+(\s+[A-Z][a-z]+)?:\s");
            var speakerMatches = speakerPattern.Matches(searchText);
            if (speakerMatches.Count > 0)
            {
                // Use the last speaker change before the limit
                var lastMatch = speakerMatches[speakerMatches.Count - 1];
                if (lastMatch.Index > maxLength * 0.5) // Only if it's in the second half
                {
                    return startPosition + lastMatch.Index;
                }
            }

            // Next, try to find sentence endings
            for (int i = searchText.Length - 1; i > maxLength / 2; i--)
            {
                if (i < searchText.Length - 1 &&
                    (searchText[i] == '.' || searchText[i] == '!' || searchText[i] == '?') &&
                    char.IsWhiteSpace(searchText[i + 1]))
                {
                    return startPosition + i + 1;
                }
            }

            // If no good break point, find last space
            var lastSpace = searchText.LastIndexOf(' ');
            if (lastSpace > maxLength / 2)
            {
                return startPosition + lastSpace;
            }

            // Worst case: hard break at max length
            return endPosition;
        }

        private string CreateEnrichedText(string teamId, DateTime meetingDate, string meetingId, int sequence, string chunkText)
        {
            var header = $"[Team: {teamId} | Date: {meetingDate:yyyy-MM-dd} | Meeting: {meetingId} | Part: {sequence}]";
            return $"{header} Content: {chunkText}";
        }
    }
}
