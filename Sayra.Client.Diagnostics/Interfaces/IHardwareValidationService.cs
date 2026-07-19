using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Interfaces
{
    public interface IHardwareValidationService
    {
        Task<ValidationResult> ValidateAsync(HardwareSpecification spec, HardwareMetrics metrics, CancellationToken cancellationToken = default);
    }
}
