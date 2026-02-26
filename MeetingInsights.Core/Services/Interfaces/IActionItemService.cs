using MeetingInsights.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingInsights.Core.Services.Interfaces
{
    public interface IActionItemService
    {
        Task<ActionItemsResponse> GetAllActionItemsAsync(string teamId);
        Task<MeetingActionItemsResponse> GetMeetingActionItemsAsync(string teamId, string meetingId);
    }
}
