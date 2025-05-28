using Newtonsoft.Json.Linq;
using PatronMonitoringAgent.Common;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System;
using System.Collections.Generic;
using System.IO;

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
            var logList = new List<object>();

            try
            {
                var logDirectory = "logs";
                if (!Directory.Exists(logDirectory))
                    return logList;

                var logFiles = Directory.GetFiles(logDirectory, "*.json");

                foreach (var file in logFiles)
                {
                    try
                    {
                        // Otevřít soubor s povolením sdíleného čtení a zápisu
                        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var sr = new StreamReader(fs))
                        {
                            string line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                if (string.IsNullOrWhiteSpace(line)) continue;

                                try
                                {
                                    var json = JObject.Parse(line);
                                    logList.Add(json);
                                }
                                catch
                                {
                                    Log.Information($"Nepodařilo se naparsovat řádek logu: {line}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, $"Nepodařilo se načíst log file: {file}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Chyba při načítání logů.");
            }

            return logList;
        }
    }
}