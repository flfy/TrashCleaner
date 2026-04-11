using DataCenterModLoader;

namespace TrashCleaner.Options
{
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
        private readonly T? minimumValue;
        private readonly T? maximumValue;

        public ConfigOption(string key, string displayName, T defaultValue, string description, T? minimumValue = null, T? maximumValue = null)
            : base(key, displayName, defaultValue, description)
        {
            DefaultValue = defaultValue;
            this.minimumValue = minimumValue;
            this.maximumValue = maximumValue;

            if (!OptionsManager.Instance.AddConfigOption(this))
            {
                return;
            }

            switch (typeof(T))
            {
                case var type when type == typeof(bool):
                    ModConfigSystem.RegisterBoolOption(TrashCleanerMod.ModName, key, displayName, (bool)DefaultValue, description);
                    break;
                case var type when type == typeof(int):
                    ModConfigSystem.RegisterIntOption(
                        TrashCleanerMod.ModName,
                        key,
                        displayName,
                        (int)DefaultValue,
                        minimumValue.HasValue ? (int)(object)minimumValue.Value : 1,
                        maximumValue.HasValue ? (int)(object)maximumValue.Value : 1440,
                        description);
                    break;
                case var type when type == typeof(float):
                    ModConfigSystem.RegisterFloatOption(
                        TrashCleanerMod.ModName,
                        key,
                        displayName,
                        (float)DefaultValue,
                        minimumValue.HasValue ? (float)(object)minimumValue.Value : 1.0f,
                        maximumValue.HasValue ? (float)(object)maximumValue.Value : 10.5f,
                        description);
                    break;
            }
        }
    }
}
