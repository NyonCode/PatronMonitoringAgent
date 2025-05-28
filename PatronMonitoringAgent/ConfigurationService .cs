using PatronMonitoringAgent.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PatronMonitoringAgent
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfigurationProvider _configurationProvider;
        private readonly ILoggerService _logger;
        private readonly IApiClient _apiClient;

        public ConfigurationService(IConfigurationProvider configProvider, ILoggerService logger, IApiClient apiClient)
        {
            _configurationProvider = configProvider;
            _logger = logger;
            _apiClient = apiClient;
        }

        public string GetToken() => _configurationProvider.GetToken();
        public string GetUUID() => _configurationProvider.GetUUID();
        public bool UUIDExist() => _configurationProvider.UUIDExist();
        public bool TokenExist() => _configurationProvider.TokenExist();
        public bool IntervalExist() => _configurationProvider.IntervalExist();
        public int GetInterval() => _configurationProvider.GetInterval();

        public void SaveToken(string token) => _configurationProvider.SaveToken(token);
        public void SaveInterval(int? interval) => _configurationProvider.SaveInterval(interval);

        public async Task UpdateConfigurationAsync()
        {
            try
            {
                // Fetch the latest configuration from the API
                var response = await _apiClient.GetAsync($"/api/clients/{GetUUID()}/configurations");
                if (response.Status != "ok")
                {
                    _logger.Error("Failed to fetch configuration from API.");
                    return;
                }
                // Update the local configuration
                SaveInterval(response.Interval);
            }
            catch (Exception ex)
            {
                _logger.Error("Error updating configuration.", ex);
            }
        }
    }
}
