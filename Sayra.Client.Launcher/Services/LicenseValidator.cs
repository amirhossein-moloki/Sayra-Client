using System;

namespace Sayra.Client.Launcher.Validation
{
    public class LicenseValidator : ILicenseValidator
    {
        public bool IsLicenseValid(string gameId)
        {
            // Simple production default: allow all games unless blacklisted/invalid
            return !string.IsNullOrWhiteSpace(gameId);
        }
    }
}
