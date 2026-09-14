using BANxOpen.SheetMetal.Beads.Rules;

namespace BANxOpen.SheetMetal.Common;

/// <summary>How a sheet metal body stands against its part's Sheet Metal Preferences.</summary>
public enum SheetMetalPreferenceStatus
{
    InSync,

    /// <summary>The body's grade is not in the NX material standards table, so the preferences cannot be set to
    /// it. Nothing the tool can do fixes this.</summary>
    MaterialNotInStandardsTable,

    /// <summary>The preferences are not using the body's grade — a different material, none, or an entry mode
    /// other than Material Table. The preferences can be synced to the body.</summary>
    MaterialOutOfSync,

    /// <summary>The body's thickness differs from the preferences' thickness. Reported, never synced: which of the
    /// two is wrong is the user's call.</summary>
    ThicknessMismatch,
}

/// <param name="Message">What is wrong, for the user. Null when <see cref="Status"/> is
/// <see cref="SheetMetalPreferenceStatus.InSync"/>.</param>
public sealed record SheetMetalPreferenceCheckResult(SheetMetalPreferenceStatus Status, string? Message)
{
    public static readonly SheetMetalPreferenceCheckResult InSync = new(SheetMetalPreferenceStatus.InSync, null);
}

/// <summary>Checks a sheet metal body against its part's Sheet Metal Preferences before any SPEC is validated
/// against the body.
///
/// Material is checked before thickness. Syncing the material switches Parameter Entry to Material Table, which
/// can change the table-driven thickness, so a thickness compared before the material is settled could be the
/// wrong one to report.
///
/// A body with no material is checked on thickness only. "No material" is the material picker's business, and
/// assigning one through the material engine syncs the preferences itself.</summary>
public static class SheetMetalPreferenceCheck
{
    /// <summary>The same tolerance the SPEC thickness check uses, so a thickness the preferences accept is one a
    /// SPEC accepts too.</summary>
    public const double ThicknessTolerance = ThicknessMatchRule.ToleranceInches;

    public static SheetMetalPreferenceCheckResult Evaluate(SheetMetalProfile profile, SheetMetalPartPreference preference)
    {
        // net48's reference assemblies aren't nullable-annotated, so the explicit null check narrows `grade`.
        var grade = profile.MaterialGradeLabel;
        if (grade is not null && !string.IsNullOrWhiteSpace(grade))
        {
            if (!preference.DefinesMaterial(grade))
            {
                return new SheetMetalPreferenceCheckResult(
                    SheetMetalPreferenceStatus.MaterialNotInStandardsTable,
                    $"This body's material grade '{grade}' is not in the NX sheet metal material standards table, so " +
                    "Sheet Metal Preferences cannot be set to it. Add it to the standards table, or assign a material " +
                    "whose grade is there.");
            }

            if (!preference.UsesMaterial(grade))
            {
                return new SheetMetalPreferenceCheckResult(
                    SheetMetalPreferenceStatus.MaterialOutOfSync,
                    $"{DescribeMaterial(preference)}, but this body's material grade is '{grade}'.");
            }
        }

        if (Math.Abs(profile.Thickness - preference.Thickness) > ThicknessTolerance)
        {
            return new SheetMetalPreferenceCheckResult(
                SheetMetalPreferenceStatus.ThicknessMismatch,
                $"This sheet metal is {profile.Thickness:0.####} thick, but Sheet Metal Preferences specify " +
                $"{preference.Thickness:0.####}. Correct the body's thickness or the preferences before validating a SPEC.");
        }

        return SheetMetalPreferenceCheckResult.InSync;
    }

    private static string DescribeMaterial(SheetMetalPartPreference preference)
    {
        var material = string.IsNullOrWhiteSpace(preference.MaterialName) ? "no material" : $"material '{preference.MaterialName}'";

        return preference.IsMaterialTableEntry
            ? $"Sheet Metal Preferences have {material}"
            : $"Sheet Metal Preferences are not set to Material Table entry (they have {material}), so NX is not using a material from them";
    }
}
