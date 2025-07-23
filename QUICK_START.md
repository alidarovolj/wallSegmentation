# ⚡ Быстрый Запуск AR Painting

## 🎯 Исправленные Проблемы

✅ Исправлена ошибка "Value cannot be null" в Setup Wizard  
✅ Упрощено создание UI элементов  
✅ Исправлена валидация компонентов  

## 🚀 Что Делать Сейчас

### 1. **Перезапустите Setup Wizard**

1. Закройте текущее окно Setup Wizard
2. Выберите в меню: `AR Painting → Setup Wizard`
3. Нажмите `Найти ресурсы автоматически`
4. Нажмите `🚀 Настроить AR Painting сцену`

### 2. **Если Setup Wizard Все Еще Не Работает - Ручная Настройка**

#### Шаг 1: Создайте новую сцену
```
File → New Scene → Basic (Built-in)
```

#### Шаг 2: Добавьте AR компоненты
```
1. GameObject → XR → AR Session
2. GameObject → XR → XR Origin (Action-based)
3. На XR Origin добавьте компонент: ARMeshManager
```

#### Шаг 3: Добавьте основные скрипты
На AR Camera (внутри XR Origin):
- `CameraFeedCapture`
- `SurfaceHighlighter`

#### Шаг 4: Создайте Core Manager
```
1. Создайте пустой GameObject "AR Painting Core"
2. Добавьте компоненты:
   - AsyncSegmentationManager
   - PaintManager 
   - CommandManager
   - MemoryPoolManager
```

#### Шаг 5: Настройте AsyncSegmentationManager
В Inspector AsyncSegmentationManager:
1. Перетащите модель (.onnx файл) в поле `Model Asset`
2. Перетащите `ImagePreprocessor` в `Preprocessor Shader`
3. Перетащите `PostProcessShader` в `Post Process Shader`
4. Перетащите AR Camera Manager в `Ar Camera Manager`

#### Шаг 6: Создайте простой UI
```
1. GameObject → UI → Canvas
2. На Canvas добавьте компонент UIManager
3. Создайте несколько UI кнопок для выбора цветов
```

### 3. **Тестирование**

1. **Нажмите Play** ▶️
2. **Проверьте Console** - должны появиться сообщения:
   ```
   🚀 Initializing Sentis ML mode...
   ✅ Sentis model loaded
   📸 GPU-центричный захват активирован
   ```
3. **Если есть ошибки** - проверьте, что все ссылки назначены

### 4. **Билд на устройство**

#### iOS:
```
1. File → Build Settings → iOS
2. Player Settings:
   - Bundle Identifier: com.yourname.arpainting
   - Target minimum iOS: 12.0
   - Camera Usage Description: "AR painting needs camera"
3. XR Plug-in Management → ARKit ✅
4. Build and Run
```

#### Android:
```
1. File → Build Settings → Android  
2. Player Settings:
   - Package Name: com.yourname.arpainting
   - Minimum API: 24
3. XR Plug-in Management → ARCore ✅
4. Build and Run
```

## 🎮 Как Использовать

1. **Запустите приложение** на устройстве
2. **Наведите камеру** на поверхности (стены, столы)
3. **Нажмите на цветную кнопку** в палитре
4. **Нажмите на поверхность** в AR - она окрасится!
5. **Нажмите Clear** для очистки

## 🛠️ Устранение Проблем

### Модель не загружается
```
❌ Ошибка: Model Asset is null
✅ Решение: Перетащите .onnx файл из Assets/Models/ в поле Model Asset
```

### AR не запускается  
```
❌ Проблема: Черный экран
✅ Решение: Edit → Project Settings → XR Plug-in Management → включите ARKit/ARCore
```

### Сегментация не работает
```
❌ Проблема: Нет результата ML
✅ Решение: Проверьте, что Preprocessor и PostProcess шейдеры назначены
```

### UI не отвечает
```
❌ Проблема: Кнопки не работают  
✅ Решение: Убедитесь, что в сцене есть EventSystem (GameObject → UI → Event System)
```

## 📞 Поддержка

Если что-то не работает:
1. Проверьте Console на ошибки
2. Убедитесь, что все ссылки назначены в Inspector
3. Проверьте, что AR включен в Project Settings

**Система готова к работе!** 🎉 