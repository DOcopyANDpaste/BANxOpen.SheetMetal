using BANxOpen.SheetMetal.Beads;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>The real <see cref="IBeadSpecSource"/>: Standards come from <see cref="StandardRegistry"/>,
/// each Standard's rows come from its workbook via <see cref="ExcelBeadSpecParser"/>. Kept as a thin
/// composition so both pieces stay independently testable (parser against an in-memory workbook, registry
/// against a temp JSON file).</summary>
public sealed class FileSystemBeadSpecSource : IBeadSpecSource
{
    private readonly StandardRegistry _registry;
    private readonly ExcelBeadSpecParser _parser;

    public FileSystemBeadSpecSource(StandardRegistry registry, ExcelBeadSpecParser parser)
    {
        _registry = registry;
        _parser = parser;
    }

    public IReadOnlyList<StandardInfo> ListStandards() => _registry.Load();

    public IReadOnlyList<BeadSpecRow> ReadWorkbook(StandardInfo standard)
    {
        using var stream = File.OpenRead(standard.WorkbookPath);
        return _parser.Parse(standard.Id, stream);
    }
}
