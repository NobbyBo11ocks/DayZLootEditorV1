# DayZLootEditor

> A clean desktop editor for **DayZ Central Economy** files — built for faster edits, safer saves, and less guesswork.

DayZLootEditor helps you manage `types.xml` and related custom CE files without fighting raw XML by hand. It is built with **Avalonia** and **.NET**, runs as a desktop app, and focuses on safe file handling, validation, and a smoother DayZ server workflow.

## Why use it?

Editing DayZ loot files manually is easy to mess up. This tool is built to make common tasks faster and safer:

- open a mission folder and auto-load `db/types.xml`
- open any `types.xml` directly
- search, filter, edit, validate, add, delete, and bulk-scale loot entries
- create backups before save
- use temp-file swap saving to reduce overwrite risk
- manage custom CE files from `cfgeconomycore.xml`
- create and register supported CE XML files with the correct root tags

---

## Features

### Loot Editor
- Open a DayZ mission folder and auto-load `db/types.xml`
- Open any `types.xml` directly
- Search entries by name
- Filter and review loot definitions quickly
- Edit common DayZ loot fields
- Add and delete entries
- Bulk-scale values
- Preview save differences
- Validate for common data issues before saving
- Save safely with backup + temp-file swap

### Custom CE Files
- Read registered custom CE files from `cfgeconomycore.xml`
- Create a custom folder such as `modtypes`
- Create a new CE XML file with the correct root tag
- Register files under the correct `<ce folder="...">` block
- Detect and report common setup problems:
  - missing folder
  - missing file
  - invalid XML
  - wrong root tag
  - duplicate registration
  - unsupported CE file type

### Supported custom CE root types
| CE type | Root tag |
|---|---|
| `types` | `<types>` |
| `spawnabletypes` | `<spawnabletypes>` |
| `events` | `<events>` |
| `eventspawns` | `<eventposdef>` |
| `globals` | `<variables>` |

---

## Screens / Workflow

Typical flow:

1. Open your mission folder.
2. Let the app load `db/types.xml`.
3. Search or filter the item you want.
4. Edit values and review validation warnings.
5. Save changes.
6. Use **Custom CE Files** for separate XML files registered in `cfgeconomycore.xml`.

---

## Getting started

### Requirements
- Windows
- .NET 10 SDK for development
- Visual Studio 2022/2026 or the `dotnet` CLI
- Access to `nuget.org`

> End users running a published release only need the packaged app build.

### Clone and build

#### PowerShell
```powershell
./scripts/restore-clean.ps1
dotnet restore ./DayZLootEditor.slnx
dotnet build ./DayZLootEditor.slnx
dotnet test ./DayZLootEditor.slnx
dotnet run --project ./src/DayZLootEditor/DayZLootEditor.csproj
```

#### Bash
```bash
./scripts/build.sh
dotnet test ./DayZLootEditor.slnx
dotnet run --project ./src/DayZLootEditor/DayZLootEditor.csproj
```

---

## Build a Windows `.exe`

### Visual Studio
1. Open the solution.
2. Set the configuration to **Release**.
3. Right-click the **DayZLootEditor** project.
4. Click **Publish**.
5. Choose **Folder**.
6. Set:
   - **Target Runtime:** `win-x64`
   - **Deployment Mode:** `Self-contained`
   - **Single file:** enabled
7. Click **Publish**.

Your published output will contain `DayZLootEditor.exe`.

### CLI
```bash
dotnet publish ./src/DayZLootEditor/DayZLootEditor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Published output is usually written to:

```text
src/DayZLootEditor/bin/Release/net10.0/win-x64/publish/
```

---

## Safe editing workflow

1. Open your mission folder.
2. Use **Loot Editor** for normal `types.xml` work.
3. Use **Custom CE Files** for external CE documents.
4. Review validation results before saving.
5. Keep backups enabled when editing live server files.
6. Test changes on a non-production server first.

---

## Stability notes

This project includes test coverage around:
- safe save behavior
- backup handling
- malformed file load handling
- recent-file workflows
- custom CE register / unregister / repair flows

Recent hardening work includes:
- backup failures no longer block a normal save
- custom CE temp files are cleaned up on failed saves
- deleting a currently open custom CE file unloads the editor state correctly
- build, restore, and test pipeline fixes

---

## Project structure

```text
src/DayZLootEditor/         Main desktop application
tests/DayZLootEditor.Tests/ Test project
scripts/                    Build and restore helper scripts
.github/workflows/          CI workflow
```

---

## Notes

- The app opens in the **Loot Editor** workspace by default.
- Switching files or mission folders prompts before discarding unsaved changes.
- Failed file loads preserve the currently loaded session where possible.
- Fatal UI-thread exceptions are logged and the app shuts down cleanly.

---

## Recommended release checklist

Before publishing a release:

- `dotnet restore`
- `dotnet build`
- `dotnet test`
- manual smoke test:
  - open `types.xml`
  - edit an item
  - save
  - save as
  - register custom CE
  - open custom CE
  - remove custom CE
  - close with unsaved changes

---

## Contributing

Bug reports, polish, and workflow improvements are welcome. If you change save logic or CE registration behavior, add or update tests with the change.

---

## License

Read Licence.txt
