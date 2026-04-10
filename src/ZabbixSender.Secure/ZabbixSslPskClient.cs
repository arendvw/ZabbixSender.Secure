using System.Text;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace ZabbixSender.Secure;

internal class ZabbixSslPskClient : PskTlsClient
{
    internal ZabbixSslPskClient(CredentialsPreSharedKey credentialsPreSharedKey)
        : base(
            new BcTlsCrypto(new SecureRandom()),
            new BasicTlsPskIdentity(
                Encoding.UTF8.GetBytes(credentialsPreSharedKey.Identity),
                credentialsPreSharedKey.Key))
    {
    }
}