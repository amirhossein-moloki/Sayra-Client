namespace Sayra.Client.Shared.UpdatePlatform.Domain.Options
{
    /// <summary>
    /// Configuration options for the Diagnostic Reporting and retry mechanics.
    /// </summary>
    public class ReportingOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether diagnostic reporting is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of retry attempts for transmission.
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 5;

        /// <summary>
        /// Gets or sets the base delay in seconds for exponential backoff retries.
        /// </summary>
        public int BaseDelaySeconds { get; set; } = 2;

        /// <summary>
        /// Gets or sets a value indicating whether certificate pinning is enforced.
        /// </summary>
        public bool EnforceCertificatePinning { get; set; } = false;

        /// <summary>
        /// Gets or sets the pinned certificate thumbprint.
        /// </summary>
        public string? PinnedCertificateThumbprint { get; set; }

        /// <summary>
        /// Gets or sets the pinned public key hash.
        /// </summary>
        public string? PinnedPublicKeyHash { get; set; }
    }
}
