namespace Sayra.Client.Shared.Interfaces.Security
{
    public interface IMessageAuthenticator
    {
        byte[] ComputeHmac(byte[] data, byte[] key);
        bool ValidateHmac(byte[] data, byte[] hmac, byte[] key);
        bool ValidateHmac(string data, string hmacBase64, string keyBase64);
    }
}
