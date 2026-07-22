# ConsumableHotkeys

A BepInEx mod for MycoPunk that adds hotkeys and a status HUD for common consumables.

## Features

- **Hotkeys** to activate consumables without opening inventory:
  - Personal Access Token
  - Premium Loot License
  - Bootleg Replicator
  - Clearance Certificate
- **Status HUD** showing active/inactive state (or remaining uses for Clearance Certificate) and inventory counts
- **Configurable keybinds** and HUD position with config reload support
- **Context-aware hotkeys** — ignored while chat is focused, any game menu/window is open, or known mod menus are open (ModSettingsMenu, CheatMenu+, ForceModifiers)


### Default Hotkeys

| Consumable | Default Key |
|---|---|
| Personal Access Token | Y |
| Premium Loot License | H |
| Bootleg Replicator | U |
| Clearance Certificate | J |

## Getting Started

### Dependencies

* MycoPunk (base game)
* [BepInEx](https://github.com/BepInEx/BepInEx) - Version 5.4.2403 or compatible
* .NET Framework 4.8
* [HarmonyLib](https://github.com/pardeike/Harmony) (included via NuGet)

### Building/Compiling

1. Clone this repository
2. Open the solution file in Visual Studio, Rider, or your preferred C# IDE
3. Build the project in Release mode to generate the .dll file

Alternatively, use dotnet CLI:
```bash
dotnet build --configuration Release
```

### Installing

**Via Thunderstore (Recommended)**:
1. Download and install via Thunderstore Mod Manager
2. The mod will be automatically installed to the correct directory

**Manual Installation**:
1. Place the built `ConsumableHotkeys.dll` in your `<MycoPunk Directory>/BepInEx/plugins/` folder

### Executing program

The mod loads automatically through BepInEx when the game starts. Check the BepInEx console for loading confirmation messages.

## Configuration

Access mod settings through the BepInEx configuration file at `<MycoPunk Directory>/BepInEx/config/sparroh.consumablehotkeys.cfg`. Key options include:

- `EnableHotkeys` / `EnableHUD` toggles
- Hotkeys for each consumable
- HUD anchor position (`ConsumableHotkeysAnchorX` / `ConsumableHotkeysAnchorY`)

## Help

* **Mod not loading?** Verify BepInEx is installed correctly and check console logs for errors
* **Hotkeys not working?** Ensure no conflicts with other mods or game settings, that you have the consumable in inventory, and that chat/menus/mod UIs are closed

* **HUD missing?** Confirm `EnableHUD` is true and that you are in-game with a local player loaded

## Authors

- Sparroh

## License

This project is licensed under the MIT License - see the LICENSE file for details
