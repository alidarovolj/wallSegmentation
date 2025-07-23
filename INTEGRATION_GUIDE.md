# 🚀 Руководство по Интеграции AR Painting System

## Быстрый Старт

### Вариант 1: Unity Editor Wizard (Рекомендуется)

1. **Откройте Unity Editor**
2. **Выберите меню**: `AR Painting → Setup Wizard`
3. **Настройте параметры** в открывшемся окне
4. **Нажмите**: `🚀 Настроить AR Painting сцену`
5. **Готово!** ✅

### Вариант 2: Ручная настройка

Следуйте пошаговому руководству ниже для детального понимания процесса.

---

## 📋 Пошаговая Интеграция

### Шаг 1: Проверка Требований

**Unity Версия**: 2022.3 LTS или новее  
**Packages Required**:
- AR Foundation 5.0+
- Unity Sentis 1.3+
- TextMeshPro

**Проверить пакеты**:
```
Window → Package Manager → Search: "AR Foundation", "Sentis"
```

### Шаг 2: Подготовка Ресурсов

**Необходимые файлы:**
- ✅ `model.onnx` или аналогичная модель сегментации
- ✅ `ImagePreprocessor.compute`
- ✅ `PostProcessShader.compute`

**Автопоиск ресурсов**:
Wizard автоматически найдет файлы по названиям в проекте.

### Шаг 3: Настройка AR Foundation

#### Автоматически (через Wizard):
Wizard создаст все необходимые компоненты автоматически.

#### Вручную:
```csharp
// 1. Создать AR Session
GameObject arSession = new GameObject("AR Session");
arSession.AddComponent<ARSession>();

// 2. Создать AR Session Origin
GameObject sessionOrigin = new GameObject("AR Session Origin");
ARSessionOrigin origin = sessionOrigin.AddComponent<ARSessionOrigin>();

// 3. Настроить AR Camera
GameObject arCamera = new GameObject("AR Camera");
Camera camera = arCamera.AddComponent<Camera>();
arCamera.AddComponent<ARCameraManager>();
arCamera.AddComponent<ARCameraBackground>();

// 4. Добавить AR Mesh Manager
sessionOrigin.AddComponent<ARMeshManager>();
```

### Шаг 4: Настройка Core Компонентов

**Создание основных систем:**

```csharp
// 1. Core GameObject
GameObject coreGO = new GameObject("AR Painting Core");

// 2. Основные компоненты
AsyncSegmentationManager segManager = coreGO.AddComponent<AsyncSegmentationManager>();
PaintManager paintManager = coreGO.AddComponent<PaintManager>();
CommandManager commandManager = coreGO.AddComponent<CommandManager>();
MemoryPoolManager memoryPool = coreGO.AddComponent<MemoryPoolManager>();

// 3. Camera компоненты
arCamera.AddComponent<CameraFeedCapture>();
arCamera.AddComponent<SurfaceHighlighter>();
```

### Шаг 5: Настройка UI

**Создание пользовательского интерфейса:**

```csharp
// 1. Главный Canvas
GameObject canvasGO = new GameObject("AR Painting UI");
Canvas canvas = canvasGO.AddComponent<Canvas>();
canvas.renderMode = RenderMode.ScreenSpaceOverlay;

// 2. UI Manager
UIManager uiManager = canvasGO.AddComponent<UIManager>();

// 3. UI Элементы создаются автоматически через ARPaintingIntegrator
```

### Шаг 6: Связывание Компонентов

**Настройка ссылок между компонентами:**

```csharp
// AsyncSegmentationManager
segManager.arCameraManager = FindObjectOfType<ARCameraManager>();
segManager.paintManager = paintManager;
segManager.modelAsset = your_model_asset;
segManager.preprocessorShader = preprocessor_shader;
segManager.postProcessShader = postprocess_shader;

// UIManager
uiManager.paintManager = paintManager;
uiManager.segmentationManager = segManager;
uiManager.commandManager = commandManager;

// SurfaceHighlighter
surfaceHighlighter.segmentationManager = segManager;
```

---

## ⚙️ Конфигурация для Различных Устройств

### High-End Устройства (iPhone 13+, Samsung S22+)
```csharp
// AsyncSegmentationManager settings
maxConcurrentInferences = 3;
overrideResolution = new Vector2Int(512, 512);
inferenceInterval = 0.05f;
enableAsyncGPUReadback = true;
```

### Mid-Range Устройства
```csharp
maxConcurrentInferences = 2;
overrideResolution = new Vector2Int(384, 384);
inferenceInterval = 0.1f;
enableAsyncGPUReadback = true;
```

### Low-End Устройства
```csharp
maxConcurrentInferences = 1;
overrideResolution = new Vector2Int(256, 256);
inferenceInterval = 0.2f;
enableAsyncGPUReadback = false;
```

---

## 🎨 Настройка UI Элементов

### Палитра Цветов

```csharp
// Предустановленные цвета
Color[] colors = {
    new Color(0.8f, 0.2f, 0.2f), // Красный
    new Color(0.2f, 0.8f, 0.2f), // Зеленый
    new Color(0.2f, 0.2f, 0.8f), // Синий
    new Color(0.8f, 0.8f, 0.2f), // Желтый
    // ... больше цветов
};

// Создание кнопок цветов
foreach (var color in colors)
{
    CreateColorButton(color, palettePanel.transform);
}
```

### Режимы Смешивания

```csharp
// Dropdown для режимов
dropdown.options.Clear();
dropdown.options.Add(new TMP_Dropdown.OptionData("Normal"));
dropdown.options.Add(new TMP_Dropdown.OptionData("Multiply"));
dropdown.options.Add(new TMP_Dropdown.OptionData("Overlay"));
dropdown.options.Add(new TMP_Dropdown.OptionData("Soft Light"));
```

---

## 📊 Мониторинг Производительности

### Базовая Настройка

```csharp
// Performance Monitor
GameObject perfGO = new GameObject("Performance Monitor");
PerformanceMonitor perfMonitor = perfGO.AddComponent<PerformanceMonitor>();

// Связывание с UI элементами
perfMonitor.fpsText = fpsTextComponent;
perfMonitor.inferenceTimeText = mlTextComponent;
perfMonitor.memoryText = memoryTextComponent;
perfMonitor.segmentationManager = asyncSegmentationManager;
```

### Автоматическая Настройка Качества

```csharp
// Вызывать каждые 5 секунд
InvokeRepeating("AutoAdjustQuality", 5f, 5f);

void AutoAdjustQuality()
{
    if (performanceMonitor != null)
    {
        performanceMonitor.AutoAdjustQuality();
    }
}
```

---

## 🧪 Тестирование Системы

### Быстрый Тест

```csharp
// 1. Добавить ARPaintingTester к GameObject
ARPaintingTester tester = gameObject.AddComponent<ARPaintingTester>();

// 2. Настроить ссылки
tester.segmentationManager = asyncSegmentationManager;
tester.performanceMonitor = performanceMonitor;
tester.memoryPoolManager = MemoryPoolManager.Instance;

// 3. Запустить тест
tester.StartAllTests();
```

### Ручное Тестирование

1. **Запуск сцены**: Play Mode в Unity
2. **Проверка AR**: Камера должна показывать видеопоток
3. **Тест сегментации**: Изображение должно обрабатываться
4. **Тест покраски**: Нажатие должно активировать покраску
5. **Проверка UI**: Палитра цветов и кнопки должны работать

---

## 🚨 Troubleshooting

### Распространенные Проблемы

#### 1. Модель не загружается
```
❌ Ошибка: ModelAsset не назначен
✅ Решение: Назначить модель в AsyncSegmentationManager
```

#### 2. Сегментация не работает
```
❌ Проблема: Результат сегментации пустой
✅ Проверить: ARCameraManager подключен и активен
✅ Проверить: Shaders правильно назначены
```

#### 3. Низкая производительность
```
❌ FPS < 20
✅ Снизить: overrideResolution до 256x256
✅ Увеличить: inferenceInterval до 0.2f
✅ Отключить: enableAsyncGPUReadback = false
```

#### 4. UI не отвечает
```
❌ Кнопки не работают
✅ Проверить: Canvas имеет GraphicRaycaster
✅ Проверить: EventSystem присутствует в сцене
✅ Проверить: UIManager правильно связан
```

#### 5. Memory Leaks
```
❌ Память растет со временем
✅ Проверить: MemoryPoolManager инициализирован
✅ Включить: returnTextureToPool() вызывается
✅ Активировать: ForceGarbageCollection() периодически
```

---

## 🔧 Дополнительная Настройка

### Кастомные Шейдеры

Если нужны специальные эффекты покраски:

```hlsl
// В SurfacePaintShader.shader
Shader "Custom/MyPaintShader"
{
    Properties
    {
        _PaintColor ("Paint Color", Color) = (1,0,0,1)
        _BlendMode ("Blend Mode", Int) = 0
        _CustomEffect ("Custom Effect", Float) = 1.0
    }
    // ... rest of shader
}
```

### Пользовательские Материалы

```csharp
// Создание материала программно
Material customPaintMaterial = new Material(Shader.Find("Custom/MyPaintShader"));
customPaintMaterial.SetColor("_PaintColor", Color.red);
customPaintMaterial.SetFloat("_CustomEffect", 2.0f);

// Назначение в PaintManager
paintManager.paintMaterial = customPaintMaterial;
```

---

## 📱 Развертывание на Устройствах

### iOS Build Settings

```
Player Settings:
- Target Device: iPhone/iPad
- Minimum iOS Version: 12.0+
- Architecture: ARM64
- Camera Usage Description: "AR painting needs camera access"

XCode Project Settings:
- Enable ARKit
- Add Camera permission in Info.plist
```

### Android Build Settings

```
Player Settings:
- Target Device: Android Phone/Tablet
- Minimum API Level: 24 (Android 7.0)
- Architecture: ARM64
- Camera Permission: Required

Manifest Additions:
- ARCore support
- Camera permission
```

---

## ✅ Финальная Проверка

### Чек-лист перед релизом:

- [ ] ✅ AR Foundation корректно инициализируется
- [ ] ✅ AsyncSegmentationManager обрабатывает кадры
- [ ] ✅ UI отвечает на пользовательский ввод
- [ ] ✅ Производительность > 25 FPS на целевых устройствах
- [ ] ✅ Memory leaks отсутствуют после 10+ минут работы
- [ ] ✅ Undo/Redo работает корректно
- [ ] ✅ Режимы смешивания применяются правильно
- [ ] ✅ Сцена корректно очищается при выходе
- [ ] ✅ Все компоненты правильно связаны
- [ ] ✅ Performance Monitor показывает корректные метрики

---

## 🎯 Готово к Production!

Поздравляем! 🎉 Ваша AR Painting система готова к использованию.

**Следующие шаги:**
1. **Билд на устройство** для реального тестирования
2. **Стресс-тестирование** в течение 30+ минут
3. **Тестирование на разных устройствах**
4. **Финальная оптимизация** на основе результатов
5. **Публикация** в App Store/Google Play

**Поддержка:**
- 📖 `IMPLEMENTATION_GUIDE.md` - детальное руководство
- 📊 `PERFORMANCE_OPTIMIZATION_GUIDE.md` - оптимизация производительности
- 🧪 `ARPaintingTester` - автоматическое тестирование
- 🎛️ `PerformanceMonitor` - мониторинг в реальном времени

*Система полностью готова к коммерческому использованию!* ✨ 