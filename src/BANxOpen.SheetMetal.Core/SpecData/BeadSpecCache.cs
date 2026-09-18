using BANxOpen.SheetMetal.Beads;
using Newtonsoft.Json;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>Fronts <see cref="IBeadSpecSource"/> with a one-JSON-file-per-workbook cache under
/// <paramref name="cacheDirectory"/>-ish (constructor), keyed by the workbook's path and its own last-write-time.
/// <see cref="GetSpecs"/> only re-imports through Excel when the workbook is newer than what's cached, or the Standard's
/// folder now holds a different workbook; <see cref="Refresh"/> always re-imports (the dialog's manual "Refresh spec
/// data" button). This is what keeps the dialog's SPEC lookups fast without giving up "Excel stays the editable source
/// of truth."
///
/// A Standard holds one workbook per bead SPEC, so everything here is addressed by (Standard, bead SPEC name) —
/// except <see cref="GetAllSpecs"/>, which reads every workbook because identifying an unstamped bead has no SPEC
/// name to go on.</summary>
public sealed class BeadSpecCache
{
    private readonly IBeadSpecSource _source;
    private readonly string _cacheDirectory;
    private readonly Action<string>? _onWarning;

    public BeadSpecCache(IBeadSpecSource source, string cacheDirectory, Action<string>? onWarning = null)
    {
        _source = source;
        _cacheDirectory = cacheDirectory;
        _onWarning = onWarning;
        Directory.CreateDirectory(_cacheDirectory);
    }

    public IReadOnlyList<StandardInfo> ListStandards() => _source.ListStandards();

    /// <summary>The workbook holding this bead SPEC's rows, or null when the Standard has none — for a dialog
    /// explaining why a Standard offers no SPECs.</summary>
    public string? FindWorkbook(StandardInfo standard, string beadSpecName) => _source.FindWorkbook(standard, beadSpecName);

    /// <summary>Empty when the Standard has no workbook for this bead SPEC: no SPEC under it is allowed.</summary>
    public IReadOnlyList<BeadSpecRow> GetSpecs(StandardInfo standard, string beadSpecName) =>
        _source.FindWorkbook(standard, beadSpecName) is { } workbookPath
            ? LoadWorkbook(standard, workbookPath)
            : Array.Empty<BeadSpecRow>();

    /// <summary>Every row of every bead SPEC workbook the Standard holds. Used to identify a bead that carries no
    /// stamp, which records no SPEC name to look one up by.
    ///
    /// SPEC ids are supposed to be unique across a Standard's workbooks. That is an assertion about the data, not
    /// something the folder layout enforces, so a collision is reported here — where the two workbooks are both in
    /// hand and can be named — rather than being left to surface downstream as a bead built to the wrong SPEC.
    ///
    /// A workbook that cannot be read is skipped with a warning, so one malformed or locked file does not hide the
    /// Standard's other workbooks.</summary>
    public IReadOnlyList<BeadSpecRow> GetAllSpecs(StandardInfo standard)
    {
        var rows = new List<BeadSpecRow>();
        foreach (var workbookPath in _source.ListWorkbooks(standard))
        {
            try
            {
                rows.AddRange(LoadWorkbook(standard, workbookPath));
            }
            catch (Exception ex) when (IsSpecDataFailure(ex))
            {
                _onWarning?.Invoke(
                    $"Skipped bead SPEC workbook '{workbookPath}' of Standard '{standard.Id}': {ex.Message}");
            }
        }

        WarnOnDuplicateSpecIds(standard, rows);
        return rows;
    }

    /// <summary>The distinct workbooks <paramref name="rows"/> come from. More than one for rows sharing a SPEC id
    /// is the cross-workbook collision a stamp cannot be traced through; repeats within one workbook are not.</summary>
    internal static IReadOnlyList<string> WorkbooksOf(IEnumerable<BeadSpecRow> rows) =>
        rows.Select(r => r.WorkbookName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>The failures that mean "this spec data could not be read", as opposed to a bug.</summary>
    internal static bool IsSpecDataFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or InvalidDataException;

    /// <summary>Always re-imports from Excel, regardless of the cached write-time. Bound to the dialog's
    /// manual refresh button for the case where a workbook was edited and saved with the same timestamp a
    /// network share sometimes reports, or the user just wants to be sure.</summary>
    public IReadOnlyList<BeadSpecRow> Refresh(StandardInfo standard, string beadSpecName) =>
        _source.FindWorkbook(standard, beadSpecName) is { } workbookPath
            ? Import(standard, workbookPath)
            : Array.Empty<BeadSpecRow>();

    private IReadOnlyList<BeadSpecRow> LoadWorkbook(StandardInfo standard, string workbookPath)
    {
        var workbookWriteTimeUtc = File.GetLastWriteTimeUtc(workbookPath);
        var cachePath = CachePathFor(standard.Id, workbookPath);

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

    private void WarnOnDuplicateSpecIds(StandardInfo standard, IReadOnlyList<BeadSpecRow> rows)
    {
        if (_onWarning is null)
            return;

        foreach (var collision in rows.GroupBy(r => r.SpecId, StringComparer.OrdinalIgnoreCase))
        {
            var workbooks = WorkbooksOf(collision);
            if (workbooks.Count < 2)
                continue;

            _onWarning($"Standard '{standard.Id}' has SPEC '{collision.Key}' in more than one bead SPEC workbook " +
                       $"({string.Join(", ", workbooks)}). SPEC ids are expected to be unique within a Standard; " +
                       "a bead stamped with this SPEC cannot be traced back to one workbook.");
        }
    }

    private IReadOnlyList<BeadSpecRow> Import(StandardInfo standard, string workbookPath)
    {
        var rows = _source.ReadWorkbook(standard, workbookPath);
        var workbookWriteTimeUtc = File.GetLastWriteTimeUtc(workbookPath);
        WriteCacheFile(CachePathFor(standard.Id, workbookPath), new CacheFile(workbookPath, workbookWriteTimeUtc, rows));
        return rows;
    }

    /// <summary>One cache file per workbook. The workbook's own name is part of it, so a Standard's several bead SPEC
    /// workbooks do not overwrite each other's cache — and so cache files written before a Standard had more than one
    /// workbook are simply orphaned rather than read back with a missing
    /// <see cref="BeadSpecRow.WorkbookName"/>.</summary>
    private string CachePathFor(string standardId, string workbookPath) =>
        Path.Combine(_cacheDirectory, $"{Sanitize(standardId)}__{Sanitize(StandardFolderLayout.WorkbookNameOf(workbookPath))}.json");

    private static string Sanitize(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

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
