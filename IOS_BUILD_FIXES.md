# 🔧 Исправления для сборки iOS - Job Reflection Errors

## 📋 Проблема
При сборке Unity проекта для iOS в Xcode возникают ошибки:
```
Undefined symbol: ___JobReflectionRegistrationOutput__XXXXXXXXX_CreateJobReflectionData_...
Undefined symbol: ___JobReflectionRegistrationOutput__XXXXXXXXX_EarlyInit_...
```

## ✅ Решения

### 1. Автоматические исправления (Рекомендуется)

В Unity откройте меню **Tools > iOS Build Fix**:
- **Force Apply Fixes** - принудительно применить все исправления
- **Check Settings** - проверить текущие настройки проекта

### 2. Ручные настройки Unity

#### A. Настройки Player Settings
1. Откройте **Edit > Project Settings > Player**
2. Выберите вкладку **iOS**
3. В разделе **Other Settings**:
   - **Scripting Backend**: IL2CPP
   - **Target Architectures**: ARM64 ✅
   - **Strip Engine Code**: OFF ❌

#### B. Burst Compiler Settings
1. Откройте **Window > Burst > Burst AOT Settings**
2. Переключитесь на **iOS**
3. Настройки:
   - **Enable Burst Compilation**: ON ✅
   - **Enable Optimisations**: ON ✅
   - **Enable Safety Checks**: OFF ❌
   - **Enable Debug In All Builds**: OFF ❌

### 3. Настройки Xcode (После сборки Unity)

#### A. Build Settings
1. Откройте ваш проект в Xcode
2. Выберите проект в навигаторе
3. Перейдите в **Build Settings**
4. Найдите **Architectures**:
   - **Architectures**: arm64
   - **Build Active Architecture Only**: NO
   - **Valid Architectures**: arm64

#### B. Linker Flags (Если проблема сохраняется)
1. В **Build Settings** найдите **Other Linker Flags**
2. Добавьте флаги:
   ```
   -ObjC
   -all_load  (только если необходимо)
   ```

#### C. Дополнительные настройки
1. **Dead Code Stripping**: NO
2. **Strip Debug Symbols During Copy**: NO (для debug сборок)

### 4. Диагностика проблем

#### Проверка настроек в Unity:
```
Tools > iOS Build Fix > Check Settings
```

#### Проверка логов сборки:
1. В Unity включите **Development Build**
2. Включите **Script Debugging**
3. Проверьте Console на сообщения JobSystemFix

### 5. Дополнительные шаги (Если проблема сохраняется)

#### A. Очистка проекта
1. В Unity: **Edit > Preferences > External Tools** - убедитесь что Xcode путь правильный
2. Удалите папку **Library** в Unity проекте
3. Удалите сборку iOS и пересоберите
4. В Xcode: **Product > Clean Build Folder**

#### B. Проверка версий
- Unity 2022.3+ LTS (рекомендуется)
- Xcode 14+ 
- iOS Deployment Target: 12.0+

#### C. Burst Inspector
1. Откройте **Window > Burst > Burst Inspector**
2. Убедитесь что все Job'ы успешно скомпилированы
3. Проверьте отсутствие ошибок компиляции

## 🚨 Если ничего не помогает

### Временное решение:
1. Отключите Burst Compiler:
   ```
   Window > Burst > Burst AOT Settings > Enable Burst Compilation: OFF
   ```
2. Пересоберите проект

### Альтернативное решение:
1. Используйте **Mono** вместо **IL2CPP** (не рекомендуется для финальной сборки)

## 📝 Дополнительная информация

### Полезные ссылки:
- [Unity iOS Build Troubleshooting](https://docs.unity3d.com/Manual/TroubleShootingIPhone.html)
- [Burst Compiler Documentation](https://docs.unity3d.com/Packages/com.unity.burst@latest)
- [IL2CPP Documentation](https://docs.unity3d.com/Manual/IL2CPP.html)

### Файлы созданные для исправления:
- `Assets/Scripts/BuildFixes/IOSBuildFix.cs` - автоматические исправления сборки
- `Assets/Scripts/BuildFixes/JobSystemFix.cs` - принудительная инициализация Job System
- `ProjectSettings/BurstAotSettings_iOS.json` - настройки Burst для iOS

Эти файлы автоматически активируются при сборке и помогают решить большинство проблем с Job Reflection.
