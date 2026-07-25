using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Runtime.Domain.Entities
{
    /// <summary>
    /// Metadata describing environmental and system configurations for the runtime.
    /// </summary>
    public class RuntimeMetadata
    {
        public string Version { get; set; } = "1.0.0";
        public string EnvironmentName { get; set; } = "Production";
        public string WorkstationId { get; set; } = string.Empty;
        public Dictionary<string, string> Attributes { get; set; } = new();
    }
}
