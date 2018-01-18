using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HTTPDataCollectorAPI;
using Moq;
using Newtonsoft.Json.Linq;
using PerformanceLogger.Logging.Interfaces;
using PerformanceLogger.Logging.Performance;
using Xunit;

namespace PerformanceLogger.Tests.Logging.Performance
{
    public class PerformanceTrackerTest
    {
        private Mock<ICollector> _collectorMock;
        private readonly Expression<Func<ICollector, Task>> _collectExpression = _ => _.Collect(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>());
        
        public PerformanceTrackerTest()
        {
            _collectorMock = new Mock<ICollector>();
        }

        [Fact]
        public void Should_send_on_dispose()
        {
            _collectorMock.Setup(_collectExpression);
            var performanceTracker = Create();

            using (performanceTracker.Track("operationName"))
            {
            }

            _collectorMock.Verify(_collectExpression, Times.Once);
        }

        [Fact]
        public void Operation_should_send_valid_operation_name()
        {
            string json = null;
            _collectorMock.Setup(_collectExpression)
                .Callback<string, string, string, string>((s1, s2, s3, s4) => json = s2)
                .Returns(Task.CompletedTask);
            var performanceTracker = Create();

            using (performanceTracker.Track("operationName"))
            {
            }

            Assert.NotNull(json);
            var obj = JToken.Parse(json).First;
            Assert.Equal("operationName", obj.Value<string>("OperationName"));
        }

        [Fact]
        public void Operation_should_send_valid_typeName_name()
        {
            string typeName = null;
            _collectorMock.Setup(_collectExpression)
                .Callback<string, string, string, string>((s1, s2, s3, s4) => typeName = s1)
                .Returns(Task.CompletedTask);
            var performanceTracker = Create();

            using (performanceTracker.Track("operationName"))
            {
            }

            Assert.NotNull(typeName);
            Assert.Equal("typeName", typeName);
        }

        [Fact]
        public void Operation_should_send_valid_operation_id()
        {
            string json = null;
            _collectorMock.Setup(_collectExpression)
                .Callback<string, string, string, string>((s1, s2, s3, s4) => json = s2)
                .Returns(Task.CompletedTask);
            var performanceTracker = Create();

            using (performanceTracker.Track("operationName"))
            {
            }

            Assert.NotNull(json);
            var obj = JToken.Parse(json).First;
            Guid id;
            var parsed = Guid.TryParse(obj.Value<string>("OperationId").ToString(), out id);
            Assert.True(parsed && id != Guid.Empty);
        }

        [Fact]
        public void Operation_should_contain_elapsed_time()
        {
            var delay = 100;
            string json = null;
            _collectorMock.Setup(_collectExpression)
                .Callback<string, string, string, string>((s1, s2, s3, s4) => json = s2)
                .Returns(Task.CompletedTask);
            var performanceTracker = Create();

            using (performanceTracker.Track("operationName"))
            {
                Thread.Sleep(delay);
            }

            Assert.NotNull(json);
            var obj = JToken.Parse(json).First;
            long elapsed;
            var parsed = long.TryParse(obj.Value<string>("Elapsed").ToString(), out elapsed);
            Assert.True(parsed && elapsed >= delay);
        }

        [Fact]
        public void Child_operation_should_have_parent_operation_id()
        {
            var results = new List<string>();
            _collectorMock.Setup<Task>(_collectExpression)
                .Callback<string, string, string, string>((s1, s2, s3, s4) => results.Add(s2))
                .Returns(Task.CompletedTask);

            var performanceTracker = Create();

            using (var measurement = performanceTracker.Track("main"))
            using (measurement.TrackChild("child"))
            {
            }

            // Expected a single request, containing two object
            Assert.Single(results);
            var objList = JToken.Parse(results[0]);
            Assert.Equal(2, objList.Children().Count());
            var child = objList.First;
            var main = objList.Last;
            Assert.Equal(main.Value<string>("OperationId"), child.Value<string>("ParentOperationId"));
        }

