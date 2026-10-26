# deploy_addin.ps1 — развёртывание плагина в папку Addins Revit 2025 для локальной отладки.
# Использование: .\build\deploy_addin.ps1
# ВАЖНО: перед запуском закрой Revit (иначе файлы будут заблокированы).

$ErrorActionPreference = "Stop"

# Папка Addins Revit 2025 (per-user, не требует прав администратора).
$revitAddinsDir = "$env:APPDATA\Autodesk\Revit\Addins\2025"

# Выходная папка сборки плагина.
$buildOutput = "$PSScriptRoot\..\src\RevitAddin\bin\Debug\net8.0-windows"

Write-Host "[1/4] Сборка RevitAddin..." -ForegroundColor Cyan
dotnet build "$PSScriptRoot\..\src\RevitAddin\RevitAddin.csproj" --configuration Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "Ошибка сборки — развёртывание прервано." -ForegroundColor Red
    exit 1
}

Write-Host "[2/4] Создание папки Addins: $revitAddinsDir" -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $revitAddinsDir | Out-Null

Write-Host "[3/4] Копирование файлов (.addin + DLL)..." -ForegroundColor Cyan
Copy-Item "$buildOutput\RevitAddin.dll"        $revitAddinsDir -Force
Copy-Item "$buildOutput\RevitMcp.Shared.dll"   $revitAddinsDir -Force
Copy-Item "$PSScriptRoot\..\src\RevitAddin\RevitMcp.addin" $revitAddinsDir -Force

Write-Host "[4/4] Готово." -ForegroundColor Green
Write-Host ""
Write-Host "Файлы скопированы в: $revitAddinsDir"
Write-Host "Теперь запусти Revit 2025 — должно появиться окно 'Revit MCP Assistant'." -ForegroundColor Yellow