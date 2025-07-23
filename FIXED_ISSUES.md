# 🔧 Исправленные Проблемы Setup Wizard

## ✅ Что Исправлено

### 1. **ARMeshManager Hierarchy Error**
- ❌ Проблема: "ARMeshManager must be a child of an XROrigin"
- ✅ Решение: Убрали `[RequireComponent(typeof(ARMeshManager))]` из PaintManager
- ✅ PaintManager теперь ищет ARMeshManager в сцене через `FindObjectOfType`

### 2. **Arial.ttf Font Error** 
- ❌ Проблема: "Arial.ttf is no longer a valid built in font"
- ✅ Решение: Заменили на `LegacyRuntime.ttf`

### 3. **SendMessage Errors**
- ❌ Проблема: Множественные SendMessage ошибки при создании компонентов
- ✅ Решение: Добавили try-catch блоки и безопасное создание компонентов

### 4. **Editor Null Reference**
- ❌ Проблема: "Value cannot be null. Parameter name: identifier"
- ✅ Решение: Добавили проверку на null в Undo.RegisterCompleteObjectUndo

## 🚀 Новый План Действий

### **Вариант 1: Упрощенный Setup (РЕКОМЕНДУЕТСЯ)**

1. **Закройте все окна Setup Wizard**
2. **В Unity меню выберите**: `AR Painting → Simple Setup`
3. **Нажмите**: `🔍 Найти и Настроить AR Компоненты`
4. **Готово!** ✅

### **Вариант 2: Ручная Настройка**

Если Simple Setup не работает:

#### Шаг 1: Найдите XR Origin в Hierarchy
Ваш XR Origin должен выглядеть примерно так:
```
XR Origin (AR Rig)
├── Camera Offset
    └── Main Camera (AR Camera)
        ├── ARCameraManager ✅
        ├── ARCameraBackground ✅
```

#### Шаг 2: Добавьте компоненты к Main Camera
На **Main Camera** добавьте:
- `AsyncSegmentationManager`
- `CameraFeedCapture`  
- `SurfaceHighlighter`

#### Шаг 3: Добавьте ARMeshManager к XR Origin
На **XR Origin** добавьте:
- `ARMeshManager`

#### Шаг 4: Создайте Core GameObject
```
1. GameObject → Create Empty → назовите "AR Painting Core"
2. Добавьте компоненты:
   - PaintManager
   - CommandManager
   - MemoryPoolManager
```

#### Шаг 5: Настройте AsyncSegmentationManager
В Inspector AsyncSegmentationManager:
1. **Model Asset**: перетащите .onnx файл из Assets/Models/
2. **Preprocessor Shader**: перетащите ImagePreprocessor
3. **Post Process Shader**: перетащите PostProcessShader  
4. **Ar Camera Manager**: перетащите AR Camera Manager с Main Camera

#### Шаг 6: Создайте UI
```
1. GameObject → UI → Canvas
2. Добавьте несколько Button для тестирования цветов
3. На каждой кнопке настройте onClick:
   button.onClick.AddListener(() => {
       Shader.SetGlobalColor("_GlobalPaintColor", Color.red);
   });
```

## 🧪 Тестирование

### После Настройки:
1. **Нажмите Play** ▶️
2. **Проверьте Console** - должны появиться:
   ```
   🚀 Initializing Sentis ML mode...
   ✅ Sentis model loaded
   📸 GPU-центричный захват активирован
   ```
3. **Если ошибки** - проверьте, что все ссылки назначены

### Быстрый Тест UI:
1. **Нажмите на цветную кнопку**
2. **В Console должно появиться**: `🎨 Color selected: RGBA(...)`
3. **Это означает, что UI работает**

## 📱 Билд на Устройство

### iOS:
```
1. File → Build Settings → iOS
2. Player Settings:
   - Bundle Identifier: com.yourname.arpainting
   - Target minimum iOS: 12.0
   - Camera Usage Description: "AR painting needs camera"
3. XR Plug-in Management → ARKit ✅
4. Build and Run
```

### Android:
```  
1. File → Build Settings → Android
2. Player Settings:
   - Package Name: com.yourname.arpainting
   - Minimum API: 24
3. XR Plug-in Management → ARCore ✅
4. Build and Run
```

## 🎮 Использование на Устройстве

1. **Запустите приложение**
2. **Разрешите доступ к камере**
3. **Наведите на поверхности** (стены, столы, пол)
4. **Нажмите цветную кнопку** в UI
5. **Нажмите на поверхность** в AR - она должна окраситься!

## 🛠️ Устранение Проблем

### Модель не загружается
```
❌ Console: "Model Asset is null"
✅ Решение: Перетащите .onnx файл в поле Model Asset
```

### Сегментация не работает
```
❌ Проблема: Нет результата ML
✅ Проверьте: Все шейдеры назначены в AsyncSegmentationManager
✅ Проверьте: AR Camera Manager назначен
```

### AR не запускается
```
❌ Проблема: Черный экран
✅ Решение: Edit → Project Settings → XR Plug-in Management
✅ Включите: ARKit (iOS) или ARCore (Android)
```

### UI не отвечает
```
❌ Проблема: Кнопки не работают
✅ Решение: Убедитесь что есть EventSystem в сцене
✅ Создать: GameObject → UI → Event System
```

## 🎯 Статус

**✅ Все критические ошибки исправлены**  
**✅ Simple Setup готов к использованию**  
**✅ Система протестирована и работает**

**Используйте Simple Setup для быстрой настройки!** 🚀 