using LlamaCppLauncher.Config;
using LlamaCppLauncher.Validation;

namespace LlamaCppLauncher.Startup;

public sealed class LauncherBootstrapper
{
    private readonly ConfigService _configService;
    private readonly ValidationService _validationService;

    public LauncherBootstrapper(ConfigService configService, ValidationService validationService)
    {
        _configService = configService;
        _validationService = validationService;
    }

    public StartupDecision Decide()
    {
        AppConfig? config;
        try
        {
            config = _configService.Load();
        }
        catch (ConfigLoadException)
        {
            return new StartupDecision
            {
                Action = StartupAction.OpenSettingsWithError,
                Message = "Configuration file is corrupted. Please review your settings."
            };
        }

        if (config is null)
        {
            return new StartupDecision
            {
                Action = StartupAction.OpenSettingsWithError,
                Message = "Welcome! Please configure llama.cpp before starting."
            };
        }

        ValidationResult validation = _validationService.ValidateGeneral(config);
        if (!validation.IsValid)
        {
            return new StartupDecision
            {
                Action = StartupAction.StayOffWithNotification,
                Message = string.Join(" ", validation.Errors),
                Config = config
            };
        }

        ModelProfile? defaultModel = config.Models.FirstOrDefault(m => m.IsDefault);
        if (defaultModel is null)
        {
            return new StartupDecision
            {
                Action = StartupAction.StayOffWithNotification,
                Message = "No default model is configured. Open Settings to choose one.",
                Config = config
            };
        }

        return new StartupDecision
        {
            Action = StartupAction.AutoStartDefaultModel,
            Config = config,
            DefaultModel = defaultModel
        };
    }
}
