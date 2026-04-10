using DataCenterModLoader;
using Il2Cpp;
using MelonLoader;

using UnityEngine;
using UnityEngine.InputSystem;

using TrashCleaner.Enums;
using TrashCleaner.Options;

using System.Text.Json;
using UnityEngine.Experimental.GlobalIllumination;

[assembly: MelonInfo(typeof(TrashCleaner.TrashCleanerMod), "TrashCleaner", "2.0.0", "derrick")]
[assembly: MelonAdditionalDependencies("DataCenterModLoader")]
[assembly: MelonGame(null, "Data Center")]

namespace TrashCleaner
{
    public sealed class TrashCleanerMod : MelonMod
    {
        public const string ModName = "TrashCleaner";
        private const string Author = "derrick";
        private const string Version = "2.0.0";
        private const string ModFolderName = "TrashCleaner";
        private const string ConfigFileName = "config.json";
        private const double DefaultCleanupIntervalSeconds = 300d;

        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
        private ModConfig config = ModConfig.CreateDefault();
        private Key cleanupKey = Key.F9;
        
        private string configPath = string.Empty;
        private double nextAutoCleanupTime;

        public override void OnInitializeMelon()
        {
            var modDirectory = Path.Combine(Path.GetDirectoryName(typeof(TrashCleanerMod).Assembly.Location), ModFolderName);
            Directory.CreateDirectory(modDirectory);

            configPath = Path.Combine(modDirectory, ConfigFileName);
            config = LoadConfig(configPath);

            cleanupKey = ParseKey(config.toggleKey);

            ModConfigSystem.SetModInfo(ModName, Author, Version);
            OptionsManager.Instance.InitializeOptions(config);

            SyncConfigFromOptions();

            LoggerInstance.Msg($"TrashCleaner Manual Keybind: {cleanupKey}");
            LoggerInstance.Msg((config.autoCleanupCableSpoolsEnabled || config.autoCleanupSFPBoxesEnabled)
                ? $"TrashCleaner automation enabled every {GetCleanupIntervalSeconds() / 60d:0.##} minute(s)."
                : "TrashCleaner automation disabled.");
            
            if (config.autoCleanupCableSpoolsEnabled || config.autoCleanupSFPBoxesEnabled)
            {
                nextAutoCleanupTime = GetCurrentTime() + GetCleanupIntervalSeconds();
            }
        }

        public sealed class ModConfig
        {
            public string toggleKey { get; set; } = nameof(Key.F9);
            public bool autoCleanupCableSpoolsEnabled { get; set; } = true;
            public bool autoCleanupSFPBoxesEnabled { get; set; } = true;
            public double autoCleanupIntervalMinutes { get; set; } = DefaultCleanupIntervalSeconds / 60d;
            public float cableSpoolLengthThreshold { get; set; } = 1.0f;

            public static ModConfig CreateDefault() => new();
        }

        public override void OnUpdate()
        {
            SyncConfigFromOptions();

            if (MainGameManager.instance != null && (config.autoCleanupCableSpoolsEnabled || config.autoCleanupSFPBoxesEnabled) && GetCurrentTime() >= nextAutoCleanupTime)
            {
                RunCleanup(automatic: true);

                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard is null)
            {
                return;
            }

            var keyControl = keyboard[cleanupKey];
            if (keyControl is null || !keyControl.wasPressedThisFrame)
            {
                return;
            }

            RunCleanup();
        }

        private void RunCleanup(bool automatic = false)
        {

            if (MainGameManager.instance == null)
            {
                LoggerInstance.Warning($"Cleanup requested before a game world was loaded.");
                return;
            }

            string triggerLabel;
            if (automatic)
            {
                triggerLabel = "Automatic cleanup";
                nextAutoCleanupTime = GetCurrentTime() + GetCleanupIntervalSeconds();

            } else
            {
                triggerLabel = "Manual cleanup";
            }

            if (config.autoCleanupCableSpoolsEnabled)
            {
                var removed = RemoveEmptyCableSpools();
                LoggerInstance.Msg(removed > 0
                    ? $"{triggerLabel}: removed {removed} empty cable spool{(removed == 1 ? string.Empty : "s")}."
                    : $"{triggerLabel}: no empty cable spools found.");
            }

            if (config.autoCleanupSFPBoxesEnabled)
            {
                var removed = RemoveEmptySfpBoxes();
                LoggerInstance.Msg(removed > 0
                    ? $"{triggerLabel}: removed {removed} empty SFP box{(removed == 1 ? string.Empty : "es")}."
                    : $"{triggerLabel}: no empty SFP boxes found.");
            }
        }

        private int RemoveEmptyCableSpools()
        {
            var removed = 0;
            var removedInstanceIds = new HashSet<int>();

            foreach (var spool in UnityEngine.Object.FindObjectsOfType<CableSpinner>())
            {
                if (spool == null || spool.cableLenght > config.cableSpoolLengthThreshold)
                {
                    continue;
                }

                removed += DestroyUsableObject(spool, removedInstanceIds);

            }

            return removed;
        }

