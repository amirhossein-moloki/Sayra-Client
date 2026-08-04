using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using Sayra.Client.Shared.Models.Phase9.Options;
using Sayra.Client.Shared.Models.Phase9.Dtos;
using Sayra.Client.Shared.Models.Phase9.Validation;

namespace Sayra.Client.Shared.DependencyInjection
{
    /// <summary>
    /// Service collection extension methods to register Phase 9 Foundation dependencies.
    /// </summary>
    public static class Phase9ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers options, validators, and core shared models for Phase 9.
        /// </summary>
        /// <param name="services">The service collection to add to.</param>
        /// <returns>The modified service collection.</returns>
        public static IServiceCollection AddPhase9Foundation(this IServiceCollection services)
        {
            // Part 5: Options Registration & Bindings (bound to standard Microsoft.Extensions.Options pattern)
            services.AddOptions<FleetOptions>();
            services.AddOptions<MonitoringOptions>();
            services.AddOptions<DiagnosticsOptions>();
            services.AddOptions<TransferOptions>();
            services.AddOptions<MaintenanceOptions>();
            services.AddOptions<PolicyOptions>();
            services.AddOptions<AuditOptions>();
            services.AddOptions<BulkOperationOptions>();
            services.AddOptions<RemoteSupportOptions>();
            services.AddOptions<AdministrationOptions>();

            // Part 7 & 10: FluentValidation Structural DTO Validators
            services.AddTransient<IValidator<MachineQueryRequest>, MachineQueryRequestValidator>();
            services.AddTransient<IValidator<FleetQueryRequest>, FleetQueryRequestValidator>();

            // Phase 9 Stage 2: Fleet Management Engine
            services.AddFleetManagement();

            // Phase 9 Stage 3: Remote Command Framework
            services.AddRemoteCommandFramework();

            // Phase 9 Stage 4: Enterprise Live Monitoring Engine
            services.AddLiveMonitoring();

            // Phase 9 Stage 5: Enterprise Remote Diagnostics Engine
            services.AddRemoteDiagnostics();

            services.AddTransient<IValidator<RemoteCommandRequest>, RemoteCommandRequestValidator>();
            services.AddTransient<IValidator<RemoteCommandResponse>, RemoteCommandResponseValidator>();
            services.AddTransient<IValidator<BulkOperationRequest>, BulkOperationRequestValidator>();
            services.AddTransient<IValidator<BulkOperationResponse>, BulkOperationResponseValidator>();
            services.AddTransient<IValidator<PolicyAssignmentRequest>, PolicyAssignmentRequestValidator>();
            services.AddTransient<IValidator<MaintenanceRequest>, MaintenanceRequestValidator>();
            services.AddTransient<IValidator<DiagnosticRequest>, DiagnosticRequestValidator>();
            services.AddTransient<IValidator<TransferRequest>, TransferRequestValidator>();
            services.AddTransient<IValidator<TransferResponse>, TransferResponseValidator>();
            services.AddTransient<IValidator<RemoteSupportRequest>, RemoteSupportRequestValidator>();
            services.AddTransient<IValidator<AuditQueryRequest>, AuditQueryRequestValidator>();
            services.AddTransient<IValidator<AdministrationReportRequest>, AdministrationReportRequestValidator>();

            return services;
        }
    }
}
