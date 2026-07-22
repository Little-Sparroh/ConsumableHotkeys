using BepInEx.Configuration;
using HarmonyLib;
using Pigeon.Movement;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Sparroh.UI;
using TMPro;



public class ConsumableHotkeysMod
{
    public static ConsumableHotkeysMod Instance { get; private set; }

    private const string PersonalAccessTokenName = "Personal Access Token";
    private const string PremiumLootLicenseName = "Premium Loot License";
    private const string BootlegReplicatorName = "Bootleg Replicator";
    private const string ClearanceCertificateName = "Clearance Certificate";
    private const int ClearanceCertificateMaxUses = 5;

    private ConfigEntry<bool> enableHotkeys;
    private ConfigEntry<bool> enableHUD;
    private ConfigEntry<float> consumableHotkeysAnchorX;
    private ConfigEntry<float> consumableHotkeysAnchorY;
    private ConfigColor activeColor;
    private ConfigColor inactiveColor;

    private ConfigEntry<Key> personalAccessTokenHotkey;

    private ConfigEntry<Key> premiumLootLicenseHotkey;
    private ConfigEntry<Key> bootlegReplicatorHotkey;
    private ConfigEntry<Key> clearanceCertificateHotkey;

    private HudHandle hud;


    private Dictionary<string, ConsumableStatus> consumableStatuses;

    private readonly ConfigFile configFile;
    private readonly Harmony harmony;

    public ConsumableHotkeysMod(ConfigFile configFile, Harmony harmony)
    {
        this.configFile = configFile;
        this.harmony = harmony;

        Instance = this;

        try
        {
            SetupConfig();
            InitializeStatuses();
            SetupConfigWatcher();
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Failed to initialize ConsumableHotkeys: {ex.Message}");
        }
    }

    private void SetupConfig()
    {
        enableHotkeys = configFile.Bind("Consumables", "EnableHotkeys", true, "Enables hotkey functionality for consumables.");
        enableHUD = configFile.Bind("Consumables", "EnableHUD", true, "Enables the HUD display for consumable statuses.");

        consumableHotkeysAnchorX = configFile.Bind("HUD Positioning", "ConsumableHotkeysAnchorX", 0.2561931f, "X anchor position for ConsumableHotkeys (0-1).");
        consumableHotkeysAnchorY = configFile.Bind("HUD Positioning", "ConsumableHotkeysAnchorY", 0.9362161f, "Y anchor position for ConsumableHotkeys (0-1).");
        consumableHotkeysAnchorX.SettingChanged += OnAnchorChanged;
        consumableHotkeysAnchorY.SettingChanged += OnAnchorChanged;

        activeColor = ConfigColor.Bind(configFile, "Colors", "ActiveColor", UIColors.Shamrock,
            "Rich-text color for active consumables (hex RRGGBB or #RRGGBB).");
        inactiveColor = ConfigColor.Bind(configFile, "Colors", "InactiveColor", UIColors.Rose,
            "Rich-text color for inactive consumables (hex RRGGBB or #RRGGBB).");

        personalAccessTokenHotkey = configFile.Bind("Consumables", "PersonalAccessToken_Hotkey", Key.H, "Hotkey for Personal Access Token.");

        premiumLootLicenseHotkey = configFile.Bind("Consumables", "PremiumLootLicense_Hotkey", Key.J, "Hotkey for Premium Loot License.");
        bootlegReplicatorHotkey = configFile.Bind("Consumables", "BootlegReplicator_Hotkey", Key.K, "Hotkey for Bootleg Replicator.");
        clearanceCertificateHotkey = configFile.Bind("Consumables", "ClearanceCertificate_Hotkey", Key.L, "Hotkey for Clearance Certificate.");
    }

    private void InitializeStatuses()
    {
        consumableStatuses = new Dictionary<string, ConsumableStatus>
        {
            { PersonalAccessTokenName, new ConsumableStatus() },
            { PremiumLootLicenseName, new ConsumableStatus() },
            { BootlegReplicatorName, new ConsumableStatus() },
            { ClearanceCertificateName, new ConsumableStatus { MaxUses = ClearanceCertificateMaxUses, UsesRemaining = ClearanceCertificateMaxUses } }
        };
    }

    private void Start()
    {
        if (enableHUD.Value)
        {
            CreateHUD();
        }
        UpdateHudVisibility();
    }

    private bool IsHudAlive => HudHandle.IsValid(hud) && hud.Lines != null && hud.Lines.Length >= 4;


