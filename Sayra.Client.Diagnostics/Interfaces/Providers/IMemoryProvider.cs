using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Interfaces.Providers
{
    public interface IMemoryProvider : IHardwareProvider
    {
        Task<MemoryInformation> GetMemoryAsync(CancellationToken cancellationToken = default);
    }
}
