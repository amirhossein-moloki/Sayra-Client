using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Fleet.Administration.Security
{
    public interface IAuthenticationService
    {
        Task<AdminUser?> ValidateCredentialsAsync(string username, string password);
        Task<string> GenerateTokenAsync(AdminUser user);
        Task<AdminUser?> ValidateTokenAsync(string token);
        Task<bool> InvalidateTokenAsync(string token);
        Task<IReadOnlyList<AdminSession>> GetActiveSessionsAsync();
    }

    public class AuthenticationService : IAuthenticationService
    {
        private static readonly byte[] SecretKey = Encoding.UTF8.GetBytes("SAYRA_ENTERPRISE_ADMINISTRATION_SECRET_JWT_KEY_2024!");
        private readonly ConcurrentDictionary<string, AdminUser> _users = new();
        private readonly ConcurrentDictionary<string, AdminSession> _sessions = new();

        public AuthenticationService()
        {
            // Seed default users for production-ready capabilities & testing
            SeedUser("admin-01", "admin", "AdminPassword123!", AdminRole.SuperAdministrator);
            SeedUser("fleet-01", "fleet", "FleetPassword123!", AdminRole.FleetAdministrator);
            SeedUser("support-01", "support", "SupportPassword123!", AdminRole.SupportEngineer);
            SeedUser("operator-01", "operator", "OperatorPassword123!", AdminRole.Operator);
            SeedUser("auditor-01", "auditor", "AuditorPassword123!", AdminRole.Auditor);
        }

        private void SeedUser(string id, string username, string password, AdminRole role)
        {
            var user = new AdminUser
            {
                AdministratorId = id,
                Username = username,
                PasswordHash = HashPassword(password),
                Role = role
            };
            _users[username.ToLowerInvariant()] = user;
        }

        public Task<AdminUser?> ValidateCredentialsAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return Task.FromResult<AdminUser?>(null);

            if (_users.TryGetValue(username.ToLowerInvariant(), out var user))
            {
                if (VerifyPassword(password, user.PasswordHash))
                {
                    return Task.FromResult<AdminUser?>(user);
                }
            }

            return Task.FromResult<AdminUser?>(null);
        }

        public Task<string> GenerateTokenAsync(AdminUser user)
        {
            var header = new { alg = "HS256", typ = "JWT" };
            var headerJson = JsonSerializer.Serialize(header);
            var headerBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));

            var expires = DateTime.UtcNow.AddHours(8);
            var payload = new
            {
                sub = user.AdministratorId,
                username = user.Username,
                role = user.Role.ToString(),
                exp = new DateTimeOffset(expires).ToUnixTimeSeconds()
            };
            var payloadJson = JsonSerializer.Serialize(payload);
            var payloadBase64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

            var rawSignature = HMACSHA256.HashData(SecretKey, Encoding.UTF8.GetBytes($"{headerBase64}.{payloadBase64}"));
            var signatureBase64 = Base64UrlEncode(rawSignature);

            var token = $"{headerBase64}.{payloadBase64}.{signatureBase64}";

            var session = new AdminSession
            {
                SessionId = Guid.NewGuid().ToString("N"),
                AdministratorId = user.AdministratorId,
                Token = token,
                ExpiresAt = expires
            };

            _sessions[token] = session;

            return Task.FromResult(token);
        }

        public Task<AdminUser?> ValidateTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return Task.FromResult<AdminUser?>(null);

            if (!_sessions.TryGetValue(token, out var session) || session.ExpiresAt < DateTime.UtcNow)
            {
                if (session != null)
                {
                    _sessions.TryRemove(token, out _);
                }
                return Task.FromResult<AdminUser?>(null);
            }

            var parts = token.Split('.');
            if (parts.Length != 3)
                return Task.FromResult<AdminUser?>(null);

            var headerBase64 = parts[0];
            var payloadBase64 = parts[1];
            var signatureBase64 = parts[2];

            // Verify signature
            var calculatedRawSig = HMACSHA256.HashData(SecretKey, Encoding.UTF8.GetBytes($"{headerBase64}.{payloadBase64}"));
            var calculatedSigBase64 = Base64UrlEncode(calculatedRawSig);

            if (signatureBase64 != calculatedSigBase64)
                return Task.FromResult<AdminUser?>(null);

            // Decode payload
            try
            {
                var payloadBytes = Base64UrlDecode(payloadBase64);
                var payloadJson = Encoding.UTF8.GetString(payloadBytes);
                var payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);

                var expSec = payload.GetProperty("exp").GetInt64();
                if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expSec)
                {
                    _sessions.TryRemove(token, out _);
                    return Task.FromResult<AdminUser?>(null);
                }

                var adminId = payload.GetProperty("sub").GetString();
                foreach (var u in _users.Values)
                {
                    if (u.AdministratorId == adminId)
                        return Task.FromResult<AdminUser?>(u);
                }
            }
            catch
            {
                return Task.FromResult<AdminUser?>(null);
            }

            return Task.FromResult<AdminUser?>(null);
        }

        public Task<bool> InvalidateTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return Task.FromResult(false);

            return Task.FromResult(_sessions.TryRemove(token, out _));
        }

        public Task<IReadOnlyList<AdminSession>> GetActiveSessionsAsync()
        {
            var list = new List<AdminSession>(_sessions.Values);
            return Task.FromResult<IReadOnlyList<AdminSession>>(list);
        }

        // Helper utilities
        private static string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private static bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }

        private static string Base64UrlEncode(byte[] input)
        {
            var base64 = Convert.ToBase64String(input);
            return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static byte[] Base64UrlDecode(string input)
        {
            var output = input.Replace('-', '+').Replace('_', '/');
            switch (output.Length % 4)
            {
                case 2: output += "=="; break;
                case 3: output += "="; break;
            }
            return Convert.FromBase64String(output);
        }
    }
}
