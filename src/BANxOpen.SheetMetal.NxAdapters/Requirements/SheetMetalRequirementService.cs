using NXOpen;
using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.Contracts.Requirements;
using BANxOpen.Foundation.Core;
using BANxOpen.Foundation.Core.Requirements;
using BANxOpen.Foundation.NxAdapters;
using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.Materials;
using BANxOpen.SheetMetal.Requirements;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.NxAdapters.Requirements;

/// <summary>What <see cref="SheetMetalRequirementService.SyncBeads"/> did.</summary>
/// <param name="Changed">Whether anything was written; false when the part already held what its beads impose.</param>
/// <param name="Notes">SPECs that could not be given a requirement, for the caller to show.</param>
public sealed record BeadRequirementSync(bool Changed, IReadOnlyList<string> Notes);

/// <summary>Brings the part's bead requirements in line with the beads on a body: one requirement per SPEC, naming
/// every bead built to it (<see cref="BeadRequirementFactory"/>), written as native NX Requirements through
/// <see cref="IPartRequirementStore"/>.
///
/// Called by the Bead tool inside its Apply undo mark (with the SPEC it just applied) and when it opens
/// (<see cref="SyncBeads"/>, with nothing applied), and by the Material Assignment dialog after an assignment (with
/// nothing applied). Nothing applied only refreshes which beads each requirement names and drops SPECs with no beads
/// left — it never changes a SPEC's recorded pairs or built thickness.
///
/// Also ensures the bridge expressions the PAIR requirements are checked against
/// (<see cref="SheetMetalRequirementNames.GradeExpression"/>, <see cref="SheetMetalRequirementNames.PairExpression"/>).</summary>
public sealed class SheetMetalRequirementService
{
    public static readonly IReadOnlyDictionary<(string StandardId, string SpecId), BeadSpecRow> NothingRebuilt =
        new Dictionary<(string, string), BeadSpecRow>();

    /// <summary>Grade first: the pair expression refers to it.</summary>
    private static readonly (string Name, Func<string> Formula)[] Bridge =
    {
        (SheetMetalRequirementNames.GradeExpression, () => SheetMetalRequirementNames.GradeExpressionFormula),
        (SheetMetalRequirementNames.PairExpression, () => SheetMetalRequirementNames.PairExpressionFormula),
    };

    private readonly NxSessionContext _context;
    private readonly IFeatureInventory _inventory;
    private readonly IBeadSpecLookup _specs;
    private readonly BeadSettings _settings;
    private readonly ISheetMetalPreferenceReader _preferences;
    private readonly IPartRequirementStore _store;
    private readonly ISheetMetalRequirementReader _reader;
    private readonly SheetMetalText _text;

