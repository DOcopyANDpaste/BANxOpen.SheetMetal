using BANxOpen.SheetMetal.Materials;

namespace BANxOpen.SheetMetal.Tests.Materials;

public class MaterialGradeMapTests
{
    [Fact]
    public void GradeFor_KnownName_ReturnsMappedGrade()
    {
        var path = Path.Combine(Path.GetTempPath(), $"grademap-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """{ "Aluminum 2024, Temper O": "2024-O" }""");

        try
        {
            var map = MaterialGradeMap.Load(path);

            Assert.Equal("2024-O", map.GradeFor("Aluminum 2024, Temper O"));
            Assert.Null(map.GradeFor("Some Unmapped Material"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
