using System.Globalization;
using BANxOpen.Foundation.Contracts.Requirements;
using BANxOpen.SheetMetal.Beads.Rules;

namespace BANxOpen.SheetMetal.Requirements;

/// <summary>Names the NX side of a sheet metal requirement relies on.</summary>
public static class SheetMetalRequirementNames
{
    /// <summary>The expression NX creates with the Sheet Metal Preferences' material assignment, holding the sheet
    /// thickness. Read by name: the THICKNESS requirement is checked against it.</summary>
    public const string ThicknessExpression = "Sheet_Metal_Material_Thickness";

    /// <summary>The part attribute NX writes when the preferences select a standards-file row: the standards file
    /// declares its <c>SheetMetal_Material</c> (grade) column as an attribute column.</summary>
    public const string GradeAttribute = "SheetMetal_Material";

    /// <summary>A string expression reading <see cref="GradeAttribute"/>, so a requirement check can see the grade:
    /// checks evaluate expressions, not attributes.</summary>
    public const string GradeExpression = "BA_SM_Grade";

    public static string GradeExpressionFormula => $"ug_askPartAttrValue(\"{GradeAttribute}\")";

    public const string GradeAspect = "GRADE";
    public const string ThicknessAspect = "THICKNESS";
}

/// <summary>Turns a <see cref="SheetMetalRequirement"/> into the two Foundation <see cref="RequirementSpec"/>s it is
/// held in the part as, and back:
/// <list type="bullet">
/// <item>GRADE — a String "set of values" requirement listing the allowed grades, checked against
/// <see cref="SheetMetalRequirementNames.GradeExpression"/>.</item>
/// <item>THICKNESS — a Number "equals" requirement holding the built thickness within
/// <see cref="ThicknessMatchRule.ToleranceInches"/>, checked against
/// <see cref="SheetMetalRequirementNames.ThicknessExpression"/>.</item>
/// </list>
/// Two native requirements rather than one combined formula: each is a plain NX definition method, so NX reports which
/// of the two failed, and the allowed grades sit in the requirement itself where a user can read them.</summary>
public static class SheetMetalRequirementMapper
{
    private const string SourcePrefix = "Source: ";

    public static IReadOnlyList<RequirementSpec> ToSpecs(SheetMetalRequirement requirement)
    {
        var allowance = requirement.Allowance;
        var users = requirement.Users.Select(u => u.FeatureKey).ToList();
        var usersLine = requirement.Users.Count == 0
            ? "Used by: (no features)"
            : $"Used by: {string.Join(", ", requirement.Users.Select(u => u.Label))}";
        var sourceLine = $"{SourcePrefix}{(string.IsNullOrEmpty(requirement.Source) ? "unknown" : requirement.Source)}";

        return new[]
        {
            new RequirementSpec(
                requirement.Domain,
                requirement.Key,
                SheetMetalRequirementNames.GradeAspect,
                RequirementValueType.String,
                new SetOfValuesRule(allowance.Grades),
                RequirementSeverity.Warning,
                SheetMetalRequirementNames.GradeExpression,
                new[]
                {
                    $"{requirement.Label}: the sheet metal grade must be one of {allowance.GradesText} " +
                    $"(allowed at {allowance.ThicknessText} in, the thickness it was built at).",
                    "If this fails, re-check the features below against the new Sheet Metal Preferences.",
                    usersLine,
                    sourceLine,
                },
                users),
            new RequirementSpec(
                requirement.Domain,
                requirement.Key,
                SheetMetalRequirementNames.ThicknessAspect,
                RequirementValueType.Number,
                new EqualsValueRule(allowance.BuiltThickness, ThicknessMatchRule.ToleranceInches),
                RequirementSeverity.Warning,
                SheetMetalRequirementNames.ThicknessExpression,
                new[]
                {
                    $"{requirement.Label} was built at {allowance.ThicknessText} in; its features carry that row's values.",
                    "If this fails, the sheet thickness changed: re-apply the features below so they take the new thickness's row.",
                    usersLine,
                    sourceLine,
                },
                users),
        };
    }

    /// <summary>The requirements held in the part for one domain. A key with only one of its two aspects, or with a
    /// value that is not what this mapper writes, is reported in the errors rather than skipped — a caller would
    /// otherwise treat a half-read requirement as no requirement.</summary>
    public static (IReadOnlyList<SheetMetalRequirement> Requirements, IReadOnlyList<string> Errors) FromRead(
        RequirementRead read, string featureNoun)
    {
        var requirements = new List<SheetMetalRequirement>();
        var errors = new List<string>(read.Errors);

        foreach (var group in read.Requirements.GroupBy(r => (r.Spec.Domain, r.Spec.Key)))
        {
            var grade = group.FirstOrDefault(r => r.Spec.Aspect == SheetMetalRequirementNames.GradeAspect)?.Spec;
            var thickness = group.FirstOrDefault(r => r.Spec.Aspect == SheetMetalRequirementNames.ThicknessAspect)?.Spec;

            if (!TrySplitKey(group.Key.Key, out var standardId, out var specId))
            {
                errors.Add($"Requirement key '{group.Key.Key}' is not a Standard|SPEC pair.");
                continue;
            }

            if (grade?.Rule is not SetOfValuesRule grades || thickness?.Rule is not EqualsValueRule built)
            {
                errors.Add($"The requirement for {featureNoun} {specId} (Standard {standardId}) is incomplete or was " +
                           "edited by hand; re-apply its features to rewrite it.");
                continue;
            }

            var users = grade.AssociatedObjectKeys
                .Concat(thickness.AssociatedObjectKeys)
                .Distinct(StringComparer.Ordinal)
                .Select(key => new RequirementUser(key, key))
                .ToList();

            requirements.Add(new SheetMetalRequirement(
                group.Key.Domain, featureNoun, standardId, specId,
                new SheetMetalAllowance(built.Value, grades.Values),
                users,
                SourceOf(grade.Description) ?? SourceOf(thickness.Description) ?? string.Empty));
        }

        return (requirements, errors);
    }

    private static bool TrySplitKey(string key, out string standardId, out string specId)
    {
        var bar = key.IndexOf('|');
        standardId = bar > 0 ? key.Substring(0, bar) : string.Empty;
        specId = bar > 0 ? key.Substring(bar + 1) : string.Empty;
        return bar > 0 && specId.Length > 0;
    }

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
    private readonly Foundation.Core.Requirements.IPartRequirementStore _store;
    private readonly IReadOnlyList<(string Domain, string FeatureNoun)> _domains;

    public SheetMetalRequirementReader(
        Foundation.Core.Requirements.IPartRequirementStore store, IReadOnlyList<(string Domain, string FeatureNoun)> domains)
    {
        _store = store;
        _domains = domains;
    }

    public (IReadOnlyList<SheetMetalRequirement> Requirements, IReadOnlyList<string> Errors) ReadAll()
    {
        var requirements = new List<SheetMetalRequirement>();
        var errors = new List<string>();

        foreach (var (domain, noun) in _domains)
        {
            var (read, readErrors) = SheetMetalRequirementMapper.FromRead(_store.Read(domain), noun);
            requirements.AddRange(read);
            errors.AddRange(readErrors);
        }

        return (requirements, errors);
    }
}
