using System.Globalization;
using BANxOpen.Foundation.Contracts.Requirements;
using BANxOpen.Foundation.Core.Requirements;

namespace BANxOpen.SheetMetal.Requirements;

/// <summary>Names the NX side of a sheet metal requirement relies on.</summary>
public static class SheetMetalRequirementNames
{
    /// <summary>The expression NX creates with the Sheet Metal Preferences' material assignment, holding the sheet
    /// thickness (in inches: parts are inch). Read by name: the pair expression is built on it.</summary>
    public const string ThicknessExpression = "Sheet_Metal_Material_Thickness";

    /// <summary>The part attribute NX writes when the preferences select a standards-file row: the standards file
    /// declares its <c>SheetMetal_Material</c> (grade) column as an attribute column.</summary>
    public const string GradeAttribute = "SheetMetal_Material";

    /// <summary>A string expression reading <see cref="GradeAttribute"/>, so a requirement check can see the grade:
    /// checks evaluate expressions, not attributes.</summary>
    public const string GradeExpression = "BA_SM_Grade";

    public static string GradeExpressionFormula => $"ug_askPartAttrValue(\"{GradeAttribute}\")";

    /// <summary>A string expression holding the sheet's grade and thickness as one value, "2024-O@0.0400" — the form
    /// <see cref="MaterialThicknessPair.Key"/> writes — so one check judges the pair.</summary>
    public const string PairExpression = "BA_SM_MatThk";

    public static string PairExpressionFormula =>
        $"{GradeExpression} + \"{MaterialThicknessPair.Separator}\" + format(\"%.4f\", {ThicknessExpression})";

    public const string PairAspect = "PAIR";

    /// <summary>The two aspects a requirement was held in before <see cref="PairAspect"/>. Only read, so a part written
    /// then is carried over; its next refresh replaces them with the pair.</summary>
    public const string LegacyGradeAspect = "GRADE";
    public const string LegacyThicknessAspect = "THICKNESS";
}

/// <summary>Turns a <see cref="SheetMetalRequirement"/> into the one Foundation <see cref="RequirementSpec"/> it is
/// held in the part as, and back: a String "set of values" requirement listing every allowed
/// <see cref="MaterialThicknessPair.Key"/>, checked against <see cref="SheetMetalRequirementNames.PairExpression"/>.
///
/// One check per SPEC, so NX judges the grade and thickness together rather than each on its own. Severity Error: a
/// sheet the SPEC does not allow stays wrong until the features are re-applied, and the failing check is what keeps
/// reminding the user after the tool that changed the sheet has closed.</summary>
public static class SheetMetalRequirementMapper
{
    private const string SourcePrefix = "Source: ";
    private const string BuiltAtPrefix = "Built at: ";
    private const string BuiltAtSuffix = " in";

    public static IReadOnlyList<RequirementSpec> ToSpecs(SheetMetalRequirement requirement)
    {
        var allowance = requirement.Allowance;
        var users = requirement.Users.Select(u => u.FeatureKey).ToList();
        var usersLine = requirement.Users.Count == 0
            ? "Used by: (no features)"
            : $"Used by: {string.Join(", ", requirement.Users.Select(u => u.Label))}";
        var sourceLine = $"{SourcePrefix}{(string.IsNullOrEmpty(requirement.Source) ? "unknown" : requirement.Source)}";

        // The NX name is the SPEC alone (BA_REQ_BEAD_B1005010_3_PAIR): the Standard and domain made it too long to
        // read in the Part Navigator, and the full identity is kept in the description's bookkeeping line regardless.
        var nameStem = $"{NameAliasFor(requirement.Domain)}_{requirement.SpecId}";

        return new[]
        {
            new RequirementSpec(
                requirement.Domain,
                requirement.Key,
                SheetMetalRequirementNames.PairAspect,
                RequirementValueType.String,
                new SetOfValuesRule(allowance.Pairs.Select(p => p.Key).ToList()),
                RequirementSeverity.Error,
                SheetMetalRequirementNames.PairExpression,
                new[]
                {
                    $"{requirement.Label}: the sheet metal material must be one of {allowance.PairsText}.",
                    "If this fails, re-apply the features below with their tool for the new Sheet Metal Preferences.",
                    $"{BuiltAtPrefix}{allowance.ThicknessText}{BuiltAtSuffix}",
                    usersLine,
                    sourceLine,
                },
                users,
                nameStem),
        };
    }

    /// <summary>The short word a domain's requirement names start with: "SHEETMETAL.BEAD" → "BEAD".</summary>
    private static string NameAliasFor(string domain)
    {
        var dot = domain.LastIndexOf('.');
        return dot >= 0 ? domain.Substring(dot + 1) : domain;
    }

    /// <summary>The requirements held in the part for one domain. A key whose value is not what this mapper writes is
    /// reported in the errors rather than skipped — a caller would otherwise treat a half-read requirement as no
    /// requirement. A key still held as the legacy GRADE + THICKNESS aspects reads as those grades at that
    /// thickness.</summary>
    /// <param name="text">Words the read errors; <see cref="SheetMetalText.Default"/> when null.</param>
    public static (IReadOnlyList<SheetMetalRequirement> Requirements, IReadOnlyList<string> Errors) FromRead(
        RequirementRead read, string featureNoun, SheetMetalText? text = null)
    {
        text ??= SheetMetalText.Default;
        var requirements = new List<SheetMetalRequirement>();
        var errors = new List<string>(read.Errors);

        foreach (var group in read.Requirements.GroupBy(r => (r.Spec.Domain, r.Spec.Key)))
        {
            if (!TrySplitKey(group.Key.Key, out var standardId, out var specId))
            {
                errors.Add(text.RequirementKeyMalformed(group.Key.Key));
                continue;
            }

            var specs = group.Select(r => r.Spec).ToList();
            if (AllowanceOf(specs) is not { } allowance)
            {
                errors.Add(text.RequirementIncomplete(featureNoun, specId, standardId));
                continue;
            }

            var users = specs
                .SelectMany(s => s.AssociatedObjectKeys)
                .Distinct(StringComparer.Ordinal)
                .Select(key => new RequirementUser(key, key))
                .ToList();

            requirements.Add(new SheetMetalRequirement(
                group.Key.Domain, featureNoun, standardId, specId, allowance, users,
                specs.Select(s => SourceOf(s.Description)).FirstOrDefault(s => s is not null) ?? string.Empty));
        }

        return (requirements, errors);
    }

    /// <summary>The allowance one key's specs hold, or null when they are not what this mapper writes (or wrote).</summary>
    private static SheetMetalAllowance? AllowanceOf(IReadOnlyList<RequirementSpec> specs)
    {
        if (specs.FirstOrDefault(s => s.Aspect == SheetMetalRequirementNames.PairAspect) is { } pair)
        {
            if (pair.Rule is not SetOfValuesRule values || BuiltAtOf(pair.Description) is not { } built)
                return null;

            var pairs = values.Values.Select(MaterialThicknessPair.ParseKey).ToList();
            return pairs.Any(p => p is null) ? null : new SheetMetalAllowance(built, pairs.Select(p => p!).ToList());
        }

        var grade = specs.FirstOrDefault(s => s.Aspect == SheetMetalRequirementNames.LegacyGradeAspect);
        var thickness = specs.FirstOrDefault(s => s.Aspect == SheetMetalRequirementNames.LegacyThicknessAspect);
        return grade?.Rule is SetOfValuesRule grades && thickness?.Rule is EqualsValueRule builtAt
            ? SheetMetalAllowance.AtBuiltThickness(builtAt.Value, grades.Values)
            : null;
    }

    private static bool TrySplitKey(string key, out string standardId, out string specId)
    {
        var bar = key.IndexOf('|');
        standardId = bar > 0 ? key.Substring(0, bar) : string.Empty;
        specId = bar > 0 ? key.Substring(bar + 1) : string.Empty;
        return bar > 0 && specId.Length > 0;
    }

    private static double? BuiltAtOf(IReadOnlyList<string> description) =>
        description.FirstOrDefault(l => l.StartsWith(BuiltAtPrefix, StringComparison.Ordinal)) is { } line
        && line.EndsWith(BuiltAtSuffix, StringComparison.Ordinal)
        && double.TryParse(
            line.Substring(BuiltAtPrefix.Length, line.Length - BuiltAtPrefix.Length - BuiltAtSuffix.Length),
            NumberStyles.Float, CultureInfo.InvariantCulture, out var built)
            ? built
            : null;

    private static string? SourceOf(IReadOnlyList<string> description) =>
        description.FirstOrDefault(l => l.StartsWith(SourcePrefix, StringComparison.Ordinal)) is { } line
            ? line.Substring(SourcePrefix.Length) is var source && source != "unknown" ? source : null
            : null;
}

/// <summary>Reads the sheet metal requirements a part holds. One seam for the material engine's constraint, the row
/// choice and the feature dialogs, so they all judge by the same read.</summary>
public interface ISheetMetalRequirementReader
{
    /// <summary>Every sheet metal requirement in the work part, across the sheet metal feature domains.</summary>
    (IReadOnlyList<SheetMetalRequirement> Requirements, IReadOnlyList<string> Errors) ReadAll();
}

/// <summary>The default reader: each domain's requirements through the Foundation store.</summary>
public sealed class SheetMetalRequirementReader : ISheetMetalRequirementReader
{
    private readonly IPartRequirementStore _store;
    private readonly IReadOnlyList<(string Domain, string FeatureNoun)> _domains;
    private readonly SheetMetalText? _text;

    /// <param name="text">Words the read errors; <see cref="SheetMetalText.Default"/> when null.</param>
    public SheetMetalRequirementReader(
        IPartRequirementStore store, IReadOnlyList<(string Domain, string FeatureNoun)> domains, SheetMetalText? text = null)
    {
        _store = store;
        _domains = domains;
        _text = text;
    }

    public (IReadOnlyList<SheetMetalRequirement> Requirements, IReadOnlyList<string> Errors) ReadAll()
    {
        var requirements = new List<SheetMetalRequirement>();
        var errors = new List<string>();

        foreach (var (domain, noun) in _domains)
        {
            var (read, readErrors) = SheetMetalRequirementMapper.FromRead(_store.Read(domain), noun, _text);
            requirements.AddRange(read);
            errors.AddRange(readErrors);
        }

        return (requirements, errors);
    }
}
