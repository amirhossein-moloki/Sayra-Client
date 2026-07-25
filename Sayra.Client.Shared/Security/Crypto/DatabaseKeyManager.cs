using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Security.Crypto.KeyManagement;

namespace Sayra.Client.Shared.Security.Crypto
{
    public static class DatabaseKeyManager
    {
        private static readonly byte[] Entropy = new byte[] { 0x47, 0x82, 0x19, 0xC3, 0xD4, 0x51, 0xFE, 0x2A };
        private static readonly SessionKeyProvider KeyProvider = new SessionKeyProvider();
        private static readonly object LockObj = new object();

        public static string GetOrInitializeKey(ICryptographyService? cryptographyService)
        {
            lock (LockObj)
            {
                // Check if the key is already loaded in our secure memory provider
                var existingKey = KeyProvider.GetSessionKeyBytes();
                if (existingKey != null)
                {
                    try
                    {
                        return Convert.ToHexString(existingKey);
                    }
                    finally
                    {
                        Array.Clear(existingKey, 0, existingKey.Length);
                    }
                }

                byte[] rawKey;

                if (OperatingSystem.IsWindows())
                {
                    var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
                    if (!Directory.Exists(dataDir))
                    {
                        Directory.CreateDirectory(dataDir);
                    }

                    var keyPath = Path.Combine(dataDir, "db_key.bin");

                    if (File.Exists(keyPath))
                    {
                        try
                        {
                            var protectedBytes = File.ReadAllBytes(keyPath);
                            // Reuse ICryptographyService for DPAPI decryption
                            if (cryptographyService != null)
                            {
                                rawKey = cryptographyService.DecryptWithDpapi(protectedBytes, useMachineStore: true, optionalEntropy: Entropy);
                            }
                            else
                            {
                                rawKey = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.LocalMachine);
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new CryptographicException("CRITICAL: Failed to decrypt database master key via DPAPI. System is locking down.", ex);
                        }
                    }
                    else
                    {
                        // Generate a strong cryptographically secure 256-bit key
                        rawKey = new byte[32];
                        RandomNumberGenerator.Fill(rawKey);

                        try
                        {
                            byte[] protectedBytes;
                            if (cryptographyService != null)
                            {
                                protectedBytes = cryptographyService.EncryptWithDpapi(rawKey, useMachineStore: true, optionalEntropy: Entropy);
                            }
                            else
                            {
                                protectedBytes = ProtectedData.Protect(rawKey, Entropy, DataProtectionScope.LocalMachine);
                            }

                            File.WriteAllBytes(keyPath, protectedBytes);
                        }
                        catch (Exception ex)
                        {
                            throw new CryptographicException("CRITICAL: Failed to protect database master key via DPAPI.", ex);
                        }
                    }
                }
                else
                {
                    // Non-Windows (tests/CI runners): deterministic mockup key
                    rawKey = Convert.FromHexString("A1B2C3D4E5F67890A1B2C3D4E5F67890A1B2C3D4E5F67890A1B2C3D4E5F67890");
                }

                try
                {
                    // Load the key into SessionKeyProvider to keep it in protected memory!
                    KeyProvider.LoadSessionKey(rawKey);
                }
                finally
                {
                    // Zero out managed memory array immediately
                    Array.Clear(rawKey, 0, rawKey.Length);
                }

                // Retrieve key as hex string from SessionKeyProvider
                var keyBytes = KeyProvider.GetSessionKeyBytes();
                if (keyBytes == null)
                {
                    throw new CryptographicException("CRITICAL: Failed to retrieve secure database key from SessionKeyProvider.");
                }

                try
                {
                    return Convert.ToHexString(keyBytes);
                }
                finally
                {
                    Array.Clear(keyBytes, 0, keyBytes.Length);
                }
            }
        }
    }
}
