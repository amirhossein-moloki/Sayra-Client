using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Interfaces
{
    public interface IHardwareSpecificationService
    {
        Task<HardwareSpecification> GetSpecificationAsync(CancellationToken cancellationToken = default);
    }
}
