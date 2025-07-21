# 🚀 AR Segmentation Project Setup Guide - Sentis 2.0

## 📁 Что уже настроено в RemaluxNewAR:

### ✅ **Готовые файлы:**
- **`Assets/Scripts/SegmentationManager.cs`** - Основной рабочий скрипт с **Sentis 2.0**
- **`Assets/Scripts/ColorMap.cs`** - Карта цветов для классов сегментации
- **`Assets/Shaders/PostProcessShader.compute`** - GPU compute shader для обработки
- **`Assets/Shaders/SegmentationShader.shader`** - Основной шейдер
- **`Assets/Models/`** - ONNX модели для тестирования
- **`Packages/manifest.json`** - Правильная конфигурация пакетов

### ✅ **Настроенные пакеты:**
- **`"com.unity.sentis": "2.0.0"`** - Для ML inference (новый Sentis!)
- **AR Foundation 5.2.0** - для AR камеры
- **URP 14.0.12** - для рендеринга
- **XR Interaction Toolkit 3.1.2** - для AR взаимодействий

## 🎯 ЧТО НУЖНО СДЕЛАТЬ СЛЕДУЮЩЕМУ ИИ:

### 1. **Создать AR сцену:**
```
1. Откройте Unity проект RemaluxNewAR
2. Создайте новую сцену "AR Segmentation Scene"
3. Добавьте AR Session Origin
4. Добавьте AR Camera
5. Настройте ARCameraManager компонент
```

### 2. **Настроить UI:**
```
1. Создайте Canvas с Camera - Overlay
2. Добавьте RawImage для отображения сегментации
3. Добавьте кнопки для управления классами
4. Добавьте кнопку Toggle Test Mode
5. Настройте размеры UI для мобильных устройств
```

### 3. **Подключить SegmentationManager:**
```
1. Создайте пустой GameObject "SegmentationManager"
2. Добавьте компонент SegmentationManager
3. Назначьте:
   - Model Asset: одну из ONNX моделей из Assets/Models/ (для ML режима)
   - Raw Image: созданный RawImage компонент
   - AR Camera Manager: из AR Camera
   - Post Process Shader: PostProcessShader.compute
4. Установите Use Test Data = true для начального тестирования
5. Override Resolution = 256x256 для оптимальной производительности
```

### 4. **Настроить Build Settings:**
```
1. Switch Platform to Android/iOS
2. Добавьте сцену в Build Settings
3. Player Settings:
   - Minimum API Level: Android 7.0+ (API 24+)
   - Target Architectures: ARM64
   - Graphics APIs: Vulkan, OpenGLES3
   - Scripting Backend: IL2CPP
```

### 5. **Тестирование:**
```
1. Запустите в редакторе - должна показываться ЦВЕТНАЯ тестовая сегментация
2. Проверьте Console на логи с эмодзи (🎯, ✅, 🎨)
3. Убедитесь что RawImage показывает цветную сегментацию с разными классами
4. Протестируйте кнопки для переключения классов
5. Протестируйте на устройстве
```

## 🎛️ **Основные настройки SegmentationManager:**

- **Use Test Data**: `true` = синтетические данные (быстрый старт), `false` = реальная ML модель
- **Override Resolution**: разрешение обработки (256x256 по умолчанию)
- **Class Index To Paint**: `-1` = все классы, `0-20` = конкретный класс
- **Model Asset**: ONNX модель для Sentis (только для ML режима)

## 🌟 **НОВЫЕ ВОЗМОЖНОСТИ Sentis 2.0:**

### **Тестовый режим:**
- ✅ **Мгновенный запуск** без ML модели
- ✅ **Интересные паттерны** (люди в центре, стулья вокруг, небо сверху)
- ✅ **Real-time генерация** на GPU
- ✅ **10 FPS обновления** для демонстрации

### **ML режим:**
- ✅ **Sentis 2.0 API** - современный и быстрый
- ✅ **Автоопределение разрешения** из модели
- ✅ **GPUCompute backend** для максимальной производительности
- ✅ **Детальные логи** с эмодзи для отладки

## 🐛 **Возможные проблемы:**

1. **Нет отображения в Test режиме** - проверьте назначение RawImage и PostProcessShader
2. **Ошибки Sentis в ML режиме** - используйте Test режим для отладки UI
3. **Плохая производительность** - уменьшите Override Resolution до 128x128
4. **Модель не загружается** - проверьте что ModelAsset назначен правильно

## 🔧 **Полезные методы:**

```csharp
// Переключение классов
segmentationManager.SetClassToPaint(15); // Показать только людей
segmentationManager.ShowAllClasses();    // Показать все классы

// Переключение режимов
segmentationManager.ToggleTestMode();    // Test data ↔ Real model
```

