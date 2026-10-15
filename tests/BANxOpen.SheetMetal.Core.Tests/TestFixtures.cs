using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Common;
using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.Requirements;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.Tests;

/// <summary>Data builders for the requirement tests.</summary>
internal static class RequirementFixtures
{
    public const string Standard = "XX_Standard";

    /// <summary>A SPEC row allowing <paramref name="grades"/> at <paramref name="thickness"/>, in workbook column order
    /// 2024-O, 5052-O, 7075-T6.</summary>
    public static BeadSpecRow SpecRow(string specId, double thickness, params string[] grades) =>
        new(Standard, "B1005010", specId, 0.245, 0.625, 0.625, 0.188, thickness,
            new Dictionary<string, bool>
            {
                ["2024-O"] = grades.Contains("2024-O"),
                ["5052-O"] = grades.Contains("5052-O"),
                ["7075-T6"] = grades.Contains("7075-T6"),
            });

    public static BeadOnBody Stamped(string featureKey, string name, string specId, params BeadSpecRow[] family) =>
        new(featureKey, name, BeadSpecStatus.Stamped, Standard, specId, family, Array.Empty<BeadSpecRow>());

    public static BeadOnBody Matched(string featureKey, string name, string specId, params BeadSpecRow[] family) =>
        new(featureKey, name, BeadSpecStatus.Matched, Standard, specId, family, Array.Empty<BeadSpecRow>());

    public static BeadOnBody Unmatched(string featureKey, string name) =>
        new(featureKey, name, BeadSpecStatus.Unmatched, null, null, Array.Empty<BeadSpecRow>(), Array.Empty<BeadSpecRow>());

    public static SheetMetalRequirement Requirement(string specId, double builtThickness, params string[] grades) =>
        new(BeadMaterialConstraintProvider.ProviderDomainId, BeadRequirementFactory.FeatureNoun, Standard, specId,
            new SheetMetalAllowance(builtThickness, grades), new[] { new RequirementUser("101", "BEAD(1)") }, "B1005010");

    public static readonly IReadOnlyDictionary<(string StandardId, string SpecId), BeadSpecRow> NothingRebuilt =
        new Dictionary<(string, string), BeadSpecRow>();

    public static SheetMetalPreferenceRead SheetMetalBody(SheetMetalMaterialTable table, string? preferenceRow = null, double? bodyThickness = 0.02) =>
        SheetMetalPreferenceRead.Of(
            new SheetMetalPartPreference(preferenceRow, IsMaterialTableEntry: true, Thickness: 0.02, SheetMetalBodyCount: 1, table.Find(preferenceRow)),
            bodyThickness);
}

internal sealed class FakeRequirementReader : ISheetMetalRequirementReader
{
    private readonly IReadOnlyList<SheetMetalRequirement> _requirements;
    private readonly IReadOnlyList<string> _errors;

    public FakeRequirementReader(IEnumerable<SheetMetalRequirement> requirements, params string[] errors)
    {
        _requirements = requirements.ToList();
        _errors = errors;
    }

    public FakeRequirementReader(params SheetMetalRequirement[] requirements) : this(requirements.AsEnumerable()) { }

    public (IReadOnlyList<SheetMetalRequirement> Requirements, IReadOnlyList<string> Errors) ReadAll() => (_requirements, _errors);
}

internal sealed class FixedPreferenceReader : ISheetMetalPreferenceReader
{
    private readonly SheetMetalPreferenceRead _read;

    public FixedPreferenceReader(SheetMetalPreferenceRead read) => _read = read;

    public SheetMetalPreferenceRead ReadFor(BodyId bodyId) => _read;
}

/// <summary>A feature inventory that reports the same read for every body, or only for <c>onlyFor</c> when given (other
/// bodies then have no features).</summary>
internal sealed class FixedInventory : IFeatureInventory
{
    private readonly BodyFeatureInventory _inventory;
    private readonly BodyId? _onlyFor;

    public FixedInventory(BodyFeatureInventory inventory, BodyId? onlyFor = null)
    {
        _inventory = inventory;
        _onlyFor = onlyFor;
    }

    public BodyFeatureInventory Read(BodyId bodyId) =>
        _onlyFor is { } only && bodyId != only ? BodyFeatureInventory.Empty : _inventory;
}

/// <summary>SPEC workbook rows in memory, each SPEC's family ordered by thickness like the real cache.</summary>
internal sealed class SpecLookup : IBeadSpecLookup
{
    private readonly IReadOnlyList<BeadSpecRow> _rows;

    public SpecLookup(IEnumerable<BeadSpecRow> rows) => _rows = rows.ToList();

    public IReadOnlyList<BeadSpecRow> FindAll(string standardId, string specId) =>
        _rows.Where(r => r.StandardId == standardId && r.SpecId == specId)
            .OrderBy(r => r.Thickness)
            .ToList();

    public IReadOnlyList<BeadSpecRow> AllSpecs() => _rows;
}
