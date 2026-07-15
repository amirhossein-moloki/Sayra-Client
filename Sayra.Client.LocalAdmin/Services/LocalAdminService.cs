using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.LocalAdmin.Authentication;
using Sayra.Client.LocalAdmin.Models;
using Sayra.Client.LocalAdmin.Security;
using Sayra.Client.LocalAdmin.Storage;

namespace Sayra.Client.LocalAdmin.Services
{
    public class LocalAdminService : ILocalAdminService
    {
        private readonly ILocalAdminRepository _repository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IAdminSessionManager _sessionManager;
        private readonly ILogger<LocalAdminService>? _logger;

        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 5;
        private const int DefaultIterations = 350000;

        public LocalAdminService(
            ILocalAdminRepository repository,
            IPasswordHasher passwordHasher,
            IAdminSessionManager sessionManager,
            ILogger<LocalAdminService>? logger = null)
        {
            _repository = repository;
            _passwordHasher = passwordHasher;
            _sessionManager = sessionManager;
            _logger = logger;
        }

        public async Task InitializeAdmin()
        {
            var credentials = (await _repository.LoadCredentialsAsync()).ToList();
            if (!credentials.Any())
            {
                _logger?.LogWarning("No administrator credentials found. Initializing secure default admin account.");
                // Create secure default admin "admin" / "Admin123!"
                await CreateAdminInternal("admin", "Admin123!", credentials);
            }
        }

        public async Task<bool> CreateAdmin(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty.", nameof(username));

            if (!ValidatePasswordStrength(password))
                throw new ArgumentException("Password does not meet complexity requirements.", nameof(password));

            var credentials = (await _repository.LoadCredentialsAsync()).ToList();
            string normalizedUsername = username.Trim().ToLowerInvariant();

            if (credentials.Any(c => c.Username.Equals(normalizedUsername, StringComparison.OrdinalIgnoreCase)))
            {
                _logger?.LogWarning("Attempt to create duplicate admin account: {Username}", username);
                return false;
            }

            await CreateAdminInternal(username, password, credentials);
            _logger?.LogInformation("Admin account created successfully: {Username}", username);
            return true;
        }

        private async Task CreateAdminInternal(string username, string password, List<LocalAdminCredential> existingList)
        {
            string salt = _passwordHasher.GenerateSalt();
            string hash = _passwordHasher.HashPassword(password, salt, DefaultIterations);

            var credential = new LocalAdminCredential
            {
                Id = Guid.NewGuid().ToString(),
                Username = username.Trim().ToLowerInvariant(),
                PasswordHash = hash,
                Salt = salt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                FailedAttempts = 0,
                IsLocked = false
            };

            existingList.Add(credential);
            await _repository.SaveCredentialsAsync(existingList);
        }

        public async Task<AdminAuthenticationResult> Authenticate(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                return new AdminAuthenticationResult
                {
                    Success = false,
                    ErrorReason = "Username and password are required."
                };
            }

            var credentials = (await _repository.LoadCredentialsAsync()).ToList();
            string normalizedUsername = username.Trim().ToLowerInvariant();

            var credential = credentials.FirstOrDefault(c => c.Username.Equals(normalizedUsername, StringComparison.OrdinalIgnoreCase));
            if (credential == null)
            {
                _logger?.LogWarning("Authentication failed: User {Username} not found.", username);
                return new AdminAuthenticationResult
                {
                    Success = false,
                    ErrorReason = "Invalid credentials."
                };
            }

            // Lockout check
            if (credential.IsLocked)
            {
                if (credential.LockedUntil.HasValue && credential.LockedUntil.Value <= DateTime.UtcNow)
                {
                    // Lockout period has passed, unlock the user
                    credential.IsLocked = false;
                    credential.LockedUntil = null;
                    credential.FailedAttempts = 0;
                    credential.UpdatedAt = DateTime.UtcNow;
                    await _repository.SaveCredentialsAsync(credentials);
                    _logger?.LogInformation("Lockout period expired. Account unlocked for user {Username}.", username);
                }
                else
                {
                    _logger?.LogWarning("Authentication rejected: Account {Username} is currently locked.", username);
                    return new AdminAuthenticationResult
                    {
                        Success = false,
                        ErrorReason = "Account is temporarily locked due to too many failed attempts. Try again later."
                    };
                }
            }

