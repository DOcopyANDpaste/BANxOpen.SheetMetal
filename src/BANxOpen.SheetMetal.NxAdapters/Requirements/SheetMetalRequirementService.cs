using NXOpen;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.Core.Requirements;
using BANxOpen.Foundation.NxAdapters;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.Requirements;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.NxAdapters.Requirements;

/// <summary>Brings the part's bead requirements in line with the beads on a body: one requirement per SPEC, naming
/// every bead built to it (<see cref="BeadRequirementFactory"/>), written as native NX Requirements through
/// <see cref="IPartRequirementStore"/>.
///
/// Called by the Bead tool inside its Apply undo mark (with the SPEC it just applied), and by the Material Assignment
/// dialog after an assignment (with nothing applied — that only refreshes which beads each requirement names, and
/// never moves a SPEC's built thickness).
///
/// Also ensures the grade bridge expression (<see cref="SheetMetalRequirementNames.GradeExpression"/>) the GRADE
/// requirements are checked against.</summary>
public sealed class SheetMetalRequirementService
{
    private readonly NxSessionContext _context;
    private readonly IFeatureInventory _inventory;
    private readonly IBeadSpecLookup _specs;
    private readonly BeadSettings _settings;
    private readonly ISheetMetalPreferenceReader _preferences;
    private readonly IPartRequirementStore _store;
    private readonly ISheetMetalRequirementReader _reader;

    public SheetMetalRequirementService(
        NxSessionContext context,
        IFeatureInventory inventory,
        IBeadSpecLookup specs,
        BeadSettings settings,
        ISheetMetalPreferenceReader preferences,
        IPartRequirementStore store,
        ISheetMetalRequirementReader reader)
    {
        _context = context;
        _inventory = inventory;
        _specs = specs;
        _settings = settings;
        _preferences = preferences;
        _store = store;
        _reader = reader;
    }

    /// <summary>The part's sheet metal requirements, for a caller that wants to judge by them.</summary>
    public (IReadOnlyList<SheetMetalRequirement> Requirements, IReadOnlyList<string> Errors) Read() => _reader.ReadAll();

    /// <summary>Rewrites the bead requirements for <paramref name="bodyId"/>'s beads.</summary>
    /// <param name="rebuilt">The SPEC rows applied in this run, by (Standard, SPEC id): those SPECs' requirements take
    /// the applied row's thickness and grades.</param>
    /// <returns>On success, notes about SPECs that could not be given a requirement, for the caller to show.</returns>
    public OperationResult<IReadOnlyList<string>> RefreshBeads(
        BodyId bodyId, IReadOnlyDictionary<(string StandardId, string SpecId), BeadSpecRow> rebuilt)
    {
        if (FindExpression(SheetMetalRequirementNames.ThicknessExpression) is null)
        {
            return OperationResult<IReadOnlyList<string>>.Fail(
                "THICKNESS_EXPRESSION_MISSING",
                $"This part has no '{SheetMetalRequirementNames.ThicknessExpression}' expression, so a bead requirement " +
                "has nothing to check the thickness against. Set the Sheet Metal Preferences to a Material Table entry first.");
        }

        var inventory = _inventory.Read(bodyId);
        if (inventory.ReadError is { } readError)
        {
            // Writing from an empty read would delete every requirement the part holds.
            return OperationResult<IReadOnlyList<string>>.Fail(
                "BEAD_FEATURES_UNREADABLE", $"The beads on this body could not be read, so their requirements were not updated: {readError}");
        }

        var (previous, errors) = _reader.ReadAll();
        if (errors.Count > 0)
        {
            return OperationResult<IReadOnlyList<string>>.Fail(
                "REQUIREMENTS_UNREADABLE", $"This part's requirements could not be read, so they were not updated: {string.Join(" ", errors)}");
        }

        var beads = BeadsOnBody.Resolve(inventory, _specs, _settings);
        var result = BeadRequirementFactory.Create(beads, previous, rebuilt, _preferences.ReadFor(bodyId).BodyThickness);

        if (result.Requirements.Count > 0)
        {
            var bridge = EnsureGradeExpression();
            if (!bridge.Ok)
                return OperationResult<IReadOnlyList<string>>.Fail(bridge.ErrorCode ?? "GRADE_EXPRESSION_FAILED", bridge.Message ?? "");
        }

        var specs = result.Requirements.SelectMany(SheetMetalRequirementMapper.ToSpecs).ToList();
        var written = _store.Refresh(BeadMaterialConstraintProvider.ProviderDomainId, specs);
        if (!written.Ok)
            return OperationResult<IReadOnlyList<string>>.Fail(written.ErrorCode ?? "REQUIREMENT_WRITE_FAILED", written.Message ?? "");

        foreach (var note in result.Notes)
            _context.Log.Warn(note);

        return OperationResult<IReadOnlyList<string>>.Success(result.Notes);
    }

    /// <summary>Creates <see cref="SheetMetalRequirementNames.GradeExpression"/>, or points it back at the grade
    /// attribute if someone repointed it.</summary>
    private OperationResult EnsureGradeExpression()
    {
        var formula = SheetMetalRequirementNames.GradeExpressionFormula;
        try
        {
            var existing = FindExpression(SheetMetalRequirementNames.GradeExpression);
            if (existing is null)
                _context.WorkPart.Expressions.CreateExpression("String", $"{SheetMetalRequirementNames.GradeExpression}={formula}");
            else if (existing.RightHandSide != formula)
                _context.WorkPart.Expressions.Edit(existing, formula);

            return OperationResult.Success();
        }
        catch (NXException ex)
        {
            _context.Log.Error($"The '{SheetMetalRequirementNames.GradeExpression}' expression could not be written: NX {ex.ErrorCode}: {ex.Message}");
            return OperationResult.Fail(
                "GRADE_EXPRESSION_FAILED", $"The '{SheetMetalRequirementNames.GradeExpression}' expression could not be written: NX {ex.ErrorCode}: {ex.Message}");
        }
    }

    private Expression? FindExpression(string name) =>
        _context.WorkPart.Expressions.Cast<Expression>().FirstOrDefault(e => e.Name == name);
}
