# Интеграция Unity AR в существующее Flutter приложение

## Архитектура интеграции

```
Ваше Flutter приложение
├── Основной UI (экраны, навигация, настройки)
├── Бизнес-логика приложения  
├── API интеграции
└── AR Модуль (Unity)
    ├── AR камера
    ├── Сегментация стен
    └── Окрашивание маски
```

## Шаг 1: Подготовка Unity проекта

### 1.1 Экспорт Unity проекта
```bash
# В Unity проекте
1. Открыть Flutter → Build Settings
2. Выбрать платформу (Android/iOS)
3. Указать путь экспорта в папку вашего Flutter проекта
4. Нажать "Build for Flutter"
```

### 1.2 Структура экспорта
Unity проект будет экспортирован в вашу Flutter папку:
```
ваш_flutter_проект/
├── android/
│   └── unityLibrary/          # Unity для Android
├── ios/
│   └── UnityFramework/        # Unity для iOS
└── unity/                     # Unity assets (опционально)
```

## Шаг 2: Настройка существующего Flutter приложения

### 2.1 Обновление pubspec.yaml
Добавьте в ваш существующий `pubspec.yaml`:

```yaml
dependencies:
  # Ваши существующие зависимости...
  
  # Unity интеграция
  flutter_unity_widget: ^2022.2.0
  
  # Для работы с цветами (если нет)
  flutter_colorpicker: ^1.0.3
```

### 2.2 Android настройки

#### android/app/build.gradle
Добавьте к существующим настройкам:
```gradle
android {
    compileSdkVersion 33 // или выше если уже есть
    
    defaultConfig {
        minSdkVersion 24  // ВАЖНО: минимум 24 для AR
        // ваши существующие настройки...
        
        ndk {
            abiFilters 'arm64-v8a', 'armeabi-v7a'
        }
    }
}

dependencies {
    // ваши существующие зависимости...
    implementation project(':unityLibrary')
}
```

#### android/settings.gradle
Добавьте:
```gradle
include ':unityLibrary'
project(':unityLibrary').projectDir = file('./unityLibrary')
```

#### android/app/src/main/AndroidManifest.xml
Добавьте разрешения:
```xml
<!-- AR разрешения -->
<uses-permission android:name="android.permission.CAMERA" />
<uses-feature android:name="android.hardware.camera.ar" android:required="false"/>
<uses-feature android:glEsVersion="0x00030000" android:required="true" />
```

### 2.3 iOS настройки

#### ios/Runner/Info.plist
Добавьте:
```xml
<key>NSCameraUsageDescription</key>
<string>Приложение использует камеру для AR функций</string>
<key>io.flutter.embedded_views_preview</key>
<true/>
```

## Шаг 3: Создание AR экрана в вашем приложении

### 3.1 Создание AR виджета
Создайте новый файл `lib/screens/ar_screen.dart`:

```dart
import 'package:flutter/material.dart';
import 'package:flutter_unity_widget/flutter_unity_widget.dart';

class ARScreen extends StatefulWidget {
  @override
  _ARScreenState createState() => _ARScreenState();
}

class _ARScreenState extends State<ARScreen> {
  UnityWidgetController? _unityController;
  Color _selectedColor = Colors.red;
  bool _isUnityReady = false;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('AR Покраска'),
        backgroundColor: _selectedColor,
        actions: [
          IconButton(
            icon: Icon(Icons.palette),
            onPressed: _showColorPicker,
          ),
        ],
      ),
      body: Column(
        children: [
          // Unity AR виджет
          Expanded(
            flex: 3,
            child: _isUnityReady 
              ? UnityWidget(
                  onUnityCreated: _onUnityCreated,
                  useAndroidViewSurface: false,
                )
              : Center(child: CircularProgressIndicator()),
          ),
          
          // Панель управления цветом
          _buildColorPanel(),
        ],
      ),
    );
  }

  Widget _buildColorPanel() {
    return Container(
      height: 120,
      padding: EdgeInsets.all(16),
      child: Column(
        children: [
          Text('Выбранный цвет:', style: TextStyle(fontSize: 16)),
          SizedBox(height: 10),
          Container(
            width: double.infinity,
            height: 50,
            decoration: BoxDecoration(
              color: _selectedColor,
              borderRadius: BorderRadius.circular(8),
              border: Border.all(color: Colors.grey),
            ),
            child: Center(
              child: Text(
                'Нажмите для изменения',
                style: TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  void _onUnityCreated(UnityWidgetController controller) {
    _unityController = controller;
    setState(() {
      _isUnityReady = true;
    });
    
    // Отправляем начальный цвет
    _sendColorToUnity(_selectedColor);
  }

  void _sendColorToUnity(Color color) {
    if (_unityController != null) {
      String hexColor = '#${color.value.toRadixString(16).substring(2)}';
      _unityController!.postMessage(
        'FlutterUnityManager',
        'SetPaintColor',
        hexColor,
      );
    }
  }

  void _showColorPicker() {
    // Интегрируйте с вашим существующим UI для выбора цвета
    // или используйте простой ColorPicker
  }
}
```

### 3.2 Добавление в навигацию
В вашем существующем роутинге добавьте:

```dart
// В main.dart или где у вас роутинг
routes: {
  // ваши существующие роуты...
  '/ar': (context) => ARScreen(),
},

// Или если используете Navigator 2.0, добавьте соответствующий роут
```

### 3.3 Добавление кнопки для перехода к AR
В нужном месте вашего приложения:

```dart
ElevatedButton(
  onPressed: () {
    Navigator.pushNamed(context, '/ar');
  },
  child: Text('Открыть AR'),
)
```

## Шаг 4: Интеграция с существующей логикой

### 4.1 Использование вашей цветовой палитры
Если у вас уже есть система выбора цветов:

```dart
class ARScreen extends StatefulWidget {
  final Color? initialColor;  // Цвет, переданный из основного приложения
  final Function(Color)? onColorSelected;  // Коллбек при выборе цвета
  
  ARScreen({this.initialColor, this.onColorSelected});
}

// В _ARScreenState
void _sendColorToUnity(Color color) {
  if (_unityController != null) {
    String hexColor = '#${color.value.toRadixString(16).substring(2)}';
    _unityController!.postMessage(
      'FlutterUnityManager',
      'SetPaintColor',
      hexColor,
    );
    
    // Уведомляем основное приложение о выборе цвета
    if (widget.onColorSelected != null) {
      widget.onColorSelected!(color);
    }
  }
}
```

### 4.2 Интеграция с API вашего приложения
```dart
class ARService {
  // Методы для работы с вашим API
  Future<List<Color>> getAvailableColors() async {
    // Получение цветов из вашего API
  }
  
  Future<void> saveColorChoice(Color color) async {
    // Сохранение выбранного цвета
  }
}
```

## Шаг 5: Тестирование интеграции

### 5.1 Проверочный список
- [ ] Unity экспортирован в папку Flutter проекта
- [ ] Flutter приложение собирается без ошибок
- [ ] AR экран открывается из основного приложения
- [ ] Unity сцена загружается внутри Flutter
- [ ] Цвета передаются из Flutter в Unity
- [ ] AR функции работают корректно

### 5.2 Отладка
```dart
// Добавьте в _onUnityCreated для отладки
void _onUnityCreated(UnityWidgetController controller) {
  _unityController = controller;
  
  // Логирование для отладки
  print('Unity AR модуль загружен');
  
  setState(() {
    _isUnityReady = true;
  });
}
```

## Шаг 6: Оптимизация производительности

### 6.1 Ленивая загрузка AR
```dart
// Загружать Unity только когда нужно
class ARScreen extends StatefulWidget {
  @override
  _ARScreenState createState() => _ARScreenState();
}

class _ARScreenState extends State<ARScreen> with WidgetsBindingObserver {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
  }
  
  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    // Управление жизненным циклом Unity
    if (state == AppLifecycleState.paused) {
      _unityController?.pause();
    } else if (state == AppLifecycleState.resumed) {
      _unityController?.resume();
    }
  }
}
```

## Преимущества такого подхода

✅ **Ваше приложение остается основным** - вся навигация, бизнес-логика в Flutter

✅ **Unity только для AR** - изолированная AR функциональность

✅ **Легкая интеграция** - минимальные изменения в существующем коде

✅ **Переиспользование UI** - используете вашу существующую систему цветов/UI

✅ **Независимая разработка** - AR и основное приложение развиваются отдельно

## Следующие шаги

1. Экспортируйте Unity проект в папку вашего Flutter приложения
2. Обновите настройки Android/iOS
3. Создайте AR экран как показано выше
4. Добавьте навигацию к AR экрану
5. Протестируйте интеграцию
6. Интегрируйте с вашей существующей логикой приложения

Нужна помощь с каким-то конкретным шагом?