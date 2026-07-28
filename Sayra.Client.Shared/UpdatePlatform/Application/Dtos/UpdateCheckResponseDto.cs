using System;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Dtos
{
    /// <summary>
    /// Data Transfer Object representing the server's check-for-update evaluation response.
    /// </summary>
    public class UpdateCheckResponseDto
    {
        /// <summary>
        /// Gets or sets a value indicating whether a newer update is available for installation.
        /// </summary>
        public bool UpdateAvailable { get; set; }

        /// <summary>
        /// Gets or sets the target update manifest details, if available.
        /// </summary>
        public UpdateManifestDto? Manifest { get; set; }
    }
}
