using System.Runtime.InteropServices;
using System.Text;

namespace DynamicDarkMode.Windows.ThemeCatalog;

internal interface IIndirectStringResolver
{
    /// <summary>Resolves an indirect Windows resource reference to its localized display string.</summary>
    string? Resolve(string resourceReference);
}

internal sealed class WindowsIndirectStringResolver : IIndirectStringResolver
{
    /// <summary>Loads the localized string identified by an indirect Windows resource reference.</summary>
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

    /// <summary>Reads and parses a theme package from disk.</summary>
    /// <param name="filePath">Absolute or relative path to the theme package.</param>
    /// <returns>The parsed theme metadata.</returns>
    public ThemeFileMetadata Parse(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string content = File.ReadAllText(filePath);
        return ParseContent(filePath: filePath, content: content);
    }

    /// <summary>Parses theme metadata from the supplied file contents.</summary>
    /// <param name="filePath">Path used as the theme package identity.</param>
    /// <param name="content">Text contents in Windows theme-file format.</param>
    /// <returns>The parsed theme metadata.</returns>
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
            visualStylePath = ThemeVisualStylePath.Normalize(visualStylePath);
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

internal static class ThemeVisualStylePath
{
    /// <summary>Expands theme path variables and returns a canonical absolute VisualStyles path.</summary>
    /// <param name="value">The path value read from a theme file or theme-manager entry.</param>
    /// <returns>The normalized absolute path, or <see langword="null"/> when it cannot be resolved.</returns>
    internal static string? Normalize(string? value) =>
        Normalize(
            value,
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetEnvironmentVariable);

    /// <summary>Normalizes a VisualStyles path using the supplied Windows directory and environment lookup.</summary>
    /// <param name="value">The path value to normalize.</param>
    /// <param name="windowsDirectory">The Windows installation directory used for the ResourceDir fallback.</param>
    /// <param name="getEnvironmentVariable">Environment-variable lookup used to honor an explicitly configured ResourceDir.</param>
    /// <returns>The normalized absolute path, or <see langword="null"/> when it cannot be resolved.</returns>
    internal static string? Normalize(
        string? value,
        string? windowsDirectory,
        Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string expanded = value.Trim().Trim('"');
        if (expanded.Contains("%ResourceDir%", StringComparison.OrdinalIgnoreCase))
        {
            string? resourceDirectory = getEnvironmentVariable("ResourceDir");
            if (string.IsNullOrWhiteSpace(resourceDirectory))
            {
                resourceDirectory = string.IsNullOrWhiteSpace(windowsDirectory)
                    ? null
                    : Path.Combine(windowsDirectory, "Resources");
            }

            if (string.IsNullOrWhiteSpace(resourceDirectory))
            {
                return null;
            }

            expanded = ReplaceResourceDirectoryToken(expanded, resourceDirectory);
        }

        expanded = Environment.ExpandEnvironmentVariables(expanded);
        if (!Path.IsPathFullyQualified(expanded))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(expanded)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            return null;
        }
    }

    private static string ReplaceResourceDirectoryToken(string value, string resourceDirectory) =>
        value.Replace("%ResourceDir%", resourceDirectory, StringComparison.OrdinalIgnoreCase);
}
