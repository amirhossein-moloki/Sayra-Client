using System.Threading.Tasks;
using Sayra.Client.LocalAdmin.Models;

namespace Sayra.Client.LocalAdmin.Services
{
    public interface ILocalAdminService
    {
        Task InitializeAdmin();
        Task<bool> CreateAdmin(string username, string password);
        Task<AdminAuthenticationResult> Authenticate(string username, string password);
        Task<bool> ChangePassword(string username, string oldPassword, string newPassword);
        Task<bool> DeleteAdmin(string username);
        bool ValidatePasswordStrength(string password);
    }
}
