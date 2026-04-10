# ZabbixSender.Secure

TLS-PSK and TLS-certificate support for [ZabbixSender.Async](https://github.com/stop-cran/ZabbixSender.Async). Drop-in `ISender` replacement that uses BouncyCastle for encryption. Zabbix 7 compatible.

## Getting started

### TLS-PSK

```csharp
using ZabbixSender.Secure;

var sender = new SecureSender(
    "zabbix.example.com",
    new CredentialsPreSharedKey("MyPSKIdentity", "aabbccdd01234567..."));

var response = await sender.Send("MyHost", "my.item.key", "42");

if (response.IsSuccess)
{
    var info = response.ParseInfo();
    Console.WriteLine($"processed: {info.Processed}, failed: {info.Failed}");
}
```

### TLS-certificate

```csharp
var sender = new SecureSender(
    "zabbix.example.com",
    new CredentialsCertificate("/path/to/cert.pem", "/path/to/key.pem"));
```

For self-signed server certificates:

```csharp
var sender = new SecureSender(
    "zabbix.example.com",
    new CredentialsCertificate(certPath, keyPath, verifyServerCertificate: false));
```

### Sending JSON payloads

For trapper templates that use a master item with dependent items (see [ZabbixSender.Template](https://github.com/arendvw/ZabbixSender.Secure/tree/main/src/ZabbixSender.Template)):

```csharp
// send full payload, key derived from class name
await sender.SendJson("MyHost", payload);

// send just one section
await sender.SendJson<WebShopPayload, OrderMetrics>("MyHost", orderMetrics);
```

### Sending multiple items

```csharp
using ZabbixSender.Async;

var data = new[]
{
    new SendData { Host = "MyHost", Key = "cpu.usage", Value = "45.2" },
    new SendData { Host = "MyHost", Key = "mem.free", Value = "2048" },
};

await sender.Send(data);
```

### Configuration

```csharp
var sender = new SecureSender(
    zabbixServer: "zabbix.example.com",
    credentials: new CredentialsPreSharedKey("id", "key"),
    port: 10051,        // default: 10051
    timeout: 5000,      // milliseconds, default: 500
    bufferSize: 4096);  // bytes, default: 1024
```

## API

| Method | Description |
|---|---|
| `Send(host, key, value)` | send a single value |
| `Send(params SendData[])` | send multiple items |
| `Send(IEnumerable<SendData>, ct)` | send with cancellation |
| `SendJson<T>(host, payload)` | serialize to camelCase JSON, key derived from class name |
| `SendJson<TPayload, TMetrics>(host, metrics)` | send one section, wrapped in payload structure |
| `SendJson<T>(host, key, payload)` | serialize with explicit key |

| Credentials | Use case |
|---|---|
| `CredentialsPreSharedKey(identity, hexKey)` | TLS-PSK (most common) |
| `CredentialsPreSharedKey(identity, byte[] key)` | TLS-PSK with raw bytes |
| `CredentialsCertificate(certPath, keyPath)` | TLS certificate (PEM files) |
| `CredentialsCertificate(certPath, keyPath, verifyServer)` | with optional server verification |

## Why this exists

`ZabbixSender.Async` only supports unencrypted connections. .NET's `SslStream` doesn't support PSK cipher suites, so BouncyCastle is needed.

This package reuses `ISender`, `IFormatter`, `SendData`, and `SenderResponse` from the base library. Only the TCP+TLS connection lifecycle is reimplemented.

## Dependencies

- [ZabbixSender.Async](https://www.nuget.org/packages/ZabbixSender.Async) 1.3.0
- [BouncyCastle.Cryptography](https://www.nuget.org/packages/BouncyCastle.Cryptography) 2.6.2

## License

[MIT](https://github.com/arendvw/ZabbixSender.Secure/blob/main/LICENSE)
