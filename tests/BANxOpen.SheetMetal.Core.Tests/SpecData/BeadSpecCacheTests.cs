using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.Tests.SpecData;

public class BeadSpecCacheTests
{
    private sealed class FakeSource : IBeadSpecSource
    {
        public int ReadCount { get; private set; }
        public double NextThickness { get; set; } = 0.02;
        public string? Workbook { get; set; }

        public IReadOnlyList<StandardInfo> ListStandards() => throw new NotSupportedException();

        public string? FindWorkbook(StandardInfo standard) => Workbook;

        public IReadOnlyList<BeadSpecRow> ReadWorkbook(StandardInfo standard, string workbookPath)
        {
            ReadCount++;
            return new[]
            {
                new BeadSpecRow(standard.Id, "SPEC-1", 0.245, 0.625, 0.625, 0.188, NextThickness,
                    new Dictionary<string, bool> { ["2024-O"] = true }),
            };
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
        cache.GetSpecs(Standard);
        cache.GetSpecs(Standard);

        Assert.Equal(1, source.ReadCount);
    });

    [Fact]
    public void GetSpecs_AfterWorkbookModified_Reimports() => WithCache((source, cache, workbookPath) =>
    {
        cache.GetSpecs(Standard);
        File.SetLastWriteTimeUtc(workbookPath, DateTime.UtcNow.AddMinutes(5));
        cache.GetSpecs(Standard);

        Assert.Equal(2, source.ReadCount);
    });

    [Fact]
    public void GetSpecs_AfterTheStandardFolderHoldsADifferentWorkbook_Reimports() => WithCache((source, cache, workbookPath) =>
    {
        cache.GetSpecs(Standard);

        var replacement = TempWorkbook();
        try
        {
            File.SetLastWriteTimeUtc(replacement, File.GetLastWriteTimeUtc(workbookPath));
            source.Workbook = replacement;
            cache.GetSpecs(Standard);
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

        Assert.Empty(cache.GetSpecs(Standard));
        Assert.Empty(cache.Refresh(Standard));
        Assert.Equal(0, source.ReadCount);
    });

    [Fact]
    public void Refresh_AlwaysReimportsRegardlessOfCache() => WithCache((source, cache, _) =>
    {
        cache.GetSpecs(Standard);
        cache.Refresh(Standard);

        Assert.Equal(2, source.ReadCount);
    });
}
