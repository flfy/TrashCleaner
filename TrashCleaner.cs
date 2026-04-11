using DataCenterModLoader;
using Il2Cpp;
using MelonLoader;

using UnityEngine;
using UnityEngine.InputSystem;

using TrashCleaner.Enums;
using TrashCleaner.Options;

using System.Text.Json;

[assembly: MelonInfo(typeof(TrashCleaner.TrashCleanerMod), "TrashCleaner", "2.1.0", "derrick")]
[assembly: MelonAdditionalDependencies("DataCenterModLoader")]
[assembly: MelonGame(null, "Data Center")]

namespace TrashCleaner
{
    public sealed class TrashCleanerMod : MelonMod
    {
        public const string ModName = "TrashCleaner";

        private const string Author = "derrick";
        private const string Version = "2.1.0";
        private const string ModFolderName = "TrashCleaner";
        private const string ConfigFileName = "config.json";
        private const double DefaultCleanupIntervalSeconds = 300d;
        private const float DefaultSfpKeepZoneRadiusMeters = 3f;
        private const float SfpKeepZoneBoxSnapDistanceMeters = 3f;

        private static readonly bool LogSfpBoxesOnlyWithoutDeleting = false;
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private ModConfig config = ModConfig.CreateDefault();
        private Key cleanupKey = Key.F9;
        private Key sfpKeepZoneCaptureKey = Key.F10;
        private string configPath = string.Empty;
        private double nextAutoCleanupTime;

        public sealed class ModConfig
        {
            public string toggleKey { get; set; } = nameof(Key.F9);
            public bool autoCleanupCableSpoolsEnabled { get; set; } = true;
            public bool autoCleanupSFPBoxesEnabled { get; set; } = true;
            public double autoCleanupIntervalMinutes { get; set; } = DefaultCleanupIntervalSeconds / 60d;
            public float cableSpoolLengthThreshold { get; set; } = 1.5f;
            public bool sfpKeepZoneEnabled { get; set; } = false;
            public string sfpKeepZoneCaptureKey { get; set; } = nameof(Key.F10);
            public float sfpKeepZoneRadiusMeters { get; set; } = DefaultSfpKeepZoneRadiusMeters;
            public float sfpKeepZoneCenterX { get; set; }
            public float sfpKeepZoneCenterY { get; set; }
            public float sfpKeepZoneCenterZ { get; set; }

            public static ModConfig CreateDefault() => new();
        }

        private readonly struct SfpBoxCleanupResult
        {
            public SfpBoxCleanupResult(int removed, int preserved, int logged, int wouldRemove)
            {
                Removed = removed;
                Preserved = preserved;
                Logged = logged;
                WouldRemove = wouldRemove;
            }

            public int Removed { get; }
            public int Preserved { get; }
            public int Logged { get; }
            public int WouldRemove { get; }
        }

        public override void OnInitializeMelon()
        {
            var modDirectory = Path.Combine(Path.GetDirectoryName(typeof(TrashCleanerMod).Assembly.Location), ModFolderName);
            Directory.CreateDirectory(modDirectory);

            configPath = Path.Combine(modDirectory, ConfigFileName);
            config = LoadConfig(configPath);
            cleanupKey = ParseKey(config.toggleKey, Key.F9, value => config.toggleKey = value, "cleanup");
            sfpKeepZoneCaptureKey = ParseKey(config.sfpKeepZoneCaptureKey, Key.F10, value => config.sfpKeepZoneCaptureKey = value, "SFP keep-zone capture");

            ModConfigSystem.SetModInfo(ModName, Author, Version);
            OptionsManager.Instance.InitializeOptions(config);

            SyncConfigFromOptions();

            LoggerInstance.Msg($"TrashCleaner Cleanup Keybind: {cleanupKey}");
            LoggerInstance.Msg($"TrashCleaner SFP Keep-Zone Capture Keybind: {sfpKeepZoneCaptureKey}");

            if (config.sfpKeepZoneEnabled)
            {
                LoggerInstance.Msg($"TrashCleaner SFP keep zone active at {FormatVector3(GetSfpKeepZoneCenter())} with a {GetSfpKeepZoneRadiusMeters():0.##}m radius.");
            }

            LoggerInstance.Msg(IsAutomationEnabled()
                ? $"TrashCleaner automation enabled every {GetCleanupIntervalSeconds() / 60d:0.##} minute(s)."
                : "TrashCleaner automation disabled.");

            if (IsAutomationEnabled())
            {
                nextAutoCleanupTime = GetCurrentTime() + GetCleanupIntervalSeconds();
            }
        }

