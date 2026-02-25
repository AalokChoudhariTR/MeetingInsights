using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingInsights.Core.Models
{
    /// <summary>
    /// Sentiment category based on score
    /// </summary>
    public enum SentimentCategory
    {
        VeryNegative,   // -1.0 to -0.6
        Negative,       // -0.6 to -0.2
        Neutral,        // -0.2 to +0.2
        Positive,       // +0.2 to +0.6
        VeryPositive    // +0.6 to +1.0
    }

    /// <summary>
    /// Sentiment for a single meeting
    /// </summary>
    public class MeetingSentiment
    {
        public string MeetingId { get; set; } = string.Empty;
        public DateTime MeetingDate { get; set; }
        public double OverallScore { get; set; }              // -1 to +1
        public SentimentCategory Category { get; set; }
        public string MoodSummary { get; set; } = string.Empty;
        public List<SpeakerSentiment> BySpeaker { get; set; } = new();
        public List<TopicSentiment> ByTopic { get; set; } = new();
        public List<SentimentHighlight> Highlights { get; set; } = new();
        public double? ChangeFromPrevious { get; set; }       // Sentiment shift
    }

    /// <summary>
    /// Sentiment for a specific speaker
    /// </summary>
    public class SpeakerSentiment
    {
        public string SpeakerName { get; set; } = string.Empty;
        public double AverageScore { get; set; }
        public SentimentCategory Category { get; set; }
        public int StatementCount { get; set; }
        public string MostPositiveStatement { get; set; } = string.Empty;
        public string MostNegativeStatement { get; set; } = string.Empty;
    }

    /// <summary>
    /// Sentiment for a specific topic
    /// </summary>
    public class TopicSentiment
    {
        public string Topic { get; set; } = string.Empty;
        public double Score { get; set; }
        public SentimentCategory Category { get; set; }
        public string Context { get; set; } = string.Empty;
    }

    /// <summary>
    /// Notable sentiment highlight
    /// </summary>
    public class SentimentHighlight
    {
        public string Type { get; set; } = string.Empty;      // "Concern", "Enthusiasm", "Frustration", etc.
        public string Speaker { get; set; } = string.Empty;
        public string Quote { get; set; } = string.Empty;
        public double Score { get; set; }
        public string Topic { get; set; } = string.Empty;
    }

    /// <summary>
    /// Team sentiment trend over time
    /// </summary>
    public class SentimentTrend
    {
        public string MeetingId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public double Score { get; set; }
        public SentimentCategory Category { get; set; }
        public string KeyDriver { get; set; } = string.Empty; // What drove the sentiment
    }

    /// <summary>
    /// Sentiment concern/flag
    /// </summary>
    public class SentimentConcern
    {
        public string MeetingId { get; set; } = string.Empty;
        public DateTime MeetingDate { get; set; }
        public string ConcernType { get; set; } = string.Empty;  // "Stress", "Frustration", "Anxiety"
        public string Description { get; set; } = string.Empty;
        public List<string> AffectedSpeakers { get; set; } = new();
        public string Topic { get; set; } = string.Empty;
        public double SentimentScore { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response for team sentiment overview
    /// </summary>
    public class TeamSentimentResponse
    {
        public bool Success { get; set; }
        public string TeamId { get; set; } = string.Empty;
        public int MeetingsAnalyzed { get; set; }
        public double OverallTeamSentiment { get; set; }
        public SentimentCategory OverallCategory { get; set; }
        public string TeamMoodSummary { get; set; } = string.Empty;
        public List<SentimentTrend> Trend { get; set; } = new();
        public List<SpeakerSentiment> SpeakerAverages { get; set; } = new();
        public MeetingSentiment? MostPositiveMeeting { get; set; }
        public MeetingSentiment? MostNegativeMeeting { get; set; }
        public List<SentimentConcern> Concerns { get; set; } = new();
        public int ProcessingTimeMs { get; set; }
    }

    /// <summary>
    /// Response for single meeting sentiment
    /// </summary>
    public class MeetingSentimentResponse
    {
        public bool Success { get; set; }
        public MeetingSentiment? Sentiment { get; set; }
        public int ProcessingTimeMs { get; set; }
    }

    /// <summary>
    /// Response for concerns
    /// </summary>
    public class SentimentConcernsResponse
    {
        public bool Success { get; set; }
        public string TeamId { get; set; } = string.Empty;
        public int TotalConcerns { get; set; }
        public string OverallAssessment { get; set; } = string.Empty;
        public List<SentimentConcern> Concerns { get; set; } = new();
        public List<string> RecommendedActions { get; set; } = new();
        public int ProcessingTimeMs { get; set; }
    }
}
