using System.IO;
using LlamaCppLauncher.Config;
using LlamaCppLauncher.Discovery;

namespace LlamaCppLauncher.Validation;

public sealed class ValidationService
{
    private readonly IPortChecker _portChecker;

    public ValidationService(IPortChecker portChecker)
    {
        _portChecker = portChecker;
    }

    public ValidationResult ValidateGeneral(AppConfig config)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(config.ExecutablePath) || !File.Exists(config.ExecutablePath))
        {
            result.AddError($"llama.cpp executable not found at '{config.ExecutablePath}'.");
        }

        if (string.IsNullOrWhiteSpace(config.ModelsDirectory) || !Directory.Exists(config.ModelsDirectory))
        {
            result.AddError($"Models directory not found at '{config.ModelsDirectory}'.");
        }
        else if (!ModelDiscoveryService.DiscoverModelFiles(config.ModelsDirectory).Any())
        {
            result.AddError($"No .gguf files found in '{config.ModelsDirectory}'.");
        }

        PortStatus portStatus = _portChecker.GetStatus(config.Port);
        if (portStatus == PortStatus.OccupiedByOther)
        {
            result.AddError($"Port {config.Port} is already in use by another application.");
        }

        return result;
    }
}
