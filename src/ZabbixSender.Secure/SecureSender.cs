using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Org.BouncyCastle.Tls;
using ZabbixSender.Async;

namespace ZabbixSender.Secure;

/// <summary>
/// Sends data to Zabbix over TLS (PSK or certificate).
/// Drop-in replacement for <see cref="Sender"/>.
/// </summary>
public class SecureSender(
    string zabbixServer,
    IZabbixSslCredentials credentials,
    int port = 10051,
    int timeout = 500,
    int bufferSize = 1024)
    : ISender
{
    private readonly IZabbixSslCredentials _credentials =
        credentials ?? throw new ArgumentNullException(nameof(credentials));

    public string ZabbixServer { get; } = zabbixServer ?? throw new ArgumentNullException(nameof(zabbixServer));
    public int Port { get; } = port;

    public Task<SenderResponse> Send(params SendData[] data)
    {
        return Send(data, CancellationToken.None);
    }

    public Task<SenderResponse> Send(string host, string key, string value,
        CancellationToken cancellationToken = default)
    {
        return Send([new SendData { Host = host, Key = key, Value = value }], cancellationToken);
    }

    public async Task<SenderResponse> Send(IEnumerable<SendData> data,
        CancellationToken cancellationToken = default)
    {
        using var tcpClient = new TcpClient();
        tcpClient.SendTimeout = timeout;
        tcpClient.ReceiveTimeout = timeout;
        tcpClient.SendBufferSize = bufferSize;
        tcpClient.ReceiveBufferSize = bufferSize;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        await tcpClient.ConnectAsync(ZabbixServer, Port, cts.Token);

        var networkStream = tcpClient.GetStream();
        var tlsProtocol = new TlsClientProtocol(networkStream);

        try
        {
            TlsClient tlsClient = _credentials switch
            {
                CredentialsPreSharedKey psk => new ZabbixSslPskClient(psk),
                CredentialsCertificate cert => new ZabbixSslCertificateClient(cert),
                _ => throw new ArgumentException($"Unsupported credential type: {_credentials.GetType()}")
            };

            tlsProtocol.Connect(tlsClient);

            var tlsStream = tlsProtocol.Stream;
            var formatter = new Formatter(bufferSize);

            // Write request through TLS
            await formatter.WriteRequestAsync(tlsStream, data, cts.Token);
            await tlsStream.FlushAsync(cts.Token);

            // Read the full response into a buffer, then parse.
            // TLS stream may deliver data in small chunks, and
            // Formatter.ReadResponse assumes full reads.
            var responseBuffer = new MemoryStream();

            // Read header (5) + length (8) = 13 bytes
            var header = new byte[13];
            ReadFully(tlsStream, header);
            responseBuffer.Write(header, 0, header.Length);

            // Extract payload length from bytes 5-12
            var payloadLength = BitConverter.ToInt64(header, 5);
            var payload = new byte[payloadLength];
            ReadFully(tlsStream, payload);
            responseBuffer.Write(payload, 0, payload.Length);

            responseBuffer.Seek(0, SeekOrigin.Begin);
            return await formatter.ReadResponseAsync(responseBuffer, cts.Token);
        }
        finally
        {
            tlsProtocol.Close();
        }
    }

    private static void ReadFully(Stream stream, byte[] buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = stream.Read(buffer, offset, buffer.Length - offset);
            if (read <= 0) throw new EndOfStreamException();
            offset += read;
        }
    }
}