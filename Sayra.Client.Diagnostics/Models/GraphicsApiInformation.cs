using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record GraphicsApiInformation(
        string DirectXVersion,
        string VulkanVersion,
        string OpenGlVersion,
        string SupportStatus
    );
}
