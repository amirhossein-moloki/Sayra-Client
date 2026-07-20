using System.Collections.Generic;
using System.Threading.Tasks;
using Sayra.Client.LocalAdmin.Models;

namespace Sayra.Client.LocalAdmin.Services
{
    public interface IAdvertisementService
    {
        Task<IEnumerable<Advertisement>> GetActiveAdvertisementsAsync();
        Task AddAdvertisementAsync(Advertisement ad);
        Task RemoveAdvertisementAsync(string id);
        Task TriggerServerSyncHookAsync();
    }
}
