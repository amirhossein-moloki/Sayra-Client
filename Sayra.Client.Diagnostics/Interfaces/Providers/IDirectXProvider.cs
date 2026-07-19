using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Diagnostics.Interfaces.Providers
{
    public interface IDirectXProvider : IHardwareProvider
    {
        Task<string> GetVersionAsync(CancellationToken cancellationToken = default);
        Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default);
    }
}