        private int RemoveEmptySfpBoxes()
        {
            var removed = 0;
            var removedInstanceIds = new HashSet<int>();

            foreach (var box in UnityEngine.Object.FindObjectsOfType<SFPBox>())
            {
                if (box == null || !ShouldRemoveLooseBox(box) || !IsActuallyEmpty(box))
                {
                    continue;
                }

                removed += DestroyUsableObject(box, removedInstanceIds);
            }

            var emptyPrefabName = NormalizeName(MainGameManager.instance.emptySfpBox?.name);

            if (string.IsNullOrWhiteSpace(emptyPrefabName))
            {
                return removed;
            }

            /*foreach (var usable in UnityEngine.Object.FindObjectsOfType<UsableObject>())
            {
                if (usable == null || usable is SFPBox || !ShouldRemoveLooseBox(usable))
                {
                    continue;
                }

                if (usable.objectInHandType != PlayerManager.ObjectInHand.SFPBox)
                {
                    continue;
                }

                if (!string.Equals(NormalizeName(usable.gameObject.name), emptyPrefabName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                removed += DestroyUsableObject(usable, removedInstanceIds);
            }*/

            return removed;
        }

        private static bool ShouldRemoveLooseBox(UsableObject usable)
        {
            if (usable.objectInHands || usable.isOnTrolley || usable.currentRackPosition != null)
            {
                return false;
            }

            if (PlayerManager.instance != null && PlayerManager.instance.objectInHand == PlayerManager.ObjectInHand.SFPBox)
            {
                var heldObjects = PlayerManager.instance.objectInHandGO;
                if (heldObjects != null)
                {
                    foreach (var heldObject in heldObjects)
                    {
                        if (heldObject != null && heldObject == usable.gameObject)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static bool IsActuallyEmpty(SFPBox box)
        {
            var usedPositions = box.usedPositions;
            if (usedPositions != null)
            {
                foreach (var usedPosition in usedPositions)
                {
                    if (usedPosition != 0)
                    {
                        return false;
                    }
                }
            }

            foreach (var module in box.GetComponentsInChildren<SFPModule>(true))
            {
                if (module != null && module.isInTheBox)
                {
                    return false;
                }
            }

            return true;
        }

        private static int DestroyUsableObject(UsableObject usable, HashSet<int> removedInstanceIds)
        {
            var gameObject = usable.gameObject;
            var instanceId = gameObject.GetInstanceID();
            if (!removedInstanceIds.Add(instanceId))
            {
                return 0;
            }

            UnityEngine.Object.Destroy(gameObject);
            return 1;
        }

        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return name.EndsWith("(Clone)", StringComparison.Ordinal)
                ? name[..^"(Clone)".Length].TrimEnd()
                : name;
        }

        private static double GetCurrentTime()
        {
            return Time.realtimeSinceStartupAsDouble;
        }

        private void SyncConfigFromOptions()
        {
            if (!OptionsManager.Instance.Initialized)
            {
                return;
            }

            var autoCleanupSFPBoxesEnabled = OptionsManager.Instance.GetConfigOptionValue<bool>(OptionType.AutoCleanupSFPBoxesEnabled);
            var autoCleanupCableSpoolsEnabled = OptionsManager.Instance.GetConfigOptionValue<bool>(OptionType.AutoCleanupCableSpoolsEnabled);
            var autoCleanupIntervalMinutes = Math.Max(1, OptionsManager.Instance.GetConfigOptionValue<int>(OptionType.AutoCleanupIntervalMinutes));
            var cableSpoolLengthThreshold = Math.Max(0.1f, OptionsManager.Instance.GetConfigOptionValue<float>(OptionType.CableSpoolLengthThreshold));

            if (config.autoCleanupSFPBoxesEnabled == autoCleanupSFPBoxesEnabled &&
                config.autoCleanupCableSpoolsEnabled == autoCleanupCableSpoolsEnabled &&
                Math.Abs(config.autoCleanupIntervalMinutes - autoCleanupIntervalMinutes) < double.Epsilon &&
                Math.Abs(config.cableSpoolLengthThreshold - cableSpoolLengthThreshold) < float.Epsilon)
            {
                return;
            }

            config.autoCleanupSFPBoxesEnabled = autoCleanupSFPBoxesEnabled;
            config.autoCleanupCableSpoolsEnabled = autoCleanupCableSpoolsEnabled;
            config.autoCleanupIntervalMinutes = autoCleanupIntervalMinutes;
            config.cableSpoolLengthThreshold = cableSpoolLengthThreshold;
            SaveConfig(configPath, config);
            nextAutoCleanupTime = GetCurrentTime() + GetCleanupIntervalSeconds();
        }

        private double GetCleanupIntervalSeconds()
        {
            return Math.Max(1d, config.autoCleanupIntervalMinutes) * 60d;
        }

        private ModConfig LoadConfig(string path)
        {
            if (!File.Exists(path))
            {
                SaveConfig(path, config);
                return config;
            }

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<ModConfig>(json) ?? ModConfig.CreateDefault();
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning($"Failed to parse config at '{path}': {ex.Message}. Rewriting defaults.");
                var fallback = ModConfig.CreateDefault();
                SaveConfig(path, fallback);
                return fallback;
            }
        }

        private static void SaveConfig(string path, ModConfig config)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptions));
        }

        private Key ParseKey(string configuredKey)
        {
            if (!string.IsNullOrWhiteSpace(configuredKey) &&
                Enum.TryParse(configuredKey, ignoreCase: true, out Key parsedKey))
            {
                return parsedKey;
            }

            if (!string.IsNullOrWhiteSpace(configuredKey))
            {
                LoggerInstance.Warning($"Unknown key '{configuredKey}' in config. Falling back to F9.");
            }

            config.toggleKey = nameof(Key.F9);
            SaveConfig(configPath, config);
            return Key.F9;
        }
    }


}