        [Fact]
        public void Custom_measurement_should_be_sent()
        {
            string captured = null;
            _collectorMock.Setup<Task>(_collectExpression)
                .Callback<string, string, string, string>((s1, json, s3, s4) => captured = json);

            var performanceTracker = Create();

            using (var measurement = performanceTracker.Track("main"))
            {
                // Add custom value
                measurement.AddValue("Custom", "ms", 1000);
            }

            var capturedObject = JToken.Parse(captured).First;

            Assert.Equal(1000, capturedObject.Value<long>("Custom"));
            Assert.Equal("ms", capturedObject.Value<string>("Custom_unit"));
        }

        [Fact]
        public void Custom_key_value_string_string_should_be_sent()
        {
            string captured = null;
            _collectorMock.Setup<Task>(_collectExpression)
                .Callback<string, string, string, string>((s1, json, s3, s4) => captured = json);

            var performanceTracker = Create();

            using (var measurement = performanceTracker.Track("main"))
            {
                // Add custom value
                measurement.AddKeyValue("Custom", "Value");
            }

            var capturedObject = JToken.Parse(captured).First;

            Assert.Equal("Value", capturedObject.Value<string>("Custom"));
        }

        [Fact]
        public void Custom_key_value_string_bool_should_be_sent()
        {
            string captured = null;
            _collectorMock.Setup<Task>(_collectExpression)
                .Callback<string, string, string, string>((s1, json, s3, s4) => captured = json);

            var performanceTracker = Create();

            using (var measurement = performanceTracker.Track("main"))
            {
                // Add custom value
                measurement.AddKeyValue("Custom", true);
            }

            var capturedObject = JToken.Parse(captured).First;

            Assert.Equal(true, capturedObject.Value<bool>("Custom"));
        }

        [Fact]
        public void Each_measurement_should_have_unit_data()
        {
            var excluded_fields = new HashSet<string>() { "LogType", "OperationName", "OperationId", "ParentOperationId" };
            string captured = null;
            _collectorMock.Setup<Task>(_collectExpression)
                .Callback<string, string, string, string>((s1, json, s3, s4) => captured = json);

            var performanceTracker = Create();

            // Test built-in Elapsed data
            using (var measurement = performanceTracker.Track("main"))
            {
                // Test also custom value
                measurement.AddValue("Custom", "ms", 1000);
            }
            var capturedObject = JObject.Parse(JToken.Parse(captured).First.ToString());

            // walk trough properties names and check did we see pair properties before
            // if not, add property name to list to check later
            // if we see it before - remove previous property from list
            var measurements = new HashSet<string>();
            foreach (var property in capturedObject.Properties())
            {
                if (excluded_fields.Contains(property.Name)) continue;

                string propertyToCheck;
                if (property.Name.EndsWith("_unit"))
                {
                    propertyToCheck = property.Name.Replace("_unit", "");
                }
                else
                {
                    propertyToCheck = property.Name + "_unit";
                }

                if (measurements.Contains(propertyToCheck))
                    measurements.Remove(propertyToCheck);
                else
                    measurements.Add(property.Name);
            }

            // If some properties does not have pairs then show this properties
            if (measurements.Count != 0)
                Assert.True(
                    false,
                    "Property(ies) does not have corresponding pair:\n" + measurements.Aggregate(new StringBuilder(),
                        (builder, s) => builder.AppendLine(s), sb => sb.ToString()));
        }

        [Fact]
        public void Should_not_send_data_if_not_allowed()
        {
            string captured = null;
            _collectorMock.Setup<Task>(_collectExpression);

            var performanceTracker = new PerformanceTracker(_collectorMock.Object, "typeName", () => false);

            using (var measurement = performanceTracker.Track("main"))
            {
                // Add custom value
                measurement.AddValue("Custom", "ms", 1000);
            }

            _collectorMock.Verify<Task>(_collectExpression, Times.Never);
        }

        private IPerformanceTracker Create()
        {
            return new PerformanceTracker(_collectorMock.Object, "typeName", () => true);
        }
    }
}