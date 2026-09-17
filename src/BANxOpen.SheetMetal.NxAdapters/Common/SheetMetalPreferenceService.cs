using BANxOpen.Foundation.Contracts.Common;
using BANxOpen.Foundation.NxAdapters;
using BANxOpen.Foundation.NxAdapters.Materials;
using BANxOpen.SheetMetal.Common;
using BANxOpen.SheetMetal.Materials;
using NXOpen;
using NXOpen.Preferences;

namespace BANxOpen.SheetMetal.NxAdapters.Common;

/// <summary>Reads and updates the material half of the work part's NX Sheet Metal Preferences.
///
/// NX keeps one set of preferences per part, so everything here is part-level; a body only decides whether the
/// preferences apply to it at all. Uses the NX2312+ <c>SheetMetalPreferencesManager</c> /
/// <c>SheetMetalPreferencesBuilder</c> API — the older <c>PartSheetmetal</c> material calls are deprecated.
///
/// No undo handling here: callers own the undo mark. The material engine calls <see cref="SyncMaterial"/> from
/// inside ApplyPlan's per-body scope, and the bead dialog wraps its own.</summary>
public sealed class SheetMetalPreferenceService : ISheetMetalPreferenceReader
{
    public const string SyncNotAppliedCode = "PREFERENCE_SYNC_NOT_APPLIED";

    private readonly NxSessionContext _context;
    private readonly SheetMetalMaterialTable _table;

    public SheetMetalPreferenceService(NxSessionContext context, SheetMetalMaterialTable table)
    {
        _context = context;
        _table = table;
    }

    public SheetMetalPreferenceRead ReadFor(BodyId bodyId)
    {
        Body? body;
        try
        {
            body = _context.WorkPart.Bodies.Cast<Body>().FirstOrDefault(b => BodyResolver.GetBodyId(b) == bodyId);
        }
        catch (NXException ex)
        {
            _context.Log.Error($"Could not scan the work part's bodies for '{bodyId}': NX {ex.ErrorCode}: {ex.Message}");
            return SheetMetalPreferenceRead.Unreadable($"NX {ex.ErrorCode}: {ex.Message}");
        }

        return body is null
            ? SheetMetalPreferenceRead.Unreadable($"body '{bodyId}' is no longer in the work part")
            : ReadFor(body);
    }

    public SheetMetalPreferenceRead ReadFor(Body body)
    {
        var sheetmetalManager = _context.WorkPart.Features.SheetmetalManager;

        try
        {
            if (!sheetmetalManager.IsSheetmetalBody(body))
                return SheetMetalPreferenceRead.NotSheetMetal;
        }
        catch (NXException)
        {
            // Not answerable for this body. PartMaterialService.ClassifyBody treats such a body as not sheet metal,
            // so the preferences are not applied to it here either.
            return SheetMetalPreferenceRead.NotSheetMetal;
        }

        try
        {
            var preferences = _context.WorkPart.Preferences.SheetMetalPreferences;

            // VERIFY: that the thickness expression's value is in the same units GetBodyThickness reports (part
            // units), which the thickness comparison relies on.
            var thickness = preferences.GetMaterialThickness();
            if (thickness is null)
                return SheetMetalPreferenceRead.Unreadable("Sheet Metal Preferences have no material thickness");

            var materialName = string.IsNullOrWhiteSpace(preferences.GetMaterialName()) ? null : preferences.GetMaterialName();
            var preference = new SheetMetalPartPreference(
                materialName,
                preferences.GetParameterEntryType() == SheetMetalPreferencesBuilder.ParameterEntryTypes.MaterialTable,
                thickness.Value,
                CountSheetMetalBodies(),
                _table.Find(materialName));

            return SheetMetalPreferenceRead.Of(preference);
        }
        catch (NXException ex)
        {
            _context.Log.Error($"Reading Sheet Metal Preferences failed: NX {ex.ErrorCode}: {ex.Message}");
            return SheetMetalPreferenceRead.Unreadable($"NX {ex.ErrorCode}: {ex.Message}");
        }
    }

