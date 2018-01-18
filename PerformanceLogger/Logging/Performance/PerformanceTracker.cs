using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using HTTPDataCollectorAPI;
using PerformanceLogger.Logging.Interfaces;

namespace PerformanceLogger.Logging.Performance
{
    public sealed class PerformanceTracker : IPerformanceTracker
    {
        private readonly ICollector _collector;
        private readonly string _typeName;
        private readonly Func<bool> _isLogEnabled;

        /// <summary>
        /// Constructor and entry point for whole performance logging.
        /// </summary>
        /// <param name="collector">Collectore who now how to send information to Azure LogAnalytics</param>
        /// <param name="typeName">Exposed TypeName that will be used to identify logs in LogAnalytics</param>
        /// <param name="isLogEnabled">Strategy that allow enable\disable logging. With current implementation
        ///  it is not possible to disable (or enable) logging after new instance is constructed. This allow
        ///  to maintain some integrity inside LogAnalytics. 
        /// </param>
        public PerformanceTracker(ICollector collector, string typeName, Func<bool> isLogEnabled)
        {
            _collector = collector;
            _typeName = typeName;
            _isLogEnabled = isLogEnabled;
        }

        public IPerformanceMeasurement Track(string operationName)
        {
            if (!_isLogEnabled())
                return new StubMeasurement();
            return new PerformanceMeasurement(operationName, _typeName, _collector);
        }

        private class PerformanceMeasurement : IPerformanceMeasurement
        {
            private readonly string _operationName;
            private readonly PerformanceMeasurement _parent;
            private readonly string _typeName;
            private readonly ICollector _collector;
            private readonly Guid _operationId;
            private readonly Stopwatch _stopWatch;
            private readonly List<Tuple<string, string, long>> _longMeasurements;
            private readonly IDictionary<string, string> _keyStringValues;
            private readonly IDictionary<string, bool> _keyBooleanValues;
            private readonly List<string> _records;

            private PerformanceMeasurement(string operationName)
            {
                _operationName = operationName;
                _operationId = Guid.NewGuid();
                _records = new List<string>();
                _longMeasurements = new List<Tuple<string, string, long>>();
                _stopWatch = new Stopwatch();
                _keyStringValues = new Dictionary<string, string>();
                _keyBooleanValues = new Dictionary<string, bool>();
                _stopWatch.Start();
            }

            public PerformanceMeasurement(string operationName, string typeName, ICollector collector) : this(operationName)
            {
                _parent = null;
                _typeName = typeName;
                _collector = collector;
            }

            private PerformanceMeasurement(string operationName, PerformanceMeasurement parent) : this(operationName)
            {
                _parent = parent;
            }

            private Guid OperationId
            {
                get { return _operationId; }
            }

            public IPerformanceMeasurement TrackChild(string operationName)
            {
                return new PerformanceMeasurement(operationName, this);
            }

            public void AddValue(string name, string units, long value)
            {
                _longMeasurements.Add(new Tuple<string, string, long>(name, units, value));
            }

            public void AddKeyValue(string key, string value)
            {
                _keyStringValues.Add(key, value);
            }

            public void AddKeyValue(string key, bool value)
            {
                _keyBooleanValues.Add(key, value);
            }

            private void AddRecord(string json)
            {
                _records.Add(json);
            }

            public void Dispose()
            {
                _stopWatch.Stop();
                AddValue("Elapsed", "ms", _stopWatch.ElapsedMilliseconds);

                AddRecord(GenerateJSON());
                var recordBatch = String.Join(",", _records);

                if (_parent == null)
                {
                    _collector.Collect(_typeName, $"[{ recordBatch }]");
                }
                else
                {
                    _parent.AddRecord(recordBatch); //send JSON records to parent
                }
            }

            private string GenerateJSON()
            {
                var sb = new StringBuilder();
                sb.Append("{");

                AddHeaders(sb);
                AddMeasurements(sb);

                // remove trailing comma
                if (sb[sb.Length - 1] == ',')
                    sb.Remove(sb.Length - 1, 1);

                sb.Append("}");
                return sb.ToString();
            }

            private void AddMeasurements(StringBuilder sb)
            {
                foreach (var tuple in _longMeasurements)
                {
                    Add(sb, $"{tuple.Item1}", tuple.Item3);
                    Add(sb, $"{tuple.Item1}_unit", tuple.Item2);
                }
                foreach (var keyValue in _keyStringValues)
                {
                    Add(sb, $"{keyValue.Key}", keyValue.Value);
                }
                foreach (var keyValue in _keyBooleanValues)
                {
                    Add(sb, $"{keyValue.Key}", keyValue.Value);
                }
            }

            private void Add(StringBuilder sb, string name, Guid value)
            {
                AddEscaped(sb, name, value);
            }

            private void Add(StringBuilder sb, string name, long value)
            {
                AddNotEscaped(sb, name, value);
            }

            private void Add(StringBuilder sb, string name, bool value)
            {
                AddNotEscaped(sb, name, value);
            }

            private void Add(StringBuilder sb, string name, string value)
            {
                AddEscaped(sb, name, value);
            }

            private void AddNotEscaped(StringBuilder sb, string name, bool value)
            {
                sb.Append(value ? $"\"{name}\":true," : $"\"{name}\":false,");
            }

            private void AddNotEscaped<T>(StringBuilder sb, string name, T value)
            {
                sb.Append($"\"{name}\":{value},");
            }

            private void AddEscaped<T>(StringBuilder sb, string name, T value)
            {
                sb.Append($"\"{name}\":\"{value}\",");
            }

            private void AddHeaders(StringBuilder stringBuilder)
            {
                Add(stringBuilder, "OperationName", _operationName);
                Add(stringBuilder, "OperationId", _operationId);
                Add(stringBuilder, "LogType", nameof(LogType.Performance));
                if (_parent != null)
                    Add(stringBuilder, "ParentOperationId", _parent.OperationId);
            }
        }

        private class StubMeasurement : IPerformanceMeasurement
        {
            public void Dispose()
            {}

            public IPerformanceMeasurement TrackChild(string operationName)
            {
                return new StubMeasurement();
            }

            public void AddValue(string name, string units, long value)
            {}

            public void AddKeyValue(string key, string value)
            {}

            public void AddKeyValue(string key, bool value)
            {}
        }
    }
}