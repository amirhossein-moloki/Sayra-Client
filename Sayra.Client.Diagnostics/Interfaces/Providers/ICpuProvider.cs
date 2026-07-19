using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Interfaces.Providers
{
    public interface ICpuProvider : IHardwareProvider
    {
        Task<CpuInformation> GetCpuAsync(CancellationToken cancellationToken = default);
    }
}
