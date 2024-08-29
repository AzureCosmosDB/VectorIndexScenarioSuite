using System;
using System.Collections.Generic;
using MathNet.Numerics.Statistics;

namespace VectorIndexScenarioSuite
{
    internal class PercentileStats
    {
        public double Min { get; set; }
        public double P50 { get; set; }
        public double P95 { get; set; }
        public double P99 { get; set; }
        public double Max { get; set; }
        public double Avg { get; set; }

        public override string ToString()
        {
            return $"Min: {Min}, P50: {P50}, P95: {P95}, P99: {P99}, Max: {Max}, Avg: {Avg}";
        }
    }

    internal class ScenarioMetrics
    {
        private List<double> requestUnits;

        private List<double> clientLatenciesInMs;

        private List<double> serverLatenciesInMs;

        private object lockObject = new object();

        public ScenarioMetrics(int totalNumberOfRequests)
        {
            this.requestUnits = new List<double>(totalNumberOfRequests);
            this.clientLatenciesInMs = new List<double>(totalNumberOfRequests);
            this.serverLatenciesInMs = new List<double>(totalNumberOfRequests);
        }

        public void AddClientLatencyMeasurement(double latency)
        {
            lock (lockObject)
            {
                this.clientLatenciesInMs.Add(latency);
            }
        }

        public void AddServerLatencyMeasurement(double latency)
        {
            lock (lockObject)
            {
                this.serverLatenciesInMs.Add(latency);
            }
        }

        public void AddRequestUnitMeasurement(double requestUnit)
        {
            lock (lockObject)
            {
                this.requestUnits.Add(requestUnit);
            }
        }

        public PercentileStats GetClientLatencyStatistics()
        {
            lock (lockObject)
            {
                return CalculatePercentiles(this.clientLatenciesInMs);
            }
        }

        public PercentileStats GetServerLatencyStatistics()
        {
            lock (lockObject)
            {
                return CalculatePercentiles(this.serverLatenciesInMs);
            }
        }

        public PercentileStats GetRequestUnitStatistics()
        {
            lock (lockObject)
            {
                return CalculatePercentiles(this.requestUnits);
            }
        }

        private PercentileStats CalculatePercentiles(List<double> measurements)
        {
            if (measurements == null || measurements.Count == 0)
            {
                return new PercentileStats();
            }

            var descriptiveStatistics = new DescriptiveStatistics(measurements);

            double min = descriptiveStatistics.Minimum;
            double max = descriptiveStatistics.Maximum;
            double p50 = Statistics.Percentile(measurements, 50);
            double p95 = Statistics.Percentile(measurements, 95);
            double p99 = Statistics.Percentile(measurements, 99);
            double avg = descriptiveStatistics.Mean;

            return new PercentileStats()
            {
                Min = min,
                P50 = p50,
                P95 = p95,
                P99 = p99,
                Max = max,
                Avg = avg
            };
        }
    }
}
