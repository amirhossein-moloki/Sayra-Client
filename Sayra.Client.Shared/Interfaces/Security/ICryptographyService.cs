using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Security;

/// <summary>
/// Governs local hardware-bound credential encryption and secret storage envelopes.
/// </summary>
public interface ICryptographyService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertextBase64);
    Task<string> EncryptAsync(string plaintext);
    Task<string> DecryptAsync(string ciphertextBase64);
    byte[] GenerateKey(int sizeInBytes);
    bool ValidateSignature(string data, string signature, string publicKeyPem);
    string CreateHash(string data);

    byte[] EncryptWithDpapi(byte[] plaintext, bool useMachineStore, byte[]? optionalEntropy = null);
    byte[] DecryptWithDpapi(byte[] ciphertext, bool useMachineStore, byte[]? optionalEntropy = null);
    byte[] EncryptAesGcm(byte[] plaintext, byte[] key, byte[] nonce, byte[] associatedData);
    byte[] DecryptAesGcm(byte[] ciphertext, byte[] key, byte[] nonce, byte[] associatedData);
    bool VerifySignature(byte[] data, byte[] signature, byte[] publicKey);

    // Centralized Hashing & Signature additions
    byte[] ComputeHash(byte[] data, string algorithmName);
    byte[] ComputeHmacSha256(byte[] data, byte[] key);
    byte[] CreateSignature(byte[] data, byte[] privateKey);
}
