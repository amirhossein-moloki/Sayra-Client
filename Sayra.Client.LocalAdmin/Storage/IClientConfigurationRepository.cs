using System.Threading.Tasks;
using Sayra.Client.LocalAdmin.Models;

namespace Sayra.Client.LocalAdmin.Storage
{
    public interface IClientConfigurationRepository
    {
        Task<ClientConfiguration> LoadConfigurationAsync();
        Task SaveConfigurationAsync(ClientConfiguration configuration);
    }
}
