using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Providers
{
    public class OpenGLProvider : IOpenGLProvider
    {
        private readonly ILogger<OpenGLProvider> _logger;

        public string ProviderName => "OpenGL Provider";

        public OpenGLProvider(ILogger<OpenGLProvider> logger)
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
            _logger.LogInformation("OpenGL Provider started.");

            string version = "4.6"; // Standard modern desktop OpenGL
            bool supported = IsSupportedInternal();
            if (!supported)
            {
                version = "Not Supported";
            }

            _logger.LogInformation("OpenGL Provider completed in {DurationMs} ms.", sw.ElapsedMilliseconds);
            return Task.FromResult(version);
        }

        public Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsSupportedInternal());
        }

        private bool IsSupportedInternal()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    string systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string openglDllPath = Path.Combine(systemPath, "opengl32.dll");
                    return File.Exists(openglDllPath);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to verify OpenGL support via opengl32.dll existence check.");
                    return false;
                }
            }
            return false;
        }
    }
}