    public void UpdateHudVisibility()
    {
        if (!IsHudAlive)
        {
            ClearDestroyedHud();
            return;
        }
        hud.SetActive(enableHUD.Value);
    }

    private void OnAnchorChanged(object sender, EventArgs e)
    {
        if (IsHudAlive)
            hud.SetAnchor(consumableHotkeysAnchorX.Value, consumableHotkeysAnchorY.Value);
    }

    private void ClearDestroyedHud()
    {
        if (hud == null) return;
        try
        {
            if (hud.Rect != null)
                HudRepositionClient.Unregister(SparrohPlugin.PluginGUID);
        }
        catch { /* ignore */ }
        hud = null;
    }

    private void CreateHUD()
    {
        if (IsHudAlive) return;
        ClearDestroyedHud();

        if (Player.LocalPlayer == null || Player.LocalPlayer.PlayerLook == null || Player.LocalPlayer.PlayerLook.Reticle == null)
            return;

        hud = HudBuilder.Create("TicketStatusHUD")
            .ParentToReticle()
            .Anchor(consumableHotkeysAnchorX.Value, consumableHotkeysAnchorY.Value)
            .Pivot(new Vector2(0f, 1f))
            .Size(420f, 100f)
            .AddLines(4, fontSize: 16f, alignment: TextAlignmentOptions.TopLeft)
            .Build();

        if (!IsHudAlive)
            return;

        HudRepositionClient.Register(
            SparrohPlugin.PluginGUID,
            "Consumable Hotkeys",
            hud.Rect,
            consumableHotkeysAnchorX,
            consumableHotkeysAnchorY);

        UpdateHudVisibility();
    }



    public void Update()
    {
        try
        {
            if (enableHotkeys != null && enableHotkeys.Value)
                CheckHotkeys();

            if (enableHUD == null || !enableHUD.Value)
            {
                if (IsHudAlive)
                    hud.SetActive(false);
                return;
            }

            if (hud != null && !IsHudAlive)
                ClearDestroyedHud();

            if (Player.LocalPlayer == null || Player.LocalPlayer.PlayerLook == null || Player.LocalPlayer.PlayerLook.Reticle == null)
                return;

            if (!IsHudAlive)
            {
                CreateHUD();
                return;
            }

            if (consumableStatuses == null) return;
            UpdateHUDText();
        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Error in ConsumableHotkeys.Update(): {ex.Message}");
        }
    }

    private void UpdateHUDText()
    {
        if (!IsHudAlive || consumableStatuses == null) return;

        int i = 0;
        foreach (var kvp in consumableStatuses)
        {
            if (i >= hud.Lines.Length) break;

            string status;
            Color color;

            if (kvp.Key.Contains("Clearance Certificate"))
            {
                int remainingUses = PlayerData.Instance.GetFlag("dur_drops");
                status = remainingUses == 0 ? "Inactive" : $"{remainingUses}/{kvp.Value.MaxUses} Uses";
                color = remainingUses > 0 ? activeColor.Value : inactiveColor.Value;
            }
            else
            {
                status = kvp.Value.IsActive ? "Active" : "Inactive";
                color = kvp.Value.IsActive ? activeColor.Value : inactiveColor.Value;
            }


            int count = GetCurrentItemCount(kvp.Key);
            // Short labels for HUD
            string shortName = kvp.Key
                .Replace("Personal Access Token", "PAT")
                .Replace("Premium Loot License", "PLL")
                .Replace("Bootleg Replicator", "Replicator")
                .Replace("Clearance Certificate", "Clearance");

            hud.Lines[i].Text = RichText.Labeled(shortName, $"{status} ({count})", color);
            i++;
        }
        for (; i < hud.Lines.Length; i++)
            hud.Lines[i].Text = "";
    }


    private int GetCurrentItemCount(string itemName)
    {
        foreach (var resource in Global.Instance.PlayerResources)
        {
            if (resource.Name == itemName)
            {
                return PlayerData.Instance.GetResource(resource);
            }
        }
        return 0;
    }

    private void CheckHotkeys()
    {
        if (!enableHotkeys.Value) return;
        if (ShouldBlockHotkeys()) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        CheckHotkey(keyboard, personalAccessTokenHotkey.Value, PersonalAccessTokenName);
        CheckHotkey(keyboard, premiumLootLicenseHotkey.Value, PremiumLootLicenseName);
        CheckHotkey(keyboard, bootlegReplicatorHotkey.Value, BootlegReplicatorName);
        CheckHotkey(keyboard, clearanceCertificateHotkey.Value, ClearanceCertificateName);
    }

