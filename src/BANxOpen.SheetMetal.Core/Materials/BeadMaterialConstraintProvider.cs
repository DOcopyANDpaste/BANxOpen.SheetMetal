using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Materials.Core.Rules.Features;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Requirements;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.Materials;

/// <summary>Tells the shared material engine what the beads already on a body allow. This is the reverse of
/// <see cref="Beads.Rules.MaterialAllowedRule"/>: that rule asks "is this SPEC allowed on the body's
/// material?", this asks "is this material allowed by the SPECs already on the body?". Both answer through
/// <see cref="BeadSpecRow.IsAllowedFor"/>, so the two directions cannot disagree about what a SPEC permits.
///
/// Deliberately thickness-blind. A SPEC allows a grade at some thicknesses and not others (it is one row per
/// thickness), but the engine asks about a physical material, and which Material Table row the part lands on — so
/// how thick the sheet becomes — is only settled by the row the user picks (<see cref="SheetMetalRowGateRule"/>). This
/// therefore refuses only a grade no row of the SPEC allows at any thickness. The thickness-exact check belongs
/// where a row with a thickness is actually in hand: <c>BeadOnBody.SpecAt</c>, used by the bead dialog.
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
/// Warned about — never blocked, since feature validation must not stand in the way of a preference update, but never
/// silent either, since a restriction that silently fails open is the defect this exists to fix:
/// <list type="bullet">
/// <item>A material with no row in the sheet metal material standards file has no grade to check against any SPEC →
/// <see cref="GradeUnrecognizedCode"/>. Emitted once, ahead of the per-SPEC constraints, so the user is told
/// to fix the mapping rather than shown one refusal per SPEC.</item>
/// <item>A recognised grade no row of a SPEC allows → <see cref="NotAllowedCode"/>, naming the SPEC.</item>
/// <item>A stamped SPEC no longer in its workbook → <see cref="SpecNotFoundCode"/>. Nothing can be verified
/// against a SPEC that no longer exists.</item>
/// </list>
///
/// A body with no beads produces no constraints and stays unrestricted.
///
/// SPECs the part already holds a requirement for (<see cref="ISheetMetalRequirementReader"/>) are judged by
/// <see cref="SheetMetalRequirementConstraintProvider"/> instead — thickness-exact, and the same thing NX checks — and
/// are skipped here, except for a warning (<see cref="RequirementOutOfDateCode"/>) when the SPEC workbook no longer
/// agrees with what the requirement recorded. This provider remains the path for beads built before requirements
/// existed.</summary>
public sealed class BeadMaterialConstraintProvider : IFeatureMaterialConstraintProvider
{
    public const string ProviderDomainId = "SHEETMETAL.BEAD";

    // Kept as aliases of SheetMetalCodes, which is where the codes are defined.
    public const string GradeUnrecognizedCode = SheetMetalCodes.MaterialGradeUnrecognized;
    public const string NotAllowedCode = SheetMetalCodes.MaterialNotAllowedByBead;
    public const string SpecNotFoundCode = SheetMetalCodes.BeadSpecNotFound;
    public const string UnstampedBeadUnmatchedCode = SheetMetalCodes.BeadSpecUnmatched;
    public const string FeaturesUnreadableCode = SheetMetalCodes.BeadFeaturesUnreadable;
    public const string RequirementOutOfDateCode = SheetMetalCodes.RequirementOutOfDate;

    private readonly IFeatureInventory _inventory;
    private readonly IBeadSpecLookup _specs;
    private readonly SheetMetalMaterialTable _table;
    private readonly BeadSettings _settings;
    private readonly ISheetMetalRequirementReader _requirements;
    private readonly SheetMetalText _text;

    /// <param name="text">Words the violations and their sources; <see cref="SheetMetalText.Default"/> when null.</param>
    public BeadMaterialConstraintProvider(
        IFeatureInventory inventory, IBeadSpecLookup specs, SheetMetalMaterialTable table, BeadSettings settings,
        ISheetMetalRequirementReader requirements, SheetMetalText? text = null)
    {
        _text = text ?? SheetMetalText.Default;
        _inventory = inventory;
        _specs = specs;
        _table = table;
        _settings = settings;
        _requirements = requirements;
    }

    public string DomainId => ProviderDomainId;

