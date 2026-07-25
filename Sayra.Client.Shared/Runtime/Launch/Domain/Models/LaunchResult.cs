using System;

namespace Sayra.Client.Shared.Runtime.Launch.Domain.Models
{
    public class LaunchResult
    {
        public bool Success { get; set; }
        public int? ProcessId { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
