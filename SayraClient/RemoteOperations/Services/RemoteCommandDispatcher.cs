using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class RemoteCommandDispatcher : IRemoteCommandDispatcher
    {
        private readonly IEnumerable<IRemoteCommandHandler> _handlers;
        private readonly ICryptoService _cryptoService;
        private readonly ISignatureVerifier _signatureVerifier;
        private readonly IMessageAuthenticator _messageAuthenticator;
        private readonly IAuditLogger _auditLogger;
        private readonly ILogger<RemoteCommandDispatcher> _logger;

        private readonly ConcurrentDictionary<string, byte> _processedNonces = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<Guid, byte> _processedCommandIds = new();
        private readonly string _publicKeyPem = string.Empty;

        public RemoteCommandDispatcher(
            IEnumerable<IRemoteCommandHandler> handlers,
            ICryptoService cryptoService,
            ISignatureVerifier signatureVerifier,
            IMessageAuthenticator messageAuthenticator,
            IAuditLogger auditLogger,
            ILogger<RemoteCommandDispatcher> logger)
        {
            _handlers = handlers;
            _cryptoService = cryptoService;
            _signatureVerifier = signatureVerifier;
            _messageAuthenticator = messageAuthenticator;
            _auditLogger = auditLogger;
            _logger = logger;

            // Load RSA Public Key
            try
            {
                var keyPath = Path.Combine(AppContext.BaseDirectory, "server_public.key");
                if (File.Exists(keyPath))
                {
                    _publicKeyPem = File.ReadAllText(keyPath).Trim();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load server_public.key from disk.");
            }
        }

        // Exposed for testing/configuration overrides
        public string PublicKeyPem { get; set; } = string.Empty;

        private string EffectivePublicKey => !string.IsNullOrEmpty(PublicKeyPem) ? PublicKeyPem : _publicKeyPem;

        public async Task<CommandResult> DispatchAsync(RemoteCommand command, CancellationToken cancellationToken)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            _logger.LogInformation("[RemoteCommandDispatcher] Dispatching command {CommandId} ({Action})", command.CommandId, command.Action);

            // 1. Check duplicate CommandId (Step 6 addition)
            if (!_processedCommandIds.TryAdd(command.CommandId, 0))
            {
                var msg = $"Duplicate command ID detected: {command.CommandId}";
                _auditLogger.LogSecurity($"[Remote Command Security] Rejected command {command.CommandId}: REPLAY_ATTACK (Command ID already seen).");
                return CommandResult.Failed(command.CommandId, "REPLAY_ATTACK", msg);
            }

            // 2. Prevent replay attacks on Nonce (Step 6)
            if (!string.IsNullOrEmpty(command.Nonce))
            {
                if (!_processedNonces.TryAdd(command.Nonce, 0))
                {
                    var msg = $"Duplicate nonce detected: {command.Nonce}";
                    _auditLogger.LogSecurity($"[Remote Command Security] Rejected command {command.CommandId}: REPLAY_ATTACK (Nonce already used).");
                    return CommandResult.Failed(command.CommandId, "REPLAY_ATTACK", msg);
                }
            }

            // 3. Check timestamp expiration (Step 5)
            var now = DateTime.UtcNow;
            if (command.ExpirationTime < now)
            {
                var msg = $"Command expired. ExpirationTime: {command.ExpirationTime:O}, CurrentTime: {now:O}";
                _auditLogger.LogSecurity($"[Remote Command Security] Rejected command {command.CommandId}: EXPIRED (Command expired).");
                return CommandResult.Failed(command.CommandId, "EXPIRED", msg);
            }

            if (Math.Abs((now - command.Timestamp).TotalSeconds) > 300)
            {
                var msg = $"Command timestamp is skewed. CommandTime: {command.Timestamp:O}, CurrentTime: {now:O}";
                _auditLogger.LogSecurity($"[Remote Command Security] Rejected command {command.CommandId}: EXPIRED (Timestamp skew too high).");
                return CommandResult.Failed(command.CommandId, "EXPIRED", msg);
            }

            // 4. Verify RSA Signature (Step 4)
            if (!string.IsNullOrEmpty(command.Signature) && !string.IsNullOrEmpty(EffectivePublicKey))
            {
                // Canonical signature data string: "CommandId:Action:SenderAdminId:Timestamp:Payload:Nonce"
                string canonicalData = $"{command.CommandId}:{command.Action}:{command.SenderAdminId}:{command.Timestamp:O}:{command.Payload}:{command.Nonce}";
                bool isSignatureValid = _signatureVerifier.VerifySignature(canonicalData, command.Signature, EffectivePublicKey);
                if (!isSignatureValid)
                {
                    _auditLogger.LogSecurity($"[Remote Command Security] Rejected command {command.CommandId}: INVALID_SIGNATURE (RSA verification failed).");
                    return CommandResult.Failed(command.CommandId, "INVALID_SIGNATURE", "RSA signature verification failed.");
                }
            }
            else if (string.IsNullOrEmpty(command.Signature))
            {
                _auditLogger.LogSecurity($"[Remote Command Security] Rejected command {command.CommandId}: INVALID_SIGNATURE (Signature is empty).");
                return CommandResult.Failed(command.CommandId, "INVALID_SIGNATURE", "RSA signature is missing.");
            }

            // 5. Select handler and execute
            var handler = _handlers.FirstOrDefault(h => h.CanHandle(command.Action));
            if (handler == null)
            {
                _logger.LogWarning("No handler found for action: {Action}", command.Action);
                _auditLogger.LogSecurity($"[Remote operations] Command {command.CommandId} rejected: Handler not found for {command.Action}.");
                return CommandResult.Failed(command.CommandId, "UNKNOWN_ACTION", $"No handler found for action: {command.Action}");
            }

            // Allow Execution (Step 7)
            _logger.LogInformation("[RemoteCommandDispatcher] Routing {CommandId} ({Action}) to {HandlerType}",
                command.CommandId, command.Action, handler.GetType().Name);

            try
            {
                var result = await handler.HandleAsync(command, cancellationToken);
                return result;
            }
            catch (NotImplementedException ex)
            {
                _logger.LogWarning(ex, "Handler threw NotImplementedException for {Action}", command.Action);
                return CommandResult.Failed(command.CommandId, "NOT_IMPLEMENTED", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing handler for {Action}", command.Action);
                return CommandResult.Failed(command.CommandId, "HANDLER_ERROR", ex.Message);
            }
        }

        /// <summary>
        /// Executes the complete 7-step pipeline from encrypted frame to execution.
        /// </summary>
        public async Task<CommandResult> DispatchSecureFrameAsync(SecureMessageFrame frame, byte[] aesKey, byte[] hmacKey, CancellationToken cancellationToken)
        {
            // Step 1: Receive encrypted message
            if (frame == null)
            {
                _auditLogger.LogSecurity("[Remote Command Security] Receive step failed: Frame is null.");
                return CommandResult.Failed(Guid.Empty, "RECEIVE_FAILED", "Message frame is null.");
            }

            // Step 3: Validate HMAC (Encrypt-then-MAC)
            bool isHmacValid = _messageAuthenticator.ValidateHmac(frame.EncryptedPayload, frame.Hmac, hmacKey);
            if (!isHmacValid)
            {
                _auditLogger.LogSecurity("[Remote Command Security] Integrity check failed: Invalid HMAC signature.");
                return CommandResult.Failed(Guid.Empty, "INVALID_HMAC", "HMAC verification failed. The ciphertext integrity has been compromised.");
            }

            // Step 2: Decrypt AES payload
            string decryptedJson;
            try
            {
                byte[] iv = new byte[16];
                byte[] plainBytes = _cryptoService.Decrypt(frame.EncryptedPayload, aesKey, iv);
                decryptedJson = Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                _auditLogger.LogSecurity($"[Remote Command Security] Decryption step failed: {ex.Message}");
                return CommandResult.Failed(Guid.Empty, "DECRYPTION_FAILED", $"Decryption failed: {ex.Message}");
            }

            // Parse Envelope
            CommandEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<CommandEnvelope>(decryptedJson);
            }
            catch (Exception ex)
            {
                _auditLogger.LogSecurity($"[Remote Command Security] JSON Parsing failed: {ex.Message}");
                return CommandResult.Failed(Guid.Empty, "MALFORMED_PAYLOAD", "Decrypted payload JSON is malformed.");
            }

            if (envelope == null)
            {
                _auditLogger.LogSecurity("[Remote Command Security] Validation failed: Decrypted envelope is null.");
                return CommandResult.Failed(Guid.Empty, "MALFORMED_PAYLOAD", "Decrypted envelope is null.");
            }

            // Map Envelope to RemoteCommand
            if (!Guid.TryParse(envelope.CommandId, out Guid commandId))
            {
                commandId = Guid.NewGuid();
            }

            var remoteCommand = new RemoteCommand
            {
                CommandId = commandId,
                Action = envelope.Action,
                SenderAdminId = envelope.SenderAdminId,
                TargetClientId = envelope.TargetClientId,
                Timestamp = envelope.Timestamp,
                Payload = envelope.Payload,
                Priority = envelope.Priority,
                Signature = envelope.Signature,
                ExpirationTime = envelope.ExpirationTime,
                Nonce = envelope.Nonce
            };

            // Step 4 to 7: Verify signature, expiration, nonces, and execute
            return await DispatchAsync(remoteCommand, cancellationToken);
        }
    }
}
