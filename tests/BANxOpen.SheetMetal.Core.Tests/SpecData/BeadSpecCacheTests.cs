using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.Tests.SpecData;

public class BeadSpecCacheTests
{
    private const string BeadSpec = "B1005010";

    private sealed class FakeSource : IBeadSpecSource
    {
        public int ReadCount { get; private set; }
        public double NextThickness { get; set; } = 0.02;

        /// <summary>The workbook <see cref="BeadSpec"/> resolves to; null means the Standard has none for it.</summary>
        public string? Workbook { get; set; }

        /// <summary>Extra workbooks the Standard holds, which only <c>GetAllSpecs</c> reaches.</summary>
        public List<string> OtherWorkbooks { get; } = new();

        /// <summary>Workbooks that fail to read, as a malformed or locked one does.</summary>
        public HashSet<string> Unreadable { get; } = new();

        /// <summary>How many times each workbook lists SPEC-1.</summary>
        public int RowsPerWorkbook { get; set; } = 1;

        public IReadOnlyList<StandardInfo> ListStandards() => new[] { Standard };

        public string? FindWorkbook(StandardInfo standard, string beadSpecName) =>
            beadSpecName == BeadSpec ? Workbook : null;

        public IReadOnlyList<string> ListWorkbooks(StandardInfo standard) =>
            (Workbook is null ? OtherWorkbooks : OtherWorkbooks.Prepend(Workbook)).ToList();

        public IReadOnlyList<BeadSpecRow> ReadWorkbook(StandardInfo standard, string workbookPath)
        {
            ReadCount++;
            if (Unreadable.Contains(workbookPath))
                throw new InvalidDataException($"'{workbookPath}' is not a valid bead SPEC workbook.");

            // Repeats of one SPEC id are the workbooks' real shape — the SPEC is driven once per sheet thickness —
            // so each repeat gets its own thickness, descending, to prove nothing here depends on file order.
            return Enumerable.Range(0, RowsPerWorkbook)
                .Select(i => new BeadSpecRow(standard.Id, Path.GetFileNameWithoutExtension(workbookPath), "SPEC-1",
                    0.245, 0.625, 0.625, 0.188, NextThickness + (RowsPerWorkbook - 1 - i) * 0.01,
                    new Dictionary<string, bool> { ["2024-O"] = true }))
                .ToList();
        }
    }

    private static readonly StandardInfo Standard = new("B1005010", "B1005010", "unused");

