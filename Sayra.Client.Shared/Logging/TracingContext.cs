using System.Threading;

namespace Sayra.Client.Shared.Logging
{
    public static class TracingContext
    {
        private static readonly AsyncLocal<string?> _correlationId = new();
        private static readonly AsyncLocal<string?> _traceId = new();

        public static string? CorrelationId
        {
            get => _correlationId.Value;
            set => _correlationId.Value = value;
        }

        public static string? TraceId
        {
            get => _traceId.Value;
            set => _traceId.Value = value;
        }
    }
}
