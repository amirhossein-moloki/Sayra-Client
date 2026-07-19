using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Diagnostics.Interfaces.Providers
{
    public interface IVulkanProvider : IHardwareProvider
    {
        Task<string> GetVersionAsync(CancellationToken cancellationToken = default);
        Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default);
    }
}
