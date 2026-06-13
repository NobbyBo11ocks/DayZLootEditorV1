# DayZ Loot Editor

A desktop editor for DayZ Central Economy loot files, built with Avalonia and .NET.

## Features

### Loot Editor
- Open a DayZ mission folder and auto-load `db/types.xml`
- Open any `types.xml` directly
- Search, filter, edit, validate, add, delete, and bulk-scale common DayZ loot fields
- Create a backup before save
- Save through a temp-file swap to reduce overwrite risk

### Custom CE Files
- Read registered custom CE files from `cfgeconomycore.xml`
- Create a custom folder such as `modtypes`
- Create a new custom XML file with the correct root tag
- Register the file under the correct `<ce folder="...">` block
- Validate common setup problems such as:
  - missing folder
  - missing file
  - invalid XML
  - wrong root tag
  - duplicate registration
  - unsupported CE file type

Supported CE types:
- `types` -> `<types>`
- `spawnabletypes` -> `<spawnabletypes>`
- `events` -> `<events>`
- `eventspawns` -> `<eventposdef>`
- `globals` -> `<variables>`

## Requirements

- .NET 10 SDK
- A recent Visual Studio release or the `dotnet` CLI
- Access to `nuget.org`

## Build

### Windows PowerShell

```powershell
./scripts/restore-clean.ps1
dotnet build ./DayZLootEditor.slnx
dotnet test ./DayZLootEditor.slnx
dotnet run --project ./src/DayZLootEditor/DayZLootEditor.csproj
```

### Bash

```bash
./scripts/build.sh
dotnet test ./DayZLootEditor.slnx
dotnet run --project ./src/DayZLootEditor/DayZLootEditor.csproj
```

## Safe workflow

1. Open your mission folder.
2. Use **Loot Editor** for normal `types.xml` editing.
3. Use **Custom CE Files** to create or register separate XML files.
4. Save only after validation is clean or after reviewing the reported issues.
5. Keep backups enabled when editing live server files.

## Notes

- The app now defaults to the **Loot Editor** workspace on startup.
- Switching files or mission folders prompts before discarding unsaved changes.
- Failed file loads keep the current loaded session intact.
- Fatal UI-thread exceptions are logged and the app shuts down cleanly instead of continuing in an unknown state.
