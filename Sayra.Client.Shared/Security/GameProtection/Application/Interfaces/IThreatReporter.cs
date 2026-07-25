using Sayra.Client.Shared.Security.GameProtection.Domain.Events;

namespace Sayra.Client.Shared.Security.GameProtection.Application.Interfaces;

public interface IThreatReporter
{
    void ReportThreat(SecurityThreatEventBase threatEvent);
}
