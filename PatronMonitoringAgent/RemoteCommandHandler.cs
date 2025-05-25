using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PatronMonitoringAgent.Common;

namespace PatronMonitoringAgent
{
    public class RemoteCommandHandler : IRemoteCommandHandler
    {
        private readonly ILoggerService _logger;

        public RemoteCommandHandler(ILoggerService logger)
        {
            _logger = logger;
        }

        public async Task HandleCommandsAsync(IEnumerable<RemoteCommand> commands, CancellationToken ct = default)
        {
            foreach (var cmd in commands)
            {
                switch (cmd.Type)
                {
                    case "restart":
                        _logger.Info("Remote restart requested.");
                        Process.Start("shutdown", "/r /t 0");
                        break;
                    case "shutdown":
                        _logger.Info("Remote shutdown requested.");
                        Process.Start("shutdown", "/s /t 0");
                        break;
                    case "exec":
                        _logger.Info($"Remote exec: {cmd.Command}");
                        try
                        {
                            Process.Start("cmd.exe", $"/c {cmd.Command}");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error("Exec failed", ex);
                        }
                        break;
                    case "update":
                        _logger.Info($"Remote update: {cmd.Url}");
                        // Handle update in UpdateService
                        break;
                }
            }
            await Task.CompletedTask;
        }
    }
}