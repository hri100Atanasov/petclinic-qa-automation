using System.Globalization;
using System.Text.Json;

namespace PetClinic.PerformanceTests.Metrics;

/// <summary>
/// Polls a focused subset of the AUT's Spring Boot Actuator metrics
/// (/actuator/metrics/{name}) on a fixed interval for the duration of a load
/// run, appending one CSV row per poll. This is deliberately not a real
/// time-series backend (no Prometheus/Grafana) — /actuator/metrics/{name}
/// only ever returns a point-in-time snapshot, so polling-and-appending is
/// the cheapest way to get a time series out of it without standing up any
/// infrastructure, per the original "no external sinks" design constraint.
///
/// Metric choice is deliberate, not exhaustive (the AUT exposes ~90 names):
/// CPU/heap to check whether a Docker resource cap is actually being hit,
/// HikariCP pool stats because the default pool size (10) is a plausible
/// bottleneck independent of any Docker cap, and http.server.requests for
/// /api/invoices as a server-side cross-check against NBomber's own
/// client-side latency numbers.
/// </summary>
public sealed class MetricsPoller(HttpClient http, string token, string outputCsvPath)
{
    private static readonly string[] Header =
    [
        "timestamp", "process_cpu_usage", "jvm_heap_used_bytes",
        "gc_pause_count", "gc_pause_total_time_s",
        "hikaricp_active", "hikaricp_pending", "hikaricp_max",
        "http_invoices_post_count", "http_invoices_post_total_time_s", "http_invoices_post_max_s"
    ];

    public async Task RunAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(outputCsvPath, string.Join(",", Header) + "\n", cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var row = await PollOnceAsync();
                await File.AppendAllTextAsync(outputCsvPath, row + "\n", cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A single failed poll (e.g. the AUT is momentarily saturated
                // and slow to answer /actuator/metrics too) shouldn't kill the
                // whole poller — skip this tick and try again next interval.
                Console.WriteLine($"[metrics-poller] poll failed: {ex.Message}");
            }

            try
            {
                await Task.Delay(interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<string> PollOnceAsync()
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        var cpu = await GetValueAsync("process.cpu.usage");
        var heapUsed = await GetValueAsync("jvm.memory.used", "area:heap");
        var (gcCount, gcTotalTime, _) = await GetTimerAsync("jvm.gc.pause");
        var hikariActive = await GetValueAsync("hikaricp.connections.active");
        var hikariPending = await GetValueAsync("hikaricp.connections.pending");
        var hikariMax = await GetValueAsync("hikaricp.connections.max");
        var (invoicesCount, invoicesTotalTime, invoicesMax) =
            await GetTimerAsync("http.server.requests", "uri:/api/invoices", "method:POST");

        return string.Join(",",
            timestamp, F(cpu), F(heapUsed),
            F(gcCount), F(gcTotalTime),
            F(hikariActive), F(hikariPending), F(hikariMax),
            F(invoicesCount), F(invoicesTotalTime), F(invoicesMax));
    }

    private async Task<double?> GetValueAsync(string metric, params string[] tags)
    {
        var json = await FetchAsync(metric, tags);
        if (json is null)
        {
            return null;
        }

        return FindStatistic(json.Value, "VALUE");
    }

    private async Task<(double? count, double? totalTime, double? max)> GetTimerAsync(string metric, params string[] tags)
    {
        var json = await FetchAsync(metric, tags);
        if (json is null)
        {
            return (null, null, null);
        }

        return (FindStatistic(json.Value, "COUNT"), FindStatistic(json.Value, "TOTAL_TIME"), FindStatistic(json.Value, "MAX"));
    }

    private async Task<JsonElement?> FetchAsync(string metric, params string[] tags)
    {
        var query = string.Join("", tags.Select(t => $"&tag={Uri.EscapeDataString(t)}"));
        var url = $"/actuator/metrics/{metric}?{query.TrimStart('&')}";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        return doc.RootElement.Clone();
    }

    private static double? FindStatistic(JsonElement root, string statisticName)
    {
        if (!root.TryGetProperty("measurements", out var measurements))
        {
            return null;
        }

        foreach (var m in measurements.EnumerateArray())
        {
            if (m.GetProperty("statistic").GetString() == statisticName)
            {
                return m.GetProperty("value").GetDouble();
            }
        }

        return null;
    }

    private static string F(double? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "";
}
