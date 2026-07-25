using Microsoft.Extensions.DependencyInjection;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Interfaces;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Services;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Infrastructure.Windows.JobObjects;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Infrastructure.Windows.ProcessMonitoring;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Infrastructure.Windows.ResourceMonitoring;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.DependencyInjection
{
    public static class ProcessSupervisorExtensions
    {
        public static IServiceCollection AddProcessSupervisorServices(this IServiceCollection services)
        {
            services.AddSingleton<IJobObjectManager, JobObjectManager>();
            services.AddSingleton<IProcessTreeMonitor, ProcessTreeMonitor>();
            services.AddSingleton<IProcessResourceMonitor, ProcessResourceMonitor>();
            services.AddSingleton<IProcessSupervisor, Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Services.ProcessSupervisor>();

            return services;
        }
    }
}
