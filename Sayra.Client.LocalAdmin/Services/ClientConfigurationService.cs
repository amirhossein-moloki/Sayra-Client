using System.Threading.Tasks;
using Sayra.Client.LocalAdmin.Models;
using Sayra.Client.LocalAdmin.Storage;

namespace Sayra.Client.LocalAdmin.Services
{
    public class ClientConfigurationService : IClientConfigurationService
    {
        private readonly IClientConfigurationRepository _repository;

        public ClientConfigurationService(IClientConfigurationRepository repository)
        {
            _repository = repository;
        }

        public async Task<ClientConfiguration> GetConfigurationAsync()
        {
            return await _repository.LoadConfigurationAsync();
        }

        public async Task SaveConfigurationAsync(ClientConfiguration configuration)
        {
            await _repository.SaveConfigurationAsync(configuration);
        }
    }
}
