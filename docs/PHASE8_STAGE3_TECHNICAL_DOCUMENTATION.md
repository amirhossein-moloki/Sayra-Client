# Phase 8 — Stage 3: Enterprise Metrics Engine Technical Documentation

## 1. Metrics Architecture

The **Enterprise Metrics Engine** is the core processing component of the SAYRA workstation observability platform. It is responsible for transforming high-frequency raw telemetry records into structured, mathematically validated, and downsampled multi-window metric series.

```
+-------------------+      +-------------------+      +-------------------------+
|  Telemetry Engine | ---> | TelemetryPipeline | ---> |    MetricsAggregator    |
|     (Stage 2)     |      |  (Channel Reader) |      | (Validation & Windowing)|
+-------------------+      +-------------------+      +-------------------------+
                                                                   |
                                                                   v
                                                      +-------------------------+
                                                      |  Pluggable Aggregators  |
                                                      |   (Counters, Gauges,    |
                                                      |   Histograms, Timers,   |
                                                      |         Rates)          |
                                                      +-------------------------+
                                                                   |
                                                                   v
                                                      +-------------------------+
                                                      |  Post-Aggregation Steps |
                                                      | (EMA, Downsampling, etc)|
                                                      +-------------------------+
                                                                   |
                                                                   v
                                                      +-------------------------+
                                                      |   MetricSeries Store    |
                                                      +-------------------------+
```

### Key Subsystems:
1. **Mathematical Engine (`MetricsMath`)**: Performs numerically stable statistical algorithms on raw sequences of data, including Min, Max, Sum, Avg, Variance, StdDev, Percentiles (P50, P90, P95, P99), Rolling Average, and Exponential Moving Average (EMA).
2. **Validation Framework (`MetricValidator`)**: Protects the engine against dirty inputs by enforcing metric naming standards, value ranges, valid measurement units, realistic timestamps, and automatic duplicate rejection.
3. **Downsampling Framework (`MetricDownsampler`)**: Implements dynamic downsampling strategies to consolidate high-frequency data into larger historical windows.
4. **Aggregation Engine (`MetricsAggregator`)**: Implements `IMetricsAggregator` to orchestrate non-blocking background queue draining, window partitioning, strategy dispatch, and memory-safe historical caching.

---

## 2. Aggregation Pipeline & Lifecycle

The aggregation cycle is executed asynchronously to ensure that workstations never suffer from CPU or thread-pool starvation:

1. **Draining**: The aggregator drains raw records from the `TelemetryPipeline.Reader` channel. Draining is non-blocking (`TryRead`) and runs at $O(1)$ amortized cost per record.
2. **Filtering & Validation**: Drained raw records are pushed through the `MetricValidator`. Records with invalid formatting, out-of-bounds values (e.g. CPU > 100%), or duplicate timestamps are discarded gracefully with structured logs.
3. **Time-Window Grouping**: Validated records are bucketed into deterministic time windows. For each configured window duration $W$ (e.g. 5s, 60s, 3600s), a record's timestamp is rounded down to the nearest multiple:
   $$\text{BucketStart} = \left\lfloor \frac{\text{TimestampSeconds}}{W} \right\rfloor \times W$$
4. **Strategy Execution**: The aggregator resolves the appropriate `IMetricAggregatorStrategy` based on the metric's name suffix/type (Counters, Gauges, Histograms, Timers, Rates).
5. **Post-Processing**:
   - **Moving Averages**: If `EnableMovingAverages` is active, the engine automatically calculates the rolling simple moving average (SMA) and exponential moving average (EMA) across historical periods, appending them as tags.
   - **Downsampling**: If the window size exceeds the default window, the engine consolidates the points using the configured strategy (e.g., Average).
6. **Storage**: Consolidated `MetricPoint`s are appended to the thread-safe `_aggregatedStore` dictionary with history limits (capped at 1000 points per series) to prevent memory leaks.

---

## 3. Window Management & Downsampling

