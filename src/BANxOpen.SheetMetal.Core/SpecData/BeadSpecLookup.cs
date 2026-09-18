using BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>Resolves SPECs for the constraint provider and for identifying unstamped beads.
///
/// Its own seam because the provider needs exactly this and nothing else. Depending on
/// <see cref="BeadSpecCache"/> directly would make the provider untestable without a standards file and a workbook
/// on disk.</summary>
public interface IBeadSpecLookup
{
    /// <summary>The row for this Standard and SPEC, or null when it cannot be found — the SPEC was retired
    /// from its workbook after parts were built to it, the Standard left the sheet metal material standards file, or the spec data could
    /// not be read at all. Callers must treat null as "unverifiable", not "unrestricted".
    ///
    /// Searches every bead SPEC workbook the Standard holds, since a stamp records the SPEC id but not which
    /// workbook it came from. SPEC ids are expected to be unique across those workbooks; if one is not, this
    /// returns null (with a warning) rather than picking whichever workbook was read first. A workbook that cannot be
    /// read is skipped with a warning, so it does not make the Standard's other SPECs unverifiable.</summary>
    BeadSpecRow? Find(string standardId, string specId);

    /// <summary>Every row of every bead SPEC workbook in every Standard in the sheet metal material standards file. An
    /// unstamped bead records no Standard and no SPEC, so identifying it means searching all of them. A workbook, or a
    /// whole Standard's folder, that cannot be read is skipped with a warning rather than failing the whole search.</summary>
    IReadOnlyList<BeadSpecRow> AllSpecs();
}

/// <summary>Looks SPECs up through the shared cache, so the Material Assignment dialog reads the same parsed
/// workbook data the bead dialog does rather than re-importing Excel on its own.</summary>
public sealed class BeadSpecLookup : IBeadSpecLookup
{
    private readonly BeadSpecCache _cache;
    private readonly Action<string>? _onWarning;

    public BeadSpecLookup(BeadSpecCache cache, Action<string>? onWarning = null)
    {
        _cache = cache;
        _onWarning = onWarning;
    }

    public BeadSpecRow? Find(string standardId, string specId)
    {
        try
        {
            var standard = _cache.ListStandards()
                .FirstOrDefault(s => string.Equals(s.Id, standardId, StringComparison.OrdinalIgnoreCase));
            if (standard is null)
                return null;

            var matches = _cache.GetAllSpecs(standard)
                .Where(row => string.Equals(row.SpecId, specId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Two workbooks claiming the same SPEC id is bad data, and either answer would be a guess. Reporting
            // "not found" is the same answer a retired SPEC gets, and callers already treat that as unverifiable.
            // GetAllSpecs has already warned about it. A SPEC repeated within one workbook resolves to its first row.
            return BeadSpecCache.WorkbooksOf(matches).Count > 1 ? null : matches.FirstOrDefault();
        }
        catch (Exception ex) when (BeadSpecCache.IsSpecDataFailure(ex))
        {
            // This runs inside an interactive dialog, so unreadable spec data must not take the dialog down.
            // Reporting "not found" is safe only because the provider turns that into a blocking constraint.
            _onWarning?.Invoke(
                $"Could not read spec data while resolving Standard '{standardId}' SPEC '{specId}': {ex.Message}");
            return null;
        }
    }

    public IReadOnlyList<BeadSpecRow> AllSpecs()
    {
        IReadOnlyList<StandardInfo> standards;
        try
        {
            standards = _cache.ListStandards();
        }
        catch (Exception ex) when (BeadSpecCache.IsSpecDataFailure(ex))
        {
            _onWarning?.Invoke($"Could not list the Standards while identifying unstamped beads: {ex.Message}");
            return Array.Empty<BeadSpecRow>();
        }

        var rows = new List<BeadSpecRow>();
        foreach (var standard in standards)
        {
            try
            {
                rows.AddRange(_cache.GetAllSpecs(standard));
            }
            catch (Exception ex) when (BeadSpecCache.IsSpecDataFailure(ex))
            {
                _onWarning?.Invoke(
                    $"Skipped Standard '{standard.Id}' while identifying unstamped beads: {ex.Message}");
            }
        }

        return rows;
    }
}
