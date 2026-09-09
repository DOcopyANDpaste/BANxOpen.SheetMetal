using System.Text.Json;
using System.Text.Json.Serialization;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>Reads the Standards registry from a small user-editable JSON file, so adding a Standard is a
/// config edit, not a rebuild:
/// <code>
/// [
///   { "id": "B1005010", "displayName": "B1005010 - Aluminum Beads", "workbookPath": "\\\\server\\share\\B1005010.xlsx" }
/// ]
/// </code></summary>
public sealed class StandardRegistry
{
    private readonly string _registryPath;

    public StandardRegistry(string registryPath) => _registryPath = registryPath;

    public IReadOnlyList<StandardInfo> Load()
    {
        if (!File.Exists(_registryPath))
            throw new FileNotFoundException($"Standards registry not found: {_registryPath}", _registryPath);

        using var stream = File.OpenRead(_registryPath);
        var entries = JsonSerializer.Deserialize<List<Entry>>(stream)
            ?? throw new InvalidDataException($"Standards registry at '{_registryPath}' is empty or invalid.");

        return entries
            .Select(e => new StandardInfo(e.Id, e.DisplayName, e.WorkbookPath))
            .ToList();
    }

    private sealed class Entry
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("workbookPath")]
        public string WorkbookPath { get; set; } = "";
    }
}
