using System;

namespace Sayra.Client.Shared.Models
{
    public class CommandResult
    {
        public Guid CommandId { get; set; }
        public bool Success { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public TimeSpan ExecutionTime { get; set; }
        public string ResultPayload { get; set; } = string.Empty;

        public static CommandResult Successful(Guid commandId, string resultPayload = "")
        {
            return new CommandResult
            {
                CommandId = commandId,
                Success = true,
                ResultPayload = resultPayload
            };
        }

        public static CommandResult Failed(Guid commandId, string errorCode, string errorMessage)
        {
            return new CommandResult
            {
                CommandId = commandId,
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }
    }
}
