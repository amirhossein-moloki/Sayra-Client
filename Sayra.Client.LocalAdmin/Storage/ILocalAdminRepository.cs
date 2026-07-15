using System.Collections.Generic;
using System.Threading.Tasks;
using Sayra.Client.LocalAdmin.Models;

namespace Sayra.Client.LocalAdmin.Storage
{
    public interface ILocalAdminRepository
    {
        Task<IEnumerable<LocalAdminCredential>> LoadCredentialsAsync();
        Task SaveCredentialsAsync(IEnumerable<LocalAdminCredential> credentials);
    }
}
