using System;
using System.IO;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.X509;

namespace ZabbixSender.Secure;

/// <summary>
/// TLS certificate credentials for Zabbix connections.
/// </summary>
public class CredentialsCertificate : IZabbixSslCredentials
{
    /// <summary>
    /// Load credentials from PEM-encoded certificate and private key files.
    /// </summary>
    public CredentialsCertificate(string certPath, string keyPath, bool verifyServerCertificate = true)
    {
        if (certPath == null) throw new ArgumentNullException(nameof(certPath));
        if (keyPath == null) throw new ArgumentNullException(nameof(keyPath));
        if (!File.Exists(certPath)) throw new FileNotFoundException("Certificate file not found.", certPath);
        if (!File.Exists(keyPath)) throw new FileNotFoundException("Private key file not found.", keyPath);

        using (var reader = new StreamReader(certPath))
        {
            var pemReader = new PemReader(reader);
            Certificate = (X509Certificate)pemReader.ReadObject();
        }

        using (var reader = new StreamReader(keyPath))
        {
            var pemReader = new PemReader(reader);
            var obj = pemReader.ReadObject();
            PrivateKey = obj is AsymmetricCipherKeyPair pair ? pair.Private : (AsymmetricKeyParameter)obj;
        }

        VerifyServerCertificate = verifyServerCertificate;
    }

    public X509Certificate Certificate { get; }
    public AsymmetricKeyParameter PrivateKey { get; }
    public bool VerifyServerCertificate { get; }
}