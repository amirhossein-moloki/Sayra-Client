using System;
using System.Collections.Generic;
using System.Linq;

namespace Sayra.Client.Shared.Telemetry.Metrics
{
    /// <summary>
    /// Thread-safe, highly optimized mathematical utility for executing enterprise-grade
    /// statistical and signal-processing calculations on metric telemetry.
    /// </summary>
    public static class MetricsMath
    {
        /// <summary>
        /// Calculates the minimum value in a sequence. Returns 0 if sequence is empty.
        /// </summary>
        public static double CalculateMin(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0) return 0.0;
            double min = double.MaxValue;
            for (int i = 0; i < values.Count; i++)
            {
                double v = values[i];
                if (!double.IsNaN(v) && v < min)
                {
                    min = v;
                }
            }
            return min == double.MaxValue ? 0.0 : min;
        }

        /// <summary>
        /// Calculates the maximum value in a sequence. Returns 0 if sequence is empty.
        /// </summary>
        public static double CalculateMax(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0) return 0.0;
            double max = double.MinValue;
            for (int i = 0; i < values.Count; i++)
            {
                double v = values[i];
                if (!double.IsNaN(v) && v > max)
                {
                    max = v;
                }
            }
            return max == double.MinValue ? 0.0 : max;
        }

        /// <summary>
        /// Calculates the sum of values in a sequence.
        /// </summary>
        public static double CalculateSum(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0) return 0.0;
            double sum = 0.0;
            for (int i = 0; i < values.Count; i++)
            {
                double v = values[i];
                if (!double.IsNaN(v))
                {
                    sum += v;
                }
            }
            return sum;
        }

        /// <summary>
        /// Calculates the arithmetic mean of a sequence. Returns 0 if sequence is empty.
        /// </summary>
        public static double CalculateAverage(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0) return 0.0;
            double sum = 0.0;
            int count = 0;
            for (int i = 0; i < values.Count; i++)
            {
                double v = values[i];
                if (!double.IsNaN(v))
                {
                    sum += v;
                    count++;
                }
            }
            return count > 0 ? sum / count : 0.0;
        }

        /// <summary>
        /// Calculates the population or sample variance of a sequence.
        /// </summary>
        public static double CalculateVariance(IReadOnlyList<double> values, bool isSample = true)
        {
            if (values == null || values.Count == 0) return 0.0;

            // Filter out NaNs
            var cleanValues = new List<double>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                double v = values[i];
                if (!double.IsNaN(v))
                {
                    cleanValues.Add(v);
                }
            }

            int n = cleanValues.Count;
            if (n == 0) return 0.0;
            if (isSample && n <= 1) return 0.0;

            double avg = cleanValues.Average();
            double sumOfSquares = 0.0;
            for (int i = 0; i < n; i++)
            {
                double diff = cleanValues[i] - avg;
                sumOfSquares += diff * diff;
            }

            int divisor = isSample ? n - 1 : n;
            return sumOfSquares / divisor;
        }

        /// <summary>
        /// Calculates the standard deviation of a sequence. Returns 0 if sequence is empty.
        /// </summary>
        public static double CalculateStandardDeviation(IReadOnlyList<double> values, bool isSample = true)
        {
            double variance = CalculateVariance(values, isSample);
            return Math.Sqrt(variance);
        }

        /// <summary>
        /// Calculates the specified percentile (0 to 100) using linear interpolation.
        /// </summary>
        public static double CalculatePercentile(IReadOnlyList<double> values, double percentile)
        {
            if (values == null || values.Count == 0) return 0.0;
            if (percentile < 0.0 || percentile > 100.0)
            {
                throw new ArgumentOutOfRangeException(nameof(percentile), "Percentile must be between 0 and 100.");
            }

            var cleanValues = new List<double>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                double v = values[i];
                if (!double.IsNaN(v))
                {
                    cleanValues.Add(v);
                }
            }

            if (cleanValues.Count == 0) return 0.0;
            if (cleanValues.Count == 1) return cleanValues[0];

            // Sort ascending
            cleanValues.Sort();

            double r = (percentile / 100.0) * (cleanValues.Count - 1);
            int idxLower = (int)Math.Floor(r);
            int idxUpper = (int)Math.Ceiling(r);

            if (idxLower == idxUpper)
            {
                return cleanValues[idxLower];
            }

            double valLower = cleanValues[idxLower];
            double valUpper = cleanValues[idxUpper];
            return valLower + (r - idxLower) * (valUpper - valLower);
        }

        /// <summary>
        /// Computes a list of rolling averages with a configurable window size.
        /// </summary>
        public static IReadOnlyList<double> CalculateRollingAverages(IReadOnlyList<double> values, int windowSize)
        {
            if (values == null || values.Count == 0) return Array.Empty<double>();
            if (windowSize <= 0) throw new ArgumentOutOfRangeException(nameof(windowSize), "Window size must be greater than zero.");

            double[] result = new double[values.Count];
            double runningSum = 0.0;
            int count = 0;

            for (int i = 0; i < values.Count; i++)
            {
                double v = values[i];
                if (!double.IsNaN(v))
                {
                    runningSum += v;
                    count++;
                }

                if (i >= windowSize)
                {
                    double oldVal = values[i - windowSize];
                    if (!double.IsNaN(oldVal))
                    {
                        runningSum -= oldVal;
                        count--;
                    }
                }

                result[i] = count > 0 ? runningSum / count : 0.0;
            }

            return result;
        }

        /// <summary>
        /// Computes the exponential moving average (EMA) sequence.
        /// </summary>
        public static IReadOnlyList<double> CalculateExponentialMovingAverages(IReadOnlyList<double> values, double alpha)
        {
            if (values == null || values.Count == 0) return Array.Empty<double>();
            if (alpha <= 0.0 || alpha > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha smoothing factor must be in range (0.0, 1.0].");
            }

            double[] result = new double[values.Count];
            bool firstAssigned = false;
            double currentEma = 0.0;

            for (int i = 0; i < values.Count; i++)
            {
                double v = values[i];
                if (double.IsNaN(v))
                {
                    result[i] = firstAssigned ? currentEma : 0.0;
                    continue;
                }

                if (!firstAssigned)
                {
                    currentEma = v;
                    firstAssigned = true;
                }
                else
                {
                    currentEma = (alpha * v) + ((1.0 - alpha) * currentEma);
                }

                result[i] = currentEma;
            }

            return result;
        }
    }
}
