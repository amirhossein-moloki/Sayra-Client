using System;

namespace Sayra.Client.Shared.Models
{
    public class CommandEnvelope
    {
        public string CommandId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string SenderAdminId { get; set; } = string.Empty;
        public string TargetClientId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Payload { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public DateTime ExpirationTime { get; set; }
        public string Nonce { get; set; } = string.Empty;
    }
}
