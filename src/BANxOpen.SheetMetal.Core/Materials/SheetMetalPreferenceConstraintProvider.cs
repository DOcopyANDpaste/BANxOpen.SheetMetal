using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.Core.Materials.Constraints;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>Tells the shared material engine what a sheet metal body's Sheet Metal Preferences allow.
///
/// Assigning a material to a sheet metal body also sets the part's preferences to that material's grade
/// (<see cref="SyncSheetMetalPreferenceEffectRule"/>). This provider refuses, up front, any material that sync
/// could not be carried out for, so the body's material and the preferences are never left out of step:
/// <list type="bullet">
/// <item>A material with no grade-map entry has no grade to set → <see cref="GradeUnmappedCode"/>.</item>
/// <item>A grade the NX material standards table does not define cannot be set → <see cref="NotInStandardsTableCode"/>.</item>
/// <item>Preferences that could not be read block every material → <see cref="PreferencesUnreadableCode"/>.</item>
/// </list>
///
/// A grade that merely differs from the current preferences is allowed: the sync is what brings them in line.
///
/// Preferences belong to the part, and the tool assumes one sheet metal body per part. A part with more than one
/// gets a warning (<see cref="SharedByBodiesCode"/>) that the assignment sets the preferences for all of them.
///
/// A body that is not sheet metal produces no constraints.</summary>
public sealed class SheetMetalPreferenceConstraintProvider : IFeatureMaterialConstraintProvider
{
    public const string ProviderDomainId = "SHEETMETAL.PREFERENCES";

    public const string PreferencesUnreadableCode = "SHEETMETAL_PREFERENCES_UNREADABLE";
    public const string GradeUnmappedCode = "PREFERENCE_GRADE_UNMAPPED";
    public const string NotInStandardsTableCode = "PREFERENCE_MATERIAL_NOT_IN_TABLE";
    public const string SharedByBodiesCode = "PREFERENCE_SHARED_BY_BODIES";

    private const string SourceLabel = "Sheet Metal Preferences";

    private readonly ISheetMetalPreferenceReader _reader;
    private readonly MaterialGradeMap _gradeMap;

    public SheetMetalPreferenceConstraintProvider(ISheetMetalPreferenceReader reader, MaterialGradeMap gradeMap)
    {
        _reader = reader;
        _gradeMap = gradeMap;
    }

    public string DomainId => ProviderDomainId;

    public IReadOnlyList<MaterialConstraint> ConstraintsFor(BodyId bodyId)
    {
        var read = _reader.ReadFor(bodyId);
        if (!read.IsSheetMetalBody)
            return Array.Empty<MaterialConstraint>();

        if (read.Preference is not { } preference)
        {
            // Nothing about the preferences is known, so no material can be shown to be settable in them.
            var error = read.ReadError ?? "no preferences were returned";
            return new[]
            {
                new MaterialConstraint(
                    DomainId, SourceLabel, PreferencesUnreadableCode,
                    _ => false,
                    _ => $"This part's Sheet Metal Preferences could not be read, so they cannot be updated to match " +
                         $"the assigned material: {error}"),
            };
        }

        var constraints = new List<MaterialConstraint>
        {
            new(
                DomainId, SourceLabel, GradeUnmappedCode,
                candidate => _gradeMap.GradeFor(candidate.Name) is not null,
                candidate =>
                    $"Material '{candidate.Name}' has no entry in material-grade-map.json, so Sheet Metal Preferences " +
                    "cannot be updated to match it. Add a mapping for it before assigning."),

            new(
                DomainId, SourceLabel, NotInStandardsTableCode,
                // An unmapped grade passes here on purpose: the constraint above already blocks it, with a message
                // that says what is actually wrong.
                candidate => _gradeMap.GradeFor(candidate.Name) is not { } grade || preference.DefinesMaterial(grade),
                candidate =>
                    $"Material '{candidate.Name}' (grade '{_gradeMap.GradeFor(candidate.Name)}') is not in the NX sheet " +
                    "metal material standards table, so Sheet Metal Preferences cannot be set to it."),
        };

        if (preference.SheetMetalBodyCount > 1)
        {
            constraints.Add(new MaterialConstraint(
                DomainId, SourceLabel, SharedByBodiesCode,
                _ => false,
                _ => $"This part has {preference.SheetMetalBodyCount} sheet metal bodies. Sheet Metal Preferences " +
                     "belong to the part, so this assignment sets the preferences' material for all of them.",
                ConstraintSeverity.Warn));
        }

        return constraints;
    }
}
