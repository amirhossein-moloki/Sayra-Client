using System;

namespace Sayra.Client.Shared.Models.Telemetry.ValueObjects
{
    /// <summary>
    /// Value object representing a logical operation correlation identifier.
    /// </summary>
    public record CorrelationId
    {
        /// <summary>
        /// Gets the raw string representation of the correlation identifier.
        /// </summary>
        public string Value { get; init; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationId"/> record with a generated unique ID.
        /// </summary>
        public CorrelationId() : this(Guid.NewGuid().ToString("D"))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CorrelationId"/> record with a custom identifier string.
        /// </summary>
        /// <param name="value">The custom correlation identifier value.</param>
        public CorrelationId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Correlation identifier cannot be null or empty.", nameof(value));
            }
            Value = value;
        }

        /// <inheritdoc />
        public override string ToString() => Value;

        /// <summary>
        /// Implicitly converts a CorrelationId to its underlying string value.
        /// </summary>
        public static implicit operator string(CorrelationId correlationId) => correlationId?.Value ?? string.Empty;

        /// <summary>
        /// Explicitly converts a string to a CorrelationId object.
        /// </summary>
        public static explicit operator CorrelationId(string value) => new(value);
    }
}
