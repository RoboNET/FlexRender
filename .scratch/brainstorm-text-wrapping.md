# Централизация переноса текста в LayoutEngine

**Дата:** 2026-02-08
**Статус:** Проект (Design Draft)

---

## 1. Текущая архитектура: как текст проходит через систему

### 1.1 Общая схема потока данных

```
YAML Template
    |
    v
TextElement (AST)  -- Content, Font, Size, Wrap, MaxLines, Overflow, LineHeight
    |
    v
LayoutEngine.LayoutTextElement()
    |
    +-- вызывает TextMeasurer delegate (если установлен)
    |       (TextElement, fontSize, maxWidth) -> LayoutSize
    |
    +-- получает только Width и Height (LayoutSize) -- без информации о строках
    |
    v
LayoutNode (x, y, width, height)  -- текст остаётся в Element.Content как одна строка
    |
    +-------------------------------+
    |                               |
    v                               v
SkiaRenderer                    SvgRenderingEngine
    |                               |
TextRenderer.DrawText()         DrawText() -- static метод
    |                               |
    +-- GetLines() ПОВТОРНО         +-- text.Content.Split('\n')
    |   вычисляет word-wrap         |   ТОЛЬКО по \n, БЕЗ word-wrap
    |                               |
    v                               v
SKCanvas.DrawText()             <text><tspan>...</tspan></text>
(каждая строка отдельно)        (каждая строка как tspan)
```

### 1.2 Ключевые компоненты

**LayoutEngine** (`FlexRender.Core/Layout/LayoutEngine.cs`):
- Свойство `TextMeasurer` -- делегат `Func<TextElement, float, float, LayoutSize>?`
- Метод `LayoutTextElement()` (строка 439) вызывает TextMeasurer для получения размеров
- Получает только `LayoutSize(Width, Height)` -- никакой информации о разбиении на строки
- Метод `MayWrapOrContainsNewlines()` (строка 494) решает, передавать ли ограничение по ширине

**TextRenderer** (`FlexRender.Skia/Rendering/TextRenderer.cs`):
- `MeasureText()` (строка 39) -- вызывается как TextMeasurer delegate для layout
- `DrawText()` (строка 72) -- вызывается при отрисовке на canvas
- `GetLines()` (строка 213) -- приватный static метод, делает word-wrap с помощью SKFont.MeasureText()
- **Вызывается ДВАЖДЫ** для одного и того же текста: сначала из MeasureText, потом из DrawText

**SvgRenderingEngine** (`FlexRender.Svg/Rendering/SvgRenderingEngine.cs`):
- `DrawText()` (строка 245) -- static метод, рисует текст в SVG
- Разбивает ТОЛЬКО по `\n`: `text.Content.Split('\n')`
- **НЕ поддерживает word-wrap вообще**
- У SvgRender **нет TextMeasurer** -- LayoutEngine создаётся без него (SvgRender.cs строка 59)

**SkiaRenderer** (`FlexRender.Skia/Rendering/SkiaRenderer.cs`, строки 113-117):
- Подключает TextMeasurer к LayoutEngine:
  ```csharp
  _layoutEngine.TextMeasurer = (element, fontSize, maxWidth) =>
  {
      var measured = _textRenderer.MeasureText(element, maxWidth, BaseFontSize);
      return new LayoutSize(measured.Width, measured.Height);
  };
  ```

### 1.3 Выявленные проблемы

| Проблема | Описание | Влияние |
|----------|----------|---------|
| **Двойное вычисление** | `GetLines()` вызывается дважды в Skia: при MeasureText и при DrawText | Производительность (2x работы) |
| **SVG без word-wrap** | SVG renderer разбивает только по `\n`, игнорирует `text.Wrap` | Функциональный баг |
| **SVG без TextMeasurer** | SvgRender создаёт LayoutEngine без TextMeasurer -- высота текста неточная | Некорректный layout |
| **Расхождение строк** | Даже если SVG добавит wrap, его алгоритм может дать другие разрывы, чем Skia | Несогласованность рендеров |
| **Каждый новый рендер** | PDF, WASM Canvas, etc. должны заново реализовывать GetLines() | Дублирование кода |