    public IReadOnlyList<MaterialConstraint> ConstraintsFor(BodyId bodyId)
    {
        var inventory = _inventory.Read(bodyId);
        if (inventory.IsEmpty)
            return Array.Empty<MaterialConstraint>();

        if (inventory.ReadError is { } error)
        {
            // Nothing about the body's beads is known, so nothing was checked against them.
            return new[]
            {
                new MaterialConstraint(
                    DomainId,
                    _text.SourceBodyFeatures,
                    FeaturesUnreadableCode,
                    _ => false,
                    _ => _text.BeadFeaturesUnreadable(error),
                    ConstraintSeverity.Warn)
                {
                    ViolationArgs = _ => new Dictionary<string, string> { ["error"] = error },
                },
            };
        }

        var applicable = new List<ApplicableSpec>();
        var warnings = new List<(string Label, string Message, IReadOnlyDictionary<string, string> Args)>();

        foreach (var bead in BeadsOnBody.Resolve(inventory, _specs, _settings))
        {
            if (bead.IsStamped)
            {
                applicable.Add(new ApplicableSpec(
                    bead.StandardId!, bead.SpecId!, bead.SpecFamily, _text.SourceStampedBead(bead.SpecId!, bead.StandardId!)));
            }
            else if (bead.IsSpecKnown)
            {
                applicable.Add(new ApplicableSpec(
                    bead.StandardId!, bead.SpecId!, bead.SpecFamily,
                    _text.SourceMatchedBead(bead.Name, bead.SpecId!, bead.StandardId!)));
            }
            else
            {
                warnings.Add((_text.SourceUnidentifiedBead(bead.Name), UnidentifiedMessage(bead), UnidentifiedArgs(bead)));
            }
        }

        var constraints = new List<MaterialConstraint>();

        // Several beads on one body are often built to the same SPEC — including an unstamped bead that
        // matches a stamped one. One constraint per SPEC is enough; the first label, a stamped one where
        // there is one, names it.
        var distinct = applicable
            .GroupBy(a => (a.StandardId, a.SpecId))
            .Select(g => g.First())
            .ToList();

        // SPECs with a requirement in use in the part are the requirement provider's to judge. When the requirements
        // could not all be read nothing is handed over: that provider warns about the read error, and the workbook check
        // below keeps every bead checked even if it is not registered.
        var (requirements, errors) = _requirements.ReadAll();
        var covered = requirements
            .Where(r => r.Domain == ProviderDomainId && r.IsInUse)
            .ToDictionary(r => r.Key, r => r);

        foreach (var spec in distinct.Where(s => covered.ContainsKey(SheetMetalRequirement.KeyFor(s.StandardId, s.SpecId))))
        {
            if (Drift(covered[SheetMetalRequirement.KeyFor(spec.StandardId, spec.SpecId)], spec.Family) is { } drift)
            {
                constraints.Add(new MaterialConstraint(
                    DomainId, spec.Label, RequirementOutOfDateCode, _ => false, _ => drift.Message, ConstraintSeverity.Warn)
                {
                    ViolationArgs = _ => drift.Args,
                });
            }
        }

        if (errors.Count == 0)
            distinct = distinct.Where(s => !covered.ContainsKey(SheetMetalRequirement.KeyFor(s.StandardId, s.SpecId))).ToList();

        if (distinct.Count > 0)
        {
            constraints.Add(new MaterialConstraint(
                DomainId,
                distinct.Count == 1 ? distinct[0].Label : _text.SourceBeadSpecCount(distinct.Count),
                GradeUnrecognizedCode,
                candidate => _table.GradeForPhysicalMaterial(candidate.Name) is not null,
                candidate => _text.BeadGradeUnrecognized(candidate.Name),
                ConstraintSeverity.Warn));
        }

        foreach (var spec in distinct)
            constraints.Add(ConstraintFor(spec));

        foreach (var (label, message, args) in warnings)
        {
            constraints.Add(new MaterialConstraint(
                DomainId, label, UnstampedBeadUnmatchedCode,
                _ => false,
                _ => message,
                ConstraintSeverity.Warn)
            {
                ViolationArgs = _ => args,
            });
        }

        return constraints;
    }