    /// <summary>Sets the preferences' material to <paramref name="row"/> of the sheet metal material standards file,
    /// switching Parameter Entry and the bend definition method to Material Table: NX ignores the material in any other
    /// mode. The order is the one NX records in a journal of the Sheet Metal Preferences dialog.</summary>
    /// <remarks>NX fills thickness, bend radius, bend reliefs and neutral factor from the row itself when the material is
    /// set, so they are not written here. If testing shows it does not, set <c>builder.MaterialThickness</c>,
    /// <c>BendRadius</c>, <c>BendReliefWidth</c> and <c>BendReliefDepth</c> <c>.RightHandSide</c> from the row's numeric
    /// values after <c>SetMaterial</c> (leaving <c>@pointer</c> values to NX), as the recorded journal does.
    ///
    /// NX's own material list is not consulted: <c>GetMaterialNames</c> fails with NX internal error 11. The row comes
    /// from the tool's own reading of the same standards file.</remarks>
    /// <returns>A failure when NX refuses the change, or the preferences read back afterwards do not show it. The
    /// read-back is what stops a caller that re-checks from asking again for a change NX quietly ignored.</returns>
    public OperationResult SyncMaterial(SheetMetalMaterialRow row)
    {
        var material = row.Name;
        var preferences = _context.WorkPart.Preferences.SheetMetalPreferences;
        SheetMetalPreferencesBuilder? builder = null;

        try
        {
            builder = preferences.CreateSheetMetalPreferencesBuilder();

            builder.ParameterEntryType = SheetMetalPreferencesBuilder.ParameterEntryTypes.MaterialTable;
            builder.SetBendDefinitionMethod(SheetMetalPreferencesBuilder.BendDefinitionMethodOptions.MaterialTable);
            builder.SetMaterial(material);
            builder.Commit();
        }
        catch (NXException ex)
        {
            _context.Log.Error($"Setting Sheet Metal Preferences material to '{material}' failed: NX {ex.ErrorCode}: {ex.Message}");
            return OperationResult.Fail(ex.ErrorCode.ToString(), $"Sheet Metal Preferences could not be set to '{material}': {ex.Message}");
        }
        finally
        {
            builder?.Destroy();
        }

        try
        {
            var applied = preferences.GetParameterEntryType() == SheetMetalPreferencesBuilder.ParameterEntryTypes.MaterialTable
                          && string.Equals(preferences.GetMaterialName(), material, StringComparison.OrdinalIgnoreCase);
            if (!applied)
            {
                _context.Log.Error($"Sheet Metal Preferences committed, but do not read back as material '{material}' with Material Table entry.");
                return OperationResult.Fail(
                    SyncNotAppliedCode,
                    $"Sheet Metal Preferences were updated, but NX does not show material '{material}' with Material " +
                    "Table entry afterwards.");
            }
        }
        catch (NXException ex)
        {
            _context.Log.Error($"Reading back Sheet Metal Preferences failed: NX {ex.ErrorCode}: {ex.Message}");
            return OperationResult.Fail(ex.ErrorCode.ToString(), $"Sheet Metal Preferences could not be read back: {ex.Message}");
        }

        _context.Log.Info($"Sheet Metal Preferences material set to '{material}' (Material Table entry).");
        return OperationResult.Success();
    }

    private int CountSheetMetalBodies()
    {
        var sheetmetalManager = _context.WorkPart.Features.SheetmetalManager;
        var count = 0;

        foreach (Body body in _context.WorkPart.Bodies)
        {
            try
            {
                if (sheetmetalManager.IsSheetmetalBody(body))
                    count++;
            }
            catch (NXException)
            {
                // Not answerable for this body — not counted, matching ReadFor(Body).
            }
        }

        return count;
    }
}
