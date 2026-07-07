# Sound Minimum

Минималистичный концертный аудиоплеер с кастомным тёмным UI, кроссфейдами, фоновой музыкой и мульти-девайс выводом.

## Возможности

- Кастомный тёмный интерфейс (GDI+)
- Плейлист с драг-скроллом, колонками громкости/лупа/кроссфейда
- Кроссфейд между треками (настраиваемая длительность 1–5 сек)
- Double-click to play
- Loop (зацикливание), Volume (индивидуальная громкость трека)
- Multi-device вывод (выбор аудиоустройств в левой панели)
- Background Music — фоновое аудио с автоматическим переключением поведения
- Избранное (Favorites)
- Авто-сохранение плейлиста
- Конфигурируемый заголовок окна (app.txt)

## Скриншот

<img width="596" height="411" alt="Снимок экрана 2026-07-07 222346" src="https://github.com/user-attachments/assets/e4b9e5c4-6421-411f-a9a4-d27e74f78f59" />


## Сборка

```bash
dotnet build SoundMinimum.csproj -c Release
```

## Запуск

Готовые сборки в папке `releases/`:
- `SoundMinimum.zip` — framework-dependent (требуется .NET 8 Runtime x64)
- `SoundMinimum-standalone.zip` — самодостаточный (Windows 10+, без .NET)

Подробная инструкция — в `instruction.txt` внутри архива.

## Лицензия

Sound Minimum License — бесплатно для личного использования.
Коммерческое использование — по согласованию с автором.

Подробнее: [LICENSE](LICENSE)

---
**Автор:** [logego](https://github.com/logego-dev) · **Telegram:** [@sound_minimum](https://t.me/sound_minimum)
