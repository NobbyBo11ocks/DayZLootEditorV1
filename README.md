# DayZ Loot Editor

A mission-first desktop editor for DayZ Central Economy loot files.

DayZ Loot Editor is built with **.NET** and **Avalonia UI** to make `types.xml` editing safer, clearer, and easier for DayZ server owners. It focuses on practical server workflows: open a mission folder, review loot values, validate real issues, preview changes, back up files, and save clean XML without damaging modded class names.

> Created by **NobbyBollocks**.

---

## Features

### Loot Editor

- Open a full DayZ mission folder and automatically load `db/types.xml`.
- Open a standalone `types.xml` file directly.
- Search and filter by item name, category, usage, value, and tag.
- Edit common DayZ loot fields:
  - `name`
  - `nominal`
  - `min`
  - `lifetime`
  - `restock`
  - `quantmin`
  - `quantmax`
  - `cost`
  - `category`
  - `usage`
  - `value`
  - `tag`
  - count flags
- Add and delete loot entries.
- Undo and redo editor changes.
- Adjust visible spawn counts by percentage.
- Preview save differences before writing to disk.
- Validate the file and show real issues in the Validation Feed.
- Auto-backup before saving.
- Save a copy to another location.
- Unload the current loot file without closing the mission session.
- Reopen recent mission folders and recent `types.xml` files.

### Custom CE Manager

The Custom CE Manager helps keep modded Central Economy files separate from the vanilla mission files.

It can:

- Read custom CE file registrations from `cfgeconomycore.xml`.
- Create a custom CE folder such as `modtypes`.
- Create a new CE XML file with the correct root element.
- Register custom files under the correct `<ce folder="...">` block.
- Open custom `type="types"` files back in the Loot Editor.
- Preview repair changes before applying them.
- Repair incorrect XML root elements.
- Unregister stale custom CE entries.
- Optionally delete a custom XML file when unregistering it.

Supported CE file types:

| CE type | Expected XML root |
| --- | --- |
| `types` | `<types>` |
| `spawnabletypes` | `<spawnabletypes>` |
| `events` | `<events>` |
| `eventspawns` | `<eventposdef>` |
| `globals` | `<variables>` |

### Validation

The app is designed to avoid fake errors on real modded servers.

It validates practical setup problems such as:

- Missing files or folders.
- Broken XML.
- Wrong XML root tags.
- Duplicate Custom CE registrations.
- Unsupported CE file types.
- Invalid loot values, such as `min` being greater than `nominal`.

It intentionally does **not** fail a file just because a class name is modded or unknown.

---

## Screenshots

Add screenshots here when you publish the repo.

```md
![Loot Editor](docs/screenshots/loot-editor.png)
![Custom CE Manager](docs/screenshots/custom-ce-manager.png)
```

---

## Safe server workflow

1. Make a backup of your mission folder.
2. Open the mission folder in DayZ Loot Editor.
3. Review or edit `db/types.xml` in the Loot Editor.
4. Use Custom CE Manager for separate modded CE files.
5. Click **Preview Diff** before saving.
6. Click **Validate** and fix any real issues.
7. Save only when the file looks correct.
8. Test the changed mission on a local or private server before using it live.

---

## Requirements

- .NET 10 SDK.
- NuGet package restore access.
- Windows, Linux, or another desktop platform supported by Avalonia.
- Optional: Visual Studio, Rider, or VS Code for development.

The project targets:

```xml
<TargetFramework>net10.0</TargetFramework>
```

---

## Build and run

From the repository root:

```bash
dotnet restore
dotnet run --project ./src/DayZLootEditor/DayZLootEditor.csproj
```

### Windows helper script

```powershell
.\scripts\restore-clean.ps1
dotnet run --project ".\src\DayZLootEditor\DayZLootEditor.csproj"
```

If PowerShell blocks the script:

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\restore-clean.ps1"
```

### Build a Windows release

```powershell
.\scripts\build.ps1
```

The published build is created in:

```text
publish\win-x64\
```

### Build a Linux release

```bash
chmod +x ./scripts/build.sh
./scripts/build.sh
```

The published build is created in:

```text
publish/linux-x64/
```

---

## Run tests

```bash
dotnet test
```

The test project covers key services and editor workflows, including:

- Backup creation.
- Crash logging.
- Custom CE creation, registration, repair, unregistering, and deletion.
- Recent files and mission folders.
- Save diff generation.
- `types.xml` save behaviour.
- Validation rules.
- ViewModel workflow state.

---

## Project structure

```text
DayZLootEditor/
├─ src/
│  └─ DayZLootEditor/
│     ├─ Models/        # Loot, Custom CE, diff, and validation models
│     ├─ Services/      # XML loading/saving, validation, backups, recent files, CE tools
│     ├─ Styles/        # DayZ-themed Avalonia styling
│     ├─ ViewModels/    # Editor state, commands, filtering, saving, validation
│     └─ Views/         # Avalonia UI screens
├─ tests/
│  └─ DayZLootEditor.Tests/
├─ scripts/
│  ├─ build.ps1
│  ├─ build.sh
│  └─ restore-clean.ps1
├─ Directory.Build.props
├─ global.json
└─ README.md
```

---

## GitHub notes

The repo should keep source files, tests, scripts, and documentation.

Do **not** commit generated folders such as:

- `.vs/`
- `bin/`
- `obj/`
- `publish/`
- `DayZLootForgeBackups/`

These are already covered by the included `.gitignore`.

---

## Roadmap ideas

Possible future improvements:

- App icon and branding polish.
- Better beginner help for new server owners.
- More validation rules for common DayZ CE mistakes.
- Exportable reports for changed loot values.
- Safer comparison tools for before-and-after mission changes.

---

## Disclaimer

DayZ Loot Editor is a community tool and is not affiliated with or endorsed by Bohemia Interactive.

Always back up your mission files before saving changes.
