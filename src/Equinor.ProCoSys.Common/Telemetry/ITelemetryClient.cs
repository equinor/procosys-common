﻿using System.Collections.Generic;

namespace Equinor.ProCoSys.Common.Telemetry
{
    public interface ITelemetryClient
    {
        void TrackEvent(string name, Dictionary<string, string> properties = null, Dictionary<string, double> metrics = null);

        void TrackMetric(string name, double metric);
        void TrackMetric(string name, double metric, string dimension1Name, string dimension1Value);
        void TrackMetric(string name, double metric, string dimension1Name, string dimension2Name, string dimension1Value, string dimension2Value);
    }
}
