using System.Text.Json;
using System.Text.Json.Serialization;

namespace BANxOpen.SheetMetal.SpecData;

/// <summary>Reads the Standards registry from a small user-editable JSON file, so adding a Standard is a
/// config edit, not a rebuild:
/// <code>
/// [
///   { "id": "B1005010", "displayName": "B1005010 - Aluminum Beads", "workbookPath": "\\\\server\\share\\B1005010.xlsx" }
/// ]
/// </code>
/// A relative <c>workbookPath</c> is resolved against the folder holding this registry file, not the process's
/// working directory. The registry is located through <c>SheetMetalConfigLocator</c> and read from inside NX,
/// whose working directory is unrelated to where the config lives.</summary>
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

        var registryDirectory = Path.GetDirectoryName(Path.GetFullPath(_registryPath)) ?? "";

        return entries
            .Select(e => new StandardInfo(e.Id, e.DisplayName, ResolveWorkbookPath(registryDirectory, e.WorkbookPath)))
            .ToList();
    }

    // Path.Combine returns the second argument unchanged when it is rooted, so absolute and UNC paths pass
    // straight through.
    private static string ResolveWorkbookPath(string registryDirectory, string workbookPath) =>
        string.IsNullOrWhiteSpace(workbookPath) || Path.IsPathRooted(workbookPath)
            ? workbookPath
            : Path.GetFullPath(Path.Combine(registryDirectory, workbookPath));

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