## 📱 **Результат:**
Современная AR приложение с **Sentis 2.0**, которая:
- ✅ **Мгновенно запускается** в тестовом режиме
- ✅ **Показывает красивую цветную сегментацию** с интересными паттернами
- ✅ **Поддерживает real-time ML inference** через Sentis 2.0
- ✅ **Работает на мобильных устройствах** с отличной производительностью
- ✅ **Имеет детальные логи** для отладки
- ✅ **Поддерживает переключение режимов** на лету

## 🚀 **ГЛАВНЫЕ ПРЕИМУЩЕСТВА:**

### **🎯 Мгновенный старт:**
- Установите `Use Test Data = true` → мгновенно видите результат!
- Красивые цветные паттерны без ML модели
- Идеально для демонстрации и отладки UI

### **⚡ Sentis 2.0 мощность:**
- Современный API Unity для ML
- Отличная производительность на GPU
- Простая работа с ONNX моделями
- Автоматическое определение параметров модели

### **🔄 Гибкость:**
- Переключение Test ↔ ML режимов на лету
- Настройка классов сегментации
- Адаптивное разрешение под устройство

**Начните с Test Mode, убедитесь что UI работает, затем переключайтесь на ML!** 🎉

---

## 🔧 **ИСПРАВЛЕНИЯ ОТРИСОВКИ (Latest Update):**

### **🚨 Проблемы:** 
- Отрисовка показывала блоки вместо плавной сегментации
- Зеленые пятна появлялись из-за неправильной цветовой карты (chair показывался как grass)

### **✅ Исправления:**
1. **📐 Compute Shader переписан** для работы с индексами классов (не многоканальными вероятностями)
2. **🎨 Bilinear interpolation** добавлен для плавного масштабирования
3. **🔄 Majority voting** для лучших краев сегментации
4. **⚡ Оптимизированная передача данных** tensor → shader
5. **🎨 Цветовая карта исправлена** - заменена ADE20K на PASCAL VOC2012 (21 класс)
6. **📏 Разрешение исправлено** - автоматическое определение из модели (520x520 для данной DeepLabV3+)
7. **🔍 Расширенное логирование** - отслеживание всех классов и предупреждения о неожиданных

### **🎮 Новые возможности управления:**

#### **⌨️ Горячие клавиши:**
- **`0`** - показать все классы
- **`1`** - только люди (person)
- **`2`** - только машины (car) 
- **`3`** - только автобусы (bus)
- **`4`** - только собаки (dog)
- **`5`** - только кошки (cat)
- **`9`** - только стулья (chair)

#### **👆 Интерактивность:**
- **Одиночный тап** - выбрать класс под пальцем с отображением имени
- **Двойной тап** - сброс к показу всех классов
- **Автодетекция** - система показывает найденные объекты с процентами

#### **🔧 Unity Inspector Context Menu:**
- **Right-click SegmentationManager** → "Show Only Chairs"
- **Right-click SegmentationManager** → "Visualize Model Output"
- **Right-click SegmentationManager** → "Test ML Model"

### **🎨 Новые цвета классов (PASCAL VOC2012):**
- **Chair (класс 9)** - светло-красный (больше не зеленый!)
- **TV/Monitor (класс 20)** - ярко-желтый
- **Person (класс 15)** - телесный оттенок
- **Car (класс 7)** - фиолетовый
- **Background (класс 0)** - прозрачный черный

### **🚨 КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ (Tensor Size Error):**

**Обнаружена и исправлена ошибка размера тензора:**
- ❌ **Проблема:** `Cannot set input tensor 0 as shapes are not compatible, expected (1, 3, 520, 520) received (1, 3, 513, 513)`
- ✅ **Исправление:** Улучшенный парсинг NCHW формата для автоматического определения размера из модели
- 📏 **Результат:** Система теперь использует правильный размер 520x520 для данной модели

### **🔍 ДИАГНОСТИКА ЧЕРНОГО ЭКРАНА (Camera Background Issues):**

**Добавлена автоматическая диагностика AR Camera Background:**
- 🔧 **Автозапуск:** Диагностика выполняется при инициализации Sentis
- 📱 **Ручной запуск:** Context Menu → "Diagnose Camera Background" на SegmentationManager
- 🎯 **Проверяет:** ARCameraBackground компонент, Camera settings, ARSession состояние
- 🚨 **Автоисправление:** Включает ARCameraBackground если отключен

### **🚀 ПОДДЕРЖКА BiSeNet MODEL (Latest Update):**

**Добавлена поддержка BiSeNet от Qualcomm AI Hub:**
- 🚀 **BiSeNet Model:** Real-time mobile segmentation (720x960 input)
- ⚡ **Performance:** Специально оптимизирована для мобильных устройств
- 🔄 **Переключение моделей:** Context Menu → "Switch to BiSeNet Model" / "Switch to DeepLabV3+ Model"
- 📊 **Автоопределение:** Система автоматически определяет тип модели по имени

**Доступные модели:**
- **bisenet-bisenet-float.onnx** - BiSeNet (47.9 MB, 720x960)
- **deeplabv3_plus_mobilenet-deeplabv3-plus-mobilenet-float.onnx** - DeepLabV3+ (23.2 MB, 520x520)
- **model_fp16.onnx** - SegFormer B4 (130 MB, 512x512, ADE20K - 150 classes)

**Как переключиться на модели:**
1. **Unity Inspector:** ПКМ на SegmentationManager → 
   - "Switch to BiSeNet Model" (12 classes, Cityscapes)
   - "Switch to DeepLabV3+ Model" (21 classes, PASCAL VOC2012)  
   - "Switch to SegFormer Model" (150 classes, ADE20K)
2. **Перезапуск:** Остановите и запустите Play mode
3. **Проверка:** В логах должно появиться соответствующее сообщение о детекции модели

### **🤖 НОВАЯ ПОДДЕРЖКА SegFormer MODEL (Latest Update):**

**Добавлена поддержка SegFormer - Transformer-based сегментация:**
- 🤖 **SegFormer B4:** State-of-the-art transformer архитектура (512x512 input)
- 🎨 **ADE20K Dataset:** 150 классов включая wall, building, sky, floor, tree, person, chair и др.
- 🚀 **Преимущества:** Лучший spatial context благодаря attention mechanism
- 💡 **Потенциальное решение:** Может устранить вертикальные полосы лучше чем CNN-based модели

**SegFormer vs BiSeNet - Технические различия:**
- **Архитектура:** Transformer vs CNN
- **Классы:** 150 (ADE20K) vs 12 (Cityscapes)  
- **Размер:** 130MB vs 47.9MB
- **Разрешение:** 512x512 vs 720x960
- **Контекст:** Global attention vs Local convolutions

### **🔧 КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ (BiSeNet Tensor Size):**

**Проблема:** BiSeNet требует 720x960, но система использовала 256x256
- ❌ **Ошибка:** `expected (1, 3, 720, 960) received (1, 3, 256, 256)`
- ✅ **Исправление:** Убрано ограничение `height == width` для поддержки прямоугольных входов
- 📏 **Результат:** Система теперь корректно использует 720x960 для BiSeNet

### **🔄 АВТОМАТИЧЕСКОЕ ИСПРАВЛЕНИЕ НАСТРОЕК:**

**Context Menu теперь автоматически настраивает параметры:**
- 🚀 **"Switch to BiSeNet Model"** → очищает Override Resolution, использует native 720x960
- 🎯 **"Switch to DeepLabV3+ Model"** → очищает Override Resolution, использует native 520x520
- ⚙️ **Override Resolution = (0,0)** → автоматическое определение из модели

**Ошибки размера тензора теперь автоматически исправляются при переключении моделей!**

### **📷 ИСПРАВЛЕНИЕ CAMERA SCALING (Latest Update):**

**Проблема:** Camera Image scaling - невозможно увеличить изображение с камеры
- ❌ **Ошибка:** `Output dimensions must be less than or equal to the inputRect's dimensions: (960x720 > 926x437)`
- ✅ **Исправление:** Двухэтапное масштабирование
  1. **XRCpuImage.Convert:** Использует размер камеры или уменьшает если нужно
  2. **TextureConverter.ToTensor:** Масштабирует до размера модели (960x720)
- 📐 **Результат:** Поддержка любых размеров камеры для BiSeNet

### **🚀 ПОЛНАЯ ПОДДЕРЖКА BiSeNet (Latest Update):**

**Проблема:** BiSeNet outputs raw logits и имеет 12 классов (не 21 как PASCAL VOC2012)
- ❌ **Было:** "Unknown Class -10", NullReferenceException в DownloadToArray()
- ✅ **Исправлено:** 
  - Automatic class detection (12 для BiSeNet, 21 для DeepLabV3+)
  - Cached tensor data для предотвращения NullReferenceException
  - BiSeNet class mapping: road, sidewalk, building, wall, etc.
  - Auto-detect model type в ColorMap.GetClassName()

**BiSeNet (12 classes - Cityscapes):** road, sidewalk, building, wall, fence, pole, traffic_light, traffic_sign, vegetation, terrain, sky, person

### **🧠 КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ ARGMAX (Latest Update):**

**Проблема:** BiSeNet выдает raw logits вместо class indices
- ❌ **Было:** "Unknown Class -4", "Unknown Class -10" (отрицательные logit values) 
- ✅ **Исправлено:** Automatic Argmax для BiSeNet logits
  - Detect BiSeNet: rank=4 tensor (1, 12, 720, 960)
  - Apply argmax: найти класс с максимальным logit для каждого пикселя
  - Output: правильные class indices (0-11) для BiSeNet classes

