using System.Security.Authentication;
using Microsoft.Extensions.Configuration;

namespace SayraClient.Security.Transport;

public class TransportPolicy
{
    public SslProtocols MinimumTlsVersion { get; } = SslProtocols.Tls13;
    public bool EnforceCertificatePinning { get; } = true;
    public string? PinnedCertificateThumbprint { get; }
    public string? PinnedPublicKeyHash { get; }
    public bool BypassLocalTrustStore { get; } = true;
    public bool AllowSelfSignedCertificates { get; } = false;
    public int HandshakeTimeoutSeconds { get; } = 5;

    public int ReconnectBaseDelaySeconds { get; } = 2;
    public int ReconnectMaxDelaySeconds { get; } = 60;
    public int ReconnectMaxAttempts { get; } = 5;

    public TransportPolicy(IConfiguration configuration)
    {
        var section = configuration.GetSection("TransportSecurity");
        if (section.Exists())
        {
            var tlsVerStr = section["MinimumTlsVersion"];
            if (!string.IsNullOrEmpty(tlsVerStr))
            {
                if (tlsVerStr.Equals("Tls13", StringComparison.OrdinalIgnoreCase))
                {
                    MinimumTlsVersion = SslProtocols.Tls13;
                }
                else
                {
                    MinimumTlsVersion = SslProtocols.Tls13;
                }
            }

            EnforceCertificatePinning = section.GetValue<bool>("EnforceCertificatePinning", true);
            PinnedCertificateThumbprint = section["PinnedCertificateThumbprint"];
            PinnedPublicKeyHash = section["PinnedPublicKeyHash"];
            BypassLocalTrustStore = section.GetValue<bool>("BypassLocalTrustStore", true);
            AllowSelfSignedCertificates = section.GetValue<bool>("AllowSelfSignedCertificates", false);
            HandshakeTimeoutSeconds = section.GetValue<int>("HandshakeTimeoutSeconds", 5);

            var reconnectSection = section.GetSection("ReconnectPolicy");
            if (reconnectSection.Exists())
            {
                ReconnectBaseDelaySeconds = reconnectSection.GetValue<int>("BaseDelaySeconds", 2);
                ReconnectMaxDelaySeconds = reconnectSection.GetValue<int>("MaxDelaySeconds", 60);
                ReconnectMaxAttempts = reconnectSection.GetValue<int>("MaxAttempts", 5);
            }
        }
        else
        {
            var serverConfig = configuration.GetSection("ServerConfig");
            if (serverConfig.Exists())
            {
                ReconnectBaseDelaySeconds = serverConfig.GetValue<int>("ReconnectIntervalSeconds", 2);
                ReconnectMaxAttempts = serverConfig.GetValue<int>("MaxReconnectAttempts", 5);
                if (ReconnectMaxAttempts < 0) ReconnectMaxAttempts = int.MaxValue;
            }
        }
    }
}