### Window Management
All aggregation windows are completely driven by `MetricsOptions` configuration to avoid hardcoded constants:
- **Supported default intervals**: 5 seconds, 15 seconds, 30 seconds, 1 minute, 5 minutes, 15 minutes, 1 hour.
- Buckets are determined using UTC Unix epoch division, guaranteeing consistent grouping across client restarts or workstation clock drifts.

### Downsampling Strategies
To support long-term visualization and minimize network/storage overhead, high-frequency metrics can be downsampled using the following configurable strategies:
- **Average**: Computes the arithmetic mean of points. Recommended for continuous sensor data like CPU and memory usage.
- **Maximum**: Captures the peak value in the period. Useful for capturing spikes, disk latencies, or CPU bursts.
- **Minimum**: Captures the valley value. Useful for tracing free memory dips or system throughput drops.
- **Sum**: Accumulates values in the window. Recommended for count metrics (e.g., total login events).
- **Last Value**: Resolves the newest recorded point in the window. Useful for state-based gauges.

---

## 4. Statistical Algorithms

To guarantee absolute numerical stability under intense telemetry load, all algorithms are heavily optimized:

### Variance & Standard Deviation
Calculates the sample variance ($n - 1$ divisor) to avoid bias in small sample sets:
$$s^2 = \frac{\sum_{i=1}^{n} (x_i - \bar{x})^2}{n - 1}$$
Standard Deviation is computed as the square root of the variance:
$$\sigma = \sqrt{s^2}$$

### High-Precision Percentiles
Percentiles (P50, P90, P95, P99) are computed using **linear interpolation** between the closest ranks:
1. Sort values in ascending order.
2. Calculate the fractional rank: $R = \frac{P}{100} \times (n - 1)$.
3. Let $I = \lfloor R \rfloor$ and $J = \lceil R \rceil$.
4. Return: $x_I + (R - I) \times (x_J - x_I)$.

### Exponential Moving Average (EMA)
Computes the exponential decay average, giving more weight to recent observations:
$$EMA_0 = x_0$$
$$EMA_t = \alpha \times x_t + (1 - \alpha) \times EMA_{t-1}$$
where $\alpha = 0.2$ provides smooth trend lines for workstation performance.

---

## 5. Extension Points

The engine is built around the **Open-Closed Principle**, allowing developers to extend behavior without modifying core code:

1. **Custom Aggregator Strategies**: Implement the `IMetricAggregatorStrategy` interface and register it in Dependency Injection. The `MetricsAggregator` will automatically discover and integrate it:
   ```csharp
   public class MyCustomStrategy : IMetricAggregatorStrategy {
       public AggregationType Type => AggregationType.Percentile;
       public MetricPoint Aggregate(...) { ... }
   }
   ```
2. **New Validation Rules**: Extend `MetricValidator.cs` to insert enterprise-specific validation boundaries.
3. **Custom Downsamplers**: Integrate new downsampling algorithms inside `MetricDownsampler.cs` (e.g., Median or Rate-adjusted).

---

## 6. Integration Architecture

While this stage is strictly focused on metrics aggregation, it is architected to seamlessly interface with adjacent platform stages:

### Telemetry Engine Integration (Stage 2)
The Metrics Engine actively drains the `TelemetryPipeline.Reader` channel of the Telemetry Engine. As collectors write raw `TelemetryRecord`s to the pipeline, they are asynchronously processed, validated, and placed in the aggregator's buffer.

### Dashboard Provider Integration (Stage 9)
The `IMetricsAggregator` exposes aggregated series via `GetAggregatedSeriesAsync`. The Dashboard Provider (Stage 9) consumes this API to render live, real-time interactive performance charts, CPU trends, and alert indicators directly in the Administration Panel.

### Historical Metrics Integration (Stage 8)
When the aggregator finishes an aggregation cycle, the generated downsampled points (e.g. 5-minute, hourly windows) can be dispatched to the `IHistoricalMetricsService` (Stage 8). The historical metrics service writes these consolidated data points to the SQLCipher-encrypted SQLite database using standard retention and archiving policies.

---
*Developed by the SAYRA Enterprise Observability & Performance Analytics Group.*
