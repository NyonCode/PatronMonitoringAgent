using System;
using System.Threading;
using System.Threading.Tasks;
using PatronMonitoringAgent.Common;
using Microsoft.Extensions.DependencyInjection;

namespace PatronMonitoringAgent
{
    public class AgentRunner
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _mainLoop;

        public void StartAsync()
        {
            _mainLoop = Task.Run(() => MainLoop(_cts.Token));
        }

        public void Stop()
        {
            _cts.Cancel();
            _mainLoop.Wait();
        }

        private async Task MainLoop(CancellationToken ct)
        {
            var services = new ServiceCollection();

            services.AddSingleton<IConfigurationProvider, RegistryConfigurationProvider>();
            services.AddSingleton<ILoggerService, SerilogLoggerService>();
            services.AddSingleton<IApiClient, LaravelApiClient>();
            services.AddSingleton<IDriveMonitor, DriveMonitor>();
            services.AddSingleton<ISystemMonitor, SystemMonitor>();
            services.AddSingleton<ISessionMonitor, SessionMonitor>();
            services.AddSingleton<IRemoteCommandHandler, RemoteCommandHandler>();
            services.AddSingleton<IUpdateService, UpdateService>();

            var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILoggerService>();
            var config = provider.GetRequiredService<IConfigurationProvider>();
            var api = provider.GetRequiredService<IApiClient>();
            var remoteCmd = provider.GetRequiredService<IRemoteCommandHandler>();
            var update = provider.GetRequiredService<IUpdateService>();

            logger.Info("PatronMonitoringAgent service running.");


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

                        var hbResponse = await api.PostAsync($"/api/clients/{config.GetUUID()}/heartbeat", heartbeatData);

                        if (hbResponse.RemoteCommands != null)
                            await remoteCmd.HandleCommandsAsync(hbResponse.RemoteCommands);

                        if (!string.IsNullOrEmpty(hbResponse.UpdateUrl))
                            await update.DownloadAndInstallAsync(hbResponse.UpdateUrl);

                        // Upload logs (structured, system logs)
                        var logs = SerilogLoggerService.CollectLogs();
                        var syslogs = provider.GetRequiredService<ISystemMonitor>().GetSystemLogs();
                        await api.PostAsync($"/api/clients/{config.GetUUID()}/log", new { log = logs, system_logs = syslogs });
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Main loop error", ex);
                    }
                    await Task.Delay(config.GetInterval() * 1000, ct);
                }
        }
    }
}