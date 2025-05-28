using PatronMonitoringAgent.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

namespace PatronMonitoringAgent
{
    public class WatchdogService
    {
        private readonly ILoggerService _logger;
        private readonly string _agentHeartbeatFile = "watchdog_heartbeat.txt";
        private readonly string _trayHeartbeatFile = "tray_heartbeat.txt";
        private readonly TimeSpan _maxSilence = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);
        private CancellationTokenSource _cts;
        private Task _watchdogLoop;

        public WatchdogService(ILoggerService logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Volat z hlavní služby
        /// </summary>
        public void ReportAgentAlive()
        {
            try { File.WriteAllText(_agentHeartbeatFile, DateTime.UtcNow.ToString("o")); }
            catch (Exception ex) { _logger.Error("Watchdog: Cannot write agent heartbeat.", ex); }
        }

        /// <summary>
        /// Volat z tray aplikace
        /// </summary>
        public static void ReportTrayAlive()
        {
            try { File.WriteAllText("tray_heartbeat.txt", DateTime.UtcNow.ToString("o")); }
            catch { /* ignore */ }
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            _watchdogLoop = Task.Run(() => Loop(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _watchdogLoop?.Wait();
        }

        private async Task Loop(CancellationToken token)
        {
            _logger.Info("Watchdog started.");
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // --- AGENT kontrola ---
                    DateTime lastAgent = ReadTimestamp(_agentHeartbeatFile);
                    var now = DateTime.UtcNow;
                    if (lastAgent == DateTime.MinValue || now - lastAgent > _maxSilence)
                    {
                        _logger.Error($"Watchdog: No agent heartbeat for {(now - lastAgent).TotalMinutes:F1} min! Restarting agent service.");
                        LogIncident(now, lastAgent, "agent");
                        RestartService("PatronMonitoringAgent");
                    }

                    // --- TRAY kontrola ---
                    DateTime lastTray = ReadTimestamp(_trayHeartbeatFile);
                    if (lastTray == DateTime.MinValue || now - lastTray > _maxSilence)
                    {
                        _logger.Error($"Watchdog: No tray heartbeat for {(now - lastTray).TotalMinutes:F1} min! Attempting tray restart.");
                        LogIncident(now, lastTray, "tray");
                        RestartTrayForAllUsers();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Watchdog: Error in monitoring loop.", ex);
                }
                await Task.Delay(_checkInterval, token);
            }
            _logger.Info("Watchdog stopped.");
        }

        private DateTime ReadTimestamp(string file)
        {
            try
            {
                if (File.Exists(file))
                {
                    var content = File.ReadAllText(file);
                    DateTime.TryParse(content, out var ts);
                    return ts;
                }
            }
            catch { }
            return DateTime.MinValue;
        }

        private void LogIncident(DateTime now, DateTime lastTick, string what)
        {
            try
            {
                File.AppendAllText("watchdog_incidents.log", $"{now:o} | {what} missed heartbeat since {lastTick:o}\r\n");
            }
            catch (Exception ex)
            {
                _logger.Error("Watchdog: Cannot write incident log.", ex);
            }
        }

        private void RestartService(string serviceName)
        {
            try
            {
                using (var sc = new ServiceController(serviceName))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped && sc.Status != ServiceControllerStatus.StopPending)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Watchdog: Service restart failed for {serviceName}.", ex);
            }
        }

        /// <summary>
        /// Restartuje tray aplikaci pro všechny uživatele, kde neběží
        /// </summary>
        private void RestartTrayForAllUsers()
        {
            try
            {
                // Najde všechny běžící tray procesy
                var trayProcesses = Process.GetProcessesByName("PatronMonitoringAgent.Tray");
                if (trayProcesses.Length == 0)
                {
                    // Tray není spuštěný, pokus o jeho spuštění ve všech sessions
                    // (pro jednoduchost zde pouze spustíme v aktuálním uživatelském kontextu)
                    var trayPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PatronMonitoringAgent.Tray.exe");
                    if (File.Exists(trayPath))
                        Process.Start(trayPath);
                    else
                        _logger.Error("Watchdog: Tray exe not found for restart!");
                }
                // případně rozšířit pro více sessions pomocí Task Scheduler či ServiceUI.exe
            }
            catch (Exception ex)
            {
                _logger.Error("Watchdog: Tray restart failed.", ex);
            }
        }
    }
}