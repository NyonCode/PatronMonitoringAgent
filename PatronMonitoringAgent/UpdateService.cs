using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using PatronMonitoringAgent.Common;

namespace PatronMonitoringAgent
{
    public class UpdateService : IUpdateService
    {
        private readonly ILoggerService _logger;

        public UpdateService(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task<bool> CheckForUpdateAsync(CancellationToken ct = default)
        {
            // Not implemented, handled via remote command
            return false;
        }

        public async Task DownloadAndInstallAsync(string url, CancellationToken ct = default)
        {
            try
            {
                var file = Path.Combine(Path.GetTempPath(), "update_installer.msi");
                using (var client = new WebClient())
                {
                    await client.DownloadFileTaskAsync(new Uri(url), file);
                }
                _logger.Info($"Downloaded update. Executing installer {file}");
                Process.Start(file, "/quiet");
            }
            catch (Exception ex)
            {
                _logger.Error("Update failed", ex);
            }
        }
    }
}