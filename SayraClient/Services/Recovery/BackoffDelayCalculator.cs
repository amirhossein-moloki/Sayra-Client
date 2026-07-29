using System;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery
{
    public class BackoffDelayCalculator
    {
        private readonly Random _random = new();

        public TimeSpan CalculateDelay(int attempt, RetryPolicy policy)
        {
            if (attempt <= 0) return TimeSpan.Zero;

            double baseSeconds = policy.InitialDelay.TotalSeconds;
            double calculatedSeconds;

            switch (policy.BackoffStrategy)
            {
                case BackoffStrategy.Constant:
                    calculatedSeconds = baseSeconds;
                    break;

                case BackoffStrategy.Linear:
                    calculatedSeconds = baseSeconds * attempt;
                    break;

                case BackoffStrategy.Exponential:
                    calculatedSeconds = baseSeconds * Math.Pow(2, attempt - 1);
                    break;

                case BackoffStrategy.ExponentialWithJitter:
                    double exponential = baseSeconds * Math.Pow(2, attempt - 1);
                    // Add standard randomized jitter (between 50% and 150% of the calculated value)
                    double jitter = (0.5 + _random.NextDouble()) * exponential;
                    calculatedSeconds = jitter;
                    break;

                default:
                    calculatedSeconds = baseSeconds;
                    break;
            }

            // Cap the delay at the policy's MaxDelay
            double maxSeconds = policy.MaxDelay.TotalSeconds;
            if (calculatedSeconds > maxSeconds)
            {
                calculatedSeconds = maxSeconds;
            }

            return TimeSpan.FromSeconds(calculatedSeconds);
        }
    }
}
