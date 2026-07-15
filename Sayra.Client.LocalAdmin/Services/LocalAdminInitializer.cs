using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Sayra.Client.LocalAdmin.Services
{
    public class LocalAdminInitializer : IHostedService
    {
        private readonly ILocalAdminService _localAdminService;

        public LocalAdminInitializer(ILocalAdminService localAdminService)
        {
            _localAdminService = localAdminService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _localAdminService.InitializeAdmin();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
