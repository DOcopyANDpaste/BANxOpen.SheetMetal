using BANxOpen.SheetMetal.Beads.Rules;

namespace BANxOpen.SheetMetal.Common;

/// <summary>How a sheet metal body stands against its part's Sheet Metal Preferences.</summary>
public enum SheetMetalPreferenceStatus
{
    InSync,

    /// <summary>No material is set, or Parameter Entry is not Material Table, so NX is not using a material from the
    /// sheet metal material standards file.</summary>
    MaterialNotSet,

    /// <summary>The preferences name a material the standards file does not list.</summary>
    MaterialNotInTable,

    /// <summary>The preferences' material is made of a different physical material than the body.</summary>
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

/// <summary>Checks a sheet metal body against its part's Sheet Metal Preferences.
///
/// Material is checked before thickness: NX fills the thickness from the material's row, so a thickness compared
/// before the material is settled could be the wrong one to report.
///
/// A body with no physical material is not checked against the row's physical material — assigning one is part of
/// applying a picked row.</summary>
public static class SheetMetalPreferenceCheck
{
    /// <summary>The same tolerance the SPEC thickness check uses, so a thickness the preferences accept is one a
    /// SPEC accepts too.</summary>
    public const double ThicknessTolerance = ThicknessMatchRule.ToleranceInches;

    public static bool ThicknessMatches(double a, double b) => Math.Abs(a - b) <= ThicknessTolerance;

    /// <param name="bodyPhysicalMaterialName">The body's physical material, or null when it has none.</param>
    /// <param name="bodyThickness">In part units.</param>
    public static SheetMetalPreferenceCheckResult Evaluate(
        string? bodyPhysicalMaterialName, double bodyThickness, SheetMetalPartPreference preference)
    {
        if (!preference.IsMaterialTableEntry || string.IsNullOrWhiteSpace(preference.MaterialName))
        {
            return new SheetMetalPreferenceCheckResult(
                SheetMetalPreferenceStatus.MaterialNotSet,
                preference.IsMaterialTableEntry
                    ? "Sheet Metal Preferences have no material."
                    : "Sheet Metal Preferences are not set to Material Table entry, so NX is not using a material from the " +
                      "sheet metal material standards file.");
        }

        if (preference.Row is not { } row)
        {
            return new SheetMetalPreferenceCheckResult(
                SheetMetalPreferenceStatus.MaterialNotInTable,
                $"Sheet Metal Preferences have material '{preference.MaterialName}', which the sheet metal material " +
                "standards file does not list.");
        }

        // net48's reference assemblies aren't nullable-annotated, so the explicit null check narrows the name.
        if (bodyPhysicalMaterialName is not null
            && !string.IsNullOrWhiteSpace(bodyPhysicalMaterialName)
            && !string.Equals(row.PhysicalMaterialName, bodyPhysicalMaterialName, StringComparison.OrdinalIgnoreCase))
        {
            return new SheetMetalPreferenceCheckResult(
                SheetMetalPreferenceStatus.MaterialOutOfSync,
                $"Sheet Metal Preferences have material '{row.Name}', made of '{row.PhysicalMaterialName}', but this " +
                $"body's material is '{bodyPhysicalMaterialName}'.");
        }

        if (!ThicknessMatches(bodyThickness, preference.Thickness))
        {
            return new SheetMetalPreferenceCheckResult(
                SheetMetalPreferenceStatus.ThicknessMismatch,
                $"This sheet metal is {bodyThickness:0.####} thick, but Sheet Metal Preferences specify " +
                $"{preference.Thickness:0.####}. Correct the body's thickness or the preferences before validating a SPEC.");
        }

        return SheetMetalPreferenceCheckResult.InSync;
    }
}