---

## 2. Предлагаемая архитектура: централизованный перенос

### 2.1 Основная идея

Перенести **вычисление разрывов строк** (line breaking) из рендереров в `LayoutEngine`. Рендеры получают предварительно разбитый текст и рисуют его без собственной логики переноса.

### 2.2 Новый поток данных

```
YAML Template
    |
    v
TextElement (AST)  -- Content, Font, Size, Wrap, MaxLines, Overflow, LineHeight
    |
    v
LayoutEngine.LayoutTextElement()
    |
    +-- вызывает ITextShaper.ShapeText()  (НОВЫЙ интерфейс)
    |       (TextElement, fontSize, maxWidth) -> TextShapingResult
    |       {
    |           Lines: ["строка 1", "строка 2", ...],
    |           TotalSize: LayoutSize,
    |           LineHeight: float
    |       }
    |
    v
LayoutNode
    +-- TextLines: IReadOnlyList<string>?    (НОВОЕ свойство)
    +-- ComputedLineHeight: float?           (НОВОЕ свойство)
    |
    +-------------------------------+
    |                               |
    v                               v
SkiaRenderer                    SvgRenderingEngine
    |                               |
    +-- рисует node.TextLines       +-- рисует node.TextLines
    |   по одной, без wrap          |   как <tspan> элементы
    |                               |
    v                               v
Идентичные строки!              Идентичные строки!
```

### 2.3 Ключевые изменения

#### 2.3.1 Новый интерфейс в FlexRender.Core

```csharp
// FlexRender.Core/Layout/ITextShaper.cs
namespace FlexRender.Layout;

/// <summary>
/// Результат вычисления разбиения текста на строки.
/// </summary>
public readonly record struct TextShapingResult(
    IReadOnlyList<string> Lines,
    LayoutSize TotalSize,
    float LineHeight);

/// <summary>
/// Абстракция для измерения и разбиения текста на строки.
/// Реализуется бэкендом (Skia, HarfBuzz, etc.).
/// </summary>
public interface ITextShaper
{
    /// <summary>
    /// Измеряет текст и вычисляет разбиение на строки.
    /// </summary>
    TextShapingResult ShapeText(TextElement element, float fontSize, float maxWidth);
}
```

#### 2.3.2 Изменения в LayoutEngine

```csharp
// Вместо:
public Func<TextElement, float, float, LayoutSize>? TextMeasurer { get; set; }

// Новое свойство:
public ITextShaper? TextShaper { get; set; }
```

Метод `LayoutTextElement()` будет:
1. Вызывать `TextShaper.ShapeText()` вместо `TextMeasurer()`
2. Сохранять результат (Lines, LineHeight) в LayoutNode

#### 2.3.3 Изменения в LayoutNode

```csharp
public sealed class LayoutNode
{
    // ... существующие свойства ...

    /// <summary>
    /// Предварительно вычисленные строки текста (только для TextElement).
    /// null для нетекстовых элементов.
    /// </summary>
    public IReadOnlyList<string>? TextLines { get; set; }

    /// <summary>
    /// Вычисленная высота строки (только для TextElement).
    /// </summary>
    public float ComputedLineHeight { get; set; }
}
```

#### 2.3.4 Реализация ITextShaper в FlexRender.Skia

```csharp
// FlexRender.Skia/Rendering/SkiaTextShaper.cs
internal sealed class SkiaTextShaper : ITextShaper
{
    private readonly FontManager _fontManager;
    private readonly RenderOptions _defaultRenderOptions;

    public TextShapingResult ShapeText(TextElement element, float fontSize, float maxWidth)
    {
        // Переиспользует существующую логику из TextRenderer.GetLines()
        // но возвращает Lines как часть результата
        ...
    }
}
```

#### 2.3.5 Упрощение рендереров

**TextRenderer.DrawText()** упрощается:
```csharp
// Было:
var lines = GetLines(element.Content, element.Wrap, effectiveMaxWidth, font, ...);

// Стало:
// lines приходят из LayoutNode.TextLines, просто рисуем их
```

