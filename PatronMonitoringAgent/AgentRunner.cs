using Microsoft.Extensions.DependencyInjection;
using PatronMonitoringAgent.Common;
using Serilog;
using Serilog.Core;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PatronMonitoringAgent
{
    
    public class AgentRunner
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private ILoggerService logger;
        //private WatchdogService _watchdog;
        private Task _mainLoop;


        private readonly IServiceProvider provider;
        public AgentRunner(IServiceProvider provider)
        {
            this.provider = provider;
            this.logger = provider.GetRequiredService<ILoggerService>();
        }

        public void StartAsync()
        {
            //_watchdog = new WatchdogService(logger);
            //_watchdog.Start();

            _mainLoop = Task.Run(() => MainLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts.Cancel();
            _mainLoop.Wait();
            //_watchdog.Stop();
        }

        private async Task MainLoop(CancellationToken ct)
        {
            var config = this.provider.GetRequiredService<IConfigurationProvider>();
            var api = this.provider.GetRequiredService<IApiClient>();
            var remoteCmd = this.provider.GetRequiredService<IRemoteCommandHandler>();
            var update = this.provider.GetRequiredService<IUpdateService>();
            var systemMonitor = this.provider.GetRequiredService<ISystemMonitor>();
            var driveMonitor = this.provider.GetRequiredService<IDriveMonitor>();

            logger.Info("Patron Monitoring Agent service running.");


            // Registration
            var registrationData = new
            {
                uuid = config.GetUUID(),
                hostname = Environment.MachineName,
                ip_address = provider.GetRequiredService<ISystemMonitor>().GetNetworkInfo().Address,
                system = provider.GetRequiredService<ISystemMonitor>().GetSystemInfo()
            };
            var regResponse = await api.PostAsync("/api/clients/register", registrationData, ct);

            if (regResponse.Status != "ok")
            {
                logger.Error("Failed to register client.");
                return;
            }

            config.SaveToken(regResponse.Token);

            if (regResponse.Interval == null)
            {
                logger.Error("Invalid interval received from API, using default 60 seconds.");
                config.SaveInterval(60);
            } else
            {
                config.SaveInterval(regResponse.Interval);
            }

            while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var heartbeatData = new
                        {
                            status = "online",
                            last_error = "",
                            drives = provider.GetRequiredService<IDriveMonitor>().GetMappedDrives(),
                            system_monitor = provider.GetRequiredService<ISystemMonitor>().GetCurrentUsage(),
                            network_info = provider.GetRequiredService<ISystemMonitor>().GetNetworkInfo(),
                            /// 
                            /// TODO: implement session monitoring if needed
                            /// Used namedPipe for session monitoring, but not implemented in this example
                            ///session_monitor = provider.GetRequiredService<ISessionMonitor>().GetCurrentSession()
                        };

                        var hbResponse = await api.PostAsync($"/api/clients/{config.GetUUID()}/heartbeat", heartbeatData, ct);

                        if (hbResponse.RemoteCommands != null)
                            await remoteCmd.HandleCommandsAsync(hbResponse.RemoteCommands);

                        if (!string.IsNullOrEmpty(hbResponse.UpdateUrl))
                            await update.DownloadAndInstallAsync(hbResponse.UpdateUrl);

                        // Upload logs (structured, system logs)
                        var logs = SerilogLoggerService.CollectLogs();
                        var syslogs = provider.GetRequiredService<ISystemMonitor>().GetSystemLogs();
                        await api.PostAsync($"/api/clients/{config.GetUUID()}/logs", new { log = logs, system_logs = syslogs }, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Main loop error", ex);
                    }
                    await Task.Delay(config.GetInterval() * 1000, ct);
                }
        }

        public async Task OnShutdown()
        {
            var config = provider.GetRequiredService<IConfigurationProvider>();
            var api = provider.GetRequiredService<IApiClient>();

            await api.PostAsync($"/api/clients/{config.GetUUID()}/shutdown", new { status = "shutdown"});
        }
    }
}