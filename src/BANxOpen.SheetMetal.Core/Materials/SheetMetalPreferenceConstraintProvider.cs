using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Materials.Core.Rules.Features;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>Tells the shared material engine what a sheet metal body's Sheet Metal Preferences allow.
///
/// Assigning a material to a sheet metal body also sets the part's preferences to a row of the sheet metal material
/// standards file made of that material (<see cref="SyncSheetMetalPreferenceEffectRule"/>). This provider refuses, up
/// front, any material that sync could not be carried out for, so the body's material and the preferences are never
/// left out of step:
/// <list type="bullet">
/// <item>A material the standards file has no row for has nothing to set → <see cref="NotInTableCode"/>.</item>
/// <item>Preferences that could not be read block every material → <see cref="PreferencesUnreadableCode"/>.</item>
/// </list>
///
/// A material that merely differs from the current preferences is allowed: the sync is what brings them in line.
///
/// Preferences belong to the part, and the tool assumes one sheet metal body per part. A part with more than one
/// gets a warning (<see cref="SharedByBodiesCode"/>) that the assignment sets the preferences for all of them.
///
/// A body that is not sheet metal produces no constraints.</summary>
public sealed class SheetMetalPreferenceConstraintProvider : IFeatureMaterialConstraintProvider
{
    public const string ProviderDomainId = "SHEETMETAL.PREFERENCES";

    // Kept as aliases of SheetMetalCodes, which is where the codes are defined.
    public const string PreferencesUnreadableCode = SheetMetalCodes.PreferencesUnreadable;
    public const string NotInTableCode = SheetMetalCodes.PreferenceMaterialNotInTable;
    public const string SharedByBodiesCode = SheetMetalCodes.PreferenceSharedByBodies;

    private readonly ISheetMetalPreferenceReader _reader;
    private readonly SheetMetalMaterialTable _table;
    private readonly SheetMetalText _text;

    /// <param name="text">Words the violations; <see cref="SheetMetalText.Default"/> when null.</param>
    public SheetMetalPreferenceConstraintProvider(ISheetMetalPreferenceReader reader, SheetMetalMaterialTable table, SheetMetalText? text = null)
    {
        _reader = reader;
        _table = table;
        _text = text ?? SheetMetalText.Default;
    }

    private string SourceLabel => _text.SourcePreferences;

    public string DomainId => ProviderDomainId;

    public IReadOnlyList<MaterialConstraint> ConstraintsFor(BodyId bodyId)
    {
        var read = _reader.ReadFor(bodyId);
        if (!read.IsSheetMetalBody)
            return Array.Empty<MaterialConstraint>();

        if (read.Preference is not { } preference)
        {
            // Nothing about the preferences is known, so no material can be shown to be settable in them.
            var error = read.ReadError ?? _text.NoPreferencesReturned;
            return new[]
            {
                new MaterialConstraint(
                    DomainId, SourceLabel, PreferencesUnreadableCode,
                    _ => false,
                    _ => _text.PreferencesUnreadableForAssignment(error))
                {
                    ViolationArgs = _ => new Dictionary<string, string> { ["error"] = error },
                },
            };
        }

        var constraints = new List<MaterialConstraint>
        {
            new(
                DomainId, SourceLabel, NotInTableCode,
                candidate => _table.FirstRowForPhysicalMaterial(candidate.Name) is not null,
                candidate => _text.PreferenceMaterialHasNoRow(candidate.Name, SheetMetalMaterialColumns.PhysicalMaterialName))
            {
                ViolationArgs = _ => new Dictionary<string, string> { ["column"] = SheetMetalMaterialColumns.PhysicalMaterialName },
            },
        };

        if (preference.SheetMetalBodyCount > 1)
        {
            constraints.Add(new MaterialConstraint(
                DomainId, SourceLabel, SharedByBodiesCode,
                _ => false,
                _ => _text.PreferencesSharedByBodies(preference.SheetMetalBodyCount),
                ConstraintSeverity.Warn)
            {
                ViolationArgs = _ => new Dictionary<string, string> { ["count"] = preference.SheetMetalBodyCount.ToString() },
            });
        }

        return constraints;
    }
}