**SvgRenderingEngine.DrawText()** упрощается:
```csharp
// Было:
var lines = text.Content.Split('\n');  // только \n, без word-wrap

// Стало:
// lines берутся из LayoutNode.TextLines, включая word-wrap!
```

---

## 3. Ключевая проблема: метрики шрифтов

### 3.1 Суть проблемы

Для вычисления переноса строк нужны **метрики шрифтов** -- точная ширина каждого слова/символа. Сейчас эти метрики живут в `SKFont.MeasureText()` (SkiaSharp), что привязано к Skia бэкенду.

`LayoutEngine` находится в `FlexRender.Core`, который **не зависит от Skia**. Это принципиально -- Core должен оставаться backend-agnostic.

### 3.2 Текущее решение -- делегат

Сейчас проблема уже решена через `TextMeasurer` делегат. LayoutEngine не знает про Skia -- он просто вызывает делегат:

```csharp
// LayoutEngine.cs:
public Func<TextElement, float, float, LayoutSize>? TextMeasurer { get; set; }

// SkiaRenderer.cs подключает:
_layoutEngine.TextMeasurer = (element, fontSize, maxWidth) =>
{
    var measured = _textRenderer.MeasureText(element, maxWidth, BaseFontSize);
    return new LayoutSize(measured.Width, measured.Height);
};
```

### 3.3 Предлагаемое решение -- интерфейс ITextShaper

Вместо делегата, который возвращает только размеры, вводим **интерфейс**, который возвращает и размеры, и разбитые строки.

Почему интерфейс лучше делегата:
- Делегат `Func<TextElement, float, float, LayoutSize>` уже на пределе -- добавить возврат строк невозможно без изменения сигнатуры
- Интерфейс даёт имя и документацию контракту
- Интерфейс расширяем -- в будущем можно добавить методы для RTL, bidirectional text, font fallback chains
- Интерфейс можно легко мокировать в тестах

### 3.4 Fallback для SVG без Skia

Когда SvgRender используется **без** Skia бэкенда (`WithSvg()` без `WithSkia()`), TextShaper не установлен. В этом случае нужен fallback:

**Вариант A: Простой текстовый шейпер в Core** (рекомендуется)
- Находится в `FlexRender.Core`
- Использует приблизительные метрики (средняя ширина символа = fontSize * 0.6)
- Достаточно для базового word-wrap
- Не требует Skia

**Вариант B: Обязательный TextShaper**
- SvgRender без Skia выбрасывает ошибку при word-wrap
- Явно документировано -- "для word-wrap подключите Skia"
- Проще, но менее user-friendly

**Рекомендация: Вариант A.** Приблизительный шейпер лучше, чем отсутствие word-wrap. Для пользователей, которым нужна точность пиксель-в-пиксель, можно рекомендовать `WithSvg(svg => svg.WithSkia())`.

### 3.5 Альтернативный вариант: HarfBuzz для точных метрик

Проект уже имеет `FlexRender.HarfBuzz`. HarfBuzz предоставляет font shaping без привязки к рендерингу. Это значит, что Core мог бы зависеть от HarfBuzzSharp для точных метрик. Однако:

- Это добавляет native dependency к Core
- HarfBuzz -- это text shaping (ligatures, kerning), а не просто измерение ширины
- Для MVP приблизительных метрик достаточно

---

## 4. Изменение модели данных

### 4.1 Текущая модель

```
LayoutNode
  ├── Element: TemplateElement (содержит TextElement.Content как одна строка)
  ├── X, Y: float
  ├── Width, Height: float
  ├── Direction: TextDirection
  └── Children: List<LayoutNode>
```

Текст хранится в `node.Element` как `TextElement` с полем `Content` -- одна строка, без информации о переносах.

### 4.2 Предлагаемая модель

```
LayoutNode
  ├── Element: TemplateElement
  ├── X, Y: float
  ├── Width, Height: float
  ├── Direction: TextDirection
  ├── Children: List<LayoutNode>
  │
  ├── TextLines: IReadOnlyList<string>?   ← НОВОЕ
  └── ComputedLineHeight: float           ← НОВОЕ
```

