using MeetingInsights.Core.Models;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using AdaptiveCards;
using static MeetingInsights.Core.Models.QueryModels;

namespace MeetingInsights.TeamsBot.Cards
{

    /// <summary>
    /// Builds adaptive cards for rich Teams responses
    /// </summary>
    public static class AdaptiveCardBuilder
    {
        public static Attachment BuildQueryResponseCard(QueryResponse response)
        {
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 4))
            {
                Body = new List<AdaptiveElement>
            {
                new AdaptiveTextBlock
                {
                    Text = "📝 Query Response",
                    Weight = AdaptiveTextWeight.Bolder,
                    Size = AdaptiveTextSize.Large
                },
                new AdaptiveTextBlock
                {
                    Text = $"**Question:** {response.Question}",
                    Wrap = true
                },
                new AdaptiveTextBlock
                {
                    Text = response.Answer,
                    Wrap = true
                }
            }
            };

            // Add conflict warning if present
            if (response.HasConflicts)
            {
                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = $"⚠️ **Note:** {response.ConflictSummary}",
                    Wrap = true,
                    Color = AdaptiveTextColor.Warning
                });
            }

            // Add timeline
            if (response.Timeline?.Any() == true)
            {
                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = "📅 **Timeline:**",
                    Weight = AdaptiveTextWeight.Bolder
                });

                foreach (var item in response.Timeline.Take(5))
                {
                    card.Body.Add(new AdaptiveTextBlock
                    {
                        Text = $"• **{item.Date:MMM d}** ({item.MeetingId}): {item.Summary}",
                        Wrap = true,
                        Size = AdaptiveTextSize.Small
                    });
                }
            }

            // Add sources
            if (response.Sources?.Any() == true)
            {
                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = $"📚 Sources: {string.Join(", ", response.Sources.Select(s => s.MeetingId).Distinct())}",
                    Size = AdaptiveTextSize.Small,
                    IsSubtle = true
                });
            }

            return CreateAttachment(card);
        }

        public static Attachment BuildDecisionsCard(DecisionsResponse response)
        {
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 4))
            {
                Body = new List<AdaptiveElement>
            {
                new AdaptiveTextBlock
                {
                    Text = "📋 Decisions Log",
                    Weight = AdaptiveTextWeight.Bolder,
                    Size = AdaptiveTextSize.Large
                },
                new AdaptiveColumnSet
                {
                    Columns = new List<AdaptiveColumn>
                    {
                        CreateStatColumn("Total", response.TotalDecisions.ToString(), "Accent"),
                        CreateStatColumn("With Changes", response.DecisionsWithConflicts.ToString(), "Warning"),
                    }
                }
            }
            };

            // Add recent decisions
            foreach (var decision in response.Decisions.TakeLast(5).Reverse())
            {
                var icon = decision.HasConflict ? "⚠️" : "✅";
                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = $"{icon} **{decision.Topic}** ({decision.MeetingId})",
                    Wrap = true
                });
                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = decision.Description,
                    Wrap = true,
                    Size = AdaptiveTextSize.Small,
                    IsSubtle = true
                });
            }

            return CreateAttachment(card);
        }

        public static Attachment BuildConflictsCard(ConflictsResponse response)
        {
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 4))
            {
                Body = new List<AdaptiveElement>
            {
                new AdaptiveTextBlock
                {
                    Text = "⚠️ Decision Conflicts",
                    Weight = AdaptiveTextWeight.Bolder,
                    Size = AdaptiveTextSize.Large
                },
                new AdaptiveTextBlock
                {
                    Text = $"Found **{response.TotalConflicts}** decisions that changed over time",
                    Wrap = true
                },
                new AdaptiveTextBlock
                {
                    Text = response.OverallAssessment,
                    Wrap = true,
                    Size = AdaptiveTextSize.Small
                }
            }
            };

            foreach (var conflict in response.Conflicts.Take(3))
            {
                card.Body.Add(new AdaptiveContainer
                {
                    Style = AdaptiveContainerStyle.Emphasis,
                    Items = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock
                    {
                        Text = $"**{conflict.Topic}** - {conflict.OverallChangeType}",
                        Weight = AdaptiveTextWeight.Bolder
                    },
                    new AdaptiveTextBlock
                    {
                        Text = conflict.ConflictSummary,
                        Wrap = true,
                        Size = AdaptiveTextSize.Small
                    }
                }
                });
            }

            return CreateAttachment(card);
        }

        public static Attachment BuildActionItemsCard(ActionItemsResponse response)
        {
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 4))
            {
                Body = new List<AdaptiveElement>
            {
                new AdaptiveTextBlock
                {
                    Text = "📋 Action Items",
                    Weight = AdaptiveTextWeight.Bolder,
                    Size = AdaptiveTextSize.Large
                },
                new AdaptiveColumnSet
                {
                    Columns = new List<AdaptiveColumn>
                    {
                        CreateStatColumn("Total", response.TotalActionItems.ToString(), "Accent"),
                        CreateStatColumn("Completed", response.CompletedCount.ToString(), "Good"),
                        CreateStatColumn("Pending", (response.TotalActionItems - response.CompletedCount).ToString(), "Warning")
                    }
                }
            }
            };

            // Add items by person
            foreach (var person in response.ItemsByPerson.Take(5))
            {
                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = $"👤 **{person.Key}**: {person.Value} items",
                    Size = AdaptiveTextSize.Small
                });
            }

            // Add recent pending items
            var pendingItems = response.ActionItems
                .Where(a => a.Status != ActionItemStatus.Completed)
                .Take(5);

            if (pendingItems.Any())
            {
                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = "**Pending Items:**",
                    Weight = AdaptiveTextWeight.Bolder
                });

                foreach (var item in pendingItems)
                {
                    var statusIcon = item.Status switch
                    {
                        ActionItemStatus.InProgress => "🔄",
                        ActionItemStatus.Blocked => "🚫",
                        _ => "⏳"
                    };
                    card.Body.Add(new AdaptiveTextBlock
                    {
                        Text = $"{statusIcon} **{item.AssignedTo}**: {item.Task}",
                        Wrap = true,
                        Size = AdaptiveTextSize.Small
                    });
                }
            }

            return CreateAttachment(card);
        }

        public static Attachment BuildPendingItemsCard(List<ActionItem> items, string teamId)
        {
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 4))
            {
                Body = new List<AdaptiveElement>
            {
                new AdaptiveTextBlock
                {
                    Text = "⏳ Pending Action Items",
                    Weight = AdaptiveTextWeight.Bolder,
                    Size = AdaptiveTextSize.Large
                },
                new AdaptiveTextBlock
                {
                    Text = $"**{items.Count}** items still pending",
                    Wrap = true
                }
            }
            };

            foreach (var item in items.Take(10))
            {
                var statusIcon = item.Status switch
                {
                    ActionItemStatus.InProgress => "🔄",
                    ActionItemStatus.Blocked => "🚫",
                    _ => "⏳"
                };

                card.Body.Add(new AdaptiveContainer
                {
                    Items = new List<AdaptiveElement>
                {
                    new AdaptiveTextBlock
                    {
                        Text = $"{statusIcon} **{item.AssignedTo}**",
                        Weight = AdaptiveTextWeight.Bolder,
                        Size = AdaptiveTextSize.Small
                    },
                    new AdaptiveTextBlock
                    {
                        Text = item.Task,
                        Wrap = true,
                        Size = AdaptiveTextSize.Small
                    },
                    new AdaptiveTextBlock
                    {
                        Text = $"Assigned: {item.AssignedDate:MMM d} in {item.AssignedInMeetingId}",
                        Size = AdaptiveTextSize.Small,
                        IsSubtle = true
                    }
                }
                });
            }

            return CreateAttachment(card);
        }

        public static Attachment BuildSentimentCard(TeamSentimentResponse response)
        {
            var sentimentEmoji = response.OverallCategory switch
            {
                SentimentCategory.VeryPositive => "😊",
                SentimentCategory.Positive => "🙂",
                SentimentCategory.Neutral => "😐",
                SentimentCategory.Negative => "😕",
                SentimentCategory.VeryNegative => "😟",
                _ => "😐"
            };

            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 4))
            {
                Body = new List<AdaptiveElement>
            {
                new AdaptiveTextBlock
                {
                    Text = $"📊 Team Sentiment {sentimentEmoji}",
                    Weight = AdaptiveTextWeight.Bolder,
                    Size = AdaptiveTextSize.Large
                },
                new AdaptiveTextBlock
                {
                    Text = $"**Overall Score:** {response.OverallTeamSentiment:F2} ({response.OverallCategory})",
                    Wrap = true
                },
                new AdaptiveTextBlock
                {
                    Text = response.TeamMoodSummary,
                    Wrap = true,
                    Size = AdaptiveTextSize.Small
                }
            }
            };

            // Add trend visualization (simple text version)
            if (response.Trend?.Any() == true)
            {
                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = "**Trend:**",
                    Weight = AdaptiveTextWeight.Bolder
                });

                foreach (var point in response.Trend.TakeLast(5))
                {
                    var bar = GetSentimentBar(point.Score);
                    card.Body.Add(new AdaptiveTextBlock
                    {
                        Text = $"{point.MeetingId} {bar} {point.Score:F2}",
                        Size = AdaptiveTextSize.Small
                    });
                }
            }

            // Add concerns if any
            if (response.Concerns?.Any() == true)
            {
                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = $"⚠️ **{response.Concerns.Count} Concerns Flagged**",
                    Color = AdaptiveTextColor.Warning
                });
            }

            return CreateAttachment(card);
        }

        public static Attachment BuildTeamSummaryCard(TeamMeetingsSummary response)
        {
            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 4))
            {
                Body = new List<AdaptiveElement>
            {
                new AdaptiveTextBlock
                {
                    Text = "📝 Team Meeting Summary",
                    Weight = AdaptiveTextWeight.Bolder,
                    Size = AdaptiveTextSize.Large
                },
                new AdaptiveTextBlock
                {
                    Text = $"**{response.TotalMeetings}** meetings from {response.FirstMeetingDate:MMM d} to {response.LastMeetingDate:MMM d, yyyy}",
                    Wrap = true
                },
                new AdaptiveTextBlock
                {
                    Text = "**Executive Summary:**",
                    Weight = AdaptiveTextWeight.Bolder
                },
                new AdaptiveTextBlock
                {
                    Text = response.ExecutiveSummary,
                    Wrap = true,
                    Size = AdaptiveTextSize.Small
                }
            }
            };

            // Add key themes
            if (response.OverallKeyThemes?.Any() == true)
            {
                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = $"**Key Themes:** {string.Join(", ", response.OverallKeyThemes)}",
                    Wrap = true,
                    Size = AdaptiveTextSize.Small
                });
            }

            // Add meeting briefs
            if (response.MeetingBriefs?.Any() == true)
            {
                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = "**Meetings:**",
                    Weight = AdaptiveTextWeight.Bolder
                });

                foreach (var brief in response.MeetingBriefs.TakeLast(5))
                {
                    card.Body.Add(new AdaptiveTextBlock
                    {
                        Text = $"• **{brief.MeetingId}** ({brief.MeetingDate:MMM d}): {brief.OneLinerSummary}",
                        Wrap = true,
                        Size = AdaptiveTextSize.Small
                    });
                }
            }

            return CreateAttachment(card);
        }

        public static Attachment BuildMeetingSummaryCard(MeetingSummaryResponse response)
        {
            var summary = response.Summary!;

            var card = new AdaptiveCard(new AdaptiveSchemaVersion(1, 4))
            {
                Body = new List<AdaptiveElement>
            {
                new AdaptiveTextBlock
                {
                    Text = $"📝 Meeting Summary: {summary.MeetingId}",
                    Weight = AdaptiveTextWeight.Bolder,
                    Size = AdaptiveTextSize.Large
                },
                new AdaptiveTextBlock
                {
                    Text = $"**Date:** {summary.MeetingDate:MMMM d, yyyy}",
                    Wrap = true
                },
                new AdaptiveTextBlock
                {
                    Text = summary.Summary,
                    Wrap = true
                }
            }
            };

            // Add key points
            if (summary.KeyPoints?.Any() == true)
            {
                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = "**Key Points:**",
                    Weight = AdaptiveTextWeight.Bolder
                });

                foreach (var point in summary.KeyPoints.Take(5))
                {
                    card.Body.Add(new AdaptiveTextBlock
                    {
                        Text = $"• {point}",
                        Wrap = true,
                        Size = AdaptiveTextSize.Small
                    });
                }
            }

            // Add participants
            if (summary.Participants?.Any() == true)
            {
                card.Body.Add(new AdaptiveTextBlock
                {
                    Text = $"**Participants:** {string.Join(", ", summary.Participants)}",
                    Wrap = true,
                    Size = AdaptiveTextSize.Small,
                    IsSubtle = true
                });
            }

            return CreateAttachment(card);
        }

        // Helper methods
        private static AdaptiveColumn CreateStatColumn(string title, string value, string color)
        {
            return new AdaptiveColumn
            {
                Width = "auto",
                Items = new List<AdaptiveElement>
            {
                new AdaptiveTextBlock
                {
                    Text = title,
                    Size = AdaptiveTextSize.Small,
                    IsSubtle = true,
                    HorizontalAlignment = AdaptiveHorizontalAlignment.Center
                },
                new AdaptiveTextBlock
                {
                    Text = value,
                    Size = AdaptiveTextSize.ExtraLarge,
                    Weight = AdaptiveTextWeight.Bolder,
                    HorizontalAlignment = AdaptiveHorizontalAlignment.Center
                }
            }
            };
        }

        private static string GetSentimentBar(double score)
        {
            var normalized = (int)((score + 1) * 5); // 0-10
            var filled = Math.Max(0, Math.Min(10, normalized));
            return new string('█', filled) + new string('░', 10 - filled);
        }

        private static Attachment CreateAttachment(AdaptiveCard card)
        {
            return new Attachment
            {
                ContentType = AdaptiveCard.ContentType,
                Content = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(card))
            };
        }
    }
}
