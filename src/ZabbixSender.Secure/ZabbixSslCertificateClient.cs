using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace ZabbixSender.Secure;

internal class ZabbixSslCertificateClient : DefaultTlsClient
{
    private readonly CredentialsCertificate _credentials;
    private readonly BcTlsCrypto _crypto;

    internal ZabbixSslCertificateClient(CredentialsCertificate credentials)
        : base(new BcTlsCrypto(new SecureRandom()))
    {
        _credentials = credentials;
        _crypto = (BcTlsCrypto)Crypto;
    }

    public override TlsAuthentication GetAuthentication()
    {
        return new ZabbixTlsAuthentication(_credentials, _crypto, m_context);
    }

    private class ZabbixTlsAuthentication : TlsAuthentication
    {
        private readonly CredentialsCertificate _credentials;
        private readonly BcTlsCrypto _crypto;
        private readonly TlsContext _context;

        internal ZabbixTlsAuthentication(CredentialsCertificate credentials, BcTlsCrypto crypto, TlsContext context)
        {
            _credentials = credentials;
            _crypto = crypto;
            _context = context;
        }

        public void NotifyServerCertificate(TlsServerCertificate serverCertificate)
        {
            if (_credentials.VerifyServerCertificate)
            {
                if (serverCertificate?.Certificate == null || serverCertificate.Certificate.IsEmpty)
                    throw new TlsFatalAlert(AlertDescription.bad_certificate);
            }
        }

        public TlsCredentials GetClientCredentials(CertificateRequest certificateRequest)
        {
            var bcCert = new BcTlsCertificate(_crypto, _credentials.Certificate.GetEncoded());
            var certStructure = new Certificate(new TlsCertificate[] { bcCert });

            var privateKeyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(_credentials.PrivateKey);
            var sigAndHash = new SignatureAndHashAlgorithm(HashAlgorithm.sha256, SignatureAlgorithm.rsa);

            return new BcDefaultTlsCredentialedSigner(
                new TlsCryptoParameters(_context),
                _crypto,
                _credentials.PrivateKey,
                certStructure,
                sigAndHash);
        }
    }
}