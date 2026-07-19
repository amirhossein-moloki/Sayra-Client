using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record OperatingSystemInformation(
        string Version,
        string BuildNumber,
        string Edition,
        string DirectXVersion,
        bool VulkanSupport,
        string OpenGlVersion
    );
}