    /// <summary>
    /// Suppress consumable hotkeys while chat, vanilla menus/windows, or known mod UIs own input.
    /// </summary>
    private static bool ShouldBlockHotkeys()
    {
        if (Player.LocalPlayer == null)
            return true;

        try
        {
            if (Menu.Instance != null && Menu.Instance.IsOpen)
                return true;

            if (PlayerInput.IsMenuEnabled)
                return true;

            if (!PlayerInput.IsPlayerEnabled)
                return true;

            if (GameManager.Instance != null &&
                GameManager.Instance.WindowSystem != null &&
                GameManager.Instance.WindowSystem.Count > 0)
                return true;
        }
        catch
        {
            // Early load / teardown races — fail open for this frame.
        }

        if (IsTextInputFocused())
            return true;

        if (ModMenuOpenDetector.IsAnyOpen())
            return true;

        return false;
    }

    private static bool IsTextInputFocused()
    {
        try
        {
            var es = EventSystem.current;
            if (es == null)
                return false;

            var selected = es.currentSelectedGameObject;
            if (selected == null)
                return false;

            var tmp = selected.GetComponent<TMP_InputField>();
            if (tmp != null && tmp.isFocused)
                return true;

            var legacy = selected.GetComponent<InputField>();
            if (legacy != null && legacy.isFocused)
                return true;
        }
        catch
        {
        }

        return false;
    }


    private void CheckHotkey(Keyboard keyboard, Key key, string consumableName)
    {
        if (keyboard[key].wasPressedThisFrame)
        {
            UseConsumable(consumableName);
        }
    }

    private void UseConsumable(string name)
    {
        TryActivateConsumableByName(name);

        UpdateConsumableStatus(name);
    }

    private void TryActivateConsumableByName(string name)
    {

        var storageWindows = UnityEngine.Object.FindObjectsOfType<StorageWindow>();
        foreach (var storageWindow in storageWindows)
        {
            if (TryActivateFromStorageWindow(storageWindow, name))
            {
                return;
            }
        }

        TryDirectActivation(name);
    }

