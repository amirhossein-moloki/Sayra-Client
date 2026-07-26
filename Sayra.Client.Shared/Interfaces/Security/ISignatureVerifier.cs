namespace Sayra.Client.Shared.Interfaces.Security
{
    public interface ISignatureVerifier
    {
        bool VerifySignature(byte[] data, byte[] signature, byte[] publicKey);
        bool VerifySignature(string data, string signatureBase64, string publicKeyPemOrHex);
    }
}
