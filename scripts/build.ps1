$ErrorActionPreference = "Stop"

$Project = ".\src\DayZLootEditor\DayZLootEditor.csproj"

Write-Host "Restoring DayZ Loot Editor..."
dotnet restore $Project

Write-Host "Building Release..."
dotnet build $Project -c Release --no-restore

Write-Host "Publishing Windows x64 self-contained build..."
dotnet publish $Project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish\win-x64

Write-Host "Done. Open .\publish\win-x64\DayZLootEditor.exe"