**TextLines** -- результат разбиения текста на строки, учитывая:
- Явные переносы (`\n`)
- Word-wrap (если `Wrap == true`)
- MaxLines ограничение
- Ellipsis обработку (последняя строка обрезана с "...")

**ComputedLineHeight** -- вычисленная высота строки в пикселях, нужна рендерерам для позиционирования.

### 4.3 Почему свойства на LayoutNode, а не отдельная структура?

Рассматривались варианты:
1. **Словарь `Dictionary<LayoutNode, TextLayoutResult>`** -- усложняет API, нужно передавать словарь отдельно
2. **Отдельный `TextLayoutNode : LayoutNode`** -- нарушает sealed, требует cast-ов
3. **Свойства на LayoutNode** (выбрано) -- простейший вариант, nullable показывает наличие/отсутствие

### 4.4 Инвариант

```
node.Element is TextElement  =>  node.TextLines != null  (если TextShaper установлен)
node.Element is not TextElement  =>  node.TextLines == null
```

---

## 5. Компромиссы и риски

### 5.1 Преимущества

| Преимущество | Описание |
|-------------|----------|
| **SVG получает word-wrap** | Автоматически, без дополнительного кода |
| **Один алгоритм** | Все рендеры дают идентичные переносы строк |
| **Нет двойного вычисления** | GetLines() вызывается один раз в layout, не повторяется при draw |
| **Простота новых рендеров** | PDF, WASM Canvas просто читают TextLines -- не нужно реализовывать word-wrap |
| **Тестируемость** | Можно тестировать word-wrap в isolation через ITextShaper mock |
| **Кэширование** | Результат ShapeText() кэшируется в LayoutNode, доступен при повторном draw |

### 5.2 Недостатки

| Недостаток | Описание | Митигация |
|-----------|----------|-----------|
| **Увеличение LayoutNode** | Два новых свойства на каждый узел | Nullable, нулевой overhead для нетекстовых узлов |
| **Разрыв обратной совместимости** | TextMeasurer delegate заменяется на ITextShaper | Оставить TextMeasurer deprecated на 1-2 релиза |
| **Приблизительные метрики** | Fallback шейпер в Core будет давать неточные переносы | Документировать, рекомендовать WithSkia() |
| **Размер LayoutNode** | Хранение списка строк увеличивает потребление памяти | Незначительно -- строки уже есть в Content |

### 5.3 Риски

**Риск: Текст с разными шрифтами в одной строке (rich text)**
- Текущая система не поддерживает mixed fonts в одном TextElement
- ITextShaper рассчитан на один шрифт -- это совпадает с текущим ограничением
- Если в будущем нужен rich text -- ITextShaper расширяем

**Риск: Производительность fallback шейпера**
- Приблизительные метрики могут давать больше/меньше строк, чем реальный шрифт
- Для SVG-only сценариев это приемлемо -- пользователь видит вектор, может масштабировать
- Для pixel-perfect нужен Skia -- это уже документировано

**Риск: Ellipsis обработка**
- Сейчас `TruncateWithEllipsis()` использует `SKFont.MeasureText()` побуквенно (binary search)
- В fallback шейпере ellipsis будет приблизительным
- Приемлемо для SVG, точность даёт Skia бэкенд

---

## 6. Путь миграции (инкрементальный)

### Фаза 1: Добавить ITextShaper без удаления TextMeasurer

1. Создать `ITextShaper` интерфейс и `TextShapingResult` в FlexRender.Core
2. Добавить `TextLines` и `ComputedLineHeight` свойства в LayoutNode
3. Добавить `ITextShaper? TextShaper` свойство в LayoutEngine
4. В `LayoutTextElement()`:
   - Если TextShaper установлен -- использовать его, сохранить результат в LayoutNode
   - Иначе если TextMeasurer установлен -- использовать его (обратная совместимость)
   - Иначе fallback на текущую логику

**Результат:** Никаких breaking changes. TextMeasurer продолжает работать.

### Фаза 2: Реализовать SkiaTextShaper

1. Создать `SkiaTextShaper : ITextShaper` в FlexRender.Skia
2. Перенести логику `GetLines()` и `MeasureText()` из TextRenderer
3. В SkiaRenderer -- подключить SkiaTextShaper вместо TextMeasurer delegate
4. Упростить `TextRenderer.DrawText()` -- брать строки из LayoutNode.TextLines

**Результат:** Skia рендер перестаёт вычислять GetLines() дважды.

### Фаза 3: Обновить SVG рендер

1. Обновить `SvgRenderingEngine.DrawText()` -- использовать TextLines из LayoutNode
2. Создать `ApproximateTextShaper : ITextShaper` в FlexRender.Core для fallback
3. В SvgRender -- подключить ApproximateTextShaper если Skia не доступен
4. Передать LayoutNode (или его TextLines) в SvgRenderingEngine

**Проблема:** сейчас `SvgRenderingEngine.DrawText()` -- static метод, получает только element и координаты:
```csharp
private static void DrawText(StringBuilder sb, TextElement text, float x, float y, ...)
```
Нужно передать также `LayoutNode` или `IReadOnlyList<string>? textLines`.

**Решение:** Изменить сигнатуру DrawElement/DrawText чтобы принимать LayoutNode вместо отдельных параметров. Или передавать TextLines отдельным параметром.

### Фаза 4: Пометить TextMeasurer как Obsolete

1. Добавить `[Obsolete]` на `TextMeasurer` свойство
2. Обновить документацию
3. В следующем major release -- удалить

### Временная шкала

| Фаза | Сложность | Затраты | Breaking? |
|-------|-----------|---------|-----------|
| 1     | Низкая    | 2-3 часа | Нет |
| 2     | Средняя   | 3-4 часа | Нет |
| 3     | Средняя   | 3-4 часа | Нет (internal API) |
| 4     | Низкая    | 0.5 часа | Soft (deprecation) |

Итого: ~10 часов работы, все фазы без breaking changes.

---

## 7. Влияние на SVG

### 7.1 Что SVG получает автоматически

После реализации Фазы 3:

- **Word-wrap** -- SVG будет переносить текст по словам с учётом maxWidth
- **MaxLines** -- ограничение количества строк будет работать
- **Ellipsis** -- обрезка с "..." на последней строке
- **Идентичные переносы** -- SVG и PNG дадут одинаковые строки текста
- **Корректная высота** -- layout будет учитывать реальную высоту многострочного текста

### 7.2 Что потребует дополнительной работы в SVG

- **LineHeight** -- сейчас SVG использует `fontSize * 1.2f` для dy. Нужно использовать `ComputedLineHeight`
- **Baseline alignment** -- SVG позиционирует текст по baseline (`y + fontSize`), Skia по top. Нужно согласовать
- **TextAlign** -- существующая логика text-anchor должна применяться к каждой строке
- **Font resolution** -- SVG использует собственный `ParseFontSize()` (строка 623) вместо общего `FontSizeResolver.Resolve()`. Нужно унифицировать

### 7.3 Ограничения SVG word-wrap через tspan

SVG `<tspan>` не поддерживает автоматический word-wrap. Но предложенный подход это обходит:
- LayoutEngine вычисляет разрывы
- SVG рендер создаёт `<tspan x="..." dy="...">` для каждой предварительно вычисленной строки
- Результат визуально идентичен word-wrap

Это стандартный подход -- так делают все SVG-генераторы (Inkscape, D3.js, etc.).

---

## 8. Влияние на SvgRenderingEngine: передача LayoutNode

### 8.1 Текущая проблема

Сейчас `SvgRenderingEngine.DrawText()` получает `TextElement`, а не `LayoutNode`:

```csharp
// Текущая цепочка вызовов:
RenderNode(sb, node, offsetX, offsetY, depth)
  -> DrawElement(sb, node.Element, x, y, width, height, direction)
    -> DrawText(sb, text, x, y, width, height, direction)
```

`DrawText` имеет доступ только к `TextElement` и не видит `TextLines` из `LayoutNode`.

### 8.2 Решение

Изменить `DrawElement()` и `DrawText()` для принятия `LayoutNode`:

```csharp
// Вариант A: передать LayoutNode целиком
private void DrawElement(StringBuilder sb, LayoutNode node, float x, float y)
{
    // ...
    case TextElement text:
        DrawText(sb, text, node.TextLines, node.ComputedLineHeight, x, y, width, height, direction);
        break;
}

// Вариант B: передать только textLines
private static void DrawText(
    StringBuilder sb, TextElement text,
    IReadOnlyList<string>? textLines, float computedLineHeight,
    float x, float y, float width, float height, TextDirection direction)
```

Рекомендуется **Вариант A** -- передача LayoutNode проще и чище. Вся нужная информация в одном месте.

---

## 9. Заключение и рекомендация

### 9.1 Рекомендация: ДЕЛАТЬ, и делать инкрементально

Централизация переноса текста решает три серьёзные проблемы:
1. **SVG без word-wrap** -- это функциональный баг, он ломает layout при длинных текстах
2. **Двойное вычисление** -- производительность
3. **Расхождение рендеров** -- предсказуемость

### 9.2 Конкретный план

1. **Начать с Фазы 1** -- добавить интерфейс ITextShaper, свойства в LayoutNode, не ломая ничего
2. **Сразу перейти к Фазе 2** -- SkiaTextShaper заменяет текущий делегат
3. **Фаза 3** -- SVG рендер использует TextLines
4. **Фаза 4** -- deprecation старого API

### 9.3 Когда делать

Это должно быть приоритетом **до** добавления новых текстовых фич (rich text, bidirectional text, text decoration). Причина: ITextShaper будет расширяться для этих фич, и лучше заложить правильную архитектуру сейчас.

### 9.4 Что НЕ делать

- НЕ добавлять HarfBuzz dependency в Core
- НЕ пытаться реализовать bidirectional text в рамках этой задачи
- НЕ удалять TextMeasurer сразу -- только deprecation
- НЕ менять public API SkiaRender/SvgRender -- все изменения internal

---

## Приложение A: Граф зависимостей проектов

```
FlexRender.Core (нет зависимостей на Skia)
  |
  +-- Layout/LayoutEngine.cs       -- ITextShaper? TextShaper
  +-- Layout/LayoutNode.cs         -- TextLines, ComputedLineHeight
  +-- Layout/ITextShaper.cs         -- НОВЫЙ интерфейс
  +-- Layout/ApproximateTextShaper.cs -- НОВЫЙ fallback

FlexRender.Skia (зависит от Core + SkiaSharp)
  |
  +-- Rendering/SkiaTextShaper.cs   -- НОВЫЙ, реализует ITextShaper через SKFont
  +-- Rendering/TextRenderer.cs     -- УПРОЩАЕТСЯ, DrawText берёт строки из LayoutNode
  +-- Rendering/SkiaRenderer.cs     -- подключает SkiaTextShaper

FlexRender.Svg (зависит от Core + SkiaSharp для QR/Barcode)
  |
  +-- Rendering/SvgRenderingEngine.cs -- ОБНОВЛЯЕТСЯ, использует TextLines
  +-- SvgRender.cs                     -- подключает ApproximateTextShaper или SkiaTextShaper
```

## Приложение B: Сравнение текущего и предлагаемого поведения

### Пример: текст "Hello World Foo Bar" в контейнере шириной 100px

**Текущее поведение (Skia):**
```
Layout:  TextMeasurer -> LayoutSize(100, 40)  // 2 строки
Draw:    GetLines() -> ["Hello World", "Foo Bar"]  // ПОВТОРНОЕ вычисление
```

**Текущее поведение (SVG):**
```
Layout:  TextMeasurer = null -> height = fontSize * 1.4  // 1 строка!
Draw:    Split('\n') -> ["Hello World Foo Bar"]  // одна строка, без wrap
```

**Предлагаемое поведение (оба рендера):**
```
Layout:  TextShaper -> TextShapingResult {
           Lines: ["Hello World", "Foo Bar"],
           TotalSize: LayoutSize(100, 40),
           LineHeight: 20
         }
LayoutNode: TextLines = ["Hello World", "Foo Bar"]

Skia Draw:  foreach line in node.TextLines -> DrawText(line)
SVG Draw:   foreach line in node.TextLines -> <tspan>line</tspan>
```

Оба рендера рисуют одинаковые строки.
