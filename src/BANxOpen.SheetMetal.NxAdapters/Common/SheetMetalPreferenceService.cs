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

    public SheetMetalPreferenceService(NxSessionContext context) => _context = context;

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

            var preference = new SheetMetalPartPreference(
                string.IsNullOrWhiteSpace(preferences.GetMaterialName()) ? null : preferences.GetMaterialName(),
                preferences.GetParameterEntryType() == SheetMetalPreferencesBuilder.ParameterEntryTypes.MaterialTable,
                thickness.Value,
                preferences.GetMaterialNames() ?? Array.Empty<string>(),
                CountSheetMetalBodies());

            return SheetMetalPreferenceRead.Of(preference);
        }
        catch (NXException ex)
        {
            _context.Log.Error($"Reading Sheet Metal Preferences failed: NX {ex.ErrorCode}: {ex.Message}");
            return SheetMetalPreferenceRead.Unreadable($"NX {ex.ErrorCode}: {ex.Message}");
        }
    }

    /// <summary>Sets the preferences' material to <paramref name="grade"/>, switching Parameter Entry to Material
    /// Table first when it is not already: NX ignores the material in any other entry mode. Switching can also
    /// change table-driven values such as thickness — accepted, and reported by the bead dialog's thickness
    /// check.</summary>
    /// <returns>A failure when the grade is not in the standards table, NX refuses the change, or the preferences
    /// read back afterwards do not show it. The read-back is what stops a caller that re-checks from asking again
    /// for a change NX quietly ignored.</returns>
    public OperationResult SyncMaterial(string grade)
    {
        var preferences = _context.WorkPart.Preferences.SheetMetalPreferences;
        SheetMetalPreferencesBuilder? builder = null;
        string? tableName;

        try
        {
            builder = preferences.CreateSheetMetalPreferencesBuilder();

            // The match ignores case like the rest of the grade comparisons, but SetMaterial gets the table's own
            // spelling.
            tableName = builder.GetMaterialNames()?.FirstOrDefault(name => string.Equals(name, grade, StringComparison.OrdinalIgnoreCase));
            if (tableName is null)
            {
                return OperationResult.Fail(
                    SheetMetalPreferenceConstraintProvider.NotInStandardsTableCode,
                    $"Material grade '{grade}' is not in the NX sheet metal material standards table, so Sheet Metal " +
                    "Preferences cannot be set to it.");
            }

            // VERIFY against a recorded journal (Preferences > Sheet Metal, Material Table entry, pick a material)
            // that the entry type is set before the material.
            if (builder.ParameterEntryType != SheetMetalPreferencesBuilder.ParameterEntryTypes.MaterialTable)
                builder.ParameterEntryType = SheetMetalPreferencesBuilder.ParameterEntryTypes.MaterialTable;

            builder.SetMaterial(tableName);
            builder.Commit();
        }
        catch (NXException ex)
        {
            _context.Log.Error($"Setting Sheet Metal Preferences material to '{grade}' failed: NX {ex.ErrorCode}: {ex.Message}");
            return OperationResult.Fail(ex.ErrorCode.ToString(), $"Sheet Metal Preferences could not be set to '{grade}': {ex.Message}");
        }
        finally
        {
            builder?.Destroy();
        }

        try
        {
            var applied = preferences.GetParameterEntryType() == SheetMetalPreferencesBuilder.ParameterEntryTypes.MaterialTable
                          && string.Equals(preferences.GetMaterialName(), tableName, StringComparison.OrdinalIgnoreCase);
            if (!applied)
            {
                _context.Log.Error($"Sheet Metal Preferences committed, but do not read back as material '{tableName}' with Material Table entry.");
                return OperationResult.Fail(
                    SyncNotAppliedCode,
                    $"Sheet Metal Preferences were updated, but NX does not show material '{tableName}' with Material " +
                    "Table entry afterwards.");
            }
        }
        catch (NXException ex)
        {
            _context.Log.Error($"Reading back Sheet Metal Preferences failed: NX {ex.ErrorCode}: {ex.Message}");
            return OperationResult.Fail(ex.ErrorCode.ToString(), $"Sheet Metal Preferences could not be read back: {ex.Message}");
        }

        _context.Log.Info($"Sheet Metal Preferences material set to '{tableName}' (Material Table entry).");
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
