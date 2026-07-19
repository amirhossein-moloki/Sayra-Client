using System;
using System.Threading.Tasks;
using Sayra.Client.Authentication.Contracts;
using Sayra.Client.Authentication.Models;

namespace Sayra.Client.Authentication.Providers
{
    public class ServerAuthenticationProvider : IAuthenticationProvider
    {
        public string ProviderName => "Server";

        public bool CanHandle(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            // Future LAN Server provider can handle usernames starting with server_ or specific enterprise/cloud identifiers,
            // or act as a default remote API validator.
            return username.StartsWith("server_") || username.EndsWith("@sayra.ir");
        }

        public Task<AuthenticationResult> AuthenticateAsync(string username, string password)
        {
            if (!CanHandle(username, password))
            {
                return Task.FromResult(AuthenticationResult.CreateFailure("This provider cannot handle the specified credentials."));
            }

            // This is a stub simulating future cloud/server authentication.
            // Currently, we return a failure or simulated result to prove compatibility with JWT/Server tokens.
            return Task.FromResult(AuthenticationResult.CreateFailure("اتصال به سرور برقرار نیست. لطفاً مجدداً تلاش کنید یا از حالت آفلاین استفاده کنید."));
        }
    }
}
