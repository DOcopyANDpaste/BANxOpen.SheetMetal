using System.Text.Json;
using BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>Fronts <see cref="IBeadSpecSource"/> with a one-JSON-file-per-Standard cache under
/// <paramref name="cacheDirectory"/>-ish (constructor), keyed by the workbook's own last-write-time.
/// <see cref="GetSpecs"/> only re-imports through Excel when the workbook is newer than what's cached;
/// <see cref="Refresh"/> always re-imports (the dialog's manual "Refresh spec data" button). This is what
/// keeps the dialog's SPEC lookups fast without giving up "Excel stays the editable source of truth."</summary>
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

    public IReadOnlyList<BeadSpecRow> GetSpecs(StandardInfo standard)
    {
        var workbookWriteTimeUtc = File.GetLastWriteTimeUtc(standard.WorkbookPath);
        var cachePath = CachePathFor(standard.Id);

        if (File.Exists(cachePath))
        {
            var cached = TryReadCacheFile(cachePath);
            if (cached is not null && cached.WorkbookWriteTimeUtc == workbookWriteTimeUtc)
                return cached.Rows;
        }

        return Refresh(standard);
    }

    /// <summary>Always re-imports from Excel, regardless of the cached write-time. Bound to the dialog's
    /// manual refresh button for the case where a workbook was edited and saved with the same timestamp a
    /// network share sometimes reports, or the user just wants to be sure.</summary>
    public IReadOnlyList<BeadSpecRow> Refresh(StandardInfo standard)
    {
        var rows = _source.ReadWorkbook(standard);
        var workbookWriteTimeUtc = File.GetLastWriteTimeUtc(standard.WorkbookPath);
        WriteCacheFile(CachePathFor(standard.Id), new CacheFile(workbookWriteTimeUtc, rows));
        return rows;
    }

    private string CachePathFor(string standardId) => Path.Combine(_cacheDirectory, $"{standardId}.json");

    private static CacheFile? TryReadCacheFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<CacheFile>(stream);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // A corrupt or half-written cache file just forces a re-import — not fatal.
            return null;
        }
    }

    private static void WriteCacheFile(string path, CacheFile file)
    {
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, file);
    }

    private sealed record CacheFile(DateTime WorkbookWriteTimeUtc, IReadOnlyList<BeadSpecRow> Rows);
}
