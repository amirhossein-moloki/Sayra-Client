using System.Collections.Generic;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IAuditLogger
    {
        void LogSecurity(string messageTemplate, Dictionary<string, object>? properties = null);
        void LogAudit(string messageTemplate, Dictionary<string, object>? properties = null);
        void LogOperational(string messageTemplate, Dictionary<string, object>? properties = null);
        void LogPerformance(string messageTemplate, Dictionary<string, object>? properties = null);
        void LogEvent(EventLogEntry entry);
    }
}
