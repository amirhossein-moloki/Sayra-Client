namespace Sayra.Client.LocalAdmin.Security
{
    public interface IPasswordHasher
    {
        string HashPassword(string password, string salt, int iterations);
        bool VerifyPassword(string password, string salt, int iterations, string hash);
        string GenerateSalt();
    }
}
