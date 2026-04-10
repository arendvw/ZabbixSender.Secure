# ZabbixSender.Secure

This repo contains two NuGet packages for sending data to Zabbix from .NET:
## Package documentation

- [ZabbixSender.Secure](src/ZabbixSender.Secure/) - TLS sender setup, credentials, configuration, API reference
- [ZabbixSender.Template](src/ZabbixSender.Template/README.md) - template builder, metrics, triggers, charts, discovery types


The packages can be used independently. 

## Quick example

Define your metrics, generate a Zabbix template, and push data:

```csharp
using ZabbixSender.Secure;
using ZabbixSender.Template;

// define metrics as plain C# classes
public class OrderMetrics
{
    public int Pending { get; set; }
    public int CompletedToday { get; set; }
}

public class WebShopPayload
{
    public OrderMetrics Orders { get; set; } = new();
}

// generate template (build time)
var template = new TemplateBuilder("WebShop")
    .Add<WebShopPayload>()
    .Build();

File.WriteAllText("webshop-template.yaml", template.ToYaml());

// push metrics (runtime)
var sender = new SecureSender("zabbix.example.com",
    new CredentialsPreSharedKey("MyIdentity", "aabbccdd..."));

await sender.SendJson("MyHost", new WebShopPayload
{
    Orders = new OrderMetrics { Pending = 42, CompletedToday = 1337 }
});
```



## License

[MIT](LICENSE)
