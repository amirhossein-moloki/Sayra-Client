using System;
using System.Security.Cryptography;
using System.Text;
using Sayra.Client.Shared.Interfaces.Security;

namespace SayraClient.RemoteOperations.Security
{
    public class SignatureVerifier : ISignatureVerifier
    {
        public bool VerifySignature(byte[] data, byte[] signature, byte[] publicKey)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (signature == null) throw new ArgumentNullException(nameof(signature));
            if (publicKey == null) throw new ArgumentNullException(nameof(publicKey));

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(publicKey, out _);
                return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch
            {
                try
                {
                    // Fallback to importing PEM string if the byte array was PEM formatted
                    string pem = Encoding.UTF8.GetString(publicKey);
                    using var rsaPem = RSA.Create();
                    rsaPem.ImportFromPem(pem);
                    return rsaPem.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
                catch
                {
                    return false;
                }
            }
        }

        public bool VerifySignature(string data, string signatureBase64, string publicKeyPemOrHex)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (signatureBase64 == null) throw new ArgumentNullException(nameof(signatureBase64));
            if (publicKeyPemOrHex == null) throw new ArgumentNullException(nameof(publicKeyPemOrHex));

            try
            {
                byte[] dataBytes = Encoding.UTF8.GetBytes(data);
                byte[] signatureBytes = Convert.FromBase64String(signatureBase64);

                using var rsa = RSA.Create();
                if (publicKeyPemOrHex.Contains("-----BEGIN"))
                {
                    rsa.ImportFromPem(publicKeyPemOrHex.Trim());
                }
                else
                {
                    // Assume it's a Hex or Base64 key
                    try
                    {
                        byte[] keyBytes = Convert.FromHexString(publicKeyPemOrHex);
                        rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
                    }
                    catch
                    {
                        byte[] keyBytes = Convert.FromBase64String(publicKeyPemOrHex);
                        rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
                    }
                }

                return rsa.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch
            {
                return false;
            }
        }
    }
}
