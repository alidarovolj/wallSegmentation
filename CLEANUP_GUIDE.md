# 🧹 Очистка проекта для простой системы

## ❌ Файлы для удаления

Эти файлы НЕ НУЖНЫ для простой системы с двумя цветами:

### 1. Сложная система каталогов:
```
❌ Assets/Scripts/ColorScanning/ColorMatcher.cs
❌ Assets/Scripts/Enhanced Features/DemoColorCatalog.cs  
❌ Assets/Scripts/Enhanced Features/FlutterUnityBridge.cs
❌ Assets/Scripts/UI/TestUI.cs (опционально)
```

### 2. Старая система каталогов (оставляем как есть):
```
⚠️ Assets/Scripts/ColorPaletteManager.cs (ОСТАВИТЬ - может использоваться в существующем коде)
```

## ✅ Файлы которые НУЖНЫ

Основные компоненты простой системы:

### 1. Сканирование цветов:
```
✅ Assets/Scripts/ColorScanning/ColorScanner.cs
```

### 2. Простая система цветов:
```
✅ Assets/Scripts/Enhanced Features/SimpleColorManager.cs
✅ Assets/Scripts/Enhanced Features/SimpleFlutterBridge.cs
✅ Assets/Scripts/UI/SimpleColorUI.cs
```

### 3. Сохранение проектов:
```
✅ Assets/Scripts/ProjectManagement/ProjectManager.cs
✅ Assets/Scripts/ProjectManagement/ProjectData.cs
```

### 4. Существующие компоненты:
```
✅ Assets/Scripts/AsyncSegmentationManager.cs
✅ Assets/Scripts/ARWallPresenter.cs
✅ Assets/FlutterUnityIntegration/ (все файлы)
```

## 🗑️ Команды для удаления

Выполните в Terminal или в Unity Project окне:

```bash
# Удаление ненужных файлов
rm "Assets/Scripts/ColorScanning/ColorMatcher.cs"
rm "Assets/Scripts/ColorScanning/ColorMatcher.cs.meta"

rm "Assets/Scripts/Enhanced Features/DemoColorCatalog.cs"
rm "Assets/Scripts/Enhanced Features/DemoColorCatalog.cs.meta"

rm "Assets/Scripts/Enhanced Features/FlutterUnityBridge.cs"
rm "Assets/Scripts/Enhanced Features/FlutterUnityBridge.cs.meta"

# Опционально - удалить тестовый UI
rm "Assets/Scripts/UI/TestUI.cs"
rm "Assets/Scripts/UI/TestUI.cs.meta"
```

## 📋 Обновление зависимостей

После удаления файлов нужно обновить ссылки:

### 1. В Unity Inspector:
- Удалите сломанные ссылки на удаленные компоненты
- Используйте только `SimpleColorManager` и `SimpleFlutterBridge`

### 2. В существующем коде:
- `ColorPaletteManager` оставляем (может использоваться)
- Новые компоненты не зависят от удаленных

## ⚡ Результат очистки

### Уменьшение размера проекта:
- **-4 файла** сложной логики
- **-300+ строк** ненужного кода
- **-0 зависимостей** - простая система автономна

### Упрощение архитектуры:
```
ДО очистки:
AsyncSegmentationManager
├── ColorScanner
├── ColorMatcher (❌ сложный)
├── DemoColorCatalog (❌ сложный)
├── FlutterUnityBridge (❌ сложный)
└── TestUI (❌ сложный)

ПОСЛЕ очистки:
AsyncSegmentationManager  
├── ColorScanner
├── SimpleColorManager ✅
├── SimpleFlutterBridge ✅
└── SimpleColorUI ✅
```

## 🎯 Финальная архитектура

Остается только необходимое:

```
📱 Flutter App
     ↕ API
🌉 SimpleFlutterBridge
     ↕
🎨 SimpleColorManager (8 пресетов)
     ↕
🔍 ColorScanner (сканирование)
     ↕
🏠 AsyncSegmentationManager (AR сегментация)
     ↕
🖼️ ARWallPresenter (отображение)
```

## ✅ Проверка после очистки

1. **Компиляция Unity** - не должно быть ошибок
2. **Тестирование** - `SimpleColorManager` работает
3. **Flutter API** - `SimpleFlutterBridge` отвечает
4. **Производительность** - никакого влияния на FPS

## 🔄 Если нужно вернуть

Все удаленные файлы можно восстановить из Git:
```bash
git checkout HEAD -- Assets/Scripts/ColorScanning/ColorMatcher.cs
git checkout HEAD -- Assets/Scripts/Enhanced Features/DemoColorCatalog.cs
# и т.д.
```

---

**Рекомендация**: Удалите ненужные файлы для упрощения проекта. Простая система полностью автономна и не зависит от удаляемых компонентов.
