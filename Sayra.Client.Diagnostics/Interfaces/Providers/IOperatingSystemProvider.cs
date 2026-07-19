using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Interfaces.Providers
{
    public interface IOperatingSystemProvider : IHardwareProvider
    {
        Task<OperatingSystemInformation> GetOperatingSystemAsync(CancellationToken cancellationToken = default);
    }
}
