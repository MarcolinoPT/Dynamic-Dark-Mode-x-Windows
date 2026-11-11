using System.Runtime.InteropServices;
using System.Text;

namespace DynamicDarkMode.Windows.ThemeCatalog;

internal interface IIndirectStringResolver
{
    string? Resolve(string resourceReference);
}

internal sealed class WindowsIndirectStringResolver : IIndirectStringResolver
{
    public string? Resolve(string resourceReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceReference);

        string expandedReference = Environment.ExpandEnvironmentVariables(resourceReference);
        var output = new StringBuilder(capacity: 4096);
        int result = SHLoadIndirectString(
            pszSource: expandedReference,
            pszOutBuf: output,
            cchOutBuf: (uint)output.Capacity,
            ppvReserved: IntPtr.Zero);

        return result >= 0 && output.Length > 0
            ? output.ToString()
            : null;
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHLoadIndirectString(
        string pszSource,
        StringBuilder pszOutBuf,
        uint cchOutBuf,
        IntPtr ppvReserved);
}

internal sealed class ThemeFileParser(IIndirectStringResolver indirectStringResolver)
{
    private readonly IIndirectStringResolver _indirectStringResolver =
        indirectStringResolver ?? throw new ArgumentNullException(nameof(indirectStringResolver));

    public ThemeFileMetadata Parse(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string content = File.ReadAllText(filePath);
        return ParseContent(filePath: filePath, content: content);
    }

    internal ThemeFileMetadata ParseContent(string filePath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(content);

        Dictionary<string, Dictionary<string, string>> sections = ParseSections(content);

        string? displayNameResource = GetValue(sections, "Theme", "DisplayName");
        string? resolvedDisplayName = ResolveDisplayName(displayNameResource);
        string displayName = resolvedDisplayName
            ?? Path.GetFileNameWithoutExtension(filePath);

        string? visualStylePath = GetValue(sections, "VisualStyles", "Path");
        if (!string.IsNullOrWhiteSpace(visualStylePath))
        {
            visualStylePath = Environment.ExpandEnvironmentVariables(visualStylePath);
            visualStylePath = NormalizeOptionalPath(visualStylePath);
        }

        return new ThemeFileMetadata(
            FilePath: Path.GetFullPath(filePath),
            DisplayName: displayName,
            IsDisplayNameResolved: resolvedDisplayName is not null,
            UnresolvedDisplayNameResource: resolvedDisplayName is null
                ? displayNameResource
                : null,
            VisualStylePath: visualStylePath,
            ColorStyle: GetValue(sections, "VisualStyles", "ColorStyle"),
            Size: GetValue(sections, "VisualStyles", "Size"));
    }

    private string? ResolveDisplayName(string? displayNameResource)
    {
        if (string.IsNullOrWhiteSpace(displayNameResource))
        {
            return null;
        }

        if (!IsIndirectResource(displayNameResource))
        {
            return displayNameResource;
        }

        return _indirectStringResolver.Resolve(displayNameResource);
    }

    private static bool IsIndirectResource(string? value) =>
        value is not null && value.StartsWith('@');

    private static string? NormalizeOptionalPath(string value)
    {
        string unquoted = Unquote(value);
        if (string.IsNullOrWhiteSpace(unquoted))
        {
            return null;
        }

        if (!Path.IsPathRooted(unquoted))
        {
            return unquoted;
        }

        try
        {
            return Path.GetFullPath(unquoted);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            return unquoted;
        }
    }

    private static Dictionary<string, Dictionary<string, string>> ParseSections(string content)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string>? currentSection = null;

        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } line)
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                string sectionName = trimmed[1..^1].Trim();
                if (!sections.TryGetValue(sectionName, out currentSection))
                {
                    currentSection = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase);
                    sections.Add(sectionName, currentSection);
                }

                continue;
            }

            int separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0 || currentSection is null)
            {
                continue;
            }

            string key = trimmed[..separatorIndex].Trim();
            string value = Unquote(trimmed[(separatorIndex + 1)..].Trim());
            if (key.Length > 0)
            {
                currentSection[key] = value;
            }
        }

        return sections;
    }

    private static string? GetValue(
        IReadOnlyDictionary<string, Dictionary<string, string>> sections,
        string sectionName,
        string key) =>
        sections.TryGetValue(sectionName, out Dictionary<string, string>? section)
            && section.TryGetValue(key, out string? value)
                ? value
                : null;

    private static string Unquote(string value)
    {
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '\'' && value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }
}
