using BANxOpen.SheetMetal.Beads;
using Newtonsoft.Json;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>Fronts <see cref="IBeadSpecSource"/> with a one-JSON-file-per-Standard cache under
/// <paramref name="cacheDirectory"/>-ish (constructor), keyed by the workbook's path and its own last-write-time.
/// <see cref="GetSpecs"/> only re-imports through Excel when the workbook is newer than what's cached, or the Standard's
/// folder now holds a different workbook; <see cref="Refresh"/> always re-imports (the dialog's manual "Refresh spec
/// data" button). This is what keeps the dialog's SPEC lookups fast without giving up "Excel stays the editable source
/// of truth."</summary>
public sealed class BeadSpecCache
{
    private readonly IBeadSpecSource _source;
    private readonly string _cacheDirectory;

    public BeadSpecCache(IBeadSpecSource source, string cacheDirectory)
    {
        _source = source;
        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(_cacheDirectory);
    }

    public IReadOnlyList<StandardInfo> ListStandards() => _source.ListStandards();

    /// <summary>The Standard's bead SPEC workbook, or null when it has none — for a dialog explaining why a Standard
    /// offers no SPECs.</summary>
    public string? FindWorkbook(StandardInfo standard) => _source.FindWorkbook(standard);

    /// <summary>Empty when the Standard has no bead SPEC workbook: no bead SPEC is allowed under it.</summary>
    public IReadOnlyList<BeadSpecRow> GetSpecs(StandardInfo standard)
    {
        if (_source.FindWorkbook(standard) is not { } workbookPath)
            return Array.Empty<BeadSpecRow>();

        var workbookWriteTimeUtc = File.GetLastWriteTimeUtc(workbookPath);
        var cachePath = CachePathFor(standard.Id);

        if (File.Exists(cachePath))
        {
            var cached = TryReadCacheFile(cachePath);
            if (cached is not null
                && cached.WorkbookWriteTimeUtc == workbookWriteTimeUtc
                && string.Equals(cached.WorkbookPath, workbookPath, StringComparison.OrdinalIgnoreCase))
                return cached.Rows;
        }

        return Import(standard, workbookPath);
    }

    /// <summary>Always re-imports from Excel, regardless of the cached write-time. Bound to the dialog's
    /// manual refresh button for the case where a workbook was edited and saved with the same timestamp a
    /// network share sometimes reports, or the user just wants to be sure.</summary>
    public IReadOnlyList<BeadSpecRow> Refresh(StandardInfo standard) =>
        _source.FindWorkbook(standard) is { } workbookPath ? Import(standard, workbookPath) : Array.Empty<BeadSpecRow>();

    private IReadOnlyList<BeadSpecRow> Import(StandardInfo standard, string workbookPath)
    {
        var rows = _source.ReadWorkbook(standard, workbookPath);
        var workbookWriteTimeUtc = File.GetLastWriteTimeUtc(workbookPath);
        WriteCacheFile(CachePathFor(standard.Id), new CacheFile(workbookPath, workbookWriteTimeUtc, rows));
        return rows;
    }

    private string CachePathFor(string standardId) => Path.Combine(_cacheDirectory, $"{standardId}.json");

    private static CacheFile? TryReadCacheFile(string path)
    {
        try
        {
            return JsonConvert.DeserializeObject<CacheFile>(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // A corrupt or half-written cache file just forces a re-import — not fatal.
            return null;
        }
    }

    private static void WriteCacheFile(string path, CacheFile file) =>
        File.WriteAllText(path, JsonConvert.SerializeObject(file));

    /// <param name="WorkbookPath">Null in a cache file written before Standards came from folders, which forces a
    /// re-import.</param>
    private sealed record CacheFile(string? WorkbookPath, DateTime WorkbookWriteTimeUtc, IReadOnlyList<BeadSpecRow> Rows);
}
