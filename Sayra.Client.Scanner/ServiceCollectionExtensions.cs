using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.GameLibrary.Services;
using Sayra.Client.Scanner.Cache;
using Sayra.Client.Scanner.Providers;
using Sayra.Client.Scanner.ScannerEngine;
using Sayra.Client.Scanner.Services;
using Sayra.Client.Scanner.Validation;

namespace Sayra.Client.Scanner
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationScanner(this IServiceCollection services, string? basePath = null)
        {
            services.AddSingleton<IKnownGameDatabase>(sp =>
                new KnownGameDatabase(basePath, sp.GetService<ILogger<KnownGameDatabase>>()));

            services.AddSingleton<IScanCacheService>(sp =>
                new ScanCacheService(basePath, sp.GetService<ILogger<ScanCacheService>>()));

            services.AddSingleton<IExecutableMetadataProvider, ExecutableMetadataProvider>();

            services.AddSingleton<IScannerValidator, ScannerValidator>();

            services.AddSingleton<IGameDetectionEngine, GameDetectionEngine>();

            services.AddSingleton<IApplicationScannerService, ApplicationScannerService>();

            return services;
        }
    }
}
