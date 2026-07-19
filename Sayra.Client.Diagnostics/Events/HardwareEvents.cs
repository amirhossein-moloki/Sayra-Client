using System;
using System.Collections.Generic;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Events
{
    public class HardwareInitializedEventArgs : EventArgs
    {
        public HardwareSpecification Specification { get; }

        public HardwareInitializedEventArgs(HardwareSpecification specification)
        {
            Specification = specification;
        }
    }

    public class HardwareMetricsUpdatedEventArgs : EventArgs
    {
        public HardwareMetrics Metrics { get; }

        public HardwareMetricsUpdatedEventArgs(HardwareMetrics metrics)
        {
            Metrics = metrics;
        }
    }

    public class HardwareValidationFailedEventArgs : EventArgs
    {
        public ValidationResult Result { get; }

        public HardwareValidationFailedEventArgs(ValidationResult result)
        {
            Result = result;
        }
    }

    public class HardwareChangedEventArgs : EventArgs
    {
        public HardwareSpecification OldSpecification { get; }
        public HardwareSpecification NewSpecification { get; }

        public HardwareChangedEventArgs(HardwareSpecification oldSpecification, HardwareSpecification newSpecification)
        {
            OldSpecification = oldSpecification;
            NewSpecification = newSpecification;
        }
    }

    public class DisplayChangedEventArgs : EventArgs
    {
        public List<DisplayInformation> OldDisplays { get; }
        public List<DisplayInformation> NewDisplays { get; }

        public DisplayChangedEventArgs(List<DisplayInformation> oldDisplays, List<DisplayInformation> newDisplays)
        {
            OldDisplays = oldDisplays;
            NewDisplays = newDisplays;
        }
    }

    public class NetworkChangedEventArgs : EventArgs
    {
        public List<NetworkInformation> OldNetworks { get; }
        public List<NetworkInformation> NewNetworks { get; }

        public NetworkChangedEventArgs(List<NetworkInformation> oldNetworks, List<NetworkInformation> newNetworks)
        {
            OldNetworks = oldNetworks;
            NewNetworks = newNetworks;
        }
    }

    public class TelemetryStartedEventArgs : EventArgs
    {
        public DateTime StartTime { get; }

        public TelemetryStartedEventArgs(DateTime startTime)
        {
            StartTime = startTime;
        }
    }

    public class TelemetryStoppedEventArgs : EventArgs
    {
        public DateTime StopTime { get; }

        public TelemetryStoppedEventArgs(DateTime stopTime)
        {
            StopTime = stopTime;
        }
    }
}
