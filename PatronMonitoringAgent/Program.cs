using Microsoft.Extensions.DependencyInjection;
using PatronMonitoringAgent.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace PatronMonitoringAgent
{
    internal static class Program
    {

        /// <summary>
        /// Hlavní vstupní bod aplikace.
        /// </summary>
        static void Main()
        {
            var serviceProvider = ConfigureServices();

            if (Environment.UserInteractive)
            {
                // Konzolová verze pro debug
                var runner = serviceProvider.GetRequiredService<AgentRunner>();
                runner.StartAsync();
                Console.WriteLine("Running... Press Enter to exit.");
                Console.ReadLine();
                runner.Stop();
            }
            else
            {
                ServiceBase.Run(new AgentService(serviceProvider));
            }
        }

        public static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Registrace provideru konfigurace (čte z registru)
            services.AddSingleton<IConfigurationProvider, RegistryConfigurationProvider>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();

            // Registrace dalších služeb
            services.AddSingleton<ILoggerService, SerilogLoggerService>();
            services.AddSingleton<IApiClient, LaravelApiClient>();
            services.AddSingleton<IDriveMonitor, DriveMonitor>();
            services.AddSingleton<ISystemMonitor, SystemMonitor>();
            services.AddSingleton<ISessionMonitor, SessionMonitor>();
            services.AddSingleton<IRemoteCommandHandler, RemoteCommandHandler>();
            services.AddSingleton<IUpdateService, UpdateService>();

            // Registrace AgentRunner
            services.AddSingleton<AgentRunner>();

            return services.BuildServiceProvider();
        }

    }
}
