using System.Threading.Tasks;
using Sayra.Client.LocalAdmin.Models;

namespace Sayra.Client.LocalAdmin.Services
{
    public interface IClientConfigurationService
    {
        Task<ClientConfiguration> GetConfigurationAsync();
        Task SaveConfigurationAsync(ClientConfiguration configuration);
    }
}
