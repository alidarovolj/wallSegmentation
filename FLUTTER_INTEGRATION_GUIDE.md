# 🎨 Flutter Integration Guide - Цветовая палитра для Unity AR

## 📋 Обзор

Unity подготовлен для приема команд от Flutter через `FlutterUnityManager`. Теперь нужно создать Flutter UI с цветовой палитрой для управления цветами сегментации.

## 🔧 Unity API - готовые методы

### Методы для вызова из Flutter:

#### 1. **SetClassColorFromFlutter(string message)**
Устанавливает цвет для конкретного класса
```json
{
  "classId": 0,
  "color": "#FF0000"
}
```

#### 2. **GetAvailableClassesFromFlutter(string message)**
Запрашивает список доступных классов в сцене
```dart
// Вызов
UnityManager.postMessage("AsyncSegmentationManager", "GetAvailableClassesFromFlutter", "");
```

#### 3. **ResetColorsFromFlutter(string message)**
Сбрасывает все пользовательские цвета
```dart
UnityManager.postMessage("AsyncSegmentationManager", "ResetColorsFromFlutter", "");
```

#### 4. **ShowAllClassesFromFlutter(string message)**
Переключает в режим показа всех классов
```dart
UnityManager.postMessage("AsyncSegmentationManager", "ShowAllClassesFromFlutter", "");
```

### События от Unity к Flutter:

#### 1. **onUnityReady**
Unity готов к работе
```json
{"status": "ready"}
```

#### 2. **onAvailableClasses**
Список доступных классов
```json
{
  "classes": [
    {
      "classId": 0,
      "className": "wall",
      "currentColor": "#808080"
    },
    {
      "classId": 3,
      "className": "floor", 
      "currentColor": "#FF0000"
    }
  ]
}
```

#### 3. **onClassClicked**
Пользователь кликнул на класс в Unity
```json
{
  "classId": 0,
  "className": "wall",
  "currentColor": "#808080"
}
```

#### 4. **onColorChanged**
Подтверждение изменения цвета
```json
{
  "classId": 0,
  "color": "#FF0000",
  "className": "wall"
}
```

## 🎨 Flutter Implementation Plan

### 1. **Создать модели данных**

```dart
// lib/models/unity_models.dart
class UnityClass {
  final int classId;
  final String className;
  final String currentColor;

  UnityClass({
    required this.classId,
    required this.className,
    required this.currentColor,
  });

  factory UnityClass.fromJson(Map<String, dynamic> json) {
    return UnityClass(
      classId: json['classId'],
      className: json['className'],
      currentColor: json['currentColor'],
    );
  }

  Color get color => Color(int.parse(currentColor.substring(1), radix: 16) + 0xFF000000);
}

class UnityClassListResponse {
  final List<UnityClass> classes;

  UnityClassListResponse({required this.classes});

  factory UnityClassListResponse.fromJson(Map<String, dynamic> json) {
    return UnityClassListResponse(
      classes: (json['classes'] as List)
          .map((x) => UnityClass.fromJson(x))
          .toList(),
    );
  }
}
```

### 2. **Создать Unity Manager**

```dart
// lib/services/unity_manager.dart
import 'dart:convert';
import 'package:flutter_unity_widget/flutter_unity_widget.dart';

class UnityColorManager {
  UnityWidgetController? _controller;
  
  // Callbacks
  Function(List<UnityClass>)? onClassesReceived;
  Function(UnityClass)? onClassClicked;
  Function(String)? onColorChanged;
  Function()? onUnityReady;

  void setController(UnityWidgetController controller) {
    _controller = controller;
    _setupListeners();
  }

  void _setupListeners() {
    // Настройка слушателей сообщений от Unity
  }

  // Отправить цвет для класса в Unity
  void setClassColor(int classId, Color color) {
    final message = {
      'classId': classId,
      'color': '#${color.value.toRadixString(16).substring(2).toUpperCase()}'
    };
    
    _controller?.postMessage(
      'AsyncSegmentationManager',
      'SetClassColorFromFlutter',
      jsonEncode(message),
    );
  }

  // Запросить список классов
  void requestAvailableClasses() {
    _controller?.postMessage(
      'AsyncSegmentationManager',
      'GetAvailableClassesFromFlutter',
      '',
    );
  }

  // Сбросить цвета
  void resetColors() {
    _controller?.postMessage(
      'AsyncSegmentationManager',
      'ResetColorsFromFlutter',
      '',
    );
  }

  // Показать все классы
  void showAllClasses() {
    _controller?.postMessage(
      'AsyncSegmentationManager',
      'ShowAllClassesFromFlutter',
      '',
    );
  }
}
```

### 3. **Создать Color Picker Widget**

```dart
// lib/widgets/color_palette_widget.dart
class ColorPaletteWidget extends StatefulWidget {
  final Function(Color) onColorSelected;
  final Color? currentColor;

  const ColorPaletteWidget({
    Key? key,
    required this.onColorSelected,
    this.currentColor,
  }) : super(key: key);

  @override
  _ColorPaletteWidgetState createState() => _ColorPaletteWidgetState();
}

class _ColorPaletteWidgetState extends State<ColorPaletteWidget> {
  final List<Color> predefinedColors = [
    Colors.red,
    Colors.green,
    Colors.blue,
    Colors.yellow,
    Colors.purple,
    Colors.orange,
    Colors.pink,
    Colors.teal,
    Colors.brown,
    Colors.grey,
    // Добавить больше цветов
  ];

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 100,
      child: GridView.builder(
        scrollDirection: Axis.horizontal,
        gridDelegate: SliverGridDelegateWithFixedCrossAxisCount(
          crossAxisCount: 2,
          childAspectRatio: 1,
          crossAxisSpacing: 8,
          mainAxisSpacing: 8,
        ),
        itemCount: predefinedColors.length,
        itemBuilder: (context, index) {
          final color = predefinedColors[index];
          final isSelected = widget.currentColor == color;
          
          return GestureDetector(
            onTap: () => widget.onColorSelected(color),
            child: Container(
              decoration: BoxDecoration(
                color: color,
                shape: BoxShape.circle,
                border: isSelected 
                  ? Border.all(color: Colors.white, width: 3)
                  : null,
                boxShadow: [
                  BoxShadow(
                    color: Colors.black26,
                    blurRadius: 4,
                    offset: Offset(0, 2),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}
```

### 4. **Создать Class List Widget**

```dart
// lib/widgets/class_list_widget.dart
class ClassListWidget extends StatelessWidget {
  final List<UnityClass> classes;
  final Function(UnityClass) onClassSelected;
  final UnityClass? selectedClass;

  const ClassListWidget({
    Key? key,
    required this.classes,
    required this.onClassSelected,
    this.selectedClass,
  }) : super(key: key);

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 150,
      child: ListView.builder(
        scrollDirection: Axis.horizontal,
        itemCount: classes.length,
        itemBuilder: (context, index) {
          final unityClass = classes[index];
          final isSelected = selectedClass?.classId == unityClass.classId;
          
          return Container(
            width: 120,
            margin: EdgeInsets.only(right: 12),
            child: GestureDetector(
              onTap: () => onClassSelected(unityClass),
              child: Container(
                decoration: BoxDecoration(
                  color: isSelected ? Colors.blue[100] : Colors.white,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(
                    color: isSelected ? Colors.blue : Colors.grey[300]!,
                    width: 2,
                  ),
                  boxShadow: [
                    BoxShadow(
                      color: Colors.black12,
                      blurRadius: 4,
                      offset: Offset(0, 2),
                    ),
                  ],
                ),
                padding: EdgeInsets.all(12),
                child: Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    // Цветной индикатор
                    Container(
                      width: 40,
                      height: 40,
                      decoration: BoxDecoration(
                        color: unityClass.color,
                        shape: BoxShape.circle,
                        border: Border.all(color: Colors.grey[400]!),
                      ),
                    ),
                    SizedBox(height: 8),
                    // Название класса
                    Text(
                      unityClass.className,
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.bold,
                        color: isSelected ? Colors.blue[800] : Colors.black87,
                      ),
                      textAlign: TextAlign.center,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                    // ID класса
                    Text(
                      'ID: ${unityClass.classId}',
                      style: TextStyle(
                        fontSize: 10,
                        color: Colors.grey[600],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
  }
}
```

### 5. **Главный экран палитры**

```dart
// lib/screens/color_palette_screen.dart
class ColorPaletteScreen extends StatefulWidget {
  @override
  _ColorPaletteScreenState createState() => _ColorPaletteScreenState();
}

class _ColorPaletteScreenState extends State<ColorPaletteScreen> {
  final UnityColorManager _unityManager = UnityColorManager();
  List<UnityClass> _availableClasses = [];
  UnityClass? _selectedClass;
  Color? _selectedColor;

  @override
  void initState() {
    super.initState();
    _setupUnityCallbacks();
  }

  void _setupUnityCallbacks() {
    _unityManager.onUnityReady = () {
      print('Unity готов!');
      _unityManager.requestAvailableClasses();
    };

    _unityManager.onClassesReceived = (classes) {
      setState(() {
        _availableClasses = classes;
      });
    };

    _unityManager.onClassClicked = (clickedClass) {
      setState(() {
        _selectedClass = clickedClass;
      });
    };

    _unityManager.onColorChanged = (message) {
      print('Цвет изменен: $message');
      // Обновить UI при необходимости
    };
  }

  void _onClassSelected(UnityClass unityClass) {
    setState(() {
      _selectedClass = unityClass;
    });
  }

  void _onColorSelected(Color color) {
    setState(() {
      _selectedColor = color;
    });

    if (_selectedClass != null) {
      _unityManager.setClassColor(_selectedClass!.classId, color);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text('Цветовая палитра AR'),
        actions: [
          IconButton(
            icon: Icon(Icons.refresh),
            onPressed: () => _unityManager.requestAvailableClasses(),
          ),
          IconButton(
            icon: Icon(Icons.clear_all),
            onPressed: () => _unityManager.resetColors(),
          ),
          IconButton(
            icon: Icon(Icons.visibility),
            onPressed: () => _unityManager.showAllClasses(),
          ),
        ],
      ),
      body: Column(
        children: [
          // Инструкция
          Padding(
            padding: EdgeInsets.all(16),
            child: Text(
              _selectedClass == null
                  ? 'Выберите класс для раскраски'
                  : 'Выберите цвет для: ${_selectedClass!.className}',
              style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
              textAlign: TextAlign.center,
            ),
          ),
          
          // Список классов
          Padding(
            padding: EdgeInsets.symmetric(horizontal: 16),
            child: ClassListWidget(
              classes: _availableClasses,
              onClassSelected: _onClassSelected,
              selectedClass: _selectedClass,
            ),
          ),
          
          SizedBox(height: 20),
          
          // Цветовая палитра
          if (_selectedClass != null) ...[
            Padding(
              padding: EdgeInsets.symmetric(horizontal: 16),
              child: Text(
                'Выберите цвет:',
                style: TextStyle(fontSize: 14, fontWeight: FontWeight.w500),
              ),
            ),
            SizedBox(height: 12),
            Padding(
              padding: EdgeInsets.symmetric(horizontal: 16),
              child: ColorPaletteWidget(
                onColorSelected: _onColorSelected,
                currentColor: _selectedColor,
              ),
            ),
          ],
          
          Spacer(),
          
          // Кнопки управления
          Padding(
            padding: EdgeInsets.all(16),
            child: Row(
              children: [
                Expanded(
                  child: ElevatedButton(
                    onPressed: () => _unityManager.showAllClasses(),
                    child: Text('Показать все'),
                  ),
                ),
                SizedBox(width: 12),
                Expanded(
                  child: ElevatedButton(
                    onPressed: () => _unityManager.resetColors(),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.red,
                    ),
                    child: Text('Сбросить цвета'),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
```

### 6. **Интеграция в основное приложение**

```dart
// lib/main.dart - добавить в существующий код
void _onUnityCreated(UnityWidgetController controller) {
  this.unityWidgetController = controller;
  
  // Настройка менеджера цветов
  final colorManager = Get.find<UnityColorManager>();
  colorManager.setController(controller);
  
  // Настройка слушателей сообщений от Unity
  controller.onUnityMessage.listen((message) {
    print('Unity Message: $message');
    _handleUnityMessage(message);
  });
}

void _handleUnityMessage(dynamic message) {
  // Обработка сообщений от Unity
  if (message is Map<String, dynamic>) {
    final method = message['method'];
    final data = message['data'];
    
    switch (method) {
      case 'onUnityReady':
        Get.find<UnityColorManager>().onUnityReady?.call();
        break;
      case 'onAvailableClasses':
        final response = UnityClassListResponse.fromJson(jsonDecode(data));
        Get.find<UnityColorManager>().onClassesReceived?.call(response.classes);
        break;
      case 'onClassClicked':
        final clickedClass = UnityClass.fromJson(jsonDecode(data));
        Get.find<UnityColorManager>().onClassClicked?.call(clickedClass);
        break;
      case 'onColorChanged':
        Get.find<UnityColorManager>().onColorChanged?.call(data);
        break;
    }
  }
}
```

## 🛠 Необходимые зависимости

```yaml
# pubspec.yaml
dependencies:
  flutter_unity_widget: ^2022.2.0
  get: ^4.6.5  # для state management (опционально)
```

## 🎯 Ожидаемый workflow

1. **Запуск приложения** → Unity отправляет `onUnityReady`
2. **Flutter запрашивает классы** → Unity отправляет список в `onAvailableClasses` 
3. **Пользователь выбирает класс** → Flutter показывает палитру цветов
4. **Пользователь выбирает цвет** → Flutter отправляет `SetClassColorFromFlutter`
5. **Unity применяет цвет** → Unity отправляет подтверждение `onColorChanged`
6. **Альтернативно:** Пользователь кликает в Unity → Unity отправляет `onClassClicked`

## ✅ Готово в Unity:
- ✅ Все методы приема команд от Flutter
- ✅ JSON сериализация/десериализация
- ✅ Автоматическая отправка событий во Flutter
- ✅ Интеграция с FlutterUnityManager
- ✅ Обработка hex цветов
- ✅ Автоматическое определение доступных классов

**Unity полностью готов принимать команды от Flutter палитры!** 🎨