    private bool TryActivateFromStorageWindow(StorageWindow storageWindow, string itemName)
    {
        try
        {
            var slotsField = storageWindow.GetType().GetField("slots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (slotsField == null) return false;

            var slots = slotsField.GetValue(storageWindow) as StorageSlot[];
            if (slots == null) return false;

            foreach (var slot in slots)
            {
                var item = slot.GetType().GetField("item", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(slot) as IInventoryItem;
                var playerRes = item as PlayerResource;
                if (item != null && playerRes != null && playerRes.Name == itemName && item.ItemCount > 0)
                {

                    if (item.GetPrimaryBinding(out var binding, out var label))
                    {

                        var primaryActionField = playerRes.GetType().GetField("onPrimaryAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (primaryActionField != null)
                        {
                            var unityEvent = primaryActionField.GetValue(playerRes) as UnityEngine.Events.UnityEvent;
                            if (unityEvent != null)
                            {
                                unityEvent.Invoke();
                                return true;
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            SparrohPlugin.Logger.LogError($"Error activating {itemName} from storage: {e.Message}");
        }

        return false;
    }

    private void TryDirectActivation(string itemName)
    {

        if (IsConsumableActive(itemName))
        {
            return;
        }

        foreach (var resource in Global.Instance.PlayerResources)
        {
            if (resource.Name == itemName)
            {
                if (PlayerData.Instance.GetResource(resource) <= 0)
                {
                    return;
                }


                if (PlayerData.Instance.TryRemoveResource(resource, 1))
                {

                    ActivateConsumableByFlag(itemName);

                    return;
                }
                else
                {
                    return;
                }
            }
        }

    }

    private bool IsConsumableActive(string name)
    {
        if (name.Contains("Personal Access Token"))
        {
            return PlayerData.Instance.GetFlag("pa_token") == 1;
        }
        else if (name.Contains("Bootleg Replicator"))
        {
            return PlayerData.Instance.GetFlag("r_replicator") == 1;
        }
        else if (name.Contains("Premium Loot License"))
        {
            return PlayerData.Instance.GetFlag("equip_loot") == 1;
        }
        else if (name.Contains("Clearance Certificate"))
        {
            return PlayerData.Instance.GetFlag("dur_drops") > 0;
        }

        return false;
    }

    private void ActivateConsumableByFlag(string name)
    {
        if (name.Contains("Personal Access Token"))
        {
            PlayerData.Instance.SetFlag("pa_token", 1);
        }
        else if (name.Contains("Bootleg Replicator"))
        {
            PlayerData.Instance.SetFlag("r_replicator", 1);
        }
        else if (name.Contains("Premium Loot License"))
        {
            PlayerData.Instance.SetFlag("equip_loot", 1);
        }
        else if (name.Contains("Clearance Certificate"))
        {
            PlayerData.Instance.SetFlag("dur_drops", ClearanceCertificateMaxUses);
        }
    }

    private void UpdateConsumableStatus(string name)
    {

        if (!consumableStatuses.TryGetValue(name, out var status))
            return;

        status.IsActive = IsConsumableActive(name);

        if (status.IsActive)
        {
        }
        else
        {
        }
    }

    public void UpdateConsumableStatuses()
    {

        foreach (var kvp in consumableStatuses)
        {
            kvp.Value.IsActive = IsConsumableActive(kvp.Key);
        }

        UpdateHUDText();

    }

    private void SetupConfigWatcher()
    {
        var configFilePath = configFile.ConfigFilePath;
        var configDirectory = Path.GetDirectoryName(configFilePath);

        var watcher = new FileSystemWatcher(configDirectory, Path.GetFileName(configFilePath));
        watcher.Changed += (s, e) =>
        {
            configFile.Reload();
            OnConfigReloaded();
        };
        watcher.EnableRaisingEvents = true;
    }



    private void OnConfigReloaded()
    {
        InitializeStatuses();
        UpdateHudVisibility();
    }

    public void OnDestroy()
    {
        try
        {
            HudRepositionClient.Unregister(SparrohPlugin.PluginGUID);
            if (hud != null)
            {
                if (hud.IsAlive)
                    hud.Destroy();
                hud = null;
            }

        }
        catch (Exception ex)
        {
            SparrohPlugin.Logger.LogError($"Error in ConsumableHotkeys.OnDestroy(): {ex.Message}");
        }
    }



    private class ConsumableStatus
    {
        public bool IsActive { get; set; } = false;
        public int UsesRemaining { get; set; } = -1;
        public int MaxUses { get; set; } = -1;
    }
}

/// <summary>
/// Soft-detects open state of known mod menus (no hard assembly references).
/// Retries type resolution so late-loaded plugins are still found.
/// </summary>
internal static class ModMenuOpenDetector
{
    private static readonly string[] KnownTypeNames =
    {
        "ModConfigGUI",
        "HudRepositionMode",
        "CheatMenu",
        "CheatMenuGUI",
        "CheatMenuPlus",
        "CheatMenuUI",
        "ForceModifiers",
        "ForceModifiersGUI",
        "ForceModifiersMenu",
        "ForceModifierMenu",
        "ForceModifiersUI",
    };

    private static readonly string[] OpenPropertyNames =
    {
        "IsVisible",
        "IsOpen",
        "IsActive",
        "IsHeld",
        "Visible",
        "Open",
    };

    private static readonly List<PropertyInfo> _openProps = new List<PropertyInfo>();
    private static readonly List<FieldInfo> _openFields = new List<FieldInfo>();
    private static bool _resolved;
    private static float _nextRetryTime;

    public static bool IsAnyOpen()
    {
        EnsureResolved();

        for (int i = 0; i < _openProps.Count; i++)
        {
            try
            {
                if (_openProps[i].GetValue(null) is bool b && b)
                    return true;
            }
            catch
            {
            }
        }

        for (int i = 0; i < _openFields.Count; i++)
        {
            try
            {
                if (_openFields[i].GetValue(null) is bool b && b)
                    return true;
            }
            catch
            {
            }
        }

        return false;
    }

    private static void EnsureResolved()
    {
        if (_resolved)
            return;

        // Retry periodically until something is found, or give up after a few attempts once game is running.
        if (Time.unscaledTime < _nextRetryTime)
            return;

        _nextRetryTime = Time.unscaledTime + 2f;
        _openProps.Clear();
        _openFields.Clear();

        var foundAny = false;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }
            catch
            {
                continue;
            }

            if (types == null)
                continue;

            for (int t = 0; t < types.Length; t++)
            {
                var type = types[t];
                if (type == null || !type.IsClass)
                    continue;

                if (!IsKnownModUiType(type))
                    continue;

                foreach (var propName in OpenPropertyNames)
                {
                    var prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (prop != null && prop.PropertyType == typeof(bool) && prop.CanRead)
                    {
                        _openProps.Add(prop);
                        foundAny = true;
                    }
                }

                foreach (var propName in OpenPropertyNames)
                {
                    var field = type.GetField(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        _openFields.Add(field);
                        foundAny = true;
                    }
                }
            }
        }

        // Once ModSettingsMenu (or any target) is loaded, stop retrying.
        if (foundAny)
            _resolved = true;
    }

    private static bool IsKnownModUiType(Type type)
    {
        var name = type.Name;
        for (int i = 0; i < KnownTypeNames.Length; i++)
        {
            if (string.Equals(name, KnownTypeNames[i], StringComparison.Ordinal))
                return true;
        }

        // Broader name match for third-party menus that don't use exact type names.
        if (name.IndexOf("CheatMenu", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (name.IndexOf("ForceModifier", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }
}


[HarmonyPatch]
public static class StorageSlotPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StorageSlot), "Setup")]
    public static void PostfixSetup(StorageSlot __instance, IInventoryItem item)
    {
        if (item != null)
        {
            string itemName = (item as PlayerResource)?.Name ?? "Unknown Item";
        }
    }
}

[HarmonyPatch]
public static class StorageWindowPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StorageWindow), "Refresh")]
    public static void PostfixRefresh(StorageWindow __instance)
    {
        var slots = __instance.GetType().GetField("slots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(__instance) as StorageSlot[];
        if (slots != null)
        {
            foreach (var slot in slots)
            {
                var item = slot.GetType().GetField("item", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(slot) as IInventoryItem;
                if (item != null && item.ItemCount > 0)
                {
                    string itemName = (item as PlayerResource)?.Name ?? "Unknown Item";
                }
            }
        }
    }
}

[HarmonyPatch]
public static class DebugPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerData), "GetFlag", new Type[] { typeof(string) })]
    public static void PostfixGetFlag(PlayerData __instance, string id, ref int __result)
    {
        if (id.ToLower().Contains("token") || id.ToLower().Contains("license") || id.ToLower().Contains("replicator") ||
            id.ToLower().Contains("certificate") || id.ToLower().Contains("clearance") || id.ToLower().Contains("permit") ||
            id.ToLower().Contains("document") || id.ToLower().Contains("upgrade") || id.ToLower().Contains("dur_") ||
            id.ToLower().Contains("pa_") || id.ToLower().Contains("pl_") || id.ToLower().Contains("cc_") ||
            id.ToLower().Contains("loot") || id.ToLower().Contains("premium") ||
            id.Contains("p_l") || id.Contains("c_c") || id.Contains("r_r"))
        {
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerData), "SetFlag", new Type[] { typeof(string), typeof(int) })]
    public static void PostfixSetFlag(PlayerData __instance, string id, int value)
    {
        if (id.ToLower().Contains("token") || id.ToLower().Contains("license") || id.ToLower().Contains("replicator") ||
            id.ToLower().Contains("certificate") || id.ToLower().Contains("pa_") || id.ToLower().Contains("pl_") ||
            id.ToLower().Contains("cc_") || id.ToLower().Contains("loot") || id.ToLower().Contains("premium") ||
            id.Contains("p_l") || id.Contains("c_c") || id.Contains("r_r"))
        {

            if ((id == "pa_token" || id == "r_replicator" || id == "equip_loot" || id == "dur_drops") && value == 0)
            {
                ConsumableHotkeysMod.Instance?.UpdateConsumableStatuses();
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerData), "TryRemoveResource", new Type[] { typeof(PlayerResource), typeof(int) })]
    public static void PostfixTryRemoveResource(PlayerData __instance, PlayerResource resource, int amount, bool __result)
    {
        if (__result && amount > 0 &&
            (resource.Name.Contains("Personal") || resource.Name.Contains("Premium") ||
             resource.Name.Contains("Bootleg") || resource.Name.Contains("Clearance")))
        {
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerData), "AddResource")]
    public static void PostfixAddResource(PlayerData __instance, PlayerResource resource, int amount)
    {
        if (amount != 0 &&
            (resource.Name.Contains("Personal") || resource.Name.Contains("Premium") ||
             resource.Name.Contains("Bootleg") || resource.Name.Contains("Clearance")))
        {
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MissionManager), "OnUpgradeCollected")]
    public static void PostfixOnUpgradeCollected(UpgradeInstance upgrade)
    {
        if (upgrade.Gear != null)
        {

            int currentDrops = PlayerData.Instance.GetFlag("dur_drops");
            if (currentDrops > 0)
            {
                PlayerData.Instance.SetFlag("dur_drops", currentDrops - 1);
            }
        }
    }
}
