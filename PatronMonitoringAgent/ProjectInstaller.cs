using System.ComponentModel;
using System.ServiceProcess;

namespace PatronMonitoringAgent
{
    [RunInstaller(true)]
    public class ProjectInstaller : System.Configuration.Install.Installer
    {
        public ProjectInstaller()
        {
            var processInstaller = new ServiceProcessInstaller
            {
                Account = ServiceAccount.LocalSystem
            };

            var serviceInstaller = new ServiceInstaller
            {
                ServiceName = "PatronMonitoringAgent",
                DisplayName = "Patron Monitoring Agent",
                Description = "Monitoring a správa endpointů pro EMS",
                StartType = ServiceStartMode.Automatic
            };

            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}