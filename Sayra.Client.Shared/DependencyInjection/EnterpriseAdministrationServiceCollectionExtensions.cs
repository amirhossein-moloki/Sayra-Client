using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sayra.Client.Shared.Fleet.Administration;
using Sayra.Client.Shared.Fleet.Administration.Security;
using Sayra.Client.Shared.Fleet.Administration.Orchestration;
using Sayra.Client.Shared.Fleet.Administration.Queries;
using Sayra.Client.Shared.Interfaces.Phase9;

namespace Sayra.Client.Shared.DependencyInjection
{
    /// <summary>
    /// Service collection extensions to register Phase 9 Stage 10 Enterprise Administration Platform.
    /// </summary>
    public static class EnterpriseAdministrationServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the ApiSecurityMiddleware in the application middleware pipeline.
        /// </summary>
        public static IApplicationBuilder UseEnterpriseAdministrationSecurity(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ApiSecurityMiddleware>();
        }

        /// <summary>
        /// Registers all Stage 10 Enterprise Administration services.
        /// </summary>
        public static IServiceCollection AddEnterpriseAdministration(this IServiceCollection services)
        {
            // 1. Core Security & Auth
            services.TryAddSingleton<IAuthenticationService, AuthenticationService>();
            services.TryAddSingleton<IAuthorizationService, AuthorizationService>();

            // 2. Integration Services (Audit, Notification, Coordinator)
            services.TryAddSingleton<IAuditIntegrationService, AuditIntegrationService>();
            services.TryAddSingleton<IAdministrationNotificationService, AdministrationNotificationService>();
            services.TryAddSingleton<IEnterpriseManagementCoordinator, EnterpriseManagementCoordinator>();

            // 3. Dashboard Queries
            services.TryAddSingleton<IDashboardQueryService, DashboardQueryService>();

            // 4. API Endpoints Registry & Requests Routing
            services.TryAddSingleton<AdministrationEndpointRegistry>(sp =>
            {
                var registry = new AdministrationEndpointRegistry();
                registry.MapAllEndpoints(sp);
                return registry;
            });

            services.TryAddSingleton<AdministrationRequestHandler>();
            services.TryAddSingleton<IAdministrationApiService, AdministrationApiService>();

            return services;
        }
    }
}
