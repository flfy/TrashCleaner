# Trash Cleaner

**Trash Cleaner** is an essential QoL mod for *Data Center*, which aims to automate the cleanup of empty SFP boxes.
This mod adds automatic and manual cleanup modes.

---

## Features

- Configurable automatic cleanup of empty SFP boxes.
- Configurable cleanup key to run cleanup whenever you want.
	- By default, the automatic cleanup interval is `5 minutes`, and the manual cleanup key is `F9`.
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

**Note:** The hotkey setting cannot be changed through the Mod Settings menu. You will have to edit `config.json`.

### Available Options

- **Enable Automatic Cleanup** `default: true`
  Enables automatic cleanup of empty SFP boxes.

- **Automatic Cleanup Interval** `default: 5 minutes`
  How often empty SFP boxes will be cleaned up.

- **Cable Spool Length Threshold** `default: 1.5 meters`
  Length threshold of cable spools to delete. Spools shorter than this will be deleted.
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
bin/Debug/net6.0/TrashCleaner.dll
```

After the build completes, the project also copies `TrashCleaner.dll` into your game's `Mods` folder:

```text
<gamepath>/Mods/TrashCleaner.dll
```
