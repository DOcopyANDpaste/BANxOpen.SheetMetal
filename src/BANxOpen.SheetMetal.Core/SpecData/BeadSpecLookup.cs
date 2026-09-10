using BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>Resolves a stamped Standard/SPEC pair back to its workbook row.
///
/// Its own seam because the constraint provider needs exactly this and nothing else: given a SPEC id
/// read off a feature, what does that SPEC allow? Depending on <see cref="BeadSpecCache"/> directly would
/// make the provider untestable without a registry and a workbook on disk.</summary>
public interface IBeadSpecLookup
{
    /// <summary>The row for this Standard and SPEC, or null when it cannot be found — the SPEC was
    /// retired from its workbook after parts were built to it, the Standard left the registry, or the
    /// spec data could not be read at all. Callers must treat null as "unverifiable", not "unrestricted".</summary>
    BeadSpecRow? Find(string standardId, string specId);
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

            return _cache.GetSpecs(standard)
                .FirstOrDefault(row => string.Equals(row.SpecId, specId, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            // This runs inside an interactive dialog, so unreadable spec data must not take the dialog down.
            // Reporting "not found" is safe only because the provider turns that into a blocking constraint.
            _onWarning?.Invoke(
                $"Could not read spec data while resolving Standard '{standardId}' SPEC '{specId}': {ex.Message}");
            return null;
        }
    }
}
