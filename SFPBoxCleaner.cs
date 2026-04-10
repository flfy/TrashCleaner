using System.Text.Json;
using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(SFPBoxCleaner.SFPBoxCleanerMod), "SFPBoxCleaner", "1.0.0", "derrick")]
[assembly: MelonGame(null, "Data Center")]

namespace SFPBoxCleaner;

public sealed class SFPBoxCleanerMod : MelonMod
{
    private const string ModFolderName = "SFPBoxCleaner";
    private const string ConfigFileName = "config.json";
    private const double DefaultCleanupIntervalSeconds = 300d;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private ModConfig _config = ModConfig.CreateDefault();
    private Key _cleanupKey = Key.F9;
    private string _configPath = string.Empty;
    private double _nextAutoCleanupTime;

    public override void OnInitializeMelon()
    {
        var modDirectory = Path.Combine(Path.GetDirectoryName(typeof(SFPBoxCleanerMod).Assembly.Location), ModFolderName);
        Directory.CreateDirectory(modDirectory);

        _configPath = Path.Combine(modDirectory, ConfigFileName);
        _config = LoadConfig(_configPath);
        _cleanupKey = ParseKey(_config.toggleKey);
        _nextAutoCleanupTime = GetCurrentTime() + GetCleanupIntervalSeconds();

        LoggerInstance.Msg($"Empty SFP box cleanup key: {_cleanupKey}");
        LoggerInstance.Msg(_config.autoCleanupEnabled
            ? $"Automatic empty SFP box cleanup enabled every {GetCleanupIntervalSeconds() / 60d:0.##} minute(s)."
            : "Automatic empty SFP box cleanup disabled.");
    }

    private sealed class ModConfig
    {
        public string toggleKey { get; set; } = nameof(Key.F9);
        public bool autoCleanupEnabled { get; set; } = true;
        public double autoCleanupIntervalMinutes { get; set; } = DefaultCleanupIntervalSeconds / 60d;

        public static ModConfig CreateDefault() => new();
    }

    public override void OnUpdate()
    {
        if (MainGameManager.instance != null && _config.autoCleanupEnabled && GetCurrentTime() >= _nextAutoCleanupTime)
        {
            RunCleanup("Automatic cleanup");
            _nextAutoCleanupTime = GetCurrentTime() + GetCleanupIntervalSeconds();
        }

        var keyboard = Keyboard.current;
        if (keyboard is null)
        {
            return;
        }

        var keyControl = keyboard[_cleanupKey];
        if (keyControl is null || !keyControl.wasPressedThisFrame)
        {
            return;
        }

        RunCleanup("Manual cleanup");
    }

    private void RunCleanup(string triggerLabel)
    {
        if (MainGameManager.instance == null)
        {
            LoggerInstance.Warning($"{triggerLabel} requested before a game world was loaded.");
            return;
        }

        var removed = RemoveEmptySfpBoxes();
        LoggerInstance.Msg(removed > 0
            ? $"{triggerLabel}: removed {removed} empty SFP box{(removed == 1 ? string.Empty : "es")}."
            : $"{triggerLabel}: no empty SFP boxes found.");
    }

    private int RemoveEmptySfpBoxes()
    {
        var removed = 0;
        var removedInstanceIds = new HashSet<int>();

        foreach (var box in Object.FindObjectsOfType<SFPBox>())
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

        foreach (var usable in Object.FindObjectsOfType<UsableObject>())
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
        }

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

        Object.Destroy(gameObject);
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

    private double GetCleanupIntervalSeconds()
    {
        return Math.Max(1d, _config.autoCleanupIntervalMinutes) * 60d;
    }

    private ModConfig LoadConfig(string path)
    {
        if (!File.Exists(path))
        {
            SaveConfig(path, _config);
            return _config;
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

        _config.toggleKey = nameof(Key.F9);
        SaveConfig(_configPath, _config);
        return Key.F9;
    }
}
