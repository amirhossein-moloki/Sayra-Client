using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SayraClient.Security.Transport;

public class TlsConnectionManager
{
    private readonly ILogger<TlsConnectionManager> _logger;
    private readonly TransportPolicy _policy;
    private readonly object _sessionLock = new();
    private TcpClient? _currentTcpClient;

    // Secure transport session states
    public Guid? SessionId { get; private set; }
    public DateTime? SessionCreatedTime { get; private set; }
    public DateTime? SessionExpirationTime { get; private set; }
    public bool IsSessionActive => SessionId.HasValue && SessionExpirationTime.HasValue && DateTime.UtcNow < SessionExpirationTime.Value;

    public TlsConnectionManager(ILogger<TlsConnectionManager> logger, TransportPolicy policy)
    {
        _logger = logger;
        _policy = policy;
    }

    /// <summary>
    /// Establishes a secure TLS 1.3 connection to the server.
    /// </summary>
    public async Task<SslStream> ConnectAsync(string ip, int port, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Establishing TCP connection to {Ip}:{Port}", ip, port);
            _currentTcpClient = new TcpClient();

            // Connect with timeout
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(5));

            await _currentTcpClient.ConnectAsync(ip, port, connectCts.Token);

            _logger.LogInformation("TCP connection established. Commencing TLS handshake...");

            var networkStream = _currentTcpClient.GetStream();
            var sslStream = new SslStream(networkStream, false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null);

            var sslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = ip,
                EnabledSslProtocols = SslProtocols.Tls13, // Force TLS 1.3
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                EncryptionPolicy = EncryptionPolicy.RequireEncryption
            };

            // TLS Handshake with explicit timeout
            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            handshakeCts.CancelAfter(TimeSpan.FromSeconds(_policy.HandshakeTimeoutSeconds));

            try
            {
                await sslStream.AuthenticateAsClientAsync(sslOptions, handshakeCts.Token);
            }
            catch (OperationCanceledException) when (handshakeCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError("TLS handshake timed out after {Seconds} seconds.", _policy.HandshakeTimeoutSeconds);
                throw new TimeoutException("TLS handshake timed out.");
            }

            _logger.LogInformation("TLS handshake completed successfully. Protocol: {Protocol}, Cipher: {Cipher}",
                sslStream.SslProtocol, sslStream.CipherAlgorithm);

            if (sslStream.SslProtocol != SslProtocols.Tls13)
            {
                _logger.LogCritical("TLS version 1.3 was not negotiated! Active protocol: {Protocol}. Aborting.", sslStream.SslProtocol);
                sslStream.Dispose();
                _currentTcpClient.Dispose();
                _currentTcpClient = null;
                throw new AuthenticationException("Insecure protocol negotiated. Connection aborted.");
            }

            // Create a secure transport session
            CreateSession(TimeSpan.FromHours(1));

            return sslStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish a secure transport connection.");
            _currentTcpClient?.Dispose();
            _currentTcpClient = null;
            // Wrap in generic secure connection exception to prevent exposing certificate details or handshake internals
            throw new Exception("Secure connection establishment failed.");
        }
    }

    /// <summary>
    /// Custom certificate validation including certificate pinning.
    /// </summary>
    public bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (certificate == null)
        {
            _logger.LogError("Certificate validation failed: No certificate presented by server.");
            return false;
        }

        var cert2 = certificate as X509Certificate2 ?? new X509Certificate2(certificate);

        // 1. Validate Expiration & Validity Period
        var now = DateTime.UtcNow;
        if (now < cert2.NotBefore.ToUniversalTime() || now > cert2.NotAfter.ToUniversalTime())
        {
            _logger.LogError("Certificate validation failed: Certificate is expired or not yet valid. Validity: {NotBefore} to {NotAfter}",
                cert2.NotBefore, cert2.NotAfter);
            return false;
        }

        // 2. Validate Hostname Match
        if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            _logger.LogError("Certificate validation failed: Hostname mismatch.");
            return false;
        }

        string subject = cert2.Subject;
        _logger.LogDebug("Validating certificate subject: {Subject}", subject);

        // 3. Chain & Issuer validation
        if (!_policy.AllowSelfSignedCertificates && sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
        {
            if (!_policy.BypassLocalTrustStore)
            {
                _logger.LogError("Certificate chain validation failed: {Errors}", sslPolicyErrors);
                return false;
            }
        }

        // 4. Certificate Pinning Validation
        if (_policy.EnforceCertificatePinning)
        {
            bool pinningMatch = false;

            // Pin by Thumbprint
            if (!string.IsNullOrEmpty(_policy.PinnedCertificateThumbprint))
            {
                string cleanPinnedThumbprint = _policy.PinnedCertificateThumbprint.Replace(":", "").Replace(" ", "").ToUpperInvariant();
                string certThumbprint = cert2.Thumbprint.ToUpperInvariant();
                if (certThumbprint == cleanPinnedThumbprint)
                {
                    _logger.LogInformation("Certificate pinning matched by thumbprint.");
                    pinningMatch = true;
                }
            }

            // Pin by Public Key SHA-256 Hash
            if (!pinningMatch && !string.IsNullOrEmpty(_policy.PinnedPublicKeyHash))
            {
                byte[] publicKeyBytes = cert2.GetPublicKey();
                using var sha256 = SHA256.Create();
                byte[] hashBytes = sha256.ComputeHash(publicKeyBytes);
                string calculatedHashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                string cleanPinnedHash = _policy.PinnedPublicKeyHash.ToLowerInvariant();

                if (calculatedHashHex == cleanPinnedHash)
                {
                    _logger.LogInformation("Certificate pinning matched by public key hash.");
                    pinningMatch = true;
                }
            }

            if (!pinningMatch)
            {
                _logger.LogCritical("Certificate pinning validation failed! The presented certificate is NOT pinned.");
                return false;
            }
        }

        _logger.LogInformation("Certificate validation succeeded.");
        return true;
    }

    #region Secure Transport Session Management

    public void CreateSession(TimeSpan lifetime)
    {
        lock (_sessionLock)
        {
            SessionId = Guid.NewGuid();
            SessionCreatedTime = DateTime.UtcNow;
            SessionExpirationTime = DateTime.UtcNow.Add(lifetime);
            _logger.LogInformation("Secure transport session created: {SessionId}. Expiration: {Expiration}", SessionId, SessionExpirationTime);
        }
    }

    public void RenewSession(TimeSpan extraLifetime)
    {
        lock (_sessionLock)
        {
            if (SessionId.HasValue)
            {
                SessionExpirationTime = DateTime.UtcNow.Add(extraLifetime);
                _logger.LogDebug("Secure transport session renewed. New Expiration: {Expiration}", SessionExpirationTime);
            }
        }
    }

    public void ExpireSession()
    {
        lock (_sessionLock)
        {
            if (SessionId.HasValue)
            {
                _logger.LogInformation("Expiring secure transport session: {SessionId}", SessionId);
                SessionExpirationTime = DateTime.UtcNow.AddSeconds(-1);
            }
        }
    }

    public void CleanupSession()
    {
        lock (_sessionLock)
        {
            if (SessionId.HasValue)
            {
                _logger.LogInformation("Cleaning up secure transport session: {SessionId}", SessionId);
                SessionId = null;
                SessionCreatedTime = null;
                SessionExpirationTime = null;
            }
            _currentTcpClient?.Dispose();
            _currentTcpClient = null;
        }
    }

    #endregion
}
