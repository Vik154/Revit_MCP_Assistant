# Интеграция с Cline / Cursor

## Как это работает

Cline (расширение VS Code) общается с нашим MCP-сервером через stdio (JSON-RPC 2.0). MCP-сервер подключается к Revit через Named Pipe и вызывает инструменты в главном потоке Revit (тред-сейф).

**ВАЖНО:** У разных MCP-клиентов РАЗНЫЕ места конфигов:
- Cline (расширение) → глобальный `cline_mcp_settings.json` в `globalStorage`
- VS Code (нативный MCP 1.99+) → `.vscode/mcp.json`
- Cursor → `~/.cursor/mcp.json`

## Предварительные требования

1. **Revit 2025** установлен и запущен
2. **Плагин загружен** (проверить: `.\build\start_with_cline.ps1`)
3. **.NET 8 SDK** установлен

## Подключение к Cline (главный способ)

### Шаг 1: Установить расширение Cline

[Cline](https://marketplace.visualstudio.com/items?itemName=saoudrizwan.claude-dev) из VS Code Marketplace.

### Шаг 2: Найти конфиг Cline

В PowerShell:
```powershell
$clinePath = Get-ChildItem "$env:APPDATA\Code\User\globalStorage" -Directory | 
    Where-Object { $_.Name -match "(cline|claude-dev|roo-cline)" } | 
    Select-Object -First 1
$settingsFile = Join-Path $clinePath.FullName "settings\cline_mcp_settings.json"
Write-Host "Конфиг: $settingsFile"
```

Типичный путь: `C:\Users\USER\AppData\Roaming\Code\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json`

### Шаг 3: Заменить содержимое конфига

Открыть файл и вставить:
```json
{
  "mcpServers": {
    "revit-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/Users/USER/Desktop/BIM-Assistant/Revit_MCP_Assistant/src/McpServer"
      ],
      "cwd": "C:/Users/USER/Desktop/BIM-Assistant/Revit_MCP_Assistant",
      "env": {},
      "disabled": false,
      "autoApprove": []
    }
  }
}
```

⚠️ Заменить `USER` на своего пользователя. Использовать прямые слэши `/`.

### Шаг 4: Перезагрузить окно VS Code

`Ctrl+Shift+P` → `Developer: Reload Window`

### Шаг 5: Проверить подключение

4. Открыть Cline (`Ctrl+Shift+P` → `Cline: Open Chat`)
5. В панели MCP должен появиться сервер **revit-mcp** со статусом running
6. Развернуть его — должен быть инструмент **get_revit_status**

### Шаг 6: Тест

В чате Cline:
```
Используй MCP инструмент get_revit_status сервера revit-mcp
```

Ожидаемый ответ:
```json
{
  "RevitVersion": "2025",
  "DocumentTitle": "Проект1",
  "HasDocument": true,
  "ElementCount": 3803
}
```

## Подключение к Cursor

7. Открыть настройки Cursor: `File` → `Preferences` → `Cursor Settings` → `MCP`
8. Нажать `+ Add new MCP server`
9. Заполнить (пример в `docs/cursor_mcp_config.json`)

## Troubleshooting

### Cline не видит сервер
- Перезагрузить окно VS Code (Developer: Reload Window)
- Проверить, что конфиг сохранён в ПРАВИЛЬНОМ месте (глобальный, не .vscode/)
- Логирование: `Ctrl+Shift+P` → `Cline: View Output`

### Revit не подключён
- Проверить, что Revit запущен
- **Остановить любые запущенные экземпляры MCP-сервера** в терминалах (Ctrl+C) — Named Pipe обслуживает одного клиента
- Проверить journal.log на `API_SUCCESS`

### Server start timeout
- `dotnet run` делает build (5-10 сек) — это нормально для разработки
- Для продакшена использовать `dotnet publish` + pre-built exe

## Демо для собеса

1. Открыть Revit 2025 с любым проектом
2. Открыть Cline в VS Code
3. Написать: "Сколько элементов в проекте?"
4. ИИ вызовет `get_revit_status` через MCP → получит данные → ответит

Это **bleeding edge** в BIM-разработке.
