using DataCenterModLoader;
using SFPBoxCleaner.Enums;

namespace SFPBoxCleaner.Options;

public sealed class OptionsManager
{
    private static readonly Dictionary<OptionType, ConfigOption> Options = new();
    private static OptionsManager _instance;

    public static OptionsManager Instance => _instance ??= new OptionsManager();

    public bool Initialized { get; private set; }

    private OptionsManager()
    {
    }

    public bool AddConfigOption(ConfigOption configOption)
    {
        if (!Enum.TryParse<OptionType>(configOption.Key, out var optionType))
        {
            return false;
        }

        return Options.TryAdd(optionType, configOption);
    }

    public void InitializeOptions(SFPBoxCleanerMod.ModConfig defaults)
    {
        if (Initialized)
        {
            return;
        }

        new ConfigOption<bool>(
            key: nameof(OptionType.AutoCleanupEnabled),
            displayName: "Enable Automatic Cleanup",
            defaultValue: defaults.autoCleanupEnabled,
            description: "Enables automatic cleanup of empty SFP boxes.");

        new ConfigOption<int>(
            key: nameof(OptionType.AutoCleanupIntervalMinutes),
            displayName: "Automatic Cleanup Interval",
            defaultValue: Math.Max(1, (int)Math.Round(defaults.autoCleanupIntervalMinutes)),
            description: "How often empty SFP boxes will be cleaned up, in minutes.");

        Initialized = true;
    }

    public T GetConfigOptionValue<T>(OptionType optionType) where T : struct
    {
        if (!Options.ContainsKey(optionType))
        {
            return default;
        }

        return typeof(T) switch
        {
            var type when type == typeof(bool) => (T)(object)ModConfigSystem.GetBoolValue(SFPBoxCleanerMod.ModName, optionType.ToString()),
            var type when type == typeof(int) => (T)(object)ModConfigSystem.GetIntValue(SFPBoxCleanerMod.ModName, optionType.ToString()),
            _ => default
        };
    }
}
