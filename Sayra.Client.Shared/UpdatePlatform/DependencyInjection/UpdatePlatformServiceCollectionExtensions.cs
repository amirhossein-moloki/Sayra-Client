using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Application.Services;
using Sayra.Client.Shared.UpdatePlatform.Application.Validation;
using Sayra.Client.Shared.UpdatePlatform.Domain.Options;

namespace Sayra.Client.Shared.UpdatePlatform.DependencyInjection
{
    /// <summary>
    /// Service collection extension methods to register Update Platform Foundation dependencies.
    /// </summary>
    public static class UpdatePlatformServiceCollectionExtensions
    {
        /// <summary>
        /// Registers options, validators, and core shared interfaces for the Update Platform.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <returns>The modified service collection.</returns>
        public static IServiceCollection AddUpdatePlatformFoundation(this IServiceCollection services)
        {
            // Register Options
            services.AddOptions<UpdateOptions>();
            services.AddOptions<RollbackOptions>();
            services.AddOptions<DownloadOptions>();

            // Register Validators
            services.AddTransient<IVersionValidator, VersionValidator>();
            services.AddTransient<IDependencyValidator, DependencyValidator>();
            services.AddTransient<IManifestValidator, ManifestValidator>();
            services.AddTransient<IUpdateValidator, UpdateValidator>();

            // Phase 6 Part 2 Core Service Implementations
            services.AddSingleton<IManifestParser, ManifestParser>();
            services.AddSingleton<ISignatureVerifier, SignatureVerifier>();
            services.AddTransient<IPackageValidator, PackageValidator>();
            services.AddTransient<IPackageReader, PackageReader>();
            services.AddTransient<IPackageVerifier, PackageVerifier>();

            return services;
        }
    }
}
