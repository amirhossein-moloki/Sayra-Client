using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Interfaces.Providers
{
    public interface IMotherboardProvider : IHardwareProvider
    {
        Task<MotherboardInformation> GetMotherboardAsync(CancellationToken cancellationToken = default);
    }
}