        public override void OnUpdate()
        {
            SyncConfigFromOptions();

            if (MainGameManager.instance != null && IsAutomationEnabled() && GetCurrentTime() >= nextAutoCleanupTime)
            {
                RunCleanup(automatic: true);
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard is null)
            {
                return;
            }

            if (WasPressedThisFrame(keyboard, sfpKeepZoneCaptureKey))
            {
                CaptureSfpKeepZone();

                if (sfpKeepZoneCaptureKey == cleanupKey)
                {
                    return;
                }
            }

            if (!WasPressedThisFrame(keyboard, cleanupKey))
            {
                return;
            }

            RunCleanup();
        }

        private void RunCleanup(bool automatic = false)
        {
            if (MainGameManager.instance == null)
            {
                LoggerInstance.Warning("Cleanup requested before a game world was loaded.");
                return;
            }

            var triggerLabel = automatic ? "Automatic cleanup" : "Manual cleanup";
            if (automatic)
            {
                nextAutoCleanupTime = GetCurrentTime() + GetCleanupIntervalSeconds();
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
                LogSfpCleanupResult(triggerLabel, RemoveEmptySfpBoxes());
            }
        }

        private void LogSfpCleanupResult(string triggerLabel, SfpBoxCleanupResult cleanupResult)
        {
            if (LogSfpBoxesOnlyWithoutDeleting)
            {
                LoggerInstance.Msg(
                    $"{triggerLabel}: logged {cleanupResult.Logged} SFP box{(cleanupResult.Logged == 1 ? string.Empty : "es")}. " +
                    $"SFP deletion disabled for diagnostics. Would remove {cleanupResult.WouldRemove} empty loose SFP box{(cleanupResult.WouldRemove == 1 ? string.Empty : "es")} outside the keep zone; " +
                    $"inside keep zone: {cleanupResult.Preserved}.");
                return;
            }

            if (cleanupResult.Removed > 0)
            {
                var preservedMessage = cleanupResult.Preserved > 0
                    ? $" Preserved {cleanupResult.Preserved} in the keep zone."
                    : string.Empty;

                LoggerInstance.Msg($"{triggerLabel}: removed {cleanupResult.Removed} empty SFP box{(cleanupResult.Removed == 1 ? string.Empty : "es")}.{preservedMessage}");
                return;
            }

            if (cleanupResult.Preserved > 0)
            {
                LoggerInstance.Msg($"{triggerLabel}: preserved {cleanupResult.Preserved} empty SFP box{(cleanupResult.Preserved == 1 ? string.Empty : "es")} in the keep zone.");
                return;
            }

            LoggerInstance.Msg($"{triggerLabel}: no empty SFP boxes found.");
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

        private SfpBoxCleanupResult RemoveEmptySfpBoxes()
        {
            var removed = 0;
            var preserved = 0;
            var logged = 0;
            var wouldRemove = 0;
            var removedInstanceIds = new HashSet<int>();

            foreach (var box in UnityEngine.Object.FindObjectsOfType<SFPBox>())
            {
                if (box == null)
                {
                    continue;
                }

                logged++;

                var isLoose = ShouldRemoveLooseBox(box);
                var isEmpty = IsActuallyEmpty(box);
                var isInsideKeepZone = isLoose && isEmpty && IsInsideSfpKeepZone(box);

                LogSfpBoxDetails(box, isLoose, isEmpty, isInsideKeepZone);

                if (!isLoose || !isEmpty)
                {
                    continue;
                }

                if (isInsideKeepZone)
                {
                    preserved++;
                    continue;
                }

                if (LogSfpBoxesOnlyWithoutDeleting)
                {
                    wouldRemove++;
                    continue;
                }

                removed += DestroyUsableObject(box, removedInstanceIds);
            }

            return new SfpBoxCleanupResult(removed, preserved, logged, wouldRemove);
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

        private void CaptureSfpKeepZone()
        {
            if (MainGameManager.instance == null)
            {
                LoggerInstance.Warning("SFP keep zone capture requested before a game world was loaded.");
                return;
            }

            if (!TryGetSfpKeepZoneCapturePosition(out var capturedPosition, out var captureSource))
            {
                LoggerInstance.Warning("Unable to determine the player position for SFP keep zone capture.");
                return;
            }

            config.sfpKeepZoneEnabled = true;
            config.sfpKeepZoneCenterX = capturedPosition.x;
            config.sfpKeepZoneCenterY = capturedPosition.y;
            config.sfpKeepZoneCenterZ = capturedPosition.z;

            SaveConfig(configPath, config);

            LoggerInstance.Msg(
                $"SFP keep zone updated to {FormatVector3(capturedPosition)} with a {GetSfpKeepZoneRadiusMeters():0.##}m radius " +
                $"from {captureSource}.");
        }

        private static bool TryGetSfpKeepZoneCapturePosition(out Vector3 capturedPosition, out string captureSource)
        {
            var captureCandidates = new List<(Vector3 Position, string Label)>();
            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                AddCaptureCandidate(captureCandidates, mainCamera.transform.position, "main camera position");
            }

            if (PlayerManager.instance != null)
            {
                if (PlayerManager.instance.playerGO != null)
                {
                    AddCaptureCandidate(captureCandidates, PlayerManager.instance.playerGO.transform.position, "player GameObject position");
                }

                AddCaptureCandidate(captureCandidates, PlayerManager.instance.transform.position, "player manager position");
            }

            foreach (var candidate in captureCandidates)
            {
                if (!TryGetNearbySfpBoxCapturePosition(candidate.Position, out capturedPosition, out var snappedBoxSource))
                {
                    continue;
                }

                captureSource = $"{snappedBoxSource} near {candidate.Label} {FormatVector3(candidate.Position)}";
                return true;
            }

            if (captureCandidates.Count > 0)
            {
                capturedPosition = captureCandidates[0].Position;
                captureSource = captureCandidates[0].Label;
                return true;
            }

            capturedPosition = default;
            captureSource = string.Empty;
            return false;
        }

        private bool IsInsideSfpKeepZone(SFPBox box)
        {
            if (!config.sfpKeepZoneEnabled)
            {
                return false;
            }

            var radius = GetSfpKeepZoneRadiusMeters();
            var keepZoneCenter = GetSfpKeepZoneCenter();
            var boxBounds = GetSfpKeepZoneBounds(box);
            var delta = GetHorizontalDeltaToBounds(boxBounds, keepZoneCenter);

            return delta.sqrMagnitude <= radius * radius;
        }

        private void LogSfpBoxDetails(SFPBox box, bool isLoose, bool isEmpty, bool isInsideKeepZone)
        {
            var boxBounds = GetSfpKeepZoneBounds(box);
            var keepZoneDetails = "keepZone=disabled";

            if (config.sfpKeepZoneEnabled)
            {
                var keepZoneCenter = GetSfpKeepZoneCenter();
                var closestPoint = GetClosestHorizontalPointOnBounds(boxBounds, keepZoneCenter);
                var horizontalDelta = closestPoint - new Vector2(keepZoneCenter.x, keepZoneCenter.z);
                var horizontalDistance = horizontalDelta.magnitude;

                keepZoneDetails =
                    $"keepZoneCenter={FormatVector3(keepZoneCenter)}, keepZoneRadius={GetSfpKeepZoneRadiusMeters():0.##}m, " +
                    $"keepZoneClosestPointXZ=({closestPoint.x:0.##}, {closestPoint.y:0.##}), " +
                    $"keepZoneDeltaXZ=({horizontalDelta.x:0.##}, {horizontalDelta.y:0.##}), " +
                    $"keepZoneDistanceXZ={horizontalDistance:0.##}m, insideKeepZone={isInsideKeepZone}";
            }

            LoggerInstance.Msg(
                $"SFP box '{box.name}' #{box.gameObject.GetInstanceID()}: " +
                $"transform={FormatVector3(box.transform.position)}, " +
                $"boundsCenter={FormatVector3(boxBounds.center)}, " +
                $"boundsMin={FormatVector3(boxBounds.min)}, " +
                $"boundsMax={FormatVector3(boxBounds.max)}, " +
                $"loose={isLoose}, empty={isEmpty}, {keepZoneDetails}");
        }

        private static Bounds GetSfpKeepZoneBounds(SFPBox box)
        {
            var hasBounds = false;
            var bounds = new Bounds(box.transform.position, Vector3.zero);

            foreach (var collider in box.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                IncludeBounds(ref bounds, ref hasBounds, collider.bounds);
            }

            if (hasBounds)
            {
                return bounds;
            }

            foreach (var renderer in box.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                IncludeBounds(ref bounds, ref hasBounds, renderer.bounds);
            }

            return bounds;
        }

        private static void IncludeBounds(ref Bounds aggregateBounds, ref bool hasBounds, Bounds candidateBounds)
        {
            if (!hasBounds)
            {
                aggregateBounds = candidateBounds;
                hasBounds = true;
                return;
            }

            aggregateBounds.Encapsulate(candidateBounds.min);
            aggregateBounds.Encapsulate(candidateBounds.max);
        }

        private static Vector2 GetHorizontalDeltaToBounds(Bounds bounds, Vector3 point)
        {
            var closestPoint = GetClosestHorizontalPointOnBounds(bounds, point);

            return closestPoint - new Vector2(point.x, point.z);
        }

        private static bool TryGetNearbySfpBoxCapturePosition(Vector3 referencePosition, out Vector3 capturedPosition, out string captureSource)
        {
            SFPBox closestBox = null;
            Bounds closestBounds = default;
            var bestDistanceSquared = SfpKeepZoneBoxSnapDistanceMeters * SfpKeepZoneBoxSnapDistanceMeters;

            foreach (var box in UnityEngine.Object.FindObjectsOfType<SFPBox>())
            {
                if (box == null)
                {
                    continue;
                }

                var bounds = GetSfpKeepZoneBounds(box);
                var delta = GetHorizontalDeltaToBounds(bounds, referencePosition);
                var distanceSquared = delta.sqrMagnitude;
                if (distanceSquared > bestDistanceSquared)
                {
                    continue;
                }

                closestBox = box;
                closestBounds = bounds;
                bestDistanceSquared = distanceSquared;
            }

            if (closestBox == null)
            {
                capturedPosition = default;
                captureSource = string.Empty;
                return false;
            }

            capturedPosition = closestBounds.center;
            captureSource = $"nearby SFP box '{closestBox.name}' #{closestBox.gameObject.GetInstanceID()}";
            return true;
        }

        private static void AddCaptureCandidate(List<(Vector3 Position, string Label)> candidates, Vector3 position, string label)
        {
            foreach (var candidate in candidates)
            {
                if ((candidate.Position - position).sqrMagnitude <= 0.0001f)
                {
                    return;
                }
            }

            candidates.Add((position, label));
        }

        private static Vector2 GetClosestHorizontalPointOnBounds(Bounds bounds, Vector3 point)
        {
            return new Vector2(
                Mathf.Clamp(point.x, bounds.min.x, bounds.max.x),
                Mathf.Clamp(point.z, bounds.min.z, bounds.max.z));
        }

        private float GetSfpKeepZoneRadiusMeters()
        {
            return Math.Max(0.1f, config.sfpKeepZoneRadiusMeters);
        }

        private Vector3 GetSfpKeepZoneCenter()
        {
            return new Vector3(config.sfpKeepZoneCenterX, config.sfpKeepZoneCenterY, config.sfpKeepZoneCenterZ);
        }

        private void SyncConfigFromOptions()
        {
            if (!OptionsManager.Instance.Initialized)
            {
                return;
            }

            var autoCleanupCableSpoolsEnabled = OptionsManager.Instance.GetConfigOptionValue<bool>(OptionType.AutoCleanupCableSpoolsEnabled);
            var autoCleanupSFPBoxesEnabled = OptionsManager.Instance.GetConfigOptionValue<bool>(OptionType.AutoCleanupSFPBoxesEnabled);
            var sfpKeepZoneEnabled = OptionsManager.Instance.GetConfigOptionValue<bool>(OptionType.SFPKeepZoneEnabled);
            var autoCleanupIntervalMinutes = Math.Max(1, OptionsManager.Instance.GetConfigOptionValue<int>(OptionType.AutoCleanupIntervalMinutes));
            var sfpKeepZoneRadiusMeters = Math.Max(0.1f, OptionsManager.Instance.GetConfigOptionValue<float>(OptionType.SFPKeepZoneRadiusMeters));
            var cableSpoolLengthThreshold = Math.Max(0.1f, OptionsManager.Instance.GetConfigOptionValue<float>(OptionType.CableSpoolLengthThreshold));

            if (config.autoCleanupCableSpoolsEnabled == autoCleanupCableSpoolsEnabled
                && config.autoCleanupSFPBoxesEnabled == autoCleanupSFPBoxesEnabled
                && config.sfpKeepZoneEnabled == sfpKeepZoneEnabled
                && Math.Abs(config.autoCleanupIntervalMinutes - autoCleanupIntervalMinutes) < double.Epsilon
                && Math.Abs(config.sfpKeepZoneRadiusMeters - sfpKeepZoneRadiusMeters) < float.Epsilon
                && Math.Abs(config.cableSpoolLengthThreshold - cableSpoolLengthThreshold) < float.Epsilon)
            {
                return;
            }

            config.autoCleanupCableSpoolsEnabled = autoCleanupCableSpoolsEnabled;
            config.autoCleanupSFPBoxesEnabled = autoCleanupSFPBoxesEnabled;
            config.sfpKeepZoneEnabled = sfpKeepZoneEnabled;
            config.autoCleanupIntervalMinutes = autoCleanupIntervalMinutes;
            config.sfpKeepZoneRadiusMeters = sfpKeepZoneRadiusMeters;
            config.cableSpoolLengthThreshold = cableSpoolLengthThreshold;

            SaveConfig(configPath, config);
            nextAutoCleanupTime = GetCurrentTime() + GetCleanupIntervalSeconds();
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

        private Key ParseKey(string configuredKey, Key fallbackKey, Action<string> applyFallback, string keyLabel)
        {
            if (!string.IsNullOrWhiteSpace(configuredKey)
                && Enum.TryParse(configuredKey, ignoreCase: true, out Key parsedKey))
            {
                return parsedKey;
            }

            if (!string.IsNullOrWhiteSpace(configuredKey))
            {
                LoggerInstance.Warning($"Unknown {keyLabel} key '{configuredKey}' in config. Falling back to {fallbackKey}.");
            }

            applyFallback(fallbackKey.ToString());
            SaveConfig(configPath, config);
            return fallbackKey;
        }

        private bool IsAutomationEnabled()
        {
            return config.autoCleanupCableSpoolsEnabled || config.autoCleanupSFPBoxesEnabled;
        }

        private static bool WasPressedThisFrame(Keyboard keyboard, Key key)
        {
            var keyControl = keyboard[key];
            return keyControl != null && keyControl.wasPressedThisFrame;
        }

        private double GetCleanupIntervalSeconds()
        {
            return Math.Max(1d, config.autoCleanupIntervalMinutes) * 60d;
        }

        private static double GetCurrentTime()
        {
            return Time.realtimeSinceStartupAsDouble;
        }

        private static string FormatVector3(Vector3 vector)
        {
            return $"({vector.x:0.##}, {vector.y:0.##}, {vector.z:0.##})";
        }
    }
}
