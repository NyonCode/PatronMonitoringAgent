using PatronMonitoringAgent.Common;
using System;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace PatronMonitoringAgent
{
    public partial class AgentService : ServiceBase
    {
        private AgentRunner _runner;
        private readonly IApiClient _apiClient;
        private readonly IConfigurationProvider _config;
        private readonly ILoggerService _logger;

        public AgentService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            _runner = new AgentRunner();
            _runner.StartAsync();
        }

        protected override void OnStop()
        {
            // Ukončete všechny běžící úlohy a uvolněte prostředky
            if (_runner != null)
            {
                _runner.Stop();
            }
        }

        protected override void OnShutdown()
        {
            // Odeslat info o vypnutí/restartu systému
            SendShutdownInfoAsync("system_shutdown").Wait();
            _runner?.Stop();
        }

        // Volitelné: lze rozlišit i typ události (shutdown vs restart), např. přes SystemEvents nebo Environment.HasShutdownStarted

        private async Task SendShutdownInfoAsync(string reason)
        {
            try
            {
                var data = new
                {
                    type = reason, // např. "system_shutdown", "service_stop"
                    time = DateTime.UtcNow,
                    user = Environment.UserName,
                    hostname = Environment.MachineName
                };
                // End-point dle původního konceptu
                string uuid = _config.GetUUID();
                await _apiClient.PostAsync($"/api/clients/{uuid}/shutdown", data);
                _logger.Info($"Sent shutdown info to API. Reason: {reason}");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to send shutdown info to API.", ex);
            }
        }
    }
}
