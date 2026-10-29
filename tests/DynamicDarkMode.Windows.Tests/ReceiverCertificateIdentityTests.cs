using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DynamicDarkMode.Windows;
using Shouldly;
using Xunit;

namespace DynamicDarkMode.Windows.Tests;

public sealed class ReceiverCertificateIdentityTests
{
    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Persistence)]
    public void CreateThenReloadPreservesFingerprint()
    {
        // Arrange
        string subject = CreateUniqueSubject();

        try
        {
            string firstFingerprint;

            // Act
            using (ReceiverCertificateIdentity identity = ReceiverCertificateIdentity.LoadOrCreate(
                storeName: "My",
                certificateSubject: subject))
            {
                firstFingerprint = identity.Fingerprint;

                // Assert
                identity.Certificate.HasPrivateKey.ShouldBeTrue();
                firstFingerprint.ShouldNotBeNullOrWhiteSpace();
            }

            // Act
            using ReceiverCertificateIdentity reloaded = ReceiverCertificateIdentity.LoadOrCreate(
                storeName: "My",
                certificateSubject: subject);

            // Assert
            reloaded.Fingerprint.ShouldBe(firstFingerprint);
            reloaded.Certificate.HasPrivateKey.ShouldBeTrue();
        }
        finally
        {
            RemoveCertificates(subject: subject);
        }
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Security)]
    public void RejectsDuplicateIdentities()
    {
        // Arrange
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

            // Act
            Action loadIdentity = () => ReceiverCertificateIdentity.LoadOrCreate(
                storeName: "My",
                certificateSubject: subject);

            // Assert
            Should.Throw<CryptographicException>(loadIdentity);
            CountCertificates(subject: subject).ShouldBe(2);
        }
        finally
        {
            RemoveCertificates(subject: subject);
        }
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Security)]
    public void RejectsExpiredIdentityWithoutReplacingIt()
    {
        // Arrange
        string subject = CreateUniqueSubject();

        try
        {
            using X509Certificate2 expired = CreateCertificate(
                subject: subject,
                notBefore: DateTimeOffset.UtcNow.AddDays(-3),
                notAfter: DateTimeOffset.UtcNow.AddDays(-2));
            AddCertificate(certificate: expired);
            string originalThumbprint = expired.Thumbprint;

            // Act
            Action loadIdentity = () => ReceiverCertificateIdentity.LoadOrCreate(
                storeName: "My",
                certificateSubject: subject);

            // Assert
            Should.Throw<CryptographicException>(loadIdentity);
            CountCertificates(subject: subject).ShouldBe(1);
            GetCertificateThumbprints(subject: subject).ShouldContain(originalThumbprint);
        }
        finally
        {
            RemoveCertificates(subject: subject);
        }
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Security)]
    public void RejectsIdentityWithoutPrivateKeyWithoutReplacingIt()
    {
        // Arrange
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
            string originalThumbprint = withoutPrivateKey.Thumbprint;

            // Act
            Action loadIdentity = () => ReceiverCertificateIdentity.LoadOrCreate(
                storeName: "My",
                certificateSubject: subject);

            // Assert
            Should.Throw<CryptographicException>(loadIdentity);
            CountCertificates(subject: subject).ShouldBe(1);
            GetCertificateThumbprints(subject: subject).ShouldContain(originalThumbprint);
        }
        finally
        {
            RemoveCertificates(subject: subject);
        }
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Concurrency)]
    public async Task ConcurrentFirstLoadsReuseOneIdentity()
    {
        // Arrange
        string subject = CreateUniqueSubject();

        try
        {
            Task<string>[] loads = Enumerable.Range(start: 0, count: 4)
                .Select(_ => Task.Run(function: () =>
                {
                    using ReceiverCertificateIdentity identity = ReceiverCertificateIdentity.LoadOrCreate(
                        storeName: "My",
                        certificateSubject: subject);
                    return identity.Fingerprint;
                }))
                .ToArray();

            // Act
            string[] fingerprints = await Task.WhenAll(loads);

            // Assert
            fingerprints.ShouldNotBeEmpty();
            fingerprints.Distinct().Count().ShouldBe(1);
            CountCertificates(subject: subject).ShouldBe(1);
            GetCertificateThumbprints(subject: subject).Length.ShouldBe(1);
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
        // Arrange
        using RSA key = RSA.Create(keySizeInBits: 2048);
        var request = new CertificateRequest(
            subjectName: subject,
            key: key,
            hashAlgorithm: HashAlgorithmName.SHA256,
            padding: RSASignaturePadding.Pkcs1);

        // Act
        X509Certificate2 certificate = request.CreateSelfSigned(
            notBefore: notBefore,
            notAfter: notAfter);

        // Assert
        certificate.Subject.ShouldBe(subject);
        certificate.HasPrivateKey.ShouldBeTrue();
        return certificate;
    }

    private static void AddCertificate(X509Certificate2 certificate)
    {
        // Arrange
        using var store = new X509Store(
            storeName: "My",
            storeLocation: StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);

        // Act
        store.Add(certificate: certificate);

        // Assert
        CountCertificates(subject: certificate.Subject).ShouldBeGreaterThan(0);
    }

    private static int CountCertificates(string subject)
    {
        // Arrange
        using var store = new X509Store(
            storeName: "My",
            storeLocation: StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        // Act
        X509Certificate2Collection matches = store.Certificates.Find(
            findType: X509FindType.FindBySubjectDistinguishedName,
            findValue: subject,
            validOnly: false);

        // Assert
        matches.ShouldNotBeNull();
        return matches.Count;
    }

    private static string[] GetCertificateThumbprints(string subject)
    {
        // Arrange
        using var store = new X509Store(
            storeName: "My",
            storeLocation: StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        // Act
        X509Certificate2Collection matches = store.Certificates.Find(
            findType: X509FindType.FindBySubjectDistinguishedName,
            findValue: subject,
            validOnly: false);

        // Assert
        matches.ShouldNotBeNull();
        return matches
            .Cast<X509Certificate2>()
            .Select(certificate => certificate.Thumbprint)
            .ToArray();
    }

    private static void RemoveCertificates(string subject)
    {
        // Arrange
        using var store = new X509Store(
            storeName: "My",
            storeLocation: StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);

        X509Certificate2Collection matches = store.Certificates.Find(
            findType: X509FindType.FindBySubjectDistinguishedName,
            findValue: subject,
            validOnly: false);

        // Act
        foreach (X509Certificate2 certificate in matches)
        {
            using (certificate)
            {
                store.Remove(certificate: certificate);
            }
        }

        // Assert
        CountCertificates(subject: subject).ShouldBe(0);
    }
}
