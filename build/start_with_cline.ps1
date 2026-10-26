# start_with_cline.ps1 — проверка готовности для Cline/Cursor
# Использование: .\build\start_with_cline.ps1

$ErrorActionPreference = "Stop"

Write-Host "=== Проверка готовности для Cline/Cursor ===" -ForegroundColor Cyan
Write-Host ""

# 1. Проверка .NET SDK
Write-Host "[1/3] Проверка .NET SDK..." -ForegroundColor Cyan
try {
    $dotnetVersion = dotnet --version
    if ($dotnetVersion -like "8.*") {
        Write-Host "✅ .NET SDK $dotnetVersion установлен" -ForegroundColor Green
    } else {
        Write-Host "⚠️  Установлен .NET $dotnetVersion, но требуется 8.x" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ .NET SDK не установлен" -ForegroundColor Red
    exit 1
}
Write-Host ""

# 2. Проверка Revit
Write-Host "[2/3] Проверка Revit 2025..." -ForegroundColor Cyan
$revitProcess = Get-Process -Name "Revit" -ErrorAction SilentlyContinue
if ($revitProcess) {
    Write-Host "✅ Revit запущен (PID: $($revitProcess.Id))" -ForegroundColor Green
} else {
    Write-Host "⚠️  Revit не запущен. Запустите Revit 2025 перед использованием Cline." -ForegroundColor Yellow
}
Write-Host ""

# 3. Проверка плагина
Write-Host "[3/3] Проверка плагина..." -ForegroundColor Cyan
$journalDir = "$env:LOCALAPPDATA\Autodesk\Revit\Autodesk Revit 2025\Journals"
if (Test-Path $journalDir) {
    $latestJournal = Get-ChildItem $journalDir -Filter "journal.*.txt" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latestJournal) {
        $content = Get-Content $latestJournal.FullName -Raw
        if ($content -match "API_SUCCESS.*Revit MCP Assistant") {
            Write-Host "✅ Плагин загружен (найдено в $($latestJournal.Name))" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Плагин не найден в последнем журнале. Убедитесь, что он загружен." -ForegroundColor Yellow
        }
    } else {
        Write-Host "⚠️  Журналы не найдены" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠️  Папка журналов не найдена: $journalDir" -ForegroundColor Yellow
}
Write-Host ""

# 4. Проверка MCP-сервера
Write-Host "[4/4] Проверка MCP-сервера..." -ForegroundColor Cyan
if (Test-Path ".\.vscode\mcp.json") {
    Write-Host "✅ Конфиг .vscode/mcp.json найден" -ForegroundColor Green
} else {
    Write-Host "❌ Конфиг .vscode/mcp.json не найден" -ForegroundColor Red
}
Write-Host ""

# Итог
Write-Host "=== Готово ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Следующие шаги:" -ForegroundColor Yellow
Write-Host "1. Убедитесь, что Revit запущен и плагин загружен"
Write-Host "2. Откройте проект в VS Code: code ."
Write-Host "3. Откройте Cline: Ctrl+Shift+P → 'Cline: Open Chat'"
Write-Host "4. Напишите: 'Покажи статус Revit'"
Write-Host ""
Write-Host "Подробнее: docs/INTEGRATION_CLINE.md" -ForegroundColor Gray