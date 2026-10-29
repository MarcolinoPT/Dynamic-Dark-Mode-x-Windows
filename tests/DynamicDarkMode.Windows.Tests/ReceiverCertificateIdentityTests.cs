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
            bool firstIdentityHasPrivateKey;
            string reloadedFingerprint;
            bool reloadedIdentityHasPrivateKey;

            // Act
            using (ReceiverCertificateIdentity identity = ReceiverCertificateIdentity.LoadOrCreate(
                storeName: "My",
                certificateSubject: subject))
            {
                firstFingerprint = identity.Fingerprint;
                firstIdentityHasPrivateKey = identity.Certificate.HasPrivateKey;
            }

            using (ReceiverCertificateIdentity reloaded = ReceiverCertificateIdentity.LoadOrCreate(
                storeName: "My",
                certificateSubject: subject))
            {
                reloadedFingerprint = reloaded.Fingerprint;
                reloadedIdentityHasPrivateKey = reloaded.Certificate.HasPrivateKey;
            }

            // Assert
            firstFingerprint.ShouldSatisfyAllConditions(
                () => firstFingerprint.ShouldNotBeNullOrWhiteSpace(),
                () => firstIdentityHasPrivateKey.ShouldBeTrue(),
                () => reloadedFingerprint.ShouldBe(firstFingerprint),
                () => reloadedIdentityHasPrivateKey.ShouldBeTrue());
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
            Exception? exception = Record.Exception(() => ReceiverCertificateIdentity.LoadOrCreate(
                storeName: "My",
                certificateSubject: subject));
            int certificateCount = CountCertificates(subject: subject);

            // Assert
            subject.ShouldSatisfyAllConditions(
                () => exception.ShouldNotBeNull(),
                () => exception.ShouldBeOfType<CryptographicException>(),
                () => certificateCount.ShouldBe(2));
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
            Exception? exception = Record.Exception(() => ReceiverCertificateIdentity.LoadOrCreate(
                storeName: "My",
                certificateSubject: subject));
            int certificateCount = CountCertificates(subject: subject);
            string[] remainingThumbprints = GetCertificateThumbprints(subject: subject);

            // Assert
            subject.ShouldSatisfyAllConditions(
                () => exception.ShouldNotBeNull(),
                () => exception.ShouldBeOfType<CryptographicException>(),
                () => certificateCount.ShouldBe(1),
                () => remainingThumbprints.ShouldContain(originalThumbprint));
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
            Exception? exception = Record.Exception(() => ReceiverCertificateIdentity.LoadOrCreate(
                storeName: "My",
                certificateSubject: subject));
            int certificateCount = CountCertificates(subject: subject);
            string[] remainingThumbprints = GetCertificateThumbprints(subject: subject);

            // Assert
            subject.ShouldSatisfyAllConditions(
                () => exception.ShouldNotBeNull(),
                () => exception.ShouldBeOfType<CryptographicException>(),
                () => certificateCount.ShouldBe(1),
                () => remainingThumbprints.ShouldContain(originalThumbprint));
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
            int certificateCount = CountCertificates(subject: subject);
            string[] storedThumbprints = GetCertificateThumbprints(subject: subject);
            int distinctFingerprintCount = fingerprints.Distinct().Count();

            // Assert
            fingerprints.ShouldSatisfyAllConditions(
                () => fingerprints.ShouldNotBeEmpty(),
                () => distinctFingerprintCount.ShouldBe(1),
                () => certificateCount.ShouldBe(1),
                () => storedThumbprints.Length.ShouldBe(1));
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

        X509Certificate2Collection matches = store.Certificates.Find(
            findType: X509FindType.FindBySubjectDistinguishedName,
            findValue: subject,
            validOnly: false);

        return matches.Count;
    }

    private static string[] GetCertificateThumbprints(string subject)
    {
        using var store = new X509Store(
            storeName: "My",
            storeLocation: StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        X509Certificate2Collection matches = store.Certificates.Find(
            findType: X509FindType.FindBySubjectDistinguishedName,
            findValue: subject,
            validOnly: false);

        return matches
            .Cast<X509Certificate2>()
            .Select(certificate => certificate.Thumbprint)
            .ToArray();
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
