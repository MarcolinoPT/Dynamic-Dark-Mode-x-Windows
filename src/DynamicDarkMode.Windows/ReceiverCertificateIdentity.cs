using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DynamicDarkMode.Windows;

/// <summary>
/// Loads or creates the Windows receiver's persistent TLS identity in the current
/// user's Personal certificate store. An existing but unusable identity is never
/// silently replaced: that condition requires deliberate recovery/re-pairing.
/// </summary>
internal sealed class ReceiverCertificateIdentity : IDisposable
{
    private const string CertificateSubject = "CN=Dynamic Dark Mode Windows Receiver";
    private const string StoreName = "My";
    private static readonly TimeSpan CertificateLifetime = TimeSpan.FromDays(365 * 5);

    private ReceiverCertificateIdentity(X509Certificate2 certificate)
    {
        Certificate = certificate;
        Fingerprint = Convert.ToHexString(SHA256.HashData(certificate.RawData));
    }

    public X509Certificate2 Certificate { get; }

    /// <summary>Full SHA-256 fingerprint of the certificate's DER encoding, uppercase hex.</summary>
    public string Fingerprint { get; }

    public static ReceiverCertificateIdentity LoadOrCreate()
    {
        using var store = new X509Store(StoreName, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);

        var matches = store.Certificates
            .Find(X509FindType.FindBySubjectDistinguishedName, CertificateSubject, validOnly: false)
            .Cast<X509Certificate2>()
            .ToArray();

        if (matches.Length > 1)
        {
            throw new CryptographicException(
                "Multiple receiver identities exist in the current user's Personal certificate store.");
        }

        if (matches.Length == 1)
        {
            X509Certificate2 existing = matches[0];
            ValidateExistingIdentity(existing);
            return new ReceiverCertificateIdentity(new X509Certificate2(existing));
        }

        using X509Certificate2 created = CreatePersistentIdentity();
        store.Add(created);
        return new ReceiverCertificateIdentity(new X509Certificate2(created));
    }

    private static void ValidateExistingIdentity(X509Certificate2 certificate)
    {
        DateTime now = DateTime.UtcNow;
        if (certificate.NotBefore.ToUniversalTime() > now
            || certificate.NotAfter.ToUniversalTime() <= now)
        {
            throw new CryptographicException(
                "The existing receiver identity is outside its validity period; refusing to replace it.");
        }

        if (!certificate.HasPrivateKey)
        {
            throw new CryptographicException(
                "The existing receiver identity has no accessible private key; refusing to replace it.");
        }

        // Prove that the current Windows user can open the private key. Never
        // replace a certificate when its key is inaccessible or missing.
        using RSA? privateKey = certificate.GetRSAPrivateKey();
        if (privateKey is null)
        {
            throw new CryptographicException(
                "The existing receiver identity's RSA private key is unavailable; refusing to replace it.");
        }
    }

    private static X509Certificate2 CreatePersistentIdentity()
    {
        using RSA key = RSA.Create(3072);
        var request = new CertificateRequest(
            CertificateSubject,
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: false,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: true));

        var serverAuthentication = new Oid("1.3.6.1.5.5.7.3.1", "Server Authentication");
        var enhancedKeyUsage = new OidCollection { serverAuthentication };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(enhancedKeyUsage, critical: true));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        using X509Certificate2 generated = request.CreateSelfSigned(
            now.AddMinutes(-5),
            now.Add(CertificateLifetime));

        // CreateSelfSigned's in-memory key is exported only long enough to import
        // it into the current user's persistent key store. UserKeySet keeps the
        // private key in the companion user's profile; no machine-wide key is used.
        string temporaryPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        byte[] pfx = generated.Export(X509ContentType.Pfx, temporaryPassword);
        try
        {
            return new X509Certificate2(
                pfx,
                temporaryPassword,
                X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }
    }

    public void Dispose() => Certificate.Dispose();
}
