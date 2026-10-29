using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

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

    public static ReceiverCertificateIdentity LoadOrCreate(
        string storeName = StoreName,
        string certificateSubject = CertificateSubject)
    {
        string userSid = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("The current Windows user SID is unavailable.");
        string mutexName = $@"Local\DynamicDarkMode.ReceiverIdentity.{userSid}";

        using var identityMutex = new Mutex(
            initiallyOwned: false,
            name: mutexName);
        bool mutexAcquired = false;

        try
        {
            try
            {
                mutexAcquired = identityMutex.WaitOne();
            }
            catch (AbandonedMutexException)
            {
                // The previous owner exited unexpectedly. The mutex is acquired
                // by this thread, so inspect the store while holding the lock.
                mutexAcquired = true;
            }

            return LoadOrCreateWhileLocked(
                storeName: storeName,
                certificateSubject: certificateSubject);
        }
        finally
        {
            if (mutexAcquired)
            {
                identityMutex.ReleaseMutex();
            }
        }
    }

    private static ReceiverCertificateIdentity LoadOrCreateWhileLocked(
        string storeName,
        string certificateSubject)
    {
        using var store = new X509Store(
            storeName: storeName,
            storeLocation: StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);

        var matches = store.Certificates
            .Find(
                findType: X509FindType.FindBySubjectDistinguishedName,
                findValue: certificateSubject,
                validOnly: false)
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
            ValidateExistingIdentity(certificate: existing);
            return new ReceiverCertificateIdentity(new X509Certificate2(existing));
        }

        using X509Certificate2 created = CreatePersistentIdentity(
            certificateSubject: certificateSubject);
        store.Add(certificate: created);
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

        if (certificate.HasPrivateKey is false)
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

    private static X509Certificate2 CreatePersistentIdentity(string certificateSubject)
    {
        using RSA key = RSA.Create(keySizeInBits: 3072);
        var request = new CertificateRequest(
            subjectName: certificateSubject,
            key: key,
            hashAlgorithm: HashAlgorithmName.SHA256,
            padding: RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: false,
            hasPathLengthConstraint: false,
            pathLengthConstraint: 0,
            critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            keyUsages: X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
            critical: true));

        var serverAuthentication = new Oid(
            value: "1.3.6.1.5.5.7.3.1",
            friendlyName: "Server Authentication");
        var enhancedKeyUsage = new OidCollection { serverAuthentication };
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            enhancedKeyUsages: enhancedKeyUsage,
            critical: true));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        using X509Certificate2 generated = request.CreateSelfSigned(
            notBefore: now.AddMinutes(-5),
            notAfter: now.Add(CertificateLifetime));

        // CreateSelfSigned's in-memory key is exported only long enough to import
        // it into the current user's persistent key store. UserKeySet keeps the
        // private key in the companion user's profile; no machine-wide key is used.
        string temporaryPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(count: 32));
        byte[] pfx = generated.Export(
            contentType: X509ContentType.Pfx,
            password: temporaryPassword);
        try
        {
            return new X509Certificate2(
                rawData: pfx,
                password: temporaryPassword,
                keyStorageFlags: X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }
    }

    public void Dispose() => Certificate.Dispose();
}
