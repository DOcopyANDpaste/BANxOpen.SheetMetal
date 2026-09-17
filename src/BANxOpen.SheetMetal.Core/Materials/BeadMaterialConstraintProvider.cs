using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.Core.Materials.Rules.Features;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>Tells the shared material engine what the beads already on a body allow. This is the reverse of
/// <see cref="Beads.Rules.MaterialAllowedRule"/>: that rule asks "is this SPEC allowed on the body's
/// material?", this asks "is this material allowed by the SPECs already on the body?". Both answer through
/// <see cref="BeadSpecRow.IsAllowedFor"/>, so the two directions cannot disagree about what a SPEC permits.
///
/// Which SPECs apply to the body:
/// <list type="bullet">
/// <item>Every SPEC stamped on a bead the tool built.</item>
/// <item>For a bead with no stamp, the one SPEC its geometry matches (<see cref="BeadSpecMatcher"/>). A matched
/// bead is enforced exactly like a stamped one. One with no match, several matches, or unreadable geometry
/// cannot be enforced, so it produces a warning (<see cref="UnstampedBeadUnmatchedCode"/>) and the assignment
/// is allowed. Stamping the SPEC onto the feature happens in the bead dialog, never here.</item>
/// </list>
///
/// Blocking cases, since a restriction that silently fails open is the defect this exists to fix:
/// <list type="bullet">
/// <item>A material with no row in the sheet metal material standards file has no grade to check against any SPEC →
/// <see cref="GradeUnrecognizedCode"/>. Emitted once, ahead of the per-SPEC constraints, so the user is told
/// to fix the mapping rather than shown one refusal per SPEC.</item>
/// <item>A recognised grade a SPEC does not allow → <see cref="NotAllowedCode"/>, naming the SPEC.</item>
/// <item>A stamped SPEC no longer in its workbook → <see cref="SpecNotFoundCode"/>. Nothing can be verified
/// against a SPEC that no longer exists.</item>
/// </list>
///
/// A body with no beads produces no constraints and stays unrestricted.</summary>
public sealed class BeadMaterialConstraintProvider : IFeatureMaterialConstraintProvider
{
    public const string ProviderDomainId = "SHEETMETAL.BEAD";

    public const string GradeUnrecognizedCode = "MATERIAL_GRADE_UNRECOGNIZED";
    public const string NotAllowedCode = "MATERIAL_NOT_ALLOWED_BY_BEAD";
    public const string SpecNotFoundCode = "BEAD_SPEC_NOT_FOUND";
    public const string UnstampedBeadUnmatchedCode = "BEAD_SPEC_UNMATCHED";
    public const string FeaturesUnreadableCode = "BEAD_FEATURES_UNREADABLE";

    private readonly IFeatureInventory _inventory;
    private readonly IBeadSpecLookup _specs;
    private readonly SheetMetalMaterialTable _table;
    private readonly BeadSettings _settings;

    public BeadMaterialConstraintProvider(
        IFeatureInventory inventory, IBeadSpecLookup specs, SheetMetalMaterialTable table, BeadSettings settings)
    {
        _inventory = inventory;
        _specs = specs;
        _table = table;
        _settings = settings;
    }

    public string DomainId => ProviderDomainId;

    public IReadOnlyList<MaterialConstraint> ConstraintsFor(BodyId bodyId)
    {
        var inventory = _inventory.Read(bodyId);
        if (inventory.IsEmpty)
            return Array.Empty<MaterialConstraint>();

        if (inventory.ReadError is { } error)
        {
            // Nothing about the body's beads is known, so nothing can be allowed on it.
            return new[]
            {
                new MaterialConstraint(
                    DomainId,
                    "this body's features",
                    FeaturesUnreadableCode,
                    _ => false,
                    _ => $"The features on this body could not be read, so its bead SPEC restrictions cannot be " +
                         $"checked: {error}"),
            };
        }

        var applicable = new List<ApplicableSpec>();
        var warnings = new List<(string Label, string Message)>();

        foreach (var stamp in inventory.Stamps.Distinct())
        {
            applicable.Add(new ApplicableSpec(
                stamp.StandardId, stamp.SpecId,
                _specs.Find(stamp.StandardId, stamp.SpecId),
                $"Bead SPEC {stamp.SpecId} (Standard {stamp.StandardId})"));
        }

        IdentifyUnstamped(inventory.UnstampedFeatures, applicable, warnings);

        var constraints = new List<MaterialConstraint>();

        // Several beads on one body are often built to the same SPEC — including an unstamped bead that
        // matches a stamped one. One constraint per SPEC is enough; the first label, a stamped one where
        // there is one, names it.
        var distinct = applicable
            .GroupBy(a => (a.StandardId, a.SpecId))
            .Select(g => g.First())
            .ToList();

        if (distinct.Count > 0)
        {
            constraints.Add(new MaterialConstraint(
                DomainId,
                distinct.Count == 1 ? distinct[0].Label : $"{distinct.Count} bead SPECs on this body",
                GradeUnrecognizedCode,
                candidate => _table.GradeForPhysicalMaterial(candidate.Name) is not null,
                candidate =>
                    $"Material '{candidate.Name}' has no row in the sheet metal material standards file, so it cannot be " +
                    "checked against the bead SPECs on this body. Add a row for it before assigning."));
        }

        foreach (var spec in distinct)
            constraints.Add(ConstraintFor(spec));

        foreach (var (label, message) in warnings)
        {
            constraints.Add(new MaterialConstraint(
                DomainId, label, UnstampedBeadUnmatchedCode,
                _ => false,
                _ => message,
                ConstraintSeverity.Warn));
        }

        return constraints;
    }

