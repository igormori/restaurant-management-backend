using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using RestaurantManagement.Modules.Identity.Models;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Modules.Organization.Models;
using RestaurantManagement.Shared;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Every message the API can send exists in English (en) and Portuguese (pt) (spec Outcome #9).
/// Reads the catalog files and the source code from the repository; no database needed.
/// Each check lists every offending key or field, so one run shows all the gaps.
/// </summary>
public class MessageCatalogCompletenessTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string CatalogFolder = Path.Combine(RepositoryRoot, "src", "RestaurantManagement.Shared", "Localization");

    private static readonly Assembly[] ModuleAssemblies =
    {
        typeof(LoginRequest).Assembly,
        typeof(CreateMenuRequest).Assembly,
        typeof(CreateOrganizationRequest).Assembly,
        typeof(SharedResource).Assembly
    };

    private static readonly Regex LocalizerLookup = new(@"(?:[Ll]ocalizer|\bloc)\[\s*""(?<key>[A-Za-z0-9_]+)""", RegexOptions.Compiled);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RestaurantManagement.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root (RestaurantManagement.sln) not found.");
    }

    private static Dictionary<string, string> ReadCatalog(string fileName)
        => XDocument.Load(Path.Combine(CatalogFolder, fileName)).Root!
            .Elements("data")
            .ToDictionary(data => data.Attribute("name")!.Value, data => data.Element("value")?.Value ?? string.Empty);

    // Scenario 20
    [Fact]
    [Trait("Rule", "SH19")]
    public void Catalog_EnglishAndPortuguese_HoldSameKeysWithText()
    {
        // Arrange
        var english = ReadCatalog("SharedResource.resx");
        var portuguese = ReadCatalog("SharedResource.pt.resx");

        // Act
        var onlyInEnglish = english.Keys.Except(portuguese.Keys).Select(key => $"{key} (missing in pt)");
        var onlyInPortuguese = portuguese.Keys.Except(english.Keys).Select(key => $"{key} (missing in en)");
        var emptyEnglish = english.Where(entry => string.IsNullOrWhiteSpace(entry.Value)).Select(entry => $"{entry.Key} (empty in en)");
        var emptyPortuguese = portuguese.Where(entry => string.IsNullOrWhiteSpace(entry.Value)).Select(entry => $"{entry.Key} (empty in pt)");
        var problems = onlyInEnglish.Concat(onlyInPortuguese).Concat(emptyEnglish).Concat(emptyPortuguese).ToList();

        // Assert
        problems.ShouldBeEmpty();
    }

    // Scenario 21
    [Fact]
    [Trait("Rule", "SH19")]
    public void RequestModels_ValidationAttributes_UseCatalogKeys()
    {
        // Arrange
        var catalogKeys = ReadCatalog("SharedResource.resx").Keys.ToHashSet();
        var requestModels = ModuleAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && type.Name.EndsWith("Request") && type.Namespace?.EndsWith(".Models") == true);

        // Act
        var offenders = requestModels
            .SelectMany(type => type.GetProperties().Select(property => (type, property)))
            .SelectMany(field => field.property.GetCustomAttributes<ValidationAttribute>(inherit: true)
                .Where(attribute => attribute is not DataTypeAttribute)
                .Select(attribute => (field.type, field.property, attribute)))
            .Where(found => found.attribute.ErrorMessage is null || !catalogKeys.Contains(found.attribute.ErrorMessage))
            .Select(found => $"{found.type.Name}.{found.property.Name} [{found.attribute.GetType().Name}] message: '{found.attribute.ErrorMessage ?? "(none)"}'")
            .ToList();

        // Assert
        offenders.ShouldBeEmpty();
    }

    // Scenario 22
    [Fact]
    [Trait("Rule", "SH19")]
    public void SourceCode_LocalizerLookups_ExistInCatalog()
    {
        // Arrange
        var catalogKeys = ReadCatalog("SharedResource.resx").Keys.ToHashSet();
        var sourceFiles = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

        // Act
        var missing = sourceFiles
            .SelectMany(path => LocalizerLookup.Matches(File.ReadAllText(path))
                .Select(match => (file: Path.GetFileName(path), key: match.Groups["key"].Value)))
            .Where(lookup => !catalogKeys.Contains(lookup.key))
            .Select(lookup => $"{lookup.key} (looked up in {lookup.file})")
            .Distinct()
            .ToList();

        // Assert
        missing.ShouldBeEmpty();
    }
}