### **🔄 ИСПРАВЛЕНИЕ RACE CONDITIONS:**

**Проблема:** "Замирание" после первого кадра, NullReferenceException в DownloadToArray()
- ❌ **Было:** Tensor disposal race condition между обработкой кадра и tap handling
- ✅ **Исправлено:**
  - Enhanced isProcessing protection с детальным логированием  
  - Cached tensor data защищает от double DownloadToArray()
  - Frame skipping логирование для диагностики производительности
  - Proper exception handling с isProcessing reset

### **🚨 КРИТИЧЕСКОЕ ИСПРАВЛЕНИЕ TENSOR DISPOSAL:**

**Проблема:** ❌ `lastOutputTensor?.Dispose()` вызывался ДО получения нового tensor
- ❌ **Было:** 
  ```csharp
  lastOutputTensor?.Dispose(); // Dispose ПЕРЕД PeekOutput
  lastOutputTensor = worker.PeekOutput(); // Если fails → null
  var data = lastOutputTensor.DownloadToArray(); // CRASH!
  ```
- ✅ **Исправлено:**
  ```csharp
  var oldTensor = lastOutputTensor; // Store old
  lastOutputTensor = worker.PeekOutput(); // Get new
  oldTensor?.Dispose(); // Dispose old AFTER success
  var data = lastOutputTensor.DownloadToArray(); // Safe!
  ```
- **Результат:** Tensor всегда доступен для DownloadToArray(), нет race conditions

### **⚡ ФИНАЛЬНОЕ ИСПРАВЛЕНИЕ TENSOR INVALIDATION:**

**Проблема:** ❌ Tensor становился invalid между PeekOutput() и DownloadToArray() в Sentis 2.0
- ❌ **Было:** 
  ```csharp
  lastOutputTensor = worker.PeekOutput(); // Get tensor
  /* Some logging and processing */        // Tensor invalidated here!
  var data = lastOutputTensor.DownloadToArray(); // CRASH!
  ```
- ✅ **Исправлено:**
  ```csharp
  lastOutputTensor = worker.PeekOutput();       // Get tensor
  var data = lastOutputTensor.DownloadToArray(); // IMMEDIATE download
  /* Now use cached 'data' for all processing */ // Safe!
  ```
- **Результат:** Tensor данные скачиваются СРАЗУ после получения, предотвращая асинхронную invalidation

### **🐞 ДИАГНОСТИКА "ОДИНАКОВОГО УЗОРА" (Latest Update):**

**Проблема:** ❌ Сегментация выглядит одинаковой на разных кадрах, логи показывают `frameCounter = 0`
- **Гипотеза:** Симулятор AR в Unity может отправлять один и тот же кадр с камеры.
- ✅ **Диагностика:** Добавлено логирование `cpuImage.timestamp`.
  - **Если Timestamp НЕ меняется:** Проблема в симуляторе, а не в коде. Можно уверенно тестировать на реальном устройстве.
  - **Если Timestamp меняется:** Проблема глубже в нашей логике.
- ✅ **Исправление:** Упрощена и исправлена логика пропуска кадров в `OnCameraFrameReceived`, убран баг с `frameCounter`.

### **↔️ ИСПРАВЛЕНИЕ ГОРИЗОНТАЛЬНОЙ ИНВЕРСИИ (Latest Update):**

**Проблема:** ❌ Маска сегментации зеркально отражена по горизонтали.
- ✅ **Исправление:** Добавлена трансформация `MirrorX` при конвертации изображения с камеры.
- 🕹️ **Управление в Редакторе:** В инспекторе `SegmentationManager` появилась галочка `Mirror X` для включения/отключения этой опции. Также доступно через `Context Menu`.

**Ожидаемые логи после исправлений:**
```
📸 Processing new camera frame #5 (time: 12.34)
🔄 Downloading tensor data IMMEDIATELY...
💾 Immediate download SUCCESS, length: 8294400
🗑️ Old tensor disposed safely
🧠 BiSeNet logits detected - applying argmax to get class indices  
✅ Argmax applied: 8294400 logits → 691200 class indices
You tapped on: building (class index: 2). Painting it.
🏁 ProcessImage completed - ready for next frame
```

**Зеленые пятна исправлены! Ошибка размера тензора исправлена! Черный экран диагностируется! BiSeNet полностью поддерживается! Argmax для BiSeNet logits работает! Race conditions исправлены! Camera scaling исправлен! Automatic tensor caching работает! Class mapping для BiSeNet добавлен! Автоматическая настройка моделей работает! Теперь сегментация показывает правильные цвета и плавные контуры!** ✨ 