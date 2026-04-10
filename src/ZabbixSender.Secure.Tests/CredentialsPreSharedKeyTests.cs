using System;
using NUnit.Framework;
using Shouldly;

namespace ZabbixSender.Secure.Tests
{
    [TestFixture]
    public class CredentialsPreSharedKeyTests
    {
        [Test]
        public void ShouldStoreIdentityAndKeyFromBytes()
        {
            var key = new byte[] { 0xAB, 0xCD, 0xEF };
            var creds = new CredentialsPreSharedKey("MyIdentity", key);

            creds.Identity.ShouldBe("MyIdentity");
            creds.Key.ShouldBe(new byte[] { 0xAB, 0xCD, 0xEF });
        }

        [Test]
        public void ShouldParseHexKey()
        {
            var creds = new CredentialsPreSharedKey("MyIdentity", "abcdef0123456789");

            creds.Identity.ShouldBe("MyIdentity");
            creds.Key.ShouldBe(new byte[] { 0xAB, 0xCD, 0xEF, 0x01, 0x23, 0x45, 0x67, 0x89 });
        }

        [Test]
        public void ShouldRejectNullIdentity()
        {
            Should.Throw<ArgumentNullException>(() => new CredentialsPreSharedKey(null, new byte[] { 0x01 }));
        }

        [Test]
        public void ShouldRejectEmptyIdentity()
        {
            Should.Throw<ArgumentException>(() => new CredentialsPreSharedKey("", new byte[] { 0x01 }));
        }

        [Test]
        public void ShouldRejectNullKey()
        {
            Should.Throw<ArgumentNullException>(() => new CredentialsPreSharedKey("id", (byte[])null));
        }

        [Test]
        public void ShouldRejectEmptyKey()
        {
            Should.Throw<ArgumentException>(() => new CredentialsPreSharedKey("id", new byte[0]));
        }

        [Test]
        public void ShouldRejectOddLengthHexKey()
        {
            Should.Throw<ArgumentException>(() => new CredentialsPreSharedKey("id", "abc"));
        }

        [Test]
        public void ShouldRejectInvalidHexKey()
        {
            Should.Throw<FormatException>(() => new CredentialsPreSharedKey("id", "zzzz"));
        }
    }
}
