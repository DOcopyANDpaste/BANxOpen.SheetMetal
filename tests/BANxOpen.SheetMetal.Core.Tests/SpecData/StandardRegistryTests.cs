using BANxOpen.SheetMetal.SpecData;

namespace BANxOpen.SheetMetal.Tests.SpecData;

public class StandardRegistryTests
{
    [Fact]
    public void Load_ReadsEntriesFromJson()
    {
        var path = Path.Combine(Path.GetTempPath(), $"standards-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            [
              { "id": "B1005010", "displayName": "B1005010 - Aluminum Beads", "workbookPath": "C:\\specs\\B1005010.xlsx" }
            ]
            """);

        try
        {
            var standards = new StandardRegistry(path).Load();

            Assert.Single(standards);
            Assert.Equal("B1005010", standards[0].Id);
            Assert.Equal("B1005010 - Aluminum Beads", standards[0].DisplayName);
            Assert.Equal(@"C:\specs\B1005010.xlsx", standards[0].WorkbookPath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MissingFile_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");

        Assert.Throws<FileNotFoundException>(() => new StandardRegistry(path).Load());
    }
}