    /// <param name="text">Words the failures and notes a tool shows; <see cref="SheetMetalText.Default"/> when null.</param>
    public SheetMetalRequirementService(
        NxSessionContext context,
        IFeatureInventory inventory,
        IBeadSpecLookup specs,
        BeadSettings settings,
        ISheetMetalPreferenceReader preferences,
        IPartRequirementStore store,
        ISheetMetalRequirementReader reader,
        SheetMetalText? text = null)
    {
        _text = text ?? SheetMetalText.Default;
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

    /// <summary>Diagnostic: the bead requirements' checks and the objects they hold, alive or dead
    /// (<see cref="IPartRequirementStore.Audit"/>).</summary>
    public IReadOnlyList<string> AuditBeads() => _store.Audit(BeadMaterialConstraintProvider.ProviderDomainId);

    /// <summary>Rewrites the bead requirements for <paramref name="bodyId"/>'s beads.</summary>
    /// <param name="rebuilt">The SPEC rows applied in this run, by (Standard, SPEC id): those SPECs' requirements take
    /// the applied row's whole family, and its thickness as built.</param>
    /// <returns>On success, notes about SPECs that could not be given a requirement, for the caller to show.</returns>
    public OperationResult<IReadOnlyList<string>> RefreshBeads(
        BodyId bodyId, IReadOnlyDictionary<(string StandardId, string SpecId), BeadSpecRow> rebuilt)
    {
        var planned = Plan(bodyId, rebuilt);
        if (!planned.Ok)
            return OperationResult<IReadOnlyList<string>>.Fail(planned.ErrorCode ?? SheetMetalCodes.RequirementRefreshFailed, planned.Message ?? "");

        var (specs, notes) = planned.Value!;
        var written = Write(specs, notes);
        return written.Ok
            ? OperationResult<IReadOnlyList<string>>.Success(notes)
            : OperationResult<IReadOnlyList<string>>.Fail(written.ErrorCode ?? FoundationCodes.RequirementWriteFailed, written.Message ?? "");
    }

    /// <summary>Brings the bead requirements up to date with the beads in the part, writing only when they differ:
    /// beads added or deleted since, a requirement still in the legacy GRADE + THICKNESS form, or a bridge expression
    /// missing or repointed. The caller holds the undo mark.</summary>
    public OperationResult<BeadRequirementSync> SyncBeads(BodyId bodyId)
    {
        var planned = Plan(bodyId, NothingRebuilt);
        if (!planned.Ok)
            return OperationResult<BeadRequirementSync>.Fail(planned.ErrorCode ?? SheetMetalCodes.RequirementRefreshFailed, planned.Message ?? "");

        var (specs, notes) = planned.Value!;
        var held = _store.Read(BeadMaterialConstraintProvider.ProviderDomainId);
        var upToDate = held.Ok
                       && RequirementDiff.Compute(held.Requirements.Select(r => r.Spec).ToList(), specs).IsEmpty
                       && (specs.Count == 0 || BridgeIsCurrent());
        if (upToDate)
            return OperationResult<BeadRequirementSync>.Success(new BeadRequirementSync(false, notes));

        var written = Write(specs, notes);
        return written.Ok
            ? OperationResult<BeadRequirementSync>.Success(new BeadRequirementSync(true, notes))
            : OperationResult<BeadRequirementSync>.Fail(written.ErrorCode ?? FoundationCodes.RequirementWriteFailed, written.Message ?? "");
    }

    /// <summary>The bead requirements <paramref name="bodyId"/>'s beads impose, as the specs to write, and notes on the
    /// SPECs that could not be given one. Fails rather than plan from a read that went wrong: writing what it planned
    /// would delete requirements the part holds.</summary>
    private OperationResult<(IReadOnlyList<RequirementSpec> Specs, IReadOnlyList<string> Notes)> Plan(
        BodyId bodyId, IReadOnlyDictionary<(string StandardId, string SpecId), BeadSpecRow> rebuilt)
    {
        if (FindExpression(SheetMetalRequirementNames.ThicknessExpression) is null)
        {
            return OperationResult<(IReadOnlyList<RequirementSpec>, IReadOnlyList<string>)>.Fail(
                SheetMetalCodes.ThicknessExpressionMissing,
                _text.ThicknessExpressionMissing(SheetMetalRequirementNames.ThicknessExpression));
        }

        var inventory = _inventory.Read(bodyId);
        if (inventory.ReadError is { } readError)
        {
            return OperationResult<(IReadOnlyList<RequirementSpec>, IReadOnlyList<string>)>.Fail(
                SheetMetalCodes.BeadFeaturesUnreadable, _text.BeadsUnreadableForRequirements(readError));
        }

        var (previous, errors) = _reader.ReadAll();
        if (errors.Count > 0)
        {
            return OperationResult<(IReadOnlyList<RequirementSpec>, IReadOnlyList<string>)>.Fail(
                FoundationCodes.RequirementsUnreadable, _text.RequirementsUnreadableForRefresh(string.Join(" ", errors)));
        }

        var beads = BeadsOnBody.Resolve(inventory, _specs, _settings);
        var result = BeadRequirementFactory.Create(beads, previous, rebuilt, _preferences.ReadFor(bodyId).BodyThickness, _text);
        IReadOnlyList<RequirementSpec> specs = result.Requirements.SelectMany(SheetMetalRequirementMapper.ToSpecs).ToList();
        return OperationResult<(IReadOnlyList<RequirementSpec>, IReadOnlyList<string>)>.Success((specs, result.Notes));
    }

    private OperationResult Write(IReadOnlyList<RequirementSpec> specs, IReadOnlyList<string> notes)
    {
        if (specs.Count > 0)
        {
            var bridge = EnsureBridgeExpressions();
            if (!bridge.Ok)
                return bridge;
        }

        var written = _store.Refresh(BeadMaterialConstraintProvider.ProviderDomainId, specs);
        if (!written.Ok)
            return written;

        foreach (var note in notes)
            _context.Log.Warn(note);

        return OperationResult.Success();
    }

    private bool BridgeIsCurrent() =>
        Bridge.All(b => FindExpression(b.Name) is { } expression && expression.RightHandSide == b.Formula());

    /// <summary>Creates the bridge expressions — <see cref="SheetMetalRequirementNames.GradeExpression"/> reading the
    /// grade attribute, then <see cref="SheetMetalRequirementNames.PairExpression"/> joining it with the thickness — or
    /// points them back at their formulas if someone repointed them, and makes them re-read the attribute either way.
    ///
    /// The grade expression reads a part attribute, which is outside NX's update graph: when the attribute changes
    /// nothing marks the expression out of date, and the Expressions dialog shows its value with a '?'. The PAIR check
    /// then has no current value and reports Unknown. Editing the formula (even to itself) and running an update makes
    /// NX evaluate it now, before the checks are evaluated.</summary>
    private OperationResult EnsureBridgeExpressions()
    {
        foreach (var (name, formula) in Bridge)
        {
            try
            {
                var expression = FindExpression(name);
                if (expression is null)
                    _context.WorkPart.Expressions.CreateExpression("String", $"{name}={formula()}");
                else
                    _context.WorkPart.Expressions.Edit(expression, formula());
            }
            catch (NXException ex)
            {
                _context.Log.Error($"The '{name}' expression could not be written: NX {ex.ErrorCode}: {ex.Message}");
                return OperationResult.Fail(SheetMetalCodes.GradeExpressionFailed, _text.GradeExpressionFailed(name, NxDialogText.Default.NxError(ex)));
            }
        }

        UpdateNow();
        foreach (var (name, _) in Bridge)
        {
            if (FindExpression(name) is { } expression)
                _context.Log.Debug($"'{name}' = '{SafeStringValue(expression)}'.");
        }

        return OperationResult.Success();
    }

    /// <summary>Runs NX's update on an invisible mark nested in the caller's: the caller's own mark still takes the
    /// whole Apply back.</summary>
    private void UpdateNow()
    {
        var session = _context.Session;
        var mark = session.SetUndoMark(Session.MarkVisibility.Invisible, "Update requirement expressions");
        var errors = session.UpdateManager.DoUpdate(mark);
        if (errors != 0)
            _context.Log.Warn($"NX reported {errors} update error(s) while refreshing the requirement expressions.");
        session.DeleteUndoMark(mark, null);
    }

    private static string SafeStringValue(Expression expression)
    {
        try
        {
            return expression.StringValue;
        }
        catch (NXException ex)
        {
            return $"(unreadable: NX {ex.ErrorCode})";
        }
    }

    private Expression? FindExpression(string name) =>
        _context.WorkPart.Expressions.Cast<Expression>().FirstOrDefault(e => e.Name == name);
}
