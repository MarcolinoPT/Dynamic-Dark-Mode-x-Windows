using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DynamicDarkMode.Windows;
using Shouldly;
using Xunit;

namespace DynamicDarkMode.Windows.Tests;

public sealed class ReceiverCertificateIdentityTests
{
    [Fact]
    public void CreateThenReloadPreservesFingerprint()
    {
        string subject = CreateUniqueSubject();

        try
        {
            string firstFingerprint;
            using (ReceiverCertificateIdentity identity = ReceiverCertificateIdentity.LoadOrCreate(
                storeName: "My",
                certificateSubject: subject))
            {
                firstFingerprint = identity.Fingerprint;
                identity.Certificate.HasPrivateKey.ShouldBeTrue();
            }

            using ReceiverCertificateIdentity reloaded = ReceiverCertificateIdentity.LoadOrCreate(
                storeName: "My",
                certificateSubject: subject);

            reloaded.Fingerprint.ShouldBe(firstFingerprint);
        }
        finally
        {
            RemoveCertificates(subject: subject);
        }
    }

    [Fact]
    public void RejectsDuplicateIdentities()
    {
        string subject = CreateUniqueSubject();

        try
        {
            using X509Certificate2 first = CreateCertificate(
                subject: subject,
                notBefore: DateTimeOffset.UtcNow.AddDays(-1),
                notAfter: DateTimeOffset.UtcNow.AddDays(1));
            using X509Certificate2 second = CreateCertificate(
                subject: subject,
                notBefore: DateTimeOffset.UtcNow.AddDays(-1),
                notAfter: DateTimeOffset.UtcNow.AddDays(1));
            AddCertificate(certificate: first);
            AddCertificate(certificate: second);

            Should.Throw<CryptographicException>(() =>
                ReceiverCertificateIdentity.LoadOrCreate(
                    storeName: "My",
                    certificateSubject: subject));
        }
        finally
        {
            RemoveCertificates(subject: subject);
        }
    }

    [Fact]
    public void RejectsExpiredIdentityWithoutReplacingIt()
    {
        string subject = CreateUniqueSubject();

        try
        {
            using X509Certificate2 expired = CreateCertificate(
                subject: subject,
                notBefore: DateTimeOffset.UtcNow.AddDays(-3),
                notAfter: DateTimeOffset.UtcNow.AddDays(-2));
            AddCertificate(certificate: expired);

            Should.Throw<CryptographicException>(() =>
                ReceiverCertificateIdentity.LoadOrCreate(
                    storeName: "My",
                    certificateSubject: subject));

            CountCertificates(subject: subject).ShouldBe(1);
        }
        finally
        {
            RemoveCertificates(subject: subject);
        }
    }

    [Fact]
    public void RejectsIdentityWithoutPrivateKeyWithoutReplacingIt()
    {
        string subject = CreateUniqueSubject();

        try
        {
            using X509Certificate2 withPrivateKey = CreateCertificate(
                subject: subject,
                notBefore: DateTimeOffset.UtcNow.AddDays(-1),
                notAfter: DateTimeOffset.UtcNow.AddDays(1));
            using var withoutPrivateKey = new X509Certificate2(
                rawData: withPrivateKey.Export(X509ContentType.Cert));
            AddCertificate(certificate: withoutPrivateKey);

            Should.Throw<CryptographicException>(() =>
                ReceiverCertificateIdentity.LoadOrCreate(
                    storeName: "My",
                    certificateSubject: subject));

            CountCertificates(subject: subject).ShouldBe(1);
        }
        finally
        {
            RemoveCertificates(subject: subject);
        }
    }

    [Fact]
    public async Task ConcurrentFirstLoadsReuseOneIdentity()
    {
        string subject = CreateUniqueSubject();

        try
        {
            Task<string>[] loads = Enumerable.Range(0, 4)
                .Select(_ => Task.Run(() =>
                {
                    using ReceiverCertificateIdentity identity = ReceiverCertificateIdentity.LoadOrCreate(
                        storeName: "My",
                        certificateSubject: subject);
                    return identity.Fingerprint;
                }))
                .ToArray();

            string[] fingerprints = await Task.WhenAll(loads);

            fingerprints.Distinct().Count().ShouldBe(1);
            CountCertificates(subject: subject).ShouldBe(1);
        }
        finally
        {
            RemoveCertificates(subject: subject);
        }
    }

    private static string CreateUniqueSubject() =>
        $"CN=Dynamic Dark Mode Test {Guid.NewGuid():N}";

    private static X509Certificate2 CreateCertificate(
        string subject,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter)
    {
        using RSA key = RSA.Create(keySizeInBits: 2048);
        var request = new CertificateRequest(
            subjectName: subject,
            key: key,
            hashAlgorithm: HashAlgorithmName.SHA256,
            padding: RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(
            notBefore: notBefore,
            notAfter: notAfter);
    }

    private static void AddCertificate(X509Certificate2 certificate)
    {
        using var store = new X509Store(
            storeName: "My",
            storeLocation: StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);
        store.Add(certificate: certificate);
    }

    private static int CountCertificates(string subject)
    {
        using var store = new X509Store(
            storeName: "My",
            storeLocation: StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        return store.Certificates
            .Find(
                findType: X509FindType.FindBySubjectDistinguishedName,
                findValue: subject,
                validOnly: false)
            .Count;
    }

    private static void RemoveCertificates(string subject)
    {
        using var store = new X509Store(
            storeName: "My",
            storeLocation: StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);

        X509Certificate2Collection matches = store.Certificates.Find(
            findType: X509FindType.FindBySubjectDistinguishedName,
            findValue: subject,
            validOnly: false);
        foreach (X509Certificate2 certificate in matches)
        {
            using (certificate)
            {
                store.Remove(certificate: certificate);
            }
        }
    }
}
