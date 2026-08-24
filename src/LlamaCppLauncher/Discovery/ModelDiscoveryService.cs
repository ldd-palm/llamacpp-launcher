using System.IO;
using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Discovery;

public static class ModelDiscoveryService
{
    public static IReadOnlyList<string> DiscoverModelFiles(string modelsDirectory)
    {
        if (!Directory.Exists(modelsDirectory))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(modelsDirectory, "*.gguf", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<ModelProfile> MergeWithConfiguredModels(
        IReadOnlyList<string> discoveredFileNames,
        IReadOnlyList<ModelProfile> configuredModels)
    {
        var byFileName = configuredModels.ToDictionary(m => m.FileName, StringComparer.OrdinalIgnoreCase);
        var merged = new List<ModelProfile>();

        foreach (string fileName in discoveredFileNames)
        {
            merged.Add(byFileName.TryGetValue(fileName, out ModelProfile? existing)
                ? existing
                : ModelProfile.CreateDefault(fileName));
        }

        return merged;
    }
}
