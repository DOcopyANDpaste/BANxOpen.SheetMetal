using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.Core.Materials.Constraints;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>Tells the shared material engine what the beads already on a body allow. This is the reverse of
/// <see cref="Beads.Rules.MaterialAllowedRule"/>: that rule asks "is this SPEC allowed on the body's
/// material?", this asks "is this material allowed by the SPECs already on the body?". Both answer through
/// <see cref="BeadSpecRow.IsAllowedFor"/>, so the two directions cannot disagree about what a SPEC permits.
///
/// Every uncertain case blocks rather than allows. A material restriction that silently fails open is the
/// exact defect this provider exists to fix, so:
/// <list type="bullet">
/// <item>A material with no grade-map entry cannot be checked against any SPEC →
/// <see cref="GradeUnrecognizedCode"/>. Emitted once, ahead of the per-SPEC constraints, so the user is told
/// to fix the mapping rather than shown one refusal per SPEC for a material that was never evaluated.</item>
/// <item>A recognised grade that a stamped SPEC does not allow → <see cref="NotAllowedCode"/>, naming the
/// SPEC.</item>
/// <item>A stamped SPEC that can no longer be found in its workbook blocks every material →
/// <see cref="SpecNotFoundCode"/>. Nothing can be verified against a SPEC that no longer exists.</item>
/// <item>A bead with no stamp at all — built by hand in NX rather than by the tool — blocks every material →
/// <see cref="UnstampedBeadCode"/>. Its SPEC is unknown, which is the same situation the bead dialog already
/// refuses to guess about. Reported first, because nothing else can be resolved until it is.</item>
/// </list>
///
/// A body with no beads at all produces no constraints, which the engine treats as unrestricted.</summary>
public sealed class BeadMaterialConstraintProvider : IFeatureMaterialConstraintProvider
{
    public const string ProviderDomainId = "SHEETMETAL.BEAD";

    public const string GradeUnrecognizedCode = "MATERIAL_GRADE_UNRECOGNIZED";
    public const string NotAllowedCode = "MATERIAL_NOT_ALLOWED_BY_BEAD";
    public const string SpecNotFoundCode = "BEAD_SPEC_NOT_FOUND";
    public const string UnstampedBeadCode = "BEAD_SPEC_UNKNOWN";

    private readonly IFeatureInventory _inventory;
    private readonly IBeadSpecLookup _specs;
    private readonly MaterialGradeMap _gradeMap;

    public BeadMaterialConstraintProvider(IFeatureInventory inventory, IBeadSpecLookup specs, MaterialGradeMap gradeMap)
    {
        _inventory = inventory;
        _specs = specs;
        _gradeMap = gradeMap;
    }

    public string DomainId => ProviderDomainId;

    public IReadOnlyList<MaterialConstraint> ConstraintsFor(BodyId bodyId)
    {
        var inventory = _inventory.Read(bodyId);
        if (inventory.IsEmpty)
            return Array.Empty<MaterialConstraint>();

        var constraints = new List<MaterialConstraint>();

        if (inventory.UnstampedFeatures.Count > 0)
        {
            var names = string.Join(", ", inventory.UnstampedFeatures.Select(n => $"'{n}'"));
            constraints.Add(new MaterialConstraint(
                DomainId,
                $"bead feature {names}",
                UnstampedBeadCode,
                _ => false,
                _ => $"Bead feature {names} on this body was not created by the bead tool, so its SPEC is unknown and " +
                     "no material can be verified against it. Re-apply it with the bead dialog to record its SPEC."));
        }

        // Several beads on one body are often built to the same SPEC; one constraint per distinct SPEC is
        // enough and keeps the block message from repeating itself.
        var stamps = inventory.Stamps.Distinct().ToList();
        if (stamps.Count == 0)
            return constraints;

        constraints.Add(new MaterialConstraint(
            DomainId,
            stamps.Count == 1 ? Label(stamps[0]) : $"{stamps.Count} bead SPECs on this body",
            GradeUnrecognizedCode,
            candidate => _gradeMap.GradeFor(candidate.Name) is not null,
            candidate =>
                $"Material '{candidate.Name}' has no entry in material-grade-map.json, so it cannot be checked " +
                "against the bead SPECs on this body. Add a mapping for it before assigning."));

        foreach (var stamp in stamps)
        {
            var found = _specs.Find(stamp.StandardId, stamp.SpecId);
            if (found is null)
            {
                constraints.Add(new MaterialConstraint(
                    DomainId,
                    Label(stamp),
                    SpecNotFoundCode,
                    _ => false,
                    _ => $"SPEC '{stamp.SpecId}' is no longer in Standard '{stamp.StandardId}', so no material can be " +
                         "verified against the bead built to it. Check the Standard's workbook, or re-apply the bead " +
                         "with a current SPEC."));
                continue;
            }

            BeadSpecRow spec = found;
            constraints.Add(new MaterialConstraint(
                DomainId,
                Label(stamp),
                NotAllowedCode,
                // An unmapped grade passes here on purpose: the grade-recognition constraint above already
                // blocks it, with a message that says what is actually wrong.
                candidate => _gradeMap.GradeFor(candidate.Name) is not { } grade || spec.IsAllowedFor(grade),
                candidate =>
                    $"Material '{candidate.Name}' (grade '{_gradeMap.GradeFor(candidate.Name)}') is not allowed by " +
                    $"SPEC '{spec.SpecId}'."));
        }

        return constraints;
    }

    private static string Label(FeatureSpecStamp stamp) => $"Bead SPEC {stamp.SpecId} (Standard {stamp.StandardId})";
}
