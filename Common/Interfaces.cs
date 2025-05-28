using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PatronMonitoringAgent.Common
{
    public interface IApiClient
    {
        Task<ApiResponse> PostAsync<T>(string endpoint, T data, CancellationToken ct = default);
        Task<ApiResponse> GetAsync(string endpoint, CancellationToken ct = default);
    }

    public interface IConfigurationProvider
    {
        string GetToken();
        string GetUUID();
        bool UUIDExist();
        bool TokenExist();
        bool IntervalExist();
        int GetInterval();
        void SaveToken(string token);
        void SaveInterval(int? interval);
    }

    public interface ILoggerService
    {
        void Info(string message);
        void Error(string message, Exception ex = null);
        void Warning(string message);
        void Debug(string message);
    }

    public interface IRemoteCommandHandler
    {
        Task HandleCommandsAsync(IEnumerable<RemoteCommand> commands, CancellationToken ct = default);
    }

    public interface IUpdateService
    {
        Task<bool> CheckForUpdateAsync(CancellationToken ct = default);
        Task DownloadAndInstallAsync(string url, CancellationToken ct = default);
    }

    public interface IDriveMonitor
    {
        IEnumerable<MappedDrive> GetMappedDrives();
    }

    public interface ISystemMonitor
    {
        SystemInfo GetSystemInfo();
        SystemUsage GetCurrentUsage();

        SystemNetworkInfo GetNetworkInfo();
        IEnumerable<SystemLogEntry> GetSystemLogs(int maxCount = 100);
    }

    public interface ISessionMonitor
    {
        SessionInfo GetCurrentSession();
    }
}