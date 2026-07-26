using System;
using System.Security.Cryptography;
using System.Text;
using Sayra.Client.Shared.Interfaces.Security;

namespace SayraClient.RemoteOperations.Security
{
    public class MessageAuthenticator : IMessageAuthenticator
    {
        public byte[] ComputeHmac(byte[] data, byte[] key)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (key == null) throw new ArgumentNullException(nameof(key));

            using var hmac = new HMACSHA256(key);
            return hmac.ComputeHash(data);
        }

        public bool ValidateHmac(byte[] data, byte[] hmac, byte[] key)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (hmac == null) throw new ArgumentNullException(nameof(hmac));
            if (key == null) throw new ArgumentNullException(nameof(key));

            byte[] computed = ComputeHmac(data, key);
            return CryptographicOperations.FixedTimeEquals(computed, hmac);
        }

        public bool ValidateHmac(string data, string hmacBase64, string keyBase64)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (hmacBase64 == null) throw new ArgumentNullException(nameof(hmacBase64));
            if (keyBase64 == null) throw new ArgumentNullException(nameof(keyBase64));

            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] hmacBytes = Convert.FromBase64String(hmacBase64);
            byte[] keyBytes = Convert.FromBase64String(keyBase64);

            return ValidateHmac(dataBytes, hmacBytes, keyBytes);
        }
    }
}
