using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Providers
{
    public class DirectXProvider : IDirectXProvider
    {
        private readonly ILogger<DirectXProvider> _logger;

        public string ProviderName => "DirectX Provider";

        public DirectXProvider(ILogger<DirectXProvider> logger)
        {
            _logger = logger;
        }

        public Task<ValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ValidationResult(true, new(), new()));
        }

        public Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("DirectX Provider started.");

            string dxVersion = "DirectX 12";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                dxVersion = "DirectX 12 (FL 12_1)";
            }

            _logger.LogInformation("DirectX Provider completed in {DurationMs} ms.", sw.ElapsedMilliseconds);
            return Task.FromResult(dxVersion);
        }

        public Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
        }
    }
}
