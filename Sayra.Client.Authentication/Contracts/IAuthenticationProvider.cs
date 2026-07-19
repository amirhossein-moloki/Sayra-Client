using System.Threading.Tasks;
using Sayra.Client.Authentication.Models;

namespace Sayra.Client.Authentication.Contracts
{
    public interface IAuthenticationProvider
    {
        string ProviderName { get; }
        bool CanHandle(string username, string password);
        Task<AuthenticationResult> AuthenticateAsync(string username, string password);
    }
}
