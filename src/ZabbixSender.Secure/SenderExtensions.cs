using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZabbixSender.Async;

namespace ZabbixSender.Secure;

public static class SenderExtensions
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] SuffixesToStrip = ["Payload", "Metrics", "Data", "Info", "Status"];
    private static readonly ConcurrentDictionary<(Type, Type), (string Prefix, string PropertyName)> Cache = new();

    /// <summary>
    /// Send the full payload. Prefix is derived from the payload class name
    /// (e.g. WebShopPayload -> "webshop", trapper key -> "webshop.data").
    /// </summary>
    public static Task<SenderResponse> SendJson<TPayload>(
        this ISender sender,
        string host,
        TPayload payload,
        CancellationToken cancellationToken = default)
    {
        var prefix = DerivePrefix(typeof(TPayload));
        var json = JsonSerializer.Serialize(payload, CamelCase);
        return sender.Send(host, $"{prefix}.data", json, cancellationToken);
    }

    /// <summary>
    /// Send a single metrics section. Prefix and JSON wrapping are derived
    /// from the type parameters.
    ///
    /// Example:
    ///   await sender.SendJson&lt;WebShopPayload, ResponseMetrics&gt;("host", metrics);
    ///   // sends key="webshop.data", value={"response":{"apiLatency":120.5}}
    /// </summary>
    public static Task<SenderResponse> SendJson<TPayload, TMetrics>(
        this ISender sender,
        string host,
        TMetrics metrics,
        CancellationToken cancellationToken = default)
    {
        var (prefix, propertyName) = Resolve<TPayload, TMetrics>();
        var wrapper = new Dictionary<string, object> { [propertyName] = metrics };
        var json = JsonSerializer.Serialize(wrapper, CamelCase);
        return sender.Send(host, $"{prefix}.data", json, cancellationToken);
    }

    /// <summary>
    /// Send the full payload with an explicit trapper key.
    /// </summary>
    public static Task<SenderResponse> SendJson<T>(
        this ISender sender,
        string host,
        string key,
        T payload,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(payload, CamelCase);
        return sender.Send(host, key, json, cancellationToken);
    }

    /// <summary>
    /// Send the full payload with an explicit trapper key and custom serialization options.
    /// </summary>
    public static Task<SenderResponse> SendJson<T>(
        this ISender sender,
        string host,
        string key,
        T payload,
        JsonSerializerOptions options,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(payload, options);
        return sender.Send(host, key, json, cancellationToken);
    }

    private static (string Prefix, string PropertyName) Resolve<TPayload, TMetrics>()
    {
        var key = (typeof(TPayload), typeof(TMetrics));
        return Cache.GetOrAdd(key, static k =>
        {
            var (payloadType, metricsType) = k;

            // direct match: property of type TMetrics
            var prop = payloadType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => p.PropertyType == metricsType);

            // dictionary match: property of type Dictionary<string, TMetrics>
            prop ??= payloadType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p =>
                    p.PropertyType.IsGenericType
                    && p.PropertyType.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                    && p.PropertyType.GetGenericArguments()[1] == metricsType);

            if (prop == null)
                throw new ArgumentException(
                    $"{payloadType.Name} has no property of type {metricsType.Name} " +
                    $"or Dictionary<string, {metricsType.Name}>");

            var prefix = DerivePrefix(payloadType);
            var name = prop.Name;
            var camelName = char.ToLowerInvariant(name[0]) + name[1..];
            return (prefix, camelName);
        });
    }

    private static string DerivePrefix(Type type)
    {
        var name = type.Name;
        foreach (var suffix in SuffixesToStrip)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && name.Length > suffix.Length)
            {
                name = name[..^suffix.Length];
                break;
            }
        }
        return name.ToLowerInvariant();
    }
}
