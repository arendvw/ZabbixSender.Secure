using System.Security.Cryptography;
using System.Text;

namespace ZabbixSender.Template;

public static class UuidGenerator
{
    public static string Generate(string templateName, string itemKey)
    {
        var input = $"{templateName}/{itemKey}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));

        // Set version 4 (byte 6, high nibble = 0100)
        hash[6] = (byte)((hash[6] & 0x0F) | 0x40);
        // Set variant (byte 8, high bits = 10xx)
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return Convert.ToHexStringLower(hash);
    }
}
