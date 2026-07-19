using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Providers
{
    public class OperatingSystemProvider : IOperatingSystemProvider
    {
        private readonly IWmiProvider _wmiProvider;
        private readonly ILogger<OperatingSystemProvider> _logger;

        public string ProviderName => "Operating System Provider";

        public OperatingSystemProvider(IWmiProvider wmiProvider, ILogger<OperatingSystemProvider> logger)
        {
            _wmiProvider = wmiProvider;
            _logger = logger;
        }

        public async Task<ValidationResult> ValidateAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var info = await GetOperatingSystemAsync(cancellationToken);
                if (string.IsNullOrEmpty(info.Edition) || string.IsNullOrEmpty(info.Version))
                {
                    return new ValidationResult(false, new() { "Operating system details are incomplete." }, new());
                }
                return new ValidationResult(true, new(), new());
            }
            catch (Exception ex)
            {
                return new ValidationResult(false, new() { $"OS Provider validation failed: {ex.Message}" }, new());
            }
        }

        public async Task<OperatingSystemInformation> GetOperatingSystemAsync(CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("Operating System Provider started.");

            string version = Environment.OSVersion.Version.ToString();
            string buildNumber = Environment.OSVersion.Version.Build.ToString();
            string edition = RuntimeInformation.OSDescription;
            string directX = "DirectX 12";
            bool vulkan = true;
            string openGl = "4.6";
            string architecture = RuntimeInformation.OSArchitecture.ToString();
            string installDate = "Unknown";
            string bootTime = "Unknown";
            string currentUser = Environment.UserName;
            string computerName = Environment.MachineName;
            string domain = Environment.UserDomainName;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var results = await _wmiProvider.QueryAsync(
                        "SELECT Caption, Version, BuildNumber, OSArchitecture, InstallDate, LastBootUpTime FROM Win32_OperatingSystem",
                        "root\\CIMV2", cancellationToken);

                    if (results.Count > 0)
                    {
                        var dict = results[0];
                        if (dict.TryGetValue("Caption", out var cap) && cap != null) edition = cap.ToString()!.Trim();
                        if (dict.TryGetValue("Version", out var ver) && ver != null) version = ver.ToString()!.Trim();
                        if (dict.TryGetValue("BuildNumber", out var bn) && bn != null) buildNumber = bn.ToString()!.Trim();
                        if (dict.TryGetValue("OSArchitecture", out var arch) && arch != null) architecture = arch.ToString()!.Trim();

                        if (dict.TryGetValue("InstallDate", out var id) && id != null)
                        {
                            string rawId = id.ToString()!;
                            if (rawId.Length >= 8)
                            {
                                installDate = $"{rawId.Substring(4, 2)}/{rawId.Substring(6, 2)}/{rawId.Substring(0, 4)}";
                            }
                        }

                        if (dict.TryGetValue("LastBootUpTime", out var lbt) && lbt != null)
                        {
                            string rawLbt = lbt.ToString()!;
                            if (rawLbt.Length >= 14)
                            {
                                bootTime = $"{rawLbt.Substring(0, 4)}-{rawLbt.Substring(4, 2)}-{rawLbt.Substring(6, 2)} " +
                                           $"{rawLbt.Substring(8, 2)}:{rawLbt.Substring(10, 2)}:{rawLbt.Substring(12, 2)}";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to query OperatingSystem details via WMI.");
                }
            }
            else
            {
                _logger.LogWarning("Non-Windows OS detected. Operating System Provider using fallback details.");
            }

            sw.Stop();
            _logger.LogInformation("Operating System Provider completed in {DurationMs} ms.", sw.ElapsedMilliseconds);

            return new OperatingSystemInformation(
                version,
                buildNumber,
                edition,
                directX,
                vulkan,
                openGl,
                architecture,
                installDate,
                bootTime,
                currentUser,
                computerName,
                domain
            );
        }
    }
}
