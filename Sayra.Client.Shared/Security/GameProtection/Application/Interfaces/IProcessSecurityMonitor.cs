using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Security.GameProtection.Application.Interfaces;

public interface IProcessSecurityMonitor
{
    Task StartMonitoringAsync(CancellationToken cancellationToken);
    Task StopMonitoringAsync();
}
