param(
    [string]$Project = ".\src\DZServerToolkit\DZServerToolkit.csproj"
)

Write-Host "Clearing NuGet caches..."
dotnet nuget locals all --clear

Write-Host "Removing bin/obj folders..."
Get-ChildItem -Path . -Recurse -Directory -Force | Where-Object { $_.Name -in @('bin','obj') } | Remove-Item -Recurse -Force

Write-Host "Restoring project..."
dotnet restore $Project --no-cache

Write-Host "Building project..."
dotnet build $Project --no-restore
