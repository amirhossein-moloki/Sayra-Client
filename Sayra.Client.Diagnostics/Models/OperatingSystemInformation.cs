using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record OperatingSystemInformation
    {
        public string Version { get; init; }
        public string BuildNumber { get; init; }
        public string Edition { get; init; }
        public string DirectXVersion { get; init; }
        public bool VulkanSupport { get; init; }
        public string OpenGlVersion { get; init; }
        public string Architecture { get; init; }
        public string InstallDate { get; init; }
        public string BootTime { get; init; }
        public string CurrentUser { get; init; }
        public string ComputerName { get; init; }
        public string Domain { get; init; }

        public OperatingSystemInformation(
            string version,
            string buildNumber,
            string edition,
            string directXVersion,
            bool vulkanSupport,
            string openGlVersion,
            string architecture = "Unknown",
            string installDate = "Unknown",
            string bootTime = "Unknown",
            string currentUser = "Unknown",
            string computerName = "Unknown",
            string domain = "Unknown")
        {
            Version = version;
            BuildNumber = buildNumber;
            Edition = edition;
            DirectXVersion = directXVersion;
            VulkanSupport = vulkanSupport;
            OpenGlVersion = openGlVersion;
            Architecture = architecture;
            InstallDate = installDate;
            BootTime = bootTime;
            CurrentUser = currentUser;
            ComputerName = computerName;
            Domain = domain;
        }
    }
}
