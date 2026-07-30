using System;
using System.Collections.Generic;
using Sayra.Client.Shared.Models.Recovery;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    /// <summary>
    /// Contract for validating resilience configurations against schema and range rules.
    /// </summary>
    public interface IConfigurationValidator
    {
        /// <summary>
        /// Validates the entire resilience configuration.
        /// </summary>
        /// <param name="configuration">The configuration to validate.</param>
        /// <returns>A validation result containing errors if validation failed.</returns>
        ConfigurationValidationResult Validate(ResilienceConfiguration configuration);
    }

    /// <summary>
    /// Represents the results of a resilience configuration validation run.
    /// </summary>
    public class ConfigurationValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the configuration passed validation successfully.
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// Gets the list of validation failure error messages.
        /// </summary>
        public List<string> Errors { get; } = new();

        /// <summary>
        /// Adds a validation error to the result.
        /// </summary>
        /// <param name="error">The error message.</param>
        public void AddError(string error)
        {
            Errors.Add(error);
        }
    }
}
