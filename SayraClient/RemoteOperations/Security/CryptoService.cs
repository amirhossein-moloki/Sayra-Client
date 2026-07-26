using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Sayra.Client.Shared.Interfaces.Security;

namespace SayraClient.RemoteOperations.Security
{
    public class CryptoService : ICryptoService
    {
        public byte[] Encrypt(byte[] plaintext, byte[] key, byte[] iv)
        {
            if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));
            if (key == null || key.Length != 32) throw new ArgumentException("AES key must be 32 bytes (256 bits).", nameof(key));
            if (iv == null || iv.Length != 16) throw new ArgumentException("AES IV must be 16 bytes (128 bits).", nameof(iv));

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(plaintext, 0, plaintext.Length);
                cs.FlushFinalBlock();
            }
            return ms.ToArray();
        }

        public byte[] Decrypt(byte[] ciphertext, byte[] key, byte[] iv)
        {
            if (ciphertext == null) throw new ArgumentNullException(nameof(ciphertext));
            if (key == null || key.Length != 32) throw new ArgumentException("AES key must be 32 bytes (256 bits).", nameof(key));
            if (iv == null || iv.Length != 16) throw new ArgumentException("AES IV must be 16 bytes (128 bits).", nameof(iv));

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
            {
                cs.Write(ciphertext, 0, ciphertext.Length);
                cs.FlushFinalBlock();
            }
            return ms.ToArray();
        }

        public string EncryptString(string plaintext, string base64Key, string base64Iv)
        {
            if (plaintext == null) throw new ArgumentNullException(nameof(plaintext));
            byte[] key = Convert.FromBase64String(base64Key);
            byte[] iv = Convert.FromBase64String(base64Iv);
            byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] cipherBytes = Encrypt(plainBytes, key, iv);
            return Convert.ToBase64String(cipherBytes);
        }

        public string DecryptString(string ciphertextBase64, string base64Key, string base64Iv)
        {
            if (ciphertextBase64 == null) throw new ArgumentNullException(nameof(ciphertextBase64));
            byte[] key = Convert.FromBase64String(base64Key);
            byte[] iv = Convert.FromBase64String(base64Iv);
            byte[] cipherBytes = Convert.FromBase64String(ciphertextBase64);
            byte[] plainBytes = Decrypt(cipherBytes, key, iv);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
