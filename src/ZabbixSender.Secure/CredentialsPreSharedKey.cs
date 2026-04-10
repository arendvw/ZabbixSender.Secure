using System;

namespace ZabbixSender.Secure;

/// <summary>
/// TLS-PSK credentials for Zabbix connections.
/// </summary>
public class CredentialsPreSharedKey : IZabbixSslCredentials
{
    /// <summary>
    /// Create PSK credentials from identity and raw key bytes.
    /// </summary>
    public CredentialsPreSharedKey(string identity, byte[] key)
    {
        if (identity == null) throw new ArgumentNullException(nameof(identity));
        if (identity.Length == 0) throw new ArgumentException("Identity must not be empty.", nameof(identity));
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (key.Length == 0) throw new ArgumentException("Key must not be empty.", nameof(key));

        Identity = identity;
        Key = key;
    }

    /// <summary>
    /// Create PSK credentials from identity and hex-encoded key string.
    /// </summary>
    public CredentialsPreSharedKey(string identity, string hexKey)
        : this(identity, ParseHex(hexKey))
    {
    }

    public string Identity { get; }
    public byte[] Key { get; }

    public static bool IsValidHexKey(string hex) =>
        hex != null && hex.Length > 0 && hex.Length % 2 == 0 &&
        hex.AsSpan().IndexOfAnyExcept("0123456789abcdefABCDEF") == -1;

    private static byte[] ParseHex(string hex)
    {
        if (hex == null) throw new ArgumentNullException(nameof(hex));
        if (hex.Length == 0) throw new ArgumentException("Key must not be empty.", nameof(hex));
        if (hex.Length % 2 != 0) throw new ArgumentException("Hex key must have even length.", nameof(hex));

        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }
}