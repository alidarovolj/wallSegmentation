# Руководство по интеграции Unity AR проекта с Flutter

## Обзор

Этот проект представляет собой AR приложение Unity для сегментации и окрашивания стен, которое может быть интегрировано с Flutter для создания мобильного приложения.

## Структура проекта

```
Assets/
├── FlutterUnityIntegration/
│   ├── Scripts/
│   │   ├── FlutterUnityManager.cs      # Основной менеджер коммуникации
│   │   └── AutoSetup.cs                # Автоматическая настройка
│   ├── BuildConfig/
│   │   └── UnityFlutterBuildSettings.cs # Настройки сборки
│   ├── integration_to_existing_flutter.md # Инструкции интеграции
│   └── README.md
├── Scripts/
│   ├── UnityMessageBridge.cs           # Устаревший мост (для совместимости)
│   ├── AsyncSegmentationManager.cs     # Основной менеджер сегментации
│   ├── ColorPaletteManager.cs          # Менеджер цветовой палитры
│   └── SimpleColorPicker.cs            # Простой выбор цвета
```

## Шаг 1: Настройка Unity проекта

### 1.1 Добавление FlutterUnityManager на сцену

1. Создайте пустой GameObject на сцене
2. Переименуйте его в "FlutterUnityManager"
3. Добавьте компонент `FlutterUnityManager`
4. Настройте ссылки на `AsyncSegmentationManager` и `ColorPaletteManager`

### 1.2 Настройка сборки

1. В Unity откройте `Flutter → Build Settings`
2. Выберите целевую платформу (Android/iOS)
3. Укажите пути для Flutter проекта и экспорта
4. Нажмите "Setup Build Settings"
5. Нажмите "Build for Flutter"

## Шаг 2: Настройка Flutter проекта

### 2.1 Добавление зависимостей

Добавьте в `pubspec.yaml`:

```yaml
dependencies:
  flutter:
    sdk: flutter
  flutter_unity_widget: ^2022.2.0
  # Для расширенного выбора цвета (опционально)
  flutter_colorpicker: ^1.0.3

dev_dependencies:
  flutter_test:
    sdk: flutter
```

### 2.2 Android настройки

#### android/app/build.gradle
```gradle
android {
    compileSdkVersion 33
    
    defaultConfig {
        minSdkVersion 24  # Требуется для AR Foundation
        targetSdkVersion 33
        ndk {
            abiFilters 'arm64-v8a', 'armeabi-v7a'
        }
    }
}
```

#### android/app/src/main/AndroidManifest.xml
```xml
<uses-permission android:name="android.permission.CAMERA" />
<uses-feature android:name="android.hardware.camera.ar" android:required="true"/>
<uses-feature android:glEsVersion="0x00030000" android:required="true" />

<application>
    <meta-data android:name="unityplayer.UnityActivity" android:value="true" />
</application>
```

### 2.3 iOS настройки

#### ios/Runner/Info.plist
```xml
<key>NSCameraUsageDescription</key>
<string>This app uses the camera for AR functionality</string>
<key>io.flutter.embedded_views_preview</key>
<true/>
```

## Шаг 3: Интеграция Unity в Flutter

### 3.1 Базовая интеграция

```dart
import 'package:flutter_unity_widget/flutter_unity_widget.dart';

class ARPage extends StatefulWidget {
  @override
  _ARPageState createState() => _ARPageState();
}

class _ARPageState extends State<ARPage> {
  UnityWidgetController? _unityController;

  void onUnityCreated(UnityWidgetController controller) {
    _unityController = controller;
    
    // Слушаем сообщения от Unity
    controller.onUnityMessage.listen((message) {
      print('Unity message: ${message.id} - ${message.data}');
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: UnityWidget(
        onUnityCreated: onUnityCreated,
        useAndroidViewSurface: false,
      ),
    );
  }
}
```

### 3.2 Передача цвета в Unity

```dart
void sendColorToUnity(Color color) {
  if (_unityController != null) {
    String hexColor = '#${color.value.toRadixString(16).substring(2)}';
    
    _unityController!.postMessage(
      'FlutterUnityManager',      // GameObject name
      'SetPaintColor',            # Method name
      hexColor,                   # Parameters
    );
  }
}
```

### 3.3 Получение сообщений от Unity

```dart
void _handleUnityMessage(UnityMessage message) {
  switch (message.id) {
    case 'onUnityReady':
      print('Unity готов к работе');
      break;
    case 'onColorChanged':
      print('Цвет изменен: ${message.data}');
      break;
    case 'onError':
      print('Ошибка Unity: ${message.data}');
      break;
  }
}
```

## Шаг 4: API методы

### Unity → Flutter (FlutterUnityManager)

| Метод | Параметры | Описание |
|-------|-----------|----------|
| `SetPaintColor` | `string hexColor` | Установить цвет в HEX формате |
| `SetPaintColorRGB` | `int r, int g, int b` | Установить цвет через RGB |
| `SetVisualizationOpacity` | `float opacity` | Установить прозрачность |
| `SetShowAllClasses` | `bool showAll` | Переключить режим отображения |

### Flutter ← Unity (Сообщения от Unity)

| Сообщение | Данные | Описание |
|-----------|--------|----------|
| `onUnityReady` | `string` | Unity загружен и готов |
| `onColorChanged` | `string hexColor` | Цвет успешно изменен |
| `onError` | `string error` | Произошла ошибка |
| `onSegmentationStateChanged` | `JSON` | Изменение состояния сегментации |

## Шаг 5: Отладка

### Логи Unity
- Все сообщения помечены эмодзи для удобства поиска
- `🎨` - Операции с цветом
- `📤` - Отправка в Flutter
- `📥` - Получение от Flutter
- `❌` - Ошибки

### Логи Flutter
```dart
// Включить отладку Unity виджета
UnityWidget(
  onUnityCreated: onUnityCreated,
  useAndroidViewSurface: false,
  unityMessageListener: (message) {
    print('Unity Debug: $message');
  },
)
```

## Шаг 6: Сборка релиза

### Android
```bash
flutter build apk --release
# или
flutter build appbundle --release
```

### iOS
```bash
flutter build ios --release
```

## Устранение проблем

### Проблема: Unity не запускается на Android
**Решение:** Проверьте, что `minSdkVersion >= 24` и включены правильные архитектуры ARM.

### Проблема: Камера не работает
**Решение:** Убедитесь, что разрешения на камеру добавлены в манифест и запрошены в runtime.

### Проблема: Сообщения не передаются
**Решение:** Проверьте имена GameObject и методов, они должны точно совпадать.

### Проблема: Низкая производительность
**Решение:** Используйте `useAndroidViewSurface: false` и оптимизируйте настройки Unity.

## Примеры использования

Полные примеры Flutter кода для интеграции смотрите в файле `integration_to_existing_flutter.md`.

## Поддержка

При возникновении проблем:
1. Проверьте логи Unity и Flutter
2. Убедитесь, что все зависимости установлены
3. Проверьте версии Unity и Flutter на совместимость
4. Используйте пример кода для тестирования базовой функциональности