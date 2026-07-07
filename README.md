# Sound Minimum

Лёгкий портативный аудиоплеер (не требует установки / portable, no installation required).
Подходит для повседневного прослушивания MP3/WAV, концертов и фоновой музыки.
(For everyday MP3/WAV listening, live performances, and background music.)

<img width="596" height="411" alt="Снимок экрана 2026-07-07 222346" src="https://github.com/user-attachments/assets/5a3e884c-2f3a-4c47-a7ea-0b0cfce5f27e" />

## Возможности (Features)

- **Основные звуки + фон**: два независимых плейлиста. Во время паузы основных звуков автоматически играет фон. 4 режима поведения (main sounds + background music; background auto-plays during pause; 4 behaviour modes)
- **Индивидуальные настройки трека**: громкость, плавность перехода (crossfade, 1-5 сек), зацикливание (loop) — для каждого трека отдельно (per-track volume, crossfade duration, loop)
- **Multi-device вывод**: одновременно на несколько аудиоустройств (simultaneous output to multiple devices)
- **Кроссфейд**: плавный переход между треками с настраиваемой длительностью (smooth crossfade, configurable 1-5 sec)
- **Избранное**: быстрый доступ к любимым трекам (favorites for quick access)
- **Master-громкость**: общая громкость поверх индивидуальной (master volume)
- **Автовоспроизведение**: автоматический переход к следующему треку (auto-play next track)
- **Drag-drop**: добавляйте файлы перетаскиванием в главное окно или в редактор фона (drag-drop files into main window or background editor)
- **Кастомный тёмный UI**: полностью ручная отрисовка GDI+, без нативных контролов (custom dark GDI+ theme, no native controls)
- **Локализация**: английский (по умолчанию) и русский; переключение заменой lang.json (English + Russian localization via lang.json)
- **Портативность**: один .exe, ничего не устанавливает в систему (portable, single .exe, no system install)
- **Проекты**: сохранение и загрузка очереди, резервные копии (save/load projects with backups)
- **app.txt**: настройка заголовка и подписи окна без пересборки (customize window title/subtitle without rebuild)
- **Горячие клавиши**: Space (play/pause), ← → (prev/next), Delete (удалить), Ctrl+↑/↓ (переместить), Enter (играть)

## Установка (Installation)

### Self-contained (рекомендуется / recommended)
- Скачайте `SoundMinimum_v.xxx-standalone.zip`
- Распакуйте, запустите `SoundMinimum_v.xxx.exe`
- Не требует установки .NET Runtime

### Framework-dependent
- Скачайте `SoundMinimum_v.xxx.zip`
- Требуется .NET 8 Runtime (на Windows 11 2024+ может быть предустановлен, иначе скачайте с dotnet.microsoft.com)
- Распакуйте и запустите `SoundMinimum.exe`

### Локализация (Localization)
По умолчанию английский интерфейс (English by default).
Для переключения на русский (to switch to Russian):
- Переименуйте `lang.ru.json` → `lang.json` (в папке с EXE)
- Или замените содержимое `lang.json` на текст из `lang.ru.json`

## Сборка (Build)

```bash
dotnet build -c Release
```

## Сборка архивов (Build packages)

```bash
dotnet publish -c Release -o publish
dotnet publish -c Release -o publish-standalone -r win-x64 --self-contained true
```

## Лицензия (License)

Sound Minimum License — бесплатно для некоммерческого использования (free for non-commercial use).
Подробнее в файле [LICENSE](LICENSE).
Репозиторий / Repository: [https://github.com/logego-dev/SoundMinimum](https://github.com/logego-dev/SoundMinimum)
