using System.Security.Cryptography;

namespace DynamicDarkMode.Windows;

internal sealed class CompanionForm : Form
{
    private readonly Label _statusLabel;

    public CompanionForm()
    {
        Text = "Dynamic Dark Mode";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 260);
        Size = new Size(680, 320);

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(24),
            AutoEllipsis = false,
            Text = "Initializing Windows receiver identity..."
        };

        Controls.Add(_statusLabel);
        Shown += async (_, _) => await InitializeIdentityAsync();
    }

    private async Task InitializeIdentityAsync()
    {
        try
        {
            string fingerprint = await Task.Run(() =>
            {
                using ReceiverCertificateIdentity identity = ReceiverCertificateIdentity.LoadOrCreate();
                return identity.Fingerprint;
            });

            if (IsDisposed || Disposing)
            {
                return;
            }

            _statusLabel.Text =
                "Windows receiver identity ready\n\n" +
                "SHA-256 certificate fingerprint (compare this full value with the Mac):\n" +
                FormatFingerprint(fingerprint) +
                "\n\nThe network listener is not enabled in this build.";
        }
        catch (Exception exception) when (
            exception is CryptographicException
                or UnauthorizedAccessException
                or IOException
                or InvalidOperationException
                or System.Threading.WaitHandleCannotBeOpenedException)
        {
            // Do not start any listener when persistent identity cannot be loaded
            // safely. Keep details out of the UI until diagnostics are designed.
            if (IsDisposed || Disposing)
            {
                return;
            }

            _statusLabel.Text =
                "Receiver identity unavailable.\n\n" +
                "The receiver must remain stopped. Check the current user's " +
                "Personal certificate store and private-key access, then restart.";
        }
    }

    private static string FormatFingerprint(string fingerprint) =>
        string.Join(":", Enumerable.Range(0, fingerprint.Length / 2)
            .Select(index => fingerprint.Substring(index * 2, 2)));
}
