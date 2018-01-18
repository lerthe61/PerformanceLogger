namespace PerformanceLogger.Logging.Interfaces
{
    public interface IPerformanceTracker
    {
        // Start tracking performance
        IPerformanceMeasurement Track(string operationName);
    }
}