namespace Sayra.Client.OfflineQueue.Security;

public interface IQueueSecurityManager
{
    string EncryptPayload(string plaintext);
    string DecryptPayload(string ciphertext);
    string GenerateSignature(string payload);
    bool VerifySignature(string payload, string signature);
}
