# Trash Cleaner

**Trash Cleaner** is an essential QoL mod for *Data Center*, which aims to automate the cleanup of empty SFP boxes and low cable spools.
This mod adds automatic and manual cleanup modes.

---

## Features

- Configurable automatic cleanup of empty SFP boxes.
- Configurable automatic cleanup of low cable spools, with a configurable threshold.
- Protected SFP keep zone so empty boxes placed in one chosen area are not deleted.
- Configurable cleanup key to run cleanup whenever you want. By default, the automatic cleanup interval is `5 minutes`, the manual cleanup key is `F9`, and the SFP keep-zone capture key is `F10`.
- In-game configuration menu using [DataCenter-RustBridge](https://github.com/Joniii11/DataCenter-RustBridge).
---

## Requirements

- **[MelonLoader](https://melonwiki.xyz/#/)**
- **[DataCenter-RustBridge](https://github.com/Joniii11/DataCenter-RustBridge)**
---

## Installation

1. Install **MelonLoader** for **Data Center**.
2. Install **DataCenter-RustBridge** for **Data Center**.
3. [Download]() the latest `.dll` release of **Trash Cleaner**
4. Drag and drop `TrashCleaner.dll` into your **Data Center/Mods** folder.

```
Data Center/
└── Mods/
    └── TrashCleaner.dll
```

5. Launch the game. The game will automatically generate a `TrashCleaner` folder containing a `config.json` file.
---

## In-Game Configuration

Configuration is available through the **DataCenter-RustBridge** menu.

**Note:** The hotkey settings and SFP keep-zone center cannot be changed through the Mod Settings menu. Use `config.json`, or press the keep-zone capture key in-game to store your current position as the protected SFP area.

### Available Options

- **Enable Automatic Cleanup of empty SFP boxes** `default: true`
  Enables automatic cleanup of empty SFP boxes.

- **Enable Automatic Cleanup of Cable Spools** `default: true`
  Enables automatic cleanup of cable spools based on the set threshold.

- **Enable SFP Keep Zone** `default true`
  Enables a "Keep Safe" zone for empty SFP boxes. `F10` to set the center-point of the zone first.

- **SFP Keep Zone Radius** `default 3 meters`
  Horizontal radius of the protected area around the saved center point.

- **Automatic Cleanup Interval** `default: 5 minutes`
  How often empty SFP boxes will be cleaned up.

- **Cable Spool Length Threshold** `default: 1.5 meters`
  Length threshold of cable spools to delete. Spools shorter than this will be deleted.

## SFP Keep Zone

Press `F10` while standing where you want to store empty SFP boxes. If you are standing on or immediately next to an SFP box, the keep zone snaps to that box so the stored center matches the box footprint more closely. Otherwise it saves your current world position into `TrashCleaner/config.json`.

---
## Building from Source

### Prerequisites

- .NET SDK 6.0 or newer.
- A working **Data Center** install with **MelonLoader** and **DataCenter-RustBridge** already installed.

### Setup

Set `gamepath` in `Local.Build.props` to your **Data Center** game folder containing **Data Center.exe**.

```xml
<Project>
  <PropertyGroup>
    <gamepath>/path/to/Data Center</gamepath>
  </PropertyGroup>
</Project>
```

### Build

Run:

```bash
dotnet build TrashCleaner.csproj
```

The compiled DLL is written to:

```text
bin/2.1.1/Debug/net6.0/TrashCleaner.dll
```

After the build completes, the project also copies `TrashCleaner.dll` into your game's `Mods` folder:

```text
<gamepath>/Mods/TrashCleaner.dll
```
