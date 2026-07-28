using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Implements high-robustness cryptographic signature verification over arrays and streams,
    /// supporting both ECDsa-P384 and RSA public keys.
    /// </summary>
    public class SignatureVerifier : ISignatureVerifier
    {
        /// <inheritdoc />
        public Task<bool> VerifySignatureAsync(byte[] data, string signature, string publicKeyPem, CancellationToken cancellationToken = default)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrWhiteSpace(signature)) throw new InvalidSignatureException("Signature cannot be empty.");
            if (string.IsNullOrWhiteSpace(publicKeyPem)) throw new InvalidSignatureException("Public key cannot be empty.");

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                byte[] signatureBytes = ConvertSignatureToBytes(signature);

                // Check key type and verify
                if (publicKeyPem.Contains("RSA PUBLIC KEY") || publicKeyPem.Contains("BEGIN PUBLIC KEY"))
                {
                    try
                    {
                        using (var rsa = RSA.Create())
                        {
                            rsa.ImportFromPem(publicKeyPem);
                            bool isValid = rsa.VerifyData(data, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                            if (!isValid)
                            {
                                // Fallback to PSS padding
                                isValid = rsa.VerifyData(data, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
                            }
                            return Task.FromResult(isValid);
                        }
                    }
                    catch (CryptographicException) when (!publicKeyPem.Contains("RSA PUBLIC KEY"))
                    {
                        // If it fails and might be ECDsa, fall through
                    }
                }

                // ECDsa Verification
                using (var ecdsa = ECDsa.Create())
                {
                    ecdsa.ImportFromPem(publicKeyPem);
                    using (var sha256 = SHA256.Create())
                    {
                        byte[] hash = sha256.ComputeHash(data);
                        bool isValid = ecdsa.VerifyHash(hash, signatureBytes);
                        return Task.FromResult(isValid);
                    }
                }
            }
            catch (CryptographicException ex)
            {
                throw new InvalidSignatureException($"Cryptographic error during verification: {ex.Message}", ex);
            }
            catch (FormatException ex)
            {
                throw new InvalidSignatureException($"Invalid signature format (expected Base64 or Hex): {ex.Message}", ex);
            }
        }

        /// <inheritdoc />
        public async Task<bool> VerifyStreamSignatureAsync(Stream dataStream, string signature, string publicKeyPem, CancellationToken cancellationToken = default)
        {
            if (dataStream == null) throw new ArgumentNullException(nameof(dataStream));
            if (string.IsNullOrWhiteSpace(signature)) throw new InvalidSignatureException("Signature cannot be empty.");
            if (string.IsNullOrWhiteSpace(publicKeyPem)) throw new InvalidSignatureException("Public key cannot be empty.");

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                byte[] signatureBytes = ConvertSignatureToBytes(signature);

                // Compute SHA-256 hash in a streaming fashion
                byte[] hash;
                using (var sha256 = SHA256.Create())
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await dataStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                    }
                    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    hash = sha256.Hash ?? throw new InvalidSignatureException("Failed to compute SHA-256 hash of stream.");
                }

                // Check key type and verify
                if (publicKeyPem.Contains("RSA PUBLIC KEY") || publicKeyPem.Contains("BEGIN PUBLIC KEY"))
                {
                    try
                    {
                        using (var rsa = RSA.Create())
                        {
                            rsa.ImportFromPem(publicKeyPem);
                            bool isValid = rsa.VerifyHash(hash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                            if (!isValid)
                            {
                                isValid = rsa.VerifyHash(hash, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
                            }
                            return isValid;
                        }
                    }
                    catch (CryptographicException) when (!publicKeyPem.Contains("RSA PUBLIC KEY"))
                    {
                        // Fall through
                    }
                }

                using (var ecdsa = ECDsa.Create())
                {
                    ecdsa.ImportFromPem(publicKeyPem);
                    bool isValid = ecdsa.VerifyHash(hash, signatureBytes);
                    return isValid;
                }
            }
            catch (CryptographicException ex)
            {
                throw new InvalidSignatureException($"Cryptographic error during stream verification: {ex.Message}", ex);
            }
            catch (FormatException ex)
            {
                throw new InvalidSignatureException($"Invalid stream signature format (expected Base64 or Hex): {ex.Message}", ex);
            }
        }

        private byte[] ConvertSignatureToBytes(string signature)
        {
            signature = signature.Trim();

            try
            {
                return Convert.FromBase64String(signature);
            }
            catch (FormatException)
            {
                return HexStringToByteArray(signature);
            }
        }

        private byte[] HexStringToByteArray(string hex)
        {
            int numberChars = hex.Length;
            if (numberChars % 2 != 0)
            {
                throw new FormatException("Hex signature must have an even number of characters.");
            }

            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
    }
}
