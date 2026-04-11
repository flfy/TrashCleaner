using DataCenterModLoader;
using TrashCleaner.Enums;

namespace TrashCleaner.Options
{
    public sealed class OptionsManager
    {
        private static readonly Dictionary<OptionType, ConfigOption> Options = new();
        private static OptionsManager _instance;
        public static OptionsManager Instance => _instance ??= new OptionsManager();
        
        private OptionsManager() {}

        public bool Initialized { get; private set; }

        public bool AddConfigOption(ConfigOption configOption)
        {
            if (!Enum.TryParse<OptionType>(configOption.Key, out var optionType))
            {
                return false;
            }

            return Options.TryAdd(optionType, configOption);
        }

        public void InitializeOptions(TrashCleanerMod.ModConfig defaults)
        {
            if (Initialized)
            {
                return;
            }

            new ConfigOption<bool>(
                key: nameof(OptionType.AutoCleanupCableSpoolsEnabled),
                displayName: "Enable Automatic Cleanup of empty Cable Spools",
                defaultValue: defaults.autoCleanupCableSpoolsEnabled,
                description: "Enables automatic cleanup of empty cable spools.");

            new ConfigOption<bool>(
                key: nameof(OptionType.AutoCleanupSFPBoxesEnabled),
                displayName: "Enable Automatic Cleanup of empty SFP Boxes",
                defaultValue: defaults.autoCleanupSFPBoxesEnabled,
                description: "Enables automatic cleanup of empty SFP boxes.");

            new ConfigOption<bool>(
                key: nameof(OptionType.SFPKeepZoneEnabled),
                displayName: "Enable SFP Box Keep Zone",
                defaultValue: defaults.sfpKeepZoneEnabled,
                description: "Prevents automatic cleanup of empty SFP boxes inside the protected keep zone.");

            new ConfigOption<float>(
                key: nameof(OptionType.SFPKeepZoneRadiusMeters),
                displayName: "SFP Keep Zone Radius",
                defaultValue: Math.Max(0.1f, defaults.sfpKeepZoneRadiusMeters),
                description: "Horizontal radius of the protected SFP keep zone, in meters.",
                minimumValue: 0.1f,
                maximumValue: 25f);

            new ConfigOption<int>(
                key: nameof(OptionType.AutoCleanupIntervalMinutes),
                displayName: "Automatic Cleanup Interval",
                defaultValue: Math.Max(1, (int)Math.Round(defaults.autoCleanupIntervalMinutes)),
                description: "How often empty SFP boxes will be cleaned up, in minutes.");

            new ConfigOption<float>(
                key: nameof(OptionType.CableSpoolLengthThreshold),
                displayName: "Cable Spool Length Threshold",
                defaultValue: Math.Max(0.1f, defaults.cableSpoolLengthThreshold),
                description: "The minimum length of cable spools to keep, in meters.",
                minimumValue: 0.1f,
                maximumValue: 10.5f);

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
                var type when type == typeof(bool) => (T)(object)ModConfigSystem.GetBoolValue(TrashCleanerMod.ModName, optionType.ToString()),
                var type when type == typeof(int) => (T)(object)ModConfigSystem.GetIntValue(TrashCleanerMod.ModName, optionType.ToString()),
                var type when type == typeof(float) => (T)(object)ModConfigSystem.GetFloatValue(TrashCleanerMod.ModName, optionType.ToString()),
                _ => default
            };
        }
    }
}
