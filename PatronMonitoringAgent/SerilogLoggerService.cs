using System;
using PatronMonitoringAgent.Common;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Collections.Generic;

namespace PatronMonitoringAgent
{
    public class SerilogLoggerService : ILoggerService
    {
        static SerilogLoggerService()
        {
            System.IO.Directory.CreateDirectory("logs");

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(
                    formatter: new JsonFormatter(),
                    path: "logs/log.json",
                    rollingInterval: RollingInterval.Day
                )
                .WriteTo.EventLog("PatronMonitoringAgent", restrictedToMinimumLevel: LogEventLevel.Error)
                .CreateLogger();
        }

        public void Info(string message) => Log.Information(message);
        public void Error(string message, Exception ex = null) => Log.Error(ex, message);
        public void Warning(string message) => Log.Warning(message);
        public void Debug(string message) => Log.Debug(message);

        // Collect structured logs for upload
        public static List<object> CollectLogs()
        {
            // For demo: in reality, parse the JSON file and return objects
            return new List<object>();
        }
    }
}