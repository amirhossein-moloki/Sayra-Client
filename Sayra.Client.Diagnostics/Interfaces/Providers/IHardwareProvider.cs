using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Interfaces.Providers
{
    public interface IHardwareProvider
    {
        string ProviderName { get; }
        Task<ValidationResult> ValidateAsync(CancellationToken cancellationToken = default);
    }
}
