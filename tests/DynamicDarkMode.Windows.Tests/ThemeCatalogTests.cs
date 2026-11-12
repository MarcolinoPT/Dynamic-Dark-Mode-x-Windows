using DynamicDarkMode.Windows.ThemeCatalog;
using Shouldly;
using Xunit;

namespace DynamicDarkMode.Windows.Tests;

public sealed class ThemeCatalogTests
{
    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void ParserReadsDisplayNameAndVisualStyleMetadata()
    {
        // Arrange
        var parser = new ThemeFileParser(new PassthroughIndirectStringResolver());
        string filePath = Path.Combine(Path.GetTempPath(), "Themes", "Sample.theme");
        const string content = """
            [Theme]
            DisplayName=Sample theme

            [VisualStyles]
            Path=%SystemRoot%\resources\themes\Aero\Aero.msstyles
            ColorStyle=NormalColor
            Size=NormalSize
            """;

        // Act
        ThemeFileMetadata metadata = parser.ParseContent(filePath, content);

        // Assert
        metadata.ShouldSatisfyAllConditions(
            () => metadata.DisplayName.ShouldBe("Sample theme"),
            () => metadata.IsDisplayNameResolved.ShouldBeTrue(),
            () => metadata.UnresolvedDisplayNameResource.ShouldBeNull(),
            () => metadata.VisualStylePath.ShouldNotBeNullOrWhiteSpace(),
            () => metadata.VisualStylePath!.EndsWith(
                Path.Combine("resources", "themes", "Aero", "Aero.msstyles"),
                StringComparison.OrdinalIgnoreCase).ShouldBeTrue(),
            () => metadata.ColorStyle.ShouldBe("NormalColor"),
            () => metadata.Size.ShouldBe("NormalSize"));
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void ParserResolvesResourceDirToWindowsResourcesDirectoryAndMatcherUsesResolvedPath()
    {
        // Arrange
        string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string? configuredResourceDirectory = Environment.GetEnvironmentVariable("ResourceDir");
        string resourceDirectory = string.IsNullOrWhiteSpace(configuredResourceDirectory)
            ? Path.Combine(windowsDirectory, "Resources")
            : configuredResourceDirectory;
        string expectedStylePath = Path.GetFullPath(Path.Combine(
            resourceDirectory,
            "Themes",
            "Aero",
            "Aero.msstyles"));
        var parser = new ThemeFileParser(new PassthroughIndirectStringResolver());
        string filePath = Path.Combine(Path.GetTempPath(), "Themes", "ResourceDir.theme");
        const string content = """
            [Theme]
            DisplayName=Resource directory theme

            [VisualStyles]
            Path=%ResourceDir%\Themes\Aero\Aero.msstyles
            ColorStyle=NormalColor
            """;

        // Act
        ThemeFileMetadata metadata = parser.ParseContent(filePath, content);
        IReadOnlyList<WindowsThemeDescriptor> result = ThemeCatalogMatcher.Match(
            themeFiles: [metadata],
            managerEntries: [
                new ThemeManagerEntry(
                    EnumerationIndex: 5,
                    DisplayName: "Resource directory theme",
                    VisualStylePath: expectedStylePath,
                    ColorStyle: "NormalColor",
                    IsDisabled: false)
            ],
            isBuiltInTheme: _ => false);

        // Assert
        metadata.VisualStylePath.ShouldBe(expectedStylePath);
        result.Single().MatchStatus.ShouldBe(ThemeManagerMatchStatus.Matched);
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void VisualStylePathNormalizationHonorsExplicitResourceDirEnvironmentValue()
    {
        // Arrange
        string configuredResourceDirectory = Path.Combine(
            Path.GetTempPath(),
            "Configured Windows Resources");
        const string value = "%ResourceDir%\\Themes\\Custom.msstyles";

        // Act
        string? result = ThemeVisualStylePath.Normalize(
            value,
            windowsDirectory: Path.GetTempPath(),
            getEnvironmentVariable: name =>
                string.Equals(name, "ResourceDir", StringComparison.OrdinalIgnoreCase)
                    ? configuredResourceDirectory
                    : null);

        // Assert
        result.ShouldBe(Path.GetFullPath(Path.Combine(
            configuredResourceDirectory,
            "Themes",
            "Custom.msstyles")));
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void VisualStylePathNormalizationFallsBackToWindowsResourcesWhenResourceDirIsUnset()
    {
        // Arrange
        string windowsDirectory = Path.Combine(Path.GetTempPath(), "Explicit Windows");
        const string value = "%ResourceDir%\\Themes\\Custom.msstyles";

        // Act
        string? result = ThemeVisualStylePath.Normalize(
            value,
            windowsDirectory: windowsDirectory,
            getEnvironmentVariable: _ => null);

        // Assert
        result.ShouldBe(Path.GetFullPath(Path.Combine(
            windowsDirectory,
            "Resources",
            "Themes",
            "Custom.msstyles")));
    }

    [Theory]
    [InlineData("Aero\\Aero.msstyles")]
    [InlineData("C:Aero\\Aero.msstyles")]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void VisualStylePathNormalizationRejectsNonFullyQualifiedPaths(string value)
    {
        // Act
        string? result = ThemeVisualStylePath.Normalize(
            value,
            windowsDirectory: null,
            getEnvironmentVariable: _ => null);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void ParserResolvesIndirectDisplayName()
    {
        // Arrange
        var parser = new ThemeFileParser(new FixedIndirectStringResolver("Resolved theme"));
        string filePath = Path.Combine(Path.GetTempPath(), "Themes", "Indirect.theme");
        const string content = """
            [Theme]
            DisplayName=@%SystemRoot%\resources\themes\Aero\Aero.msstyles,-2013
            [VisualStyles]
            Path=%SystemRoot%\resources\themes\Aero\Aero.msstyles
            ColorStyle=NormalColor
            """;

        // Act
        ThemeFileMetadata metadata = parser.ParseContent(filePath, content);

        // Assert
        metadata.ShouldSatisfyAllConditions(
            () => metadata.DisplayName.ShouldBe("Resolved theme"),
            () => metadata.IsDisplayNameResolved.ShouldBeTrue(),
            () => metadata.UnresolvedDisplayNameResource.ShouldBeNull());
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void ParserPreservesUnresolvedIndirectDisplayNameAndUsesFilenameFallback()
    {
        // Arrange
        var parser = new ThemeFileParser(new FixedIndirectStringResolver(null));
        string filePath = Path.Combine(Path.GetTempPath(), "Themes", "Unresolved.theme");
        const string resource = "@%SystemRoot%\\system32\\themeui.dll,-1234";
        string content = $"[Theme]{Environment.NewLine}DisplayName={resource}";

        // Act
        ThemeFileMetadata metadata = parser.ParseContent(filePath, content);

        // Assert
        metadata.ShouldSatisfyAllConditions(
            () => metadata.DisplayName.ShouldBe("Unresolved"),
            () => metadata.IsDisplayNameResolved.ShouldBeFalse(),
            () => metadata.UnresolvedDisplayNameResource.ShouldBe(resource));
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void ParserReportsMissingThemeFile()
    {
        // Arrange
        var parser = new ThemeFileParser(new PassthroughIndirectStringResolver());
        string missingDirectory = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(missingDirectory);
        string missingPath = Path.Combine(missingDirectory, "Missing.theme");

        try
        {
            // Act
            Exception? exception = Record.Exception(() => parser.Parse(missingPath));

            // Assert
            exception.ShouldBeOfType<FileNotFoundException>();
        }
        finally
        {
            Directory.Delete(missingDirectory, recursive: true);
        }
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void MatcherUsesDisplayNameAndStyleMetadataInsteadOfEnumerationOrder()
    {
        // Arrange
        string firstPath = Path.Combine(Path.GetTempPath(), "Themes", "Light.theme");
        string secondPath = Path.Combine(Path.GetTempPath(), "Themes", "Dark.theme");
        string stylePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "Resources",
            "Themes",
            "Aero",
            "Aero.msstyles");

        ThemeFileMetadata[] files =
        [
            CreateMetadata(firstPath, "Windows (light)", stylePath),
            CreateMetadata(secondPath, "Windows (dark)", stylePath)
        ];

        ThemeManagerEntry[] managerEntries =
        [
            new(EnumerationIndex: 9, DisplayName: "Windows (dark)", VisualStylePath: stylePath,
                ColorStyle: "NormalColor", IsDisabled: false),
            new(EnumerationIndex: 3, DisplayName: "Windows (light)", VisualStylePath: stylePath,
                ColorStyle: "NormalColor", IsDisabled: false)
        ];

        // Act
        IReadOnlyList<WindowsThemeDescriptor> result = ThemeCatalogMatcher.Match(
            themeFiles: files,
            managerEntries: managerEntries,
            isBuiltInTheme: _ => true);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Count.ShouldBe(2),
            () => result[0].MatchStatus.ShouldBe(ThemeManagerMatchStatus.Matched),
            () => result[1].MatchStatus.ShouldBe(ThemeManagerMatchStatus.Matched),
            () => result[0].Id.ShouldNotBe(result[1].Id));
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void MatcherMarksDuplicateManagerEntriesAsAmbiguous()
    {
        // Arrange
        string stylePath = Path.Combine(
            Path.GetTempPath(),
            "Themes",
            "Aero.msstyles");
        ThemeFileMetadata[] files =
        [
            CreateMetadata(Path.Combine(Path.GetTempPath(), "Themes", "One.theme"), "Same", stylePath)
        ];
        ThemeManagerEntry[] managerEntries =
        [
            new(EnumerationIndex: 2, DisplayName: "Same", VisualStylePath: stylePath, ColorStyle: "NormalColor", IsDisabled: false),
            new(EnumerationIndex: 7, DisplayName: "Same", VisualStylePath: stylePath, ColorStyle: "NormalColor", IsDisabled: false)
        ];

        // Act
        IReadOnlyList<WindowsThemeDescriptor> result = ThemeCatalogMatcher.Match(
            files,
            managerEntries,
            _ => false);

        // Assert
        result.Single().MatchStatus.ShouldBe(ThemeManagerMatchStatus.Ambiguous);
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void MatcherMarksMultipleFilesResolvingToOneManagerEntryAsAmbiguous()
    {
        // Arrange
        string stylePath = Path.Combine(Path.GetTempPath(), "Themes", "Aero.msstyles");
        ThemeFileMetadata[] files =
        [
            CreateMetadata(Path.Combine(Path.GetTempPath(), "Themes", "One.theme"), "Same", stylePath),
            CreateMetadata(Path.Combine(Path.GetTempPath(), "Themes", "Two.theme"), "Same", stylePath)
        ];
        ThemeManagerEntry[] managerEntries =
        [
            new(EnumerationIndex: 4, DisplayName: "Same", VisualStylePath: stylePath, ColorStyle: "NormalColor", IsDisabled: false)
        ];

        // Act
        IReadOnlyList<WindowsThemeDescriptor> result = ThemeCatalogMatcher.Match(
            files,
            managerEntries,
            _ => false);

        // Assert
        result.All(descriptor => descriptor.MatchStatus == ThemeManagerMatchStatus.Ambiguous)
            .ShouldBeTrue();
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void MatcherDoesNotMatchUnresolvedDisplayName()
    {
        // Arrange
        string stylePath = Path.Combine(Path.GetTempPath(), "Themes", "Aero.msstyles");
        ThemeFileMetadata file = CreateMetadata(
            Path.Combine(Path.GetTempPath(), "Themes", "One.theme"),
            "One",
            stylePath) with
        {
            IsDisplayNameResolved = false,
            UnresolvedDisplayNameResource = "@%SystemRoot%\\system32\\themeui.dll,-1234"
        };
        ThemeManagerEntry[] managerEntries =
        [
            new(EnumerationIndex: 4, DisplayName: "One", VisualStylePath: stylePath, ColorStyle: "NormalColor", IsDisabled: false)
        ];

        // Act
        IReadOnlyList<WindowsThemeDescriptor> result = ThemeCatalogMatcher.Match(
            [file],
            managerEntries,
            _ => false);

        // Assert
        result.Single().MatchStatus.ShouldBe(ThemeManagerMatchStatus.Unmatched);
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void MatcherDoesNotResolveRelativeVisualStylePathsAgainstWorkingDirectory()
    {
        // Arrange
        ThemeFileMetadata[] files =
        [
            CreateMetadata(
                Path.Combine(Path.GetTempPath(), "Themes", "Relative.theme"),
                "Relative",
                "Aero\\Aero.msstyles")
        ];
        ThemeManagerEntry[] managerEntries =
        [
            new(EnumerationIndex: 4, DisplayName: "Relative", VisualStylePath: Path.Combine(Environment.CurrentDirectory, "Aero", "Aero.msstyles"),
                ColorStyle: "NormalColor", IsDisabled: false)
        ];

        // Act
        IReadOnlyList<WindowsThemeDescriptor> result = ThemeCatalogMatcher.Match(
            files,
            managerEntries,
            _ => false);

        // Assert
        result.Single().MatchStatus.ShouldBe(ThemeManagerMatchStatus.Unmatched);
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void MatcherMarksDisabledManagerEntryAsDisabled()
    {
        // Arrange
        string stylePath = Path.Combine(Path.GetTempPath(), "Themes", "Aero.msstyles");
        ThemeFileMetadata[] files =
        [
            CreateMetadata(Path.Combine(Path.GetTempPath(), "Themes", "One.theme"), "One", stylePath)
        ];
        ThemeManagerEntry[] managerEntries =
        [
            new(EnumerationIndex: 4, DisplayName: "One", VisualStylePath: stylePath, ColorStyle: "NormalColor", IsDisabled: true)
        ];

        // Act
        IReadOnlyList<WindowsThemeDescriptor> result = ThemeCatalogMatcher.Match(
            files,
            managerEntries,
            _ => false);

        // Assert
        result.Single().MatchStatus.ShouldBe(ThemeManagerMatchStatus.Disabled);
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void ThemeIdentityIsStableForTheSameCanonicalPath()
    {
        // Arrange
        string path = Path.Combine(Path.GetTempPath(), "Themes", "Stable.theme");
        string alternateCase = path.ToUpperInvariant();

        // Act
        string first = ThemeIdentity.Create(path);
        string second = ThemeIdentity.Create(alternateCase);

        // Assert
        first.ShouldSatisfyAllConditions(
            () => first.ShouldStartWith("theme-file-v1:"),
            () => second.ShouldBe(first));
    }


    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void FileDiscoveryEnumeratesSystemSharedAndCurrentUserThemes()
    {
        // Arrange
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string system = Path.Combine(root, "Windows", "Resources", "Themes");
        string easeOfAccess = Path.Combine(root, "Windows", "Resources", "Ease of Access Themes");
        string shared = Path.Combine(root, "ProgramData", "Microsoft", "Windows", "Themes");
        string user = Path.Combine(root, "Users", "Test", "AppData", "Local", "Microsoft", "Windows", "Themes");
        Directory.CreateDirectory(system);
        Directory.CreateDirectory(easeOfAccess);
        Directory.CreateDirectory(shared);
        Directory.CreateDirectory(user);

        string[] expectedFiles =
        [
            Path.Combine(system, "System.theme"),
            Path.Combine(shared, "Shared.theme"),
            Path.Combine(user, "User.theme")
        ];
        foreach (string file in expectedFiles.Append(Path.Combine(easeOfAccess, "Contrast.theme")))
        {
            File.WriteAllText(file, $"[Theme]{Environment.NewLine}DisplayName=Example");
        }

        string hiddenDirectory = Path.Combine(user, "Hidden");
        Directory.CreateDirectory(hiddenDirectory);
        string hiddenTheme = Path.Combine(hiddenDirectory, "Hidden.theme");
        string systemTheme = Path.Combine(shared, "System.theme");
        File.WriteAllText(hiddenTheme, "[Theme]\\nDisplayName=Hidden");
        File.WriteAllText(systemTheme, "[Theme]\\nDisplayName=System");
        File.SetAttributes(hiddenDirectory, File.GetAttributes(hiddenDirectory) | FileAttributes.Hidden);
        File.SetAttributes(hiddenTheme, File.GetAttributes(hiddenTheme) | FileAttributes.Hidden);
        File.SetAttributes(systemTheme, File.GetAttributes(systemTheme) | FileAttributes.System);

        var paths = new WindowsThemeSearchPaths(
            WindowsDirectory: Path.Combine(root, "Windows"),
            SystemThemesDirectory: system,
            EaseOfAccessThemesDirectory: easeOfAccess,
            SharedThemesDirectory: shared,
            UserThemesDirectory: user);

        try
        {
            var discovery = new WindowsThemeFileDiscovery(paths);

            // Act
            IReadOnlyList<string> discovered = discovery.EnumerateThemeFiles();

            // Assert
            discovered.ShouldContain(Path.GetFullPath(hiddenTheme));
            discovered.ShouldContain(Path.GetFullPath(systemTheme));
            discovered.ShouldContain(Path.GetFullPath(expectedFiles[0]));
            discovered.ShouldContain(Path.GetFullPath(expectedFiles[1]));
            discovered.ShouldContain(Path.GetFullPath(expectedFiles[2]));
            discovered.ShouldNotContain(Path.GetFullPath(Path.Combine(easeOfAccess, "Contrast.theme")));
        }
        finally
        {
            if (File.Exists(systemTheme))
            {
                File.SetAttributes(systemTheme, FileAttributes.Normal);
            }

            if (File.Exists(hiddenTheme))
            {
                File.SetAttributes(hiddenTheme, FileAttributes.Normal);
            }

            if (Directory.Exists(hiddenDirectory))
            {
                File.SetAttributes(hiddenDirectory, FileAttributes.Normal);
            }

            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void HighContrastClassificationUsesAccessibilityLocationsAndStyleNotDisplayName()
    {
        // Arrange
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string system = Path.Combine(root, "Windows", "Resources", "Themes");
        string easeOfAccess = Path.Combine(root, "Windows", "Resources", "Ease of Access Themes");
        var paths = new WindowsThemeSearchPaths(
            WindowsDirectory: Path.Combine(root, "Windows"),
            SystemThemesDirectory: system,
            EaseOfAccessThemesDirectory: easeOfAccess,
            SharedThemesDirectory: Path.Combine(root, "ProgramData", "Themes"),
            UserThemesDirectory: Path.Combine(root, "Users", "Test", "Themes"));
        var discovery = new WindowsThemeFileDiscovery(paths);

        ThemeFileMetadata contrastDirectoryTheme = CreateMetadata(
            Path.Combine(easeOfAccess, "Contrast.theme"),
            "Custom display name",
            Path.Combine(system, "Aero.msstyles"));
        ThemeFileMetadata aeroLiteTheme = CreateMetadata(
            Path.Combine(system, "Custom.theme"),
            "Another custom display name",
            Path.Combine(system, "AeroLite.msstyles"));
        ThemeFileMetadata misleadingNameTheme = CreateMetadata(
            Path.Combine(system, "High Contrast.theme"),
            "High Contrast",
            Path.Combine(system, "Aero.msstyles"));

        // Act
        bool directoryThemeIsHighContrast = discovery.IsHighContrastTheme(contrastDirectoryTheme);
        bool aeroLiteThemeIsHighContrast = discovery.IsHighContrastTheme(aeroLiteTheme);
        bool displayNameOnlyIsHighContrast = discovery.IsHighContrastTheme(misleadingNameTheme);

        // Assert
        directoryThemeIsHighContrast.ShouldSatisfyAllConditions(
            () => directoryThemeIsHighContrast.ShouldBeTrue(),
            () => aeroLiteThemeIsHighContrast.ShouldBeTrue(),
            () => displayNameOnlyIsHighContrast.ShouldBeFalse());
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void MatcherDisambiguatesDuplicateNamesUsingVisualStyleMetadata()
    {
        // Arrange
        string firstStyle = Path.Combine(Path.GetTempPath(), "Themes", "First.msstyles");
        string secondStyle = Path.Combine(Path.GetTempPath(), "Themes", "Second.msstyles");
        ThemeFileMetadata[] files =
        [
            CreateMetadata(Path.Combine(Path.GetTempPath(), "Themes", "One.theme"), "Same name", firstStyle),
            CreateMetadata(Path.Combine(Path.GetTempPath(), "Themes", "Two.theme"), "Same name", secondStyle)
        ];
        ThemeManagerEntry[] managerEntries =
        [
            new(EnumerationIndex: 8, DisplayName: "Same name", VisualStylePath: secondStyle, ColorStyle: "NormalColor", IsDisabled: false),
            new(EnumerationIndex: 2, DisplayName: "Same name", VisualStylePath: firstStyle, ColorStyle: "NormalColor", IsDisabled: false)
        ];

        // Act
        IReadOnlyList<WindowsThemeDescriptor> result = ThemeCatalogMatcher.Match(
            files,
            managerEntries,
            _ => false);

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Count.ShouldBe(2),
            () => result.All(item => item.MatchStatus == ThemeManagerMatchStatus.Matched).ShouldBeTrue());
    }

    [Fact]
    [Trait(TestTraits.Names.Category, TestTraits.Values.Windows)]
    public void CatalogReportsHighContrastStateAndMatchesDiscoveredTheme()
    {
        // Arrange
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string system = Path.Combine(root, "Windows", "Resources", "Themes");
        string easeOfAccess = Path.Combine(root, "Windows", "Resources", "Ease of Access Themes");
        string shared = Path.Combine(root, "ProgramData", "Microsoft", "Windows", "Themes");
        string user = Path.Combine(root, "Users", "Test", "AppData", "Local", "Microsoft", "Windows", "Themes");
        Directory.CreateDirectory(system);
        string themeFile = Path.Combine(system, "Sample.theme");
        string stylePath = Path.Combine(system, "Aero.msstyles");
        File.WriteAllText(
            themeFile,
            $"[Theme]{Environment.NewLine}DisplayName=Sample{Environment.NewLine}" +
            $"[VisualStyles]{Environment.NewLine}Path={stylePath}{Environment.NewLine}" +
            "ColorStyle=NormalColor");

        var paths = new WindowsThemeSearchPaths(
            WindowsDirectory: Path.Combine(root, "Windows"),
            SystemThemesDirectory: system,
            EaseOfAccessThemesDirectory: easeOfAccess,
            SharedThemesDirectory: shared,
            UserThemesDirectory: user);
        var catalog = new WindowsThemeCatalog(
            fileDiscovery: new WindowsThemeFileDiscovery(paths),
            fileParser: new ThemeFileParser(new PassthroughIndirectStringResolver()),
            managerEntrySource: new FixedThemeManagerEntrySource(
            [
                new ThemeManagerEntry(EnumerationIndex: 12, DisplayName: "Sample",
                    VisualStylePath: stylePath, ColorStyle: "NormalColor", IsDisabled: false)
            ]),
            highContrastStateProvider: new FixedHighContrastStateProvider(isEnabled: true));

        try
        {
            // Act
            WindowsThemeCatalogResult result = catalog.Discover();

            // Assert
            result.ShouldSatisfyAllConditions(
                () => result.HighContrastModeEnabled.ShouldBeTrue(),
                () => result.Themes.Count.ShouldBe(1),
                () => result.Themes[0].MatchStatus.ShouldBe(ThemeManagerMatchStatus.Matched),
                () => result.Themes[0].IsBuiltIn.ShouldBeTrue());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ThemeFileMetadata CreateMetadata(
        string filePath,
        string displayName,
        string visualStylePath) =>
        new(
            FilePath: filePath,
            DisplayName: displayName,
            IsDisplayNameResolved: true,
            UnresolvedDisplayNameResource: null,
            VisualStylePath: visualStylePath,
            ColorStyle: "NormalColor",
            Size: "NormalSize");


    private sealed class FixedThemeManagerEntrySource(
        IReadOnlyList<ThemeManagerEntry> entries) : IThemeManagerEntrySource
    {
        public IReadOnlyList<ThemeManagerEntry> GetEntries() => entries;
    }

    private sealed class FixedHighContrastStateProvider(bool isEnabled) : IHighContrastStateProvider
    {
        public bool IsEnabled() => isEnabled;
    }

    private sealed class PassthroughIndirectStringResolver : IIndirectStringResolver
    {
        public string? Resolve(string resourceReference) => resourceReference;
    }

    private sealed class FixedIndirectStringResolver(string? result) : IIndirectStringResolver
    {
        public string? Resolve(string resourceReference) => result;
    }
}