    private string UnidentifiedMessage(BeadOnBody bead) => bead.Status switch
    {
        BeadSpecStatus.GeometryUnreadable => _text.BeadGeometryUnreadable(bead.Name),
        BeadSpecStatus.Ambiguous => _text.BeadAmbiguous(bead.Name, string.Join(", ", bead.Candidates.Select(c => c.SpecId))),
        _ => _text.BeadUnmatched(bead.Name),
    };

    /// <summary>UnstampedBeadUnmatchedCode: feature, status (geometryUnreadable | ambiguous | noMatch), candidates.</summary>
    private static IReadOnlyDictionary<string, string> UnidentifiedArgs(BeadOnBody bead) => new Dictionary<string, string>
    {
        ["feature"] = bead.Name,
        ["status"] = bead.Status switch
        {
            BeadSpecStatus.GeometryUnreadable => "geometryUnreadable",
            BeadSpecStatus.Ambiguous => "ambiguous",
            _ => "noMatch",
        },
        ["candidates"] = string.Join(", ", bead.Candidates.Select(c => c.SpecId)),
    };

    private MaterialConstraint ConstraintFor(ApplicableSpec spec)
    {
        if (spec.Family.Count == 0)
        {
            return new MaterialConstraint(
                DomainId,
                spec.Label,
                SpecNotFoundCode,
                _ => false,
                _ => _text.BeadSpecNotFound(spec.SpecId, spec.StandardId),
                ConstraintSeverity.Warn)
            {
                ViolationArgs = _ => new Dictionary<string, string> { ["spec"] = spec.SpecId, ["standard"] = spec.StandardId },
            };
        }

        // Asked of a PHYSICAL MATERIAL, which carries no thickness: which Material Table row the part ends up on —
        // and therefore how thick the sheet becomes — is settled by the picked row, in SheetMetalRowGateRule. So the
        // question this gate can honestly answer is "could this grade ever work with this SPEC", i.e. is it allowed
        // by any thickness the SPEC is driven for. The thickness-exact answer is the bead dialog's (BeadOnBody.SpecAt).
        IReadOnlyList<BeadSpecRow> family = spec.Family;
        return new MaterialConstraint(
            DomainId,
            spec.Label,
            NotAllowedCode,
            // An unmapped grade passes here on purpose: the grade-recognition constraint already warns about it, with
            // a message that says what is actually wrong.
            candidate => _table.GradeForPhysicalMaterial(candidate.Name) is not { } grade
                         || family.Any(row => row.IsAllowedFor(grade)),
            candidate => _text.BeadMaterialNotAllowed(candidate.Name, _table.GradeForPhysicalMaterial(candidate.Name), spec.SpecId),
            ConstraintSeverity.Warn)
        {
            ViolationArgs = candidate => new Dictionary<string, string>
            {
                ["spec"] = spec.SpecId,
                ["grade"] = _table.GradeForPhysicalMaterial(candidate.Name) ?? string.Empty,
            },
        };
    }

    private sealed record DriftNote(string Message, IReadOnlyDictionary<string, string> Args);

    /// <summary>Why the requirement the part holds for a SPEC no longer matches its workbook, or null when it does. The
    /// requirement still governs — it is what the beads were built to — but re-applying them would change it.
    /// RequirementOutOfDateCode args: requirement, builtThickness, workbookPairs, recordedPairs.</summary>
    private DriftNote? Drift(SheetMetalRequirement requirement, IReadOnlyList<BeadSpecRow> family)
    {
        if (family.Count == 0)
            return null; // SpecNotFoundCode is not raised for covered SPECs, so the stamp's own path is not needed here.

        var recorded = requirement.Allowance;
        var workbook = SheetMetalAllowance.FromFamily(family, recorded.BuiltThickness);
        if (workbook.SamePairsAs(recorded))
            return null;

        var args = new Dictionary<string, string>
        {
            ["requirement"] = requirement.Label,
            ["builtThickness"] = recorded.ThicknessText,
            ["workbookPairs"] = workbook.PairsText,
            ["recordedPairs"] = recorded.PairsText,
        };
        return new DriftNote(_text.RequirementPairsChanged(requirement.Label, workbook.PairsText, recorded.PairsText), args);
    }

    /// <summary>A SPEC that applies to the body, whether read off a stamp or matched from geometry. An empty
    /// <see cref="Family"/> means a stamped SPEC that could not be found.</summary>
    private sealed record ApplicableSpec(
        string StandardId, string SpecId, IReadOnlyList<BeadSpecRow> Family, string Label);
}
