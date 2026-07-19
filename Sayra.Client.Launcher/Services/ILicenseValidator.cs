using System;

namespace Sayra.Client.Launcher.Validation
{
    public interface ILicenseValidator
    {
        bool IsLicenseValid(string gameId);
    }
}
