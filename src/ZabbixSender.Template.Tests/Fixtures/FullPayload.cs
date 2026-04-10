using ZabbixSender.Template;

namespace ZabbixSender.Template.Tests.Fixtures;

public class TestAppPayload
{
    public AppStatus Status { get; set; } = new();
    public ConnectedClients Clients { get; set; } = new();
    public MemoryMetrics Memory { get; set; } = new();
    public DiskStatus Disk { get; set; } = new();
    public DatabaseStatus Database { get; set; } = new();
    public JobStatus Jobs { get; set; } = new();
    public HttpMetrics Http { get; set; } = new();
}

public class AppStatus
{
    public Dictionary<string, HealthEntry> HealthStatus { get; set; } = new();

    [TriggerOnChange(Priority.Info, Duration = "1m")]
    public string Version { get; set; } = "";

    [Units("uptime")]
    [Trigger("<", 300, Priority.Info, Recovery = "10m")]
    public long Uptime { get; set; }
}

public class HealthEntry
{
    [Trigger("Degraded", Priority.Warning)]
    [Trigger("Unhealthy", Priority.High)]
    public string Status { get; set; } = "";

    public string Message { get; set; } = "";
}

public class ConnectedClients
{
    [Trigger("<", 1, Priority.High, Duration = "5m")]
    public int Workers { get; set; }

    public int Other { get; set; }

    [ChangePerSecond]
    public long MessagesSent { get; set; }
}

public class DiskStatus
{
    [Units("B")]
    [Trigger("<", 5_000_000_000, Priority.Warning, Duration = "10m")]
    [Trigger("<", 1_000_000_000, Priority.High, Duration = "5m")]
    public long Available { get; set; }

    [Units("B")]
    public long UsedByApp { get; set; }
}

public class DatabaseStatus
{
    [Trigger(">", 80, Priority.Warning, Duration = "10m")]
    public long ActiveConnections { get; set; }

    [Units("B")]
    public long Size { get; set; }

    [Units("uptime")]
    [Trigger("<", 300, Priority.Warning, Recovery = "10m")]
    public long Uptime { get; set; }
}

public class JobStatus
{
    public int Total { get; set; }

    [Trigger(">", 0, Priority.High)]
    public int FailedPermanently { get; set; }

    public int Open { get; set; }

    [Trigger(">", 0, Priority.Warning, Duration = "30m")]
    public int LongOpen { get; set; }
}

public class HttpMetrics
{
    [ChangePerSecond]
    public long Requests { get; set; }

    [ChangePerSecond]
    [Trigger(">", 10, Priority.Warning, Duration = "5m")]
    public long Requests404 { get; set; }

    [ChangePerSecond]
    [Trigger(">", 5, Priority.High, Duration = "5m")]
    public long RequestsError { get; set; }

    [ChangePerSecond]
    [Trigger(">", 1, Priority.Warning, Duration = "10m")]
    public long LongRequests { get; set; }
}
