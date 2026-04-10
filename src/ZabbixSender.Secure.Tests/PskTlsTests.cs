using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;
using Shouldly;

namespace ZabbixSender.Secure.Tests
{
    [TestFixture]
    public class PskTlsTests
    {
        private const string TestIdentity = "TestPSK";
        private static readonly byte[] TestKey = new byte[]
        {
            0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10
        };

        [Test]
        public async Task ShouldSendDataOverPskTls()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var serverTask = Task.Run(() =>
            {
                using var client = listener.AcceptTcpClient();
                listener.Stop();
                using var networkStream = client.GetStream();

                var serverProtocol = new TlsServerProtocol(networkStream);
                serverProtocol.Accept(new TestPskTlsServer(TestIdentity, TestKey));

                var tlsStream = serverProtocol.Stream;

                // Read Zabbix request: 5-byte header + 8-byte length + JSON payload
                var header = new byte[5];
                ReadFully(tlsStream, header);
                Encoding.ASCII.GetString(header, 0, 4).ShouldBe("ZBXD");

                var lengthBytes = new byte[8];
                ReadFully(tlsStream, lengthBytes);
                var length = (int)BitConverter.ToInt64(lengthBytes, 0);

                var payload = new byte[length];
                ReadFully(tlsStream, payload);

                // Send a valid Zabbix response
                var response = Encoding.ASCII.GetBytes(
                    "{\"response\":\"success\",\"info\":\"processed: 1; failed: 0; total: 1; seconds spent: 0.000100\"}");
                var responseHeader = Encoding.ASCII.GetBytes("ZBXD\x01");
                var responseLength = BitConverter.GetBytes((long)response.Length);

                tlsStream.Write(responseHeader, 0, responseHeader.Length);
                tlsStream.Write(responseLength, 0, responseLength.Length);
                tlsStream.Write(response, 0, response.Length);
                tlsStream.Flush();

                serverProtocol.Close();
            });

            var credentials = new CredentialsPreSharedKey(TestIdentity, TestKey);
            var sender = new SecureSender("127.0.0.1", credentials, port, timeout: 5000);
            var result = await sender.Send("TestHost", "test.key", "hello");

            await serverTask;

            result.IsSuccess.ShouldBeTrue();
            result.ParseInfo().Processed.ShouldBe(1);
        }

        private static void ReadFully(Stream stream, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read <= 0) throw new EndOfStreamException();
                offset += read;
            }
        }
    }

    internal class TestPskTlsServer : PskTlsServer
    {
        internal TestPskTlsServer(string expectedIdentity, byte[] key)
            : base(
                new BcTlsCrypto(new SecureRandom()),
                new TestPskIdentityManager(expectedIdentity, key))
        {
        }
    }

    internal class TestPskIdentityManager : TlsPskIdentityManager
    {
        private readonly string _expectedIdentity;
        private readonly byte[] _key;

        internal TestPskIdentityManager(string expectedIdentity, byte[] key)
        {
            _expectedIdentity = expectedIdentity;
            _key = key;
        }

        public byte[] GetHint() => null;

        public byte[] GetPsk(byte[] identity)
        {
            var id = Encoding.UTF8.GetString(identity);
            return id == _expectedIdentity ? _key : null;
        }
    }
}
