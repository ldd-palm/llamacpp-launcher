using System.IO;
using System.Text.Json;

namespace LlamaCppLauncher.Config;

public sealed class ConfigLoadException : Exception
{
    public ConfigLoadException(string message, Exception inner) : base(message, inner) { }
}

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _configFilePath;

    public ConfigService(string configFilePath)
    {
        _configFilePath = configFilePath;
    }

    public AppConfig? Load()
    {
        if (!File.Exists(_configFilePath))
        {
            return null;
        }

        string json;
        try
        {
            json = File.ReadAllText(_configFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ConfigLoadException("Configuration file could not be read.", ex);
        }

        try
        {
            return JsonSerializer.Deserialize<AppConfig>(json)
                ?? throw new ConfigLoadException("Configuration file is empty.", new InvalidDataException());
        }
        catch (JsonException ex)
        {
            throw new ConfigLoadException("Configuration file is not valid JSON.", ex);
        }
    }

    public void Save(AppConfig config)
    {
        string? directory = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(config, SerializerOptions);
        File.WriteAllText(_configFilePath, json);
    }
}
