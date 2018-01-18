using System;

namespace PerformanceLogger.Logging.Interfaces
{
    public interface IPerformanceMeasurement : IDisposable
    {
        // Create child measurement with separated scope
        IPerformanceMeasurement TrackChild(string operationName);

        // Add custom value to current measurement
        void AddValue(string name, string units, long value);

        // Add key-value information
        void AddKeyValue(string key, string value);
        void AddKeyValue(string key, bool value);
    }
}