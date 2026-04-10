using System;
using System.IO;
using NUnit.Framework;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Shouldly;

namespace ZabbixSender.Secure.Tests
{
    [TestFixture]
    public class CredentialsCertificateTests
    {
        private string _certPath;
        private string _keyPath;

        [OneTimeSetUp]
        public void Setup()
        {
            var keyGen = new RsaKeyPairGenerator();
            keyGen.Init(new KeyGenerationParameters(new SecureRandom(), 2048));
            var keyPair = keyGen.GenerateKeyPair();

            var certGen = new X509V3CertificateGenerator();
            certGen.SetSerialNumber(BigInteger.ProbablePrime(120, new SecureRandom()));
            certGen.SetIssuerDN(new X509Name("CN=Test"));
            certGen.SetSubjectDN(new X509Name("CN=Test"));
            certGen.SetNotBefore(DateTime.UtcNow.AddDays(-1));
            certGen.SetNotAfter(DateTime.UtcNow.AddYears(1));
            certGen.SetPublicKey(keyPair.Public);
            var cert = certGen.Generate(new Asn1SignatureFactory("SHA256WITHRSA", keyPair.Private));

            _certPath = Path.GetTempFileName();
            _keyPath = Path.GetTempFileName();

            using (var writer = new StreamWriter(_certPath))
                new PemWriter(writer).WriteObject(cert);

            using (var writer = new StreamWriter(_keyPath))
                new PemWriter(writer).WriteObject(keyPair.Private);
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            if (File.Exists(_certPath)) File.Delete(_certPath);
            if (File.Exists(_keyPath)) File.Delete(_keyPath);
        }

        [Test]
        public void ShouldLoadFromPemFiles()
        {
            var creds = new CredentialsCertificate(_certPath, _keyPath);

            creds.Certificate.ShouldNotBeNull();
            creds.PrivateKey.ShouldNotBeNull();
            creds.VerifyServerCertificate.ShouldBeTrue();
        }

        [Test]
        public void ShouldRespectVerifyFlag()
        {
            var creds = new CredentialsCertificate(_certPath, _keyPath, verifyServerCertificate: false);
            creds.VerifyServerCertificate.ShouldBeFalse();
        }

        [Test]
        public void ShouldRejectNullCertPath()
        {
            Should.Throw<ArgumentNullException>(() => new CredentialsCertificate(null, _keyPath));
        }

        [Test]
        public void ShouldRejectNullKeyPath()
        {
            Should.Throw<ArgumentNullException>(() => new CredentialsCertificate(_certPath, null));
        }

        [Test]
        public void ShouldRejectMissingCertFile()
        {
            Should.Throw<FileNotFoundException>(() => new CredentialsCertificate("/nonexistent/cert.pem", _keyPath));
        }

        [Test]
        public void ShouldRejectMissingKeyFile()
        {
            Should.Throw<FileNotFoundException>(() => new CredentialsCertificate(_certPath, "/nonexistent/key.pem"));
        }
    }
}