    private void IdentifyUnstamped(
        IReadOnlyList<UnstampedFeature> unstamped,
        List<ApplicableSpec> applicable,
        List<(string Label, string Message)> warnings)
    {
        if (unstamped.Count == 0)
            return;

        // Read once: an unstamped bead has no Standard, so every Standard is searched, and a body can carry
        // several such beads.
        var allSpecs = _specs.AllSpecs();

        foreach (var feature in unstamped)
        {
            var label = $"bead feature '{feature.Name}'";

            if (feature.Geometry is null)
            {
                warnings.Add((label,
                    $"Bead feature '{feature.Name}' was not created by the bead tool and its geometry could not be " +
                    "read, so its SPEC material restriction is not enforced. Re-apply it with the bead dialog to " +
                    "record its SPEC."));
                continue;
            }

            var match = BeadSpecMatcher.Match(feature.Geometry, allSpecs, _settings);

            if (match.Single is { } row)
            {
                applicable.Add(new ApplicableSpec(
                    row.StandardId, row.SpecId, row,
                    $"bead feature '{feature.Name}' (unstamped; geometry matches SPEC {row.SpecId}, Standard {row.StandardId})"));
            }
            else if (match.IsAmbiguous)
            {
                var specs = string.Join(", ", match.Candidates.Select(c => c.SpecId));
                warnings.Add((label,
                    $"Bead feature '{feature.Name}' was not created by the bead tool and its geometry matches more " +
                    $"than one SPEC ({specs}), so no SPEC material restriction is enforced for it. Re-apply it with " +
                    "the bead dialog to record which SPEC it is."));
            }
            else
            {
                warnings.Add((label,
                    $"Bead feature '{feature.Name}' was not created by the bead tool and its geometry matches no SPEC " +
                    "in any Standard, so no SPEC material restriction is enforced for it. Re-apply it with the bead " +
                    "dialog using a valid SPEC."));
            }
        }
    }

    private MaterialConstraint ConstraintFor(ApplicableSpec spec)
    {
        if (spec.Row is null)
        {
            return new MaterialConstraint(
                DomainId,
                spec.Label,
                SpecNotFoundCode,
                _ => false,
                _ => $"SPEC '{spec.SpecId}' is no longer in Standard '{spec.StandardId}', so no material can be " +
                     "verified against the bead built to it. Check the Standard's workbook, or re-apply the bead " +
                     "with a current SPEC.");
        }

        BeadSpecRow row = spec.Row;
        return new MaterialConstraint(
            DomainId,
            spec.Label,
            NotAllowedCode,
            // An unmapped grade passes here on purpose: the grade-recognition constraint already blocks it, with
            // a message that says what is actually wrong.
            candidate => _table.GradeForPhysicalMaterial(candidate.Name) is not { } grade || row.IsAllowedFor(grade),
            candidate =>
                $"Material '{candidate.Name}' (grade '{_table.GradeForPhysicalMaterial(candidate.Name)}') is not allowed by " +
                $"SPEC '{row.SpecId}'.");
    }

    /// <summary>A SPEC that applies to the body, whether read off a stamp or matched from geometry. A null
    /// <see cref="Row"/> means a stamped SPEC that could not be found.</summary>
    private sealed record ApplicableSpec(string StandardId, string SpecId, BeadSpecRow? Row, string Label);
}
