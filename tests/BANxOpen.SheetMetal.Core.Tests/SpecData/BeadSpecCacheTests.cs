using BANxOpen.SheetMetal.Beads;
using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.Tests.SpecData;

public class BeadSpecCacheTests
{
    private sealed class FakeSource : IBeadSpecSource
    {
        public int ReadCount { get; private set; }
        public double NextThickness { get; set; } = 0.02;

        public IReadOnlyList<StandardInfo> ListStandards() => throw new NotSupportedException();

        public IReadOnlyList<BeadSpecRow> ReadWorkbook(StandardInfo standard)
        {
            ReadCount++;
            return new[]
            {
                new BeadSpecRow(standard.Id, "SPEC-1", 0.245, 0.625, 0.625, 0.188, NextThickness,
                    new Dictionary<string, bool> { ["2024-O"] = true }),
            };
        }
    }

    private static (string WorkbookPath, string CacheDir) MakeTempPaths()
    {
        var workbookPath = Path.Combine(Path.GetTempPath(), $"wb-{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(workbookPath, "placeholder");
        var cacheDir = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid():N}");
        return (workbookPath, cacheDir);
    }

    [Fact]
    public void GetSpecs_SecondCallWithUnchangedWorkbook_DoesNotReimport()
    {
        var (workbookPath, cacheDir) = MakeTempPaths();
        try
        {
            var source = new FakeSource();
            var cache = new BeadSpecCache(source, cacheDir);
            var standard = new StandardInfo("B1005010", "B1005010", workbookPath);

            cache.GetSpecs(standard);
            cache.GetSpecs(standard);

            Assert.Equal(1, source.ReadCount);
        }
        finally
        {
            File.Delete(workbookPath);
            Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public void GetSpecs_AfterWorkbookModified_Reimports()
    {
        var (workbookPath, cacheDir) = MakeTempPaths();
        try
        {
            var source = new FakeSource();
            var cache = new BeadSpecCache(source, cacheDir);
            var standard = new StandardInfo("B1005010", "B1005010", workbookPath);

            cache.GetSpecs(standard);
            File.SetLastWriteTimeUtc(workbookPath, DateTime.UtcNow.AddMinutes(5));
            cache.GetSpecs(standard);

            Assert.Equal(2, source.ReadCount);
        }
        finally
        {
            File.Delete(workbookPath);
            Directory.Delete(cacheDir, recursive: true);
        }
    }

    [Fact]
    public void Refresh_AlwaysReimportsRegardlessOfCache()
    {
        var (workbookPath, cacheDir) = MakeTempPaths();
        try
        {
            var source = new FakeSource();
            var cache = new BeadSpecCache(source, cacheDir);
            var standard = new StandardInfo("B1005010", "B1005010", workbookPath);

            cache.GetSpecs(standard);
            cache.Refresh(standard);

            Assert.Equal(2, source.ReadCount);
        }
        finally
        {
            File.Delete(workbookPath);
            Directory.Delete(cacheDir, recursive: true);
        }
    }
}
