using DataCenterModLoader;

namespace SFPBoxCleaner.Options;

public abstract class ConfigOption
{
    public string Key { get; }
    public string DisplayName { get; }
    public object DefaultValue { get; protected init; }
    public string Description { get; }

    protected ConfigOption(string key, string displayName, object defaultValue, string description)
    {
        Key = key;
        DisplayName = displayName;
        DefaultValue = defaultValue;
        Description = description;
    }
}

public sealed class ConfigOption<T> : ConfigOption where T : struct
{
    public ConfigOption(string key, string displayName, T defaultValue, string description)
        : base(key, displayName, defaultValue, description)
    {
        DefaultValue = defaultValue;

        if (!OptionsManager.Instance.AddConfigOption(this))
        {
            return;
        }

        switch (typeof(T))
        {
            case var type when type == typeof(bool):
                ModConfigSystem.RegisterBoolOption(SFPBoxCleanerMod.ModName, key, displayName, (bool)DefaultValue, description);
                break;
            case var type when type == typeof(int):
                ModConfigSystem.RegisterIntOption(SFPBoxCleanerMod.ModName, key, displayName, (int)DefaultValue, 1, 1440, description);
                break;
        }
    }
}
