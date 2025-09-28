# Flutter Unity Integration

Этот каталог содержит файлы для интеграции Unity проекта с Flutter.

## Структура

- `Scripts/` - Скрипты для коммуникации с Flutter
- `BuildConfig/` - Настройки сборки для Flutter
- `integration_to_existing_flutter.md` - Инструкции по интеграции в существующее Flutter приложение

## Основные компоненты

1. **UnityMessageBridge** - Основной мост для коммуникации
2. **FlutterUnityWidget** - Виджет Flutter для отображения Unity
3. **ColorPicker Integration** - Интеграция выбора цвета

## Использование

### Из Flutter в Unity

```dart
// Передача цвета в Unity
unityController.postMessage(
  'UnityMessageBridge',
  'SetPaintColorFromHex',
  '#FF5733'
);
```

### Из Unity во Flutter

```csharp
// Отправка сообщения в Flutter
FlutterUnityManager.Instance.SendMessage("onColorApplied", colorHex);
```