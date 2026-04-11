# Changelog

All notable changes to this project will be documented in this file.

The format is based on Keep a Changelog, and this project follows Semantic Versioning.

## [2.1.0] - 2026-04-10

### Added
- Added an SFP keep zone that preserves empty SFP boxes placed inside a chosen area.
- Added an `F10` capture hotkey to save the keep-zone center from the player's current position.

## [2.0.1] - 2026-04-10

### Changed
- Fixed default cable threshold (was 1.0, now 1.5)
- Enforced codestyle

## [2.0.0] - 2026-04-10

### Changed

- Renamed the mod from `SFPBoxCleaner` to `TrashCleaner`.
- Renamed the output assembly to `TrashCleaner.dll`.
- Renamed the generated config folder to `TrashCleaner`.
- Renamed the project, solution, and repository to `TrashCleaner`.
- Added support for cleaning up cable spools, and an option to set the threshold

### Build

- Restored standard local build output to `bin/Debug/net6.0/`.
- Added a post-build copy step that copies only `TrashCleaner.dll` into `<gamepath>/Mods/`.
- Disabled dependency file and debug symbol generation for game-directory copies.
- Disabled NuGet audit warnings for this project to avoid `NU1900` noise during local builds.
