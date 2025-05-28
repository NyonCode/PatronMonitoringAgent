using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using PatronMonitoringAgent.Common;
using Newtonsoft.Json;
using Polly;

namespace PatronMonitoringAgent
{
    public class LaravelApiClient : IApiClient
    {
        private readonly IConfigurationProvider _config;
        private readonly ILoggerService _logger;
        private readonly HttpClient _client;
        private readonly Polly.AsyncPolicy<HttpResponseMessage> _retryPolicy;

        public LaravelApiClient(IConfigurationProvider config, ILoggerService logger)
        {
            _config = config;
            _logger = logger;
            _client = new HttpClient { BaseAddress = new Uri("http://localhost:8000/") };

            _retryPolicy = Policy
                .HandleResult<HttpResponseMessage>(r =>
                    (int)r.StatusCode == 429 ||
                    r.StatusCode == HttpStatusCode.RequestTimeout ||
                    r.StatusCode == HttpStatusCode.ServiceUnavailable)
                .WaitAndRetryAsync(
                    5,
                    (retryAttempt, response, context) =>
                    {
                        if (response.Result?.Headers?.RetryAfter != null)
                            return response.Result.Headers.RetryAfter.Delta ?? TimeSpan.FromSeconds(60);
                        return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                    },
                    (result, span, retryCount, context) =>
                    {
                        _logger.Warning($"API rate-limit or error, retry {retryCount}, waiting {span.TotalSeconds}s");
                        return Task.CompletedTask;
                    }
                );
        }

        public async Task<ApiResponse> PostAsync<T>(string endpoint, T data, CancellationToken ct = default)
        {
            SetAuthHeader();
            var content = new StringContent(JsonConvert.SerializeObject(data), System.Text.Encoding.UTF8, "application/json");
            var response = await _retryPolicy.ExecuteAsync(() => _client.PostAsync(endpoint, content, ct));
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.Error($"API POST {endpoint} failed: {json}");
                return new ApiResponse { Status = "error" };
            }
            return !string.IsNullOrWhiteSpace(json)
                ? JsonConvert.DeserializeObject<ApiResponse>(json)
                : new ApiResponse { Status = "empty" };
        }

        public async Task<ApiResponse> GetAsync(string endpoint, CancellationToken ct = default)
        {
            SetAuthHeader();
            var response = await _retryPolicy.ExecuteAsync(() => _client.GetAsync(endpoint, ct));
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                _logger.Error($"API GET {endpoint} failed: {json}");
                return new ApiResponse { Status = "error" };
            }
            return !string.IsNullOrWhiteSpace(json)
                ? JsonConvert.DeserializeObject<ApiResponse>(json)
                : new ApiResponse { Status = "empty" };
        }

        private void SetAuthHeader()
        {
            var token = _config.GetToken();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }
}