# TODO

## Padding: поддержка раздельных отступов (non-uniform)

Сейчас `padding` принимает одно значение, применяемое ко всем сторонам одинаково.

**Текущее поведение:**
```yaml
padding: 20  # → 20px со всех сторон
```

**Ожидаемое поведение:**
Поддержать CSS-подобный синтаксис с раздельными значениями:
```yaml
padding: "20 40"           # → top/bottom=20, left/right=40
padding: "20 40 30"        # → top=20, left/right=40, bottom=30
padding: "20 40 30 10"     # → top=20, right=40, bottom=30, left=10
```

**Требуемые изменения:**
- `UnitParser` — парсинг строки с несколькими значениями
- `LayoutEngine` — раздельный учёт padding по сторонам при расчёте размеров и позиционировании
- Тесты для всех вариантов формата

## Конфигурируемые лимиты ресурсов

Сейчас все лимиты безопасности захардкожены как константы в разных классах:

| Константа | Значение | Класс |
|-----------|----------|-------|
| `MaxFileSize` | 1 MB | `TemplateParser` |
| `MaxFileSize` | 10 MB | `DataLoader` |
| `MaxNestingDepth` | 50 | `YamlPreprocessor` |
| `MaxInputSizeBytes` | 1 MB | `YamlPreprocessor` |
| `MaxNestingDepth` | 100 | `TemplateProcessor` |
| `MaxRenderDepth` | 100 | `SkiaLayoutRenderer` |
| `MaxImageSize` | 10 MB | `SkiaLayoutOptions` |
| `HttpTimeout` | 30s | `SkiaLayoutOptions` |

**Ожидаемое поведение:**
Перенести все лимиты в единую конфигурацию (например, в `SkiaLayoutOptions` или отдельный класс `ResourceLimits`), чтобы пользователь мог переопределить значения по умолчанию через DI/builder. Дефолтные значения должны оставаться такими же (безопасные по умолчанию).
