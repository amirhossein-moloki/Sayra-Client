using System;
using System.Security.Cryptography;

namespace Sayra.Client.LocalAdmin.Security
{
    public class PasswordHasher : IPasswordHasher
    {
        private const int KeySize = 32; // 256 bits

        public string HashPassword(string password, string salt, int iterations)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));
            if (string.IsNullOrEmpty(salt))
                throw new ArgumentNullException(nameof(salt));

            byte[] saltBytes = Convert.FromBase64String(salt);
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, iterations, HashAlgorithmName.SHA256))
            {
                byte[] hashBytes = pbkdf2.GetBytes(KeySize);
                return Convert.ToBase64String(hashBytes);
            }
        }

        public bool VerifyPassword(string password, string salt, int iterations, string hash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(hash))
                return false;

            try
            {
                string computedHash = HashPassword(password, salt, iterations);
                byte[] computedBytes = Convert.FromBase64String(computedHash);
                byte[] hashBytes = Convert.FromBase64String(hash);

                return CryptographicOperations.FixedTimeEquals(computedBytes, hashBytes);
            }
            catch
            {
                return false;
            }
        }

        public string GenerateSalt()
        {
            byte[] saltBytes = RandomNumberGenerator.GetBytes(KeySize);
            return Convert.ToBase64String(saltBytes);
        }
    }
}
