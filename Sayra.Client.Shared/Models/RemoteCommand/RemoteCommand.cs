using System;

namespace Sayra.Client.Shared.Models
{
    public class RemoteCommand
    {
        public Guid CommandId { get; set; } = Guid.NewGuid();
        public string Action { get; set; } = string.Empty;
        public string SenderAdminId { get; set; } = string.Empty;
        public string TargetClientId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Payload { get; set; } = string.Empty;
        public string Priority { get; set; } = "Normal";
        public CommandStatus Status { get; set; } = CommandStatus.Pending;
        public string Signature { get; set; } = string.Empty;
        public DateTime ExpirationTime { get; set; } = DateTime.UtcNow.AddMinutes(5);
        public string Nonce { get; set; } = string.Empty;
    }
}
