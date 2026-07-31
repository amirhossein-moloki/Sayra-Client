using System;

namespace Sayra.Client.Shared.Models.Telemetry.ValueObjects
{
    /// <summary>
    /// Value object representing a globally unique distributed trace identifier.
    /// </summary>
    public record TraceId
    {
        /// <summary>
        /// Gets the raw string representation of the trace identifier.
        /// </summary>
        public string Value { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceId"/> record with a generated unique ID.
        /// </summary>
        public TraceId() : this(Guid.NewGuid().ToString("N"))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TraceId"/> record with a custom identifier string.
        /// </summary>
        /// <param name="value">The custom trace identifier value.</param>
        public TraceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Trace identifier cannot be null or empty.", nameof(value));
            }
            Value = value;
        }

        /// <inheritdoc />
        public override string ToString() => Value;

        /// <summary>
        /// Implicitly converts a TraceId to its underlying string value.
        /// </summary>
        public static implicit operator string(TraceId traceId) => traceId?.Value ?? string.Empty;

        /// <summary>
        /// Explicitly converts a string to a TraceId object.
        /// </summary>
        public static explicit operator TraceId(string value) => new(value);
    }
}
