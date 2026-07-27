using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    public interface ISelfHealingService
    {
        Task MonitorAndHealAsync(CancellationToken cancellationToken = default);
        Task RecoverSubsystemAsync(string subsystemName, CancellationToken cancellationToken = default);
        int GetRecoveryAttemptsCount(string subsystemName);
    }
}
