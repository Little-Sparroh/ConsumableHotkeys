using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.IO;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInDependency("sparroh.uilibrary")]
[MycoMod(null, ModFlags.IsClientSide)]
public class SparrohPlugin : BaseUnityPlugin

{
    public const string PluginGUID = "sparroh.consumablehotkeys";
    public const string PluginName = "ConsumableHotkeys";
    public const string PluginVersion = "1.0.1";


    internal static new ManualLogSource Logger;

    private Harmony harmony;
    private ConsumableHotkeysMod consumableHotkeys;

    private void Awake()
    {
        Logger = base.Logger;

        try
        {
            harmony = new Harmony(PluginGUID);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to create Harmony instance: {ex.Message}");
            return;
        }

        var configFile = Config;
        try
        {
            var watcher = new FileSystemWatcher(Paths.ConfigPath, "sparroh.consumablehotkeys.cfg");
            watcher.Changed += (s, e) =>
            {
                configFile.Reload();
            };
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to set up config watcher: {ex.Message}");
        }

        try
        {
            consumableHotkeys = new ConsumableHotkeysMod(configFile, harmony);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to initialize ConsumableHotkeys: {ex.Message}");
        }

        try
        {
            harmony.PatchAll();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to apply Harmony patches: {ex.Message}");
        }

        Logger.LogInfo($"{PluginName} loaded successfully.");
    }

    private void Update()
    {
        try
        {
            if (consumableHotkeys != null) consumableHotkeys.UpdateHudVisibility();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in ConsumableHotkeys.UpdateHudVisibility(): {ex.Message}");
        }

        try
        {
            if (consumableHotkeys != null) consumableHotkeys.Update();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in ConsumableHotkeys.Update(): {ex.Message}");
        }
    }

    private void OnDestroy()
    {
        try
        {
            if (consumableHotkeys != null) consumableHotkeys.OnDestroy();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in ConsumableHotkeys.OnDestroy(): {ex.Message}");
        }

        try
        {
            if (harmony != null) harmony.UnpatchSelf();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error unpatching Harmony: {ex.Message}");
        }
    }
}