            bool passwordCorrect = _passwordHasher.VerifyPassword(password, credential.Salt, DefaultIterations, credential.PasswordHash);
            if (passwordCorrect)
            {
                credential.FailedAttempts = 0;
                credential.IsLocked = false;
                credential.LockedUntil = null;
                credential.LastLoginAt = DateTime.UtcNow;
                credential.UpdatedAt = DateTime.UtcNow;

                await _repository.SaveCredentialsAsync(credentials);

                string sessionToken = _sessionManager.CreateSession(credential.Username);
                _logger?.LogInformation("Security Alert: Admin user {Username} successfully authenticated from local client.", username);

                return new AdminAuthenticationResult
                {
                    Success = true,
                    SessionToken = sessionToken
                };
            }
            else
            {
                credential.FailedAttempts++;
                if (credential.FailedAttempts >= MaxFailedAttempts)
                {
                    credential.IsLocked = true;
                    credential.LockedUntil = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                    _logger?.LogCritical("SECURITY ALERT: Admin account {Username} has been locked for {LockoutMinutes} minutes due to {MaxFailedAttempts} consecutive failed attempts.", username, LockoutMinutes, MaxFailedAttempts);
                }
                else
                {
                    _logger?.LogWarning("Failed login attempt for admin {Username} (Attempt {FailedAttempts}/{MaxFailedAttempts}).", username, credential.FailedAttempts, MaxFailedAttempts);
                }

                credential.UpdatedAt = DateTime.UtcNow;
                await _repository.SaveCredentialsAsync(credentials);

                return new AdminAuthenticationResult
                {
                    Success = false,
                    ErrorReason = "Invalid credentials."
                };
            }
        }

        public async Task<bool> ChangePassword(string username, string oldPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty.", nameof(username));

            if (!ValidatePasswordStrength(newPassword))
                throw new ArgumentException("New password does not meet complexity requirements.", nameof(newPassword));

            var credentials = (await _repository.LoadCredentialsAsync()).ToList();
            string normalizedUsername = username.Trim().ToLowerInvariant();

            var credential = credentials.FirstOrDefault(c => c.Username.Equals(normalizedUsername, StringComparison.OrdinalIgnoreCase));
            if (credential == null)
            {
                throw new InvalidOperationException("User not found.");
            }

            bool oldPasswordCorrect = _passwordHasher.VerifyPassword(oldPassword, credential.Salt, DefaultIterations, credential.PasswordHash);
            if (!oldPasswordCorrect)
            {
                _logger?.LogWarning("Password change failed: Incorrect current password for user {Username}.", username);
                return false;
            }

            string salt = _passwordHasher.GenerateSalt();
            string hash = _passwordHasher.HashPassword(newPassword, salt, DefaultIterations);

            credential.PasswordHash = hash;
            credential.Salt = salt;
            credential.UpdatedAt = DateTime.UtcNow;

            await _repository.SaveCredentialsAsync(credentials);
            _logger?.LogInformation("Password updated successfully for admin user {Username}.", username);
            return true;
        }

        public async Task<bool> DeleteAdmin(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            var credentials = (await _repository.LoadCredentialsAsync()).ToList();
            string normalizedUsername = username.Trim().ToLowerInvariant();

            var credential = credentials.FirstOrDefault(c => c.Username.Equals(normalizedUsername, StringComparison.OrdinalIgnoreCase));
            if (credential == null)
            {
                return false;
            }

            credentials.Remove(credential);
            await _repository.SaveCredentialsAsync(credentials);
            _logger?.LogInformation("Admin account deleted: {Username}", username);
            return true;
        }

        public bool ValidatePasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password)) return false;
            if (password.Length < 8) return false;

            bool hasUpper = false;
            bool hasLower = false;
            bool hasDigit = false;
            bool hasSpecial = false;

            foreach (char c in password)
            {
                if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsLower(c)) hasLower = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else if (!char.IsLetterOrDigit(c)) hasSpecial = true;
            }

            return hasUpper && hasLower && hasDigit && hasSpecial;
        }
    }
}
