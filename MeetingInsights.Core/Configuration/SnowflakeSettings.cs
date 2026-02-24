using System;
using System.Collections.Generic;
using System.Text;

namespace MeetingInsights.Core.Configuration
{
    public class SnowflakeSettings
    {
        public string Account { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string Schema { get; set; } = string.Empty;
        public string Warehouse { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string ConnectionString => $"account={Account};user={User};authenticator=externalbrowser;db={Database};schema={Schema};warehouse={Warehouse};role={Role}";

    }
}
