# Family Tree Builder

WPF desktop application for creating and managing family trees. The project targets `.NET 9` for Windows and uses WPF for the UI.

## What’s in this repository

- `FamilyTreeApp/` — Main WPF application (XAML + C#).
- `family tree maker.sln` — Solution file.
- `build-installer.bat` — Publishes the app and builds an MSI installer via WiX.
- `Installer/` — WiX v4 installer definitions.
- `publish/` — Output folder used by the installer build script.

## Features (from code)

- Create, open, and save `.tree` files.
- Export preview window with PNG and SVG export.
- Zoom, pan, and reset view.
- Optional grid overlay.
- Add nodes and edit node properties (name, gender, royalty, deceased marker).
- Parent/child/partner connections with multiple connection types:
	- Biological, Adopted, Step, Partner, FormerPartner, Hidden.
- Automatic layout with top‑down or left‑right alignment.
- Layout mode: fixed (auto) or free (manual positioning).
- Snap options: grid, angle, geometry (used in free layout mode).
- Toolbar customization (saved to disk).
- Groups and group colors.
- Connection style settings (color, width, dash style by connection type).
- Tree font settings (family, size, bold, italic).
- Settings and keybinds windows.

## Data model and file format

### `.tree` files

`.tree` files are JSON and are written/read by `FileService`.

Top-level structure:

- `Version`
- `Settings`
	- `Alignment` (`topdown` or `leftright`)
	- `AllowIncest`
	- `AllowThreesome`
- `Nodes`
	- `Id`, `Name`, `Gender`, `IsAlive`, `IsRoyal`, `RoyalTitle`, `GroupId`
	- `X`, `Y`
	- `BirthDate`, `DeathDate`
- `Connections`
	- `Id`, `FromNodeId`, `ToNodeId`, `ConnectionType`
- `Groups`
	- `Id`, `Name`, `Color`, `IsVisible`

### App settings on disk

The app stores settings and toolbar configuration in `%AppData%\FamilyTreeApp`:

- `settings.json` — `AppSettings` (theme, layout, snapping, colors, font, keybinds, connection styles).
- `toolbar.json` — toolbar layout and custom tools.

## Installer build

`build-installer.bat` runs three steps:

1. `dotnet publish` (Release, win-x64, self-contained) to `publish/`.
2. `wix build` on `Installer/Package.wxs` to create `FamilyTreeBuilder.msi`.
3. Prints the resulting MSI path.

The WiX installer:

- Installs the published EXE and dependencies.
- Creates Start Menu and Desktop shortcuts.
- Associates the `.tree` extension with the app.

## Dependencies

The application references:

- `Extended.Wpf.Toolkit` (5.0.0)
- `Newtonsoft.Json` (13.0.4)