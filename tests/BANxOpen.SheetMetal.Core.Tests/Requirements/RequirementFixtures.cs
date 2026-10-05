using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Common;
using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.Requirements;

namespace BANxOpen.SheetMetal.Tests.Requirements;

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

    public sealed class FakeRequirementReader : ISheetMetalRequirementReader
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

    public sealed class FixedPreferenceReader : ISheetMetalPreferenceReader
    {
        private readonly SheetMetalPreferenceRead _read;

        public FixedPreferenceReader(SheetMetalPreferenceRead read) => _read = read;

        public SheetMetalPreferenceRead ReadFor(BodyId bodyId) => _read;
    }

    public static SheetMetalPreferenceRead SheetMetalBody(SheetMetalMaterialTable table, string? preferenceRow = null, double? bodyThickness = 0.02) =>
        SheetMetalPreferenceRead.Of(
            new SheetMetalPartPreference(preferenceRow, IsMaterialTableEntry: true, Thickness: 0.02, SheetMetalBodyCount: 1, table.Find(preferenceRow)),
            bodyThickness);
}
