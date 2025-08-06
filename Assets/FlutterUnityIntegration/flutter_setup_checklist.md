# Чек-лист настройки Flutter Unity интеграции

## ✅ Подготовка Unity проекта

### Шаг 1: Автоматическая настройка
- [ ] Открыть Unity проект RemaluxNewAR
- [ ] Перейти в меню `Flutter → Auto Setup Project`
- [ ] Нажать "Выполнить полную настройку"
- [ ] Проверить, что все компоненты найдены без ошибок

### Шаг 2: Ручная проверка (если нужно)
- [ ] Убедиться, что на сцене есть `FlutterUnityManager`
- [ ] Проверить ссылки на `AsyncSegmentationManager` и `ColorPaletteManager`
- [ ] Убедиться, что `UnityMessageBridge` настроен на использование нового менеджера

### Шаг 3: Настройка сборки
- [ ] Открыть `Flutter → Build Settings`
- [ ] Выбрать целевую платформу (Android/iOS)
- [ ] Указать пути для экспорта
- [ ] Нажать "Setup Build Settings"

## ✅ Сборка Unity для Flutter

### Android
- [ ] Выбрать платформу Android в Build Settings
- [ ] Убедиться, что `Export Project` включен
- [ ] Нажать "Build for Flutter"
- [ ] Проверить, что сборка завершилась успешно

### iOS
- [ ] Выбрать платформу iOS в Build Settings
- [ ] Настроить Bundle Identifier
- [ ] Нажать "Build for Flutter"
- [ ] Проверить Xcode проект

## ✅ Создание Flutter проекта

### Шаг 1: Создание проекта
```bash
flutter create remalux_ar_flutter
cd remalux_ar_flutter
```

### Шаг 2: Добавление зависимостей
- [ ] Скопировать содержимое `pubspec.yaml` из примеров
- [ ] Выполнить `flutter pub get`

### Шаг 3: Копирование Unity сборки
- [ ] Скопировать Unity сборку в папку Flutter проекта
- [ ] Следовать инструкциям интеграции из документации

## ✅ Настройка Android

### android/app/build.gradle
- [ ] Установить `compileSdkVersion 33`
- [ ] Установить `minSdkVersion 24`
- [ ] Добавить `ndk.abiFilters`

### android/app/src/main/AndroidManifest.xml
- [ ] Добавить разрешение на камеру
- [ ] Добавить AR required feature
- [ ] Добавить OpenGL ES 3.0 requirement

## ✅ Настройка iOS

### ios/Runner/Info.plist
- [ ] Добавить `NSCameraUsageDescription`
- [ ] Добавить `io.flutter.embedded_views_preview`

### Xcode настройки
- [ ] Установить Deployment Target 11.0+
- [ ] Настроить подписание приложения
- [ ] Добавить AR capabilities если нужно

## ✅ Тестирование интеграции

### Базовое тестирование
- [ ] Создать тестовый AR экран в вашем Flutter приложении
- [ ] Запустить приложение на устройстве
- [ ] Проверить загрузку Unity сцены
- [ ] Протестировать передачу цветов

### Расширенное тестирование
- [ ] Проверить работу AR камеры
- [ ] Протестировать сегментацию стен
- [ ] Проверить окрашивание маски
- [ ] Тестировать различные цвета

## ✅ Производственная сборка

### Android Release
```bash
flutter build apk --release
# или
flutter build appbundle --release
```

### iOS Release
```bash
flutter build ios --release
```

## 🚨 Решение проблем

### Unity не запускается
- [ ] Проверить архитектуры ARM в Android build.gradle
- [ ] Убедиться, что minSdkVersion >= 24
- [ ] Проверить наличие всех native библиотек

### Камера не работает
- [ ] Проверить разрешения в манифесте
- [ ] Запросить разрешения в runtime
- [ ] Проверить AR Foundation настройки

### Низкая производительность
- [ ] Использовать `useAndroidViewSurface: false`
- [ ] Оптимизировать настройки Unity
- [ ] Проверить частоту кадров сегментации

### Сообщения не передаются
- [ ] Проверить имена GameObject и методов
- [ ] Убедиться, что FlutterUnityManager на сцене
- [ ] Проверить логи Unity и Flutter

## 📋 Финальная проверка

- [ ] Unity проект собирается без ошибок
- [ ] Flutter приложение запускается
- [ ] AR камера работает
- [ ] Сегментация выполняется
- [ ] Цвета передаются из Flutter в Unity
- [ ] Маска окрашивается правильно
- [ ] Приложение стабильно работает на целевом устройстве

## 📞 Поддержка

При возникновении проблем:

1. Проверьте логи Unity (`Console → Clear on Play`)
2. Проверьте логи Flutter (`flutter logs`)
3. Убедитесь в версионной совместимости
4. Следуйте troubleshooting guide в документации
5. Используйте тестовые примеры для изоляции проблем

---

**Успешной интеграции!** 🚀