# ZabbixSender.Template

Send strongly typed C# metrics to Zabbix and auto-generate a matching Zabbix 7.0 template. 

Intended to quickly get started with zabbix templates, and skip some of the boilerplate of configuring zabbix.

Define metrics as plain C# classes. Use the same classes to both generate a Zabbix template (YAML import file) and to serialize the data you push.

## Build time - generate template

1. Define metric classes
2. `TemplateBuilder` generates a Zabbix template from those types via reflection
3. Import the template YAML into Zabbix

## Runtime - send data

4. Serialize your metric classes as JSON and send to Zabbix
5. Zabbix extracts individual metrics from the JSON using JSONPath preprocessing

## Getting started

This walkthrough uses a web shop as an example. We'll monitor orders, response times, and service health.

### 1. Define your metrics

Each class is a group of related metrics. Properties become individual monitored items in Zabbix.

```csharp
using ZabbixSender.Template;

public class OrderMetrics
{
    public int Pending { get; set; }
    public int CompletedToday { get; set; }
    public int FailedToday { get; set; }
}

public class ResponseMetrics
{
    [Units("ms")]
    public double ApiLatency { get; set; }

    public int ActiveConnections { get; set; }

    [Trigger(">", 5000, Priority.High, Duration = "5m")]
    public double SlowQueryMs { get; set; }
}
```

The `[Trigger]` on `SlowQueryMs` creates a Zabbix trigger that fires when slow queries exceed 5000ms for 5 minutes straight.

### 2. Discovery types

Some metrics are dynamic: you don't know how many there will be at template time. For example, health checks, queues, or endpoints. Use `Dictionary<string, T>` for these. Zabbix discovers new entries automatically at runtime.

```csharp
public class ServiceHealth
{
    [Trigger("Degraded", Priority.Warning)]
    [Trigger("Unhealthy", Priority.High)]
    public string Status { get; set; } = "";

    public string Description { get; set; } = "";
}
```

Each trigger fires on a different value. When `Status` is `"Degraded"`, only the warning fires. When `"Unhealthy"`, only the high priority fires. They don't stack.

### 3. Combine into a payload

Create a class that combines all your metric groups. This is what gets serialized to JSON and sent to Zabbix. `Dictionary<string, T>` properties are automatically detected as discovery collections.

```csharp
public class WebShopPayload
{
    public OrderMetrics Orders { get; set; } = new();
    public ResponseMetrics Response { get; set; } = new();
    public Dictionary<string, ServiceHealth> Services { get; set; } = new();
}
```

### 4. Generate the template

Pass your payload class to the builder. It picks up all metric groups and discovery collections automatically.

```csharp
var template = new TemplateBuilder("WebShop")
    .Add<WebShopPayload>()
    .Build();

File.WriteAllText("webshop-template.yaml", template.ToYaml());
```

This generates a Zabbix 7.0 YAML template. Import it into Zabbix under Configuration > Templates > Import. The template creates:

- a trapper item `webshop.data` that receives the full JSON
- dependent items for each property (e.g. `webshop.orders.pending`, `webshop.response.apilatency`)
- a discovery rule for services that auto-creates items when new services appear
- triggers for slow queries and unhealthy services
- a nodata trigger that fires when your app stops sending

### 5. Push metrics

Send the full payload, or just the sections you have data for. Keys are derived automatically from the class names.

```csharp
using ZabbixSender.Secure;

var sender = new SecureSender("zabbix.example.com",
    new CredentialsPreSharedKey("MyIdentity", "aabbccdd..."));

// send everything
var payload = new WebShopPayload
{
    Orders = new OrderMetrics { Pending = 42, CompletedToday = 1337, FailedToday = 3 },
    Response = new ResponseMetrics { ApiLatency = 120.5, ActiveConnections = 85, SlowQueryMs = 45 },
    Services = new Dictionary<string, ServiceHealth>
    {
        ["payments"] = new() { Status = "Healthy", Description = "" },
        ["inventory"] = new() { Status = "Degraded", Description = "Slow response from warehouse API" },
        ["email"] = new() { Status = "Healthy", Description = "" },
    }
};
await sender.SendJson("MyHost", payload);
// sends to key "webshop.data" (derived from WebShopPayload -> "webshop")
```

### 6. Partial updates

Send just one section. The other metrics keep their last known value in Zabbix.

```csharp
// only update orders
await sender.SendJson<WebShopPayload, OrderMetrics>(
    "MyHost", new OrderMetrics { Pending = 38, CompletedToday = 1340, FailedToday = 3 });
// sends to key "webshop.data", value {"orders":{"pending":38,...}}

// only update service health
await sender.SendJson<WebShopPayload, ServiceHealth>(
    "MyHost", new Dictionary<string, ServiceHealth>
    {
        ["payments"] = new() { Status = "Healthy", Description = "" },
        ["inventory"] = new() { Status = "Healthy", Description = "" },
    });
// sends to key "webshop.data", value {"services":{"payments":{...},...}}
```

The key and JSON structure are derived from the type parameters:
- `WebShopPayload` -> prefix `webshop` -> trapper key `webshop.data`
- `OrderMetrics` -> finds `Orders` property on `WebShopPayload` -> wraps under `"orders"`
- `ServiceHealth` -> finds `Dictionary<string, ServiceHealth> Services` -> wraps under `"services"`

## Metrics attributes

| Attribute | C# target | Purpose |
|---|---|---|
| `[Units("ms")]` | property | Zabbix units (`B` for bytes, `ms` for milliseconds, `%` for percent, etc.) |
| `[ZabbixIgnore]` | property | exclude property from template |
| `[ZabbixDescription("...")]` | property, class | description text |
| `[ZabbixPrefix("myPrefix")]` | class | manually set prefix, default: builder name lowercased |
| `[ItemKey("custom.key")]` | property | manually set the item key, default: prefix.section.property |
| `[Discovery("MACRO")]` | property | override the auto-generated discovery macro name |

## Charts

Line graphs are auto-generated for numeric items in the same section.

| Attribute | target | Purpose |
|---|---|---|
| `[PieChart("Title")]` | class | add a pie chart from numeric properties |
| `[StackedGraph]` | class | force stacked line graph |
| `[SingleGraph]` | class | force individual line graphs per property |

```csharp
[PieChart("Order breakdown")]
public class OrderMetrics
{
    public int Pending { get; set; }
    public int CompletedToday { get; set; }
    public int FailedToday { get; set; }
}
```

## Triggers

All repeatable on the same property. Duration uses Zabbix time suffixes: `s` (seconds), `m` (minutes), `h` (hours), `d` (days).

| Attribute | target | Purpose |
|---|---|---|
| `[Trigger("value", Priority)]` | property | alert on exact string match |
| `[Trigger("op", threshold, Priority)]` | property | alert on numeric threshold (`>`, `<`, `>=`, `<=`) |
| `[Trigger(..., Duration = "5m")]` | property | condition must hold for duration before firing |
| `[Trigger(..., Recovery = "5m")]` | property | auto-resolve when inverse condition holds |
| `[TriggerOnChange(Priority)]` | property | fire when value changes, auto-resolve when stable |
| `[TriggerOnChange(..., Duration = "5m")]` | property | keep trigger active for duration after change |

```csharp
// fires when slow queries exceed 5 seconds for 5 minutes straight
[Trigger(">", 5000, Priority.High, Duration = "5m")]
public double SlowQueryMs { get; set; }

// fires when status equals "Degraded", separate trigger for "Unhealthy"
// these don't stack: only the matching trigger fires
[Trigger("Degraded", Priority.Warning)]
[Trigger("Unhealthy", Priority.High)]
public string Status { get; set; }

// fires when failed orders exceed 10, auto-resolves when back to 10 or below
[Trigger(">", 10, Priority.Warning, Recovery = "5m")]
public int FailedToday { get; set; }

// detect app restarts: fires on change, resolves after 5 minutes of stability
[TriggerOnChange(Priority.Information, Duration = "5m")]
public string AppVersion { get; set; }
```

## Under the hood

### Type to template mapping

| C# pattern | what Zabbix gets |
|---|---|
| class name (minus `Payload`/`Metrics`/`Data`/`Info`/`Status` suffix) | item group name (component tag) |
| property (`int`, `string`, etc.) | monitored item with JSONPath extraction |
| `Dictionary<string, T>` property | auto-discovered collection (LLD) |
| nullable type (`int?`) | same item, optional in payload |

### How keys are generated

Class and property names map to Zabbix item keys (all lowercase, dot-separated) and JSONPath expressions (camelCase, matching `System.Text.Json` defaults):

| C# | Zabbix key | JSONPath |
|---|---|---|
| `OrderMetrics.Pending` | `webshop.orders.pending` | `$.orders.pending` |
| `ResponseMetrics.ApiLatency` | `webshop.response.apilatency` | `$.response.apiLatency` |

### C# type to Zabbix type

| C# type | Zabbix value_type |
|---|---|
| `int`, `long`, `uint`, `bool` | UNSIGNED (default) |
| `double`, `float`, `decimal` | FLOAT |
| `string` | CHAR |

## Builder API

The simplest approach: pass your payload class and everything is derived.

```csharp
var template = new TemplateBuilder("WebShop")
    .Add<WebShopPayload>()
    .Build();
```

You can also add metric classes individually if you don't have a payload class:

```csharp
var template = new TemplateBuilder("WebShop")
    .Add<OrderMetrics>()
    .Add<ResponseMetrics>()
    .AddDiscovery<ServiceHealth>("Services")
    .NodataTrigger(seconds: 300)
    .Build();
```

`AddDiscovery` is only needed when adding classes individually, since the builder can't detect the dictionary property without a parent class.

### Composable design

Reuse metric classes across different app templates:

```csharp
var shop = new TemplateBuilder("WebShop")
    .Add<WebShopPayload>()
    .Build();

var api = new TemplateBuilder("PublicAPI")
    .Add<ResponseMetrics>()
    .AddDiscovery<ServiceHealth>("Services")
    .Build();
```