    private static string TempWorkbook()
    {
        var workbookPath = Path.Combine(Path.GetTempPath(), $"wb-{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(workbookPath, "placeholder");
        return workbookPath;
    }

    private static void WithCache(Action<FakeSource, BeadSpecCache, string> test)
    {
        var workbookPath = TempWorkbook();
        var cacheDir = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid():N}");
        try
        {
            var source = new FakeSource { Workbook = workbookPath };
            test(source, new BeadSpecCache(source, cacheDir), workbookPath);
        }
        finally
        {
            File.Delete(workbookPath);
            Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public void GetSpecs_SecondCallWithUnchangedWorkbook_DoesNotReimport() => WithCache((source, cache, _) =>
    {
        cache.GetSpecs(Standard, BeadSpec);
        cache.GetSpecs(Standard, BeadSpec);

        Assert.Equal(1, source.ReadCount);
    });

    [Fact]
    public void GetSpecs_AfterWorkbookModified_Reimports() => WithCache((source, cache, workbookPath) =>
    {
        cache.GetSpecs(Standard, BeadSpec);
        File.SetLastWriteTimeUtc(workbookPath, DateTime.UtcNow.AddMinutes(5));
        cache.GetSpecs(Standard, BeadSpec);

        Assert.Equal(2, source.ReadCount);
    });

    [Fact]
    public void GetSpecs_AfterTheStandardFolderHoldsADifferentWorkbook_Reimports() => WithCache((source, cache, workbookPath) =>
    {
        cache.GetSpecs(Standard, BeadSpec);

        var replacement = TempWorkbook();
        try
        {
            File.SetLastWriteTimeUtc(replacement, File.GetLastWriteTimeUtc(workbookPath));
            source.Workbook = replacement;
            cache.GetSpecs(Standard, BeadSpec);
        }
        finally
        {
            File.Delete(replacement);
        }

        Assert.Equal(2, source.ReadCount);
    });

    [Fact]
    public void GetSpecs_ForAStandardWithNoWorkbook_IsEmptyAndReadsNothing() => WithCache((source, cache, _) =>
    {
        source.Workbook = null;

        Assert.Empty(cache.GetSpecs(Standard, BeadSpec));
        Assert.Equal(0, source.ReadCount);
    });

    [Fact]
    public void GetSpecs_ForASpecNoWorkbookIsNamedFor_IsEmpty() => WithCache((source, cache, _) =>
    {
        Assert.Empty(cache.GetSpecs(Standard, "S5010"));
        Assert.Equal(0, source.ReadCount);
    });

    [Fact]
    public void GetAllSpecs_ReadsEveryWorkbookTheStandardHolds() => WithCache((source, cache, _) =>
    {
        var other = TempWorkbook();
        try
        {
            source.OtherWorkbooks.Add(other);

            var rows = cache.GetAllSpecs(Standard);

            Assert.Equal(2, rows.Count);
            Assert.Equal(2, source.ReadCount);
        }
        finally
        {
            File.Delete(other);
        }
    });

    [Fact]
    public void GetAllSpecs_CachesEachWorkbookSeparately() => WithCache((source, cache, _) =>
    {
        var other = TempWorkbook();
        try
        {
            source.OtherWorkbooks.Add(other);

            cache.GetAllSpecs(Standard);
            cache.GetAllSpecs(Standard);

            // Two workbooks, read once each — one cache file per workbook, not one per Standard that the
            // second workbook would keep overwriting.
            Assert.Equal(2, source.ReadCount);
        }
        finally
        {
            File.Delete(other);
        }
    });

    [Fact]
    public void GetAllSpecs_WarnsWhenTwoWorkbooksShareASpecId()
    {
        var workbookPath = TempWorkbook();
        var other = TempWorkbook();
        var cacheDir = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid():N}");
        var warnings = new List<string>();
        try
        {
            var source = new FakeSource { Workbook = workbookPath };
            source.OtherWorkbooks.Add(other);

            // FakeSource gives every workbook a row called SPEC-1, so the two collide.
            new BeadSpecCache(source, cacheDir, warnings.Add).GetAllSpecs(Standard);

            var warning = Assert.Single(warnings);
            Assert.Contains("SPEC-1", warning);
            Assert.Contains(Path.GetFileNameWithoutExtension(workbookPath), warning);
            Assert.Contains(Path.GetFileNameWithoutExtension(other), warning);
        }
        finally
        {
            File.Delete(workbookPath);
            File.Delete(other);
            Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public void GetAllSpecs_DoesNotWarnWhenSpecIdsAreUnique() => WithCache((_, cache, _) =>
    {
        // One workbook, so nothing to collide with. WithCache passes no warning sink; this asserts the
        // happy path does not need one.
        Assert.Single(cache.GetAllSpecs(Standard));
    });

    [Fact]
    public void GetAllSpecs_SkipsAnUnreadableWorkbookAndKeepsTheRest() => WithCache((source, cache, workbookPath) =>
    {
        var bad = TempWorkbook();
        try
        {
            source.OtherWorkbooks.Add(bad);
            source.Unreadable.Add(bad);

            var row = Assert.Single(cache.GetAllSpecs(Standard));
            Assert.Equal(Path.GetFileNameWithoutExtension(workbookPath), row.WorkbookName);
        }
        finally
        {
            File.Delete(bad);
        }
    });

    /// <summary>A SPEC id repeated within one workbook is a family — the SPEC driven once per sheet thickness — not
    /// an ambiguity, so every row comes back. Resolving it to the first row was the defect that made a bead judge a
    /// 0.05 sheet by its 0.02 row and therefore allow nothing.</summary>
    [Fact]
    public void Lookup_ReturnsEveryRowOfASpecRepeatedWithinOneWorkbook() => WithCache((source, cache, _) =>
    {
        source.RowsPerWorkbook = 3;

        Assert.Equal(3, new BeadSpecLookup(cache).FindAll(Standard.Id, "SPEC-1").Count);
    });

    [Fact]
    public void Lookup_OrdersAFamilyByThickness() => WithCache((source, cache, _) =>
    {
        source.RowsPerWorkbook = 3;

        // The fake lists them thickest first, so file order cannot be what this passes on.
        var family = new BeadSpecLookup(cache).FindAll(Standard.Id, "SPEC-1");

        Assert.Equal(family.Select(r => r.Thickness).OrderBy(t => t), family.Select(r => r.Thickness));
    });

    [Fact]
    public void Lookup_DoesNotResolveASpecClaimedByTwoWorkbooks() => WithCache((source, cache, _) =>
    {
        var other = TempWorkbook();
        try
        {
            source.OtherWorkbooks.Add(other);

            Assert.Empty(new BeadSpecLookup(cache).FindAll(Standard.Id, "SPEC-1"));
        }
        finally
        {
            File.Delete(other);
        }
    });

    [Fact]
    public void Lookup_StillFindsASpecWhenAnotherWorkbookIsUnreadable() => WithCache((source, cache, _) =>
    {
        var bad = TempWorkbook();
        try
        {
            source.OtherWorkbooks.Add(bad);
            source.Unreadable.Add(bad);

            Assert.NotEmpty(new BeadSpecLookup(cache).FindAll(Standard.Id, "SPEC-1"));
        }
        finally
        {
            File.Delete(bad);
        }
    });
}
