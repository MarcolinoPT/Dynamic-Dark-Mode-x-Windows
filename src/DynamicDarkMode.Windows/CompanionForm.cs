namespace DynamicDarkMode.Windows;

internal sealed class CompanionForm : Form
{
    public CompanionForm()
    {
        Text = "Dynamic Dark Mode";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(420, 180);
        Size = new Size(520, 220);

        var statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(24),
            Text = "Windows companion foundation\n\nTheme sync is not implemented yet."
        };

        Controls.Add(statusLabel);
    }
}
