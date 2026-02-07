# Brainstorming: ImageSharp Rendering Engine + QR SVG Direct Output

**Дата:** 2026-02-08
**Статус:** Черновик исследования

---

## Часть 1: Анализ текущей архитектуры

### Конвейер рендеринга FlexRender

FlexRender использует четкое разделение ответственности:

```
YAML Template
  -> TemplateParser           (YAML -> AST)
  -> TemplateExpander         (раскрытие each/if в конкретные элементы)
  -> TemplateProcessor        (обработка {{variable}} выражений)
  -> LayoutEngine             (два прохода: измерение -> расположение -> LayoutNode tree)
  -> [SkiaRenderer|SvgRenderingEngine]  (обход LayoutNode дерева -> отрисовка)
```

Ключевое наблюдение: **первые четыре шага полностью не зависят от бэкенда рендеринга**. Они живут в `FlexRender.Core` и работают с абстрактными AST-элементами и `LayoutNode`. Бэкенд-зависимость возникает только на последнем шаге.

### Текущие бэкенды

| Бэкенд | Проект | Зависимость | Выход |
|--------|--------|-------------|-------|
| Skia (растр) | `FlexRender.Skia` | SkiaSharp 3.119.1 | PNG, JPEG, BMP, Raw |
| SVG (вектор) | `FlexRender.Svg` | FlexRender.Skia (для шрифтов + raster fallback) | SVG XML |

### Интерфейс рендерера: IFlexRender

```csharp
public interface IFlexRender : IDisposable
{
    Task<byte[]> Render(Template, ObjectValue?, ImageFormat, CancellationToken);
    Task Render(Stream, Template, ObjectValue?, ImageFormat, CancellationToken);
    Task<byte[]> RenderToPng(Template, ObjectValue?, PngOptions?, RenderOptions?, CancellationToken);
    Task RenderToPng(Stream, Template, ObjectValue?, PngOptions?, RenderOptions?, CancellationToken);
    Task<byte[]> RenderToJpeg(Template, ObjectValue?, JpegOptions?, RenderOptions?, CancellationToken);
    // ... RenderToBmp, RenderToRaw, RenderToSvg
}
```

### Паттерн Builder

```csharp
var render = new FlexRenderBuilder()
    .WithSkia(skia => skia.WithQr().WithBarcode())  // или .WithSvg(svg => svg.WithSkia())
    .Build();  // -> IFlexRender
```

`FlexRenderBuilder.SetRendererFactory(Func<FlexRenderBuilder, IFlexRender>)` -- внутренний метод, через который бэкенды регистрируются.

### Критическая точка связи: TextMeasurer

```csharp
_layoutEngine.TextMeasurer = (element, fontSize, maxWidth) =>
{
    var measured = _textRenderer.MeasureText(element, maxWidth, BaseFontSize);
    return new LayoutSize(measured.Width, measured.Height);
};
```

Лейаут-движок не знает про Skia, но ему нужна функция измерения текста. Текущая реализация делегирует это в `TextRenderer`, который использует `SKFont.MeasureText()`. Любой альтернативный рендерер **обязан** предоставить свою реализацию TextMeasurer.

### IContentProvider<T> -- привязка к SkiaSharp

```csharp
public interface IContentProvider<in TElement>
{
    SKBitmap Generate(TElement element);  // <-- возвращает SKBitmap!
}
```

Это **главная проблема для абстрагирования**: интерфейс провайдера контента жестко привязан к `SKBitmap`. QrProvider и BarcodeProvider возвращают `SKBitmap`, и весь пайплайн ожидает его.

---

## Часть 2: Фича 1 -- ImageSharp Rendering Engine

### 2.1 Мотивация

SkiaSharp имеет известные проблемы:
- **Нативные зависимости**: `libSkiaSharp.so` / `.dylib` / `.dll` -- разные пакеты для разных OS
- **Совместимость**: проблемы с Alpine Linux, musl, ARM (Raspberry Pi)
- **Поддержка**: проект Google/mono, неопределенный roadmap
- **Размер**: ~30-50 МБ нативных библиотек

ImageSharp (SixLabors) -- чистый .NET, нулевые нативные зависимости:
- Полностью управляемый код
- Работает везде где есть .NET runtime
- Активная поддержка
- AOT-совместимость (нужно проверить)

### 2.2 Оценка API ImageSharp

| Возможность | SkiaSharp | ImageSharp | Статус |
|-------------|-----------|------------|--------|
| Создание изображения | `new SKBitmap(w, h)` | `new Image<Rgba32>(w, h)` | Прямой аналог |
| Канвас для рисования | `SKCanvas` | `image.Mutate(ctx => ...)` | Другая парадигма (лямбда вместо объекта) |
| Заливка прямоугольника | `canvas.DrawRect(rect, paint)` | `ctx.Fill(color, rect)` | Прямой аналог |
| Рисование текста | `canvas.DrawText(text, x, y, font, paint)` | `ctx.DrawText(text, font, color, point)` | Аналог, но шрифты через SixLabors.Fonts |
| Измерение текста | `font.MeasureText(text)` | `TextMeasurer.MeasureSize(text, options)` | Другой API |
| Загрузка шрифтов | `SKTypeface.FromFile(path)` | `FontCollection.Add(path)` | Другая модель (коллекция, не загрузка по одному) |
| Градиенты | `SKShader.CreateLinearGradient(...)` | `LinearGradientBrush(...)` | Аналог |
| Скругленные углы | `canvas.ClipRoundRect(...)` | `ctx.Fill(brush, roundedRect)` | Нужна IPath |
| Тени | `SKImageFilter.CreateDropShadow(...)` | Нет встроенного -- ручная реализация | **Пробел** |
| Вращение | `canvas.RotateDegrees(...)` | `ctx.Transform(matrix)` | Аналог |
| Сохранение/восстановление | `canvas.Save()` / `canvas.Restore()` | Нет стека состояний -- через `ctx.SetDrawingTransform()` | **Архитектурное отличие** |
| Clipping | `canvas.ClipRect(...)` | `ctx.Clip(path)` через DrawingOptions | Аналог |
| Антиалиасинг текста | `SKFont.Edging` | Настройки через `TextOptions` | Другой механизм |
| PNG/JPEG/BMP кодирование | `image.Encode(format, quality)` | `image.Save(stream, encoder)` | Прямой аналог |
| Прозрачность | `SKPaint.Color.WithAlpha()` | `Color.WithAlpha()` или `DrawingOptions.BlendPercentage` | Аналог |

### 2.3 Необходимые NuGet пакеты

```xml
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.7" />
<PackageReference Include="SixLabors.ImageSharp.Drawing" Version="2.1.6" />
<PackageReference Include="SixLabors.Fonts" Version="2.1.1" />
```

**Важно**: `SixLabors.ImageSharp.Drawing` -- отдельный пакет для рисования примитивов (прямоугольники, линии, пути). Без него доступна только работа с пикселями.

### 2.4 AOT-совместимость ImageSharp

**Критический вопрос** -- проект требует `IsAotCompatible=true`.

Состояние на 2025:
- **SixLabors.ImageSharp 3.x**: AOT-совместимость заявлена (Source Generator для кодеков). Используют `[UnconditionalSuppressMessage]` для подавления предупреждений trimmer.
- **SixLabors.Fonts 2.x**: Использует рефлексию для чтения OpenType таблиц. Есть проблемы с AOT -- **нужна проверка**.
- **SixLabors.ImageSharp.Drawing 2.x**: Зависит от Fonts. Наследует те же проблемы.

**Вердикт**: AOT-совместимость ImageSharp **не полностью гарантирована** на уровне шрифтов. Это потенциальный блокер. Нужно тестирование:
```bash
dotnet publish -c Release -r osx-arm64 /p:PublishAot=true
```

### 2.5 Производительность

Общие ожидания (на основе бенчмарков сообщества):
- **Простые операции** (заливка, прямоугольники): ImageSharp медленнее в 2-5 раз (чистый .NET vs нативный C++)
- **Текст**: ImageSharp может быть сопоставим (SixLabors.Fonts хорошо оптимизирован)
- **Кодирование PNG/JPEG**: SkiaSharp значительно быстрее (нативные кодеки)
- **Память**: ImageSharp может потреблять больше для больших изображений

Для типичных шаблонов FlexRender (чеки, карточки, инфографика <4000x4000px) разница в производительности **вероятно приемлема** -- речь идет о миллисекундах vs десятках миллисекунд.

### 2.6 Архитектурные подходы

#### Подход A: Параллельный бэкенд (аналогично SvgRender)

Создать `FlexRender.ImageSharp` как отдельный проект, аналогичный `FlexRender.Skia`:

```
src/FlexRender.ImageSharp/          # ImageSharp renderer
  ImageSharpRender.cs               # IFlexRender impl
  ImageSharpBuilder.cs              # Builder с WithQr(), WithBarcode()
  FlexRenderBuilderExtensions.cs    # .WithImageSharp()
  Rendering/
    ImageSharpRenderingEngine.cs    # Рисование в Image<Rgba32>
    ImageSharpTextRenderer.cs       # Текст через SixLabors.Fonts
    ImageSharpFontManager.cs        # Управление шрифтами
```

Использование:
```csharp
var render = new FlexRenderBuilder()
    .WithImageSharp(is => is.WithQr())  // вместо .WithSkia()
    .Build();
```

**Плюсы**: Минимальные изменения в существующем коде. Чистое разделение.
**Минусы**: Дублирование логики (QrProvider, BarcodeProvider нужны ImageSharp-версии). `IContentProvider<T>` привязан к SKBitmap.

#### Подход B: Абстракция рендеринга в Core

Ввести абстрактный интерфейс рисования в Core:

```csharp
public interface IRenderCanvas
{
    void DrawRect(float x, float y, float w, float h, RenderPaint paint);
    void DrawText(string text, float x, float y, RenderFont font, RenderPaint paint);
    void Save();
    void Restore();
    void ClipRect(float x, float y, float w, float h);
    // ...
}
```

И реализовать его для Skia и ImageSharp.

**Плюсы**: Устраняет дублирование. Провайдеры контента работают через абстракцию.
**Минусы**: Огромный рефакторинг. Потеря производительности из-за абстракции. Сложно покрыть все нюансы обоих API. Leaky abstraction problem.

#### Подход C: Конвертация через промежуточный формат

ImageSharp рендерер работает с `byte[]` PNG вместо `SKBitmap` для провайдеров:

```csharp
// Новый интерфейс для рендерер-агностичных провайдеров
public interface IRawContentProvider<in TElement>
{
    byte[] GeneratePng(TElement element);
}
```

Или рефакторинг `IContentProvider<T>` для возврата `byte[]` вместо `SKBitmap`.

**Плюсы**: Умеренный рефакторинг. Провайдеры становятся бэкенд-агностичными.
**Минусы**: Дополнительное кодирование/декодирование PNG. Для Skia -- регрессия (сейчас SKBitmap передается напрямую без перекодирования).

### 2.7 Рекомендация

**Подход A (параллельный бэкенд)** -- наиболее прагматичный.

Обоснование:
1. Не требует рефакторинга существующего кода
2. FlexRender.Skia остается стабильным
3. ImageSharp может начать с подмножества фич и расти
4. Если AOT проблемы с ImageSharp -- не затрагивает основной продукт
5. Пользователи выбирают бэкенд через Builder API

Проблема `IContentProvider<T>` решается так:
- ImageSharp рендерер имеет **свой** аналог IContentProvider, возвращающий `Image<Rgba32>`
- Или QrProvider для ImageSharp просто генерирует QR в `Image<Rgba32>` напрямую через QRCoder (библиотека QRCoder не зависит от Skia -- она работает с boolean matrix)

### 2.8 Оценка трудоемкости (Подход A)

| Компонент | Сложность | Оценка |
|-----------|-----------|--------|
| ImageSharpRender (IFlexRender impl) | Средняя | 2 дня |
| ImageSharpRenderingEngine (отрисовка) | Высокая | 5-7 дней |
| ImageSharpTextRenderer + FontManager | Высокая | 3-4 дня |
| ImageSharpQrProvider | Низкая | 0.5 дня |
| ImageSharpBarcodeProvider | Низкая | 0.5 дня |
| Builder + Extensions | Низкая | 0.5 дня |
| Тесты (unit + snapshot) | Средняя | 3-4 дня |
| AOT тестирование и фиксы | Неизвестно | 1-3 дня |
| **Итого** | | **15-22 дня** |

### 2.9 Критические риски

1. **AOT совместимость SixLabors.Fonts** -- потенциальный блокер. Если шрифты используют рефлексию для парсинга OpenType таблиц, AOT publish сломается.
2. **Тени (box-shadow)** -- ImageSharp не имеет встроенного drop shadow фильтра. Придется реализовывать вручную через Gaussian blur.
3. **Canvas Save/Restore стек** -- ImageSharp использует другую модель. Нужна обертка с ручным стеком трансформаций.
4. **Pixel-perfect совпадение** -- рендеринг текста будет отличаться от SkiaSharp. Snapshot тесты потребуют отдельных golden images.
5. **Лицензия**: ImageSharp использует Apache 2.0 с коммерческим дополнением для SaaS. Для open-source проектов бесплатно, но для коммерческого SaaS нужна лицензия Six Labors.

---

## Часть 3: Фича 2 -- QR Code Direct SVG Output

### 3.1 Текущая реализация

Когда SVG рендерер встречает QR элемент:

```csharp
// SvgRenderingEngine.cs, строка 274-276
case QrElement qr when _qrProvider is not null:
    DrawBitmapElement(sb, _qrProvider.Generate(qr), x, y, width, height);
    break;
```

`DrawBitmapElement` делает:
1. `_qrProvider.Generate(qr)` -- генерирует `SKBitmap` с QR кодом
2. `SKImage.FromBitmap(bitmap)` -- конвертирует в SKImage
3. `image.Encode(SKEncodedImageFormat.Png, 100)` -- кодирует в PNG
4. `Convert.ToBase64String(data.ToArray())` -- кодирует в base64
5. Вставляет `<image href="data:image/png;base64,..." />` в SVG

**Проблемы**:
- QR код -- это набор черных и белых квадратов. Идеальный кандидат для SVG `<rect>` элементов
- Растеризация QR кода для SVG -- потеря векторности, масштабируемости, и качества
- Base64 PNG QR кода ~5-20 КБ vs SVG path ~1-3 КБ
- Зависимость от SkiaSharp для SVG-only рендеринга (через `IContentProvider<QrElement>`)

### 3.2 Библиотека QRCoder

Проект использует **QRCoder 1.7.0**. Эта библиотека:
- Чистый .NET (нет нативных зависимостей)
- `QRCodeGenerator.CreateQrCode()` возвращает `QRCodeData` с `ModuleMatrix` (bool[][])
- `ModuleMatrix` -- это матрица модулей (черный/белый) QR кода
- Библиотека имеет встроенные рендереры: `PngByteQRCode`, `SvgQRCode`, `AsciiQRCode`

**Критически важно**: QRCoder 1.7.0 включает класс `SvgQRCode`, который генерирует SVG напрямую из `QRCodeData`.

Однако `SvgQRCode` в стандартной поставке использует `System.Drawing` для некоторых операций. В пакете QRCoder есть также `QRCoder.SvgQRCode` который полностью чистый.

Но нам даже не нужен встроенный `SvgQRCode`. Мы можем сгенерировать SVG вручную из `ModuleMatrix`, что дает полный контроль.

### 3.3 Текущий QrProvider -- как работает

```csharp
// QrProvider.cs
public static SKBitmap Generate(QrElement element, int? layoutWidth, int? layoutHeight)
{
    using var qrGenerator = new QRCodeGenerator();
    using var qrCodeData = qrGenerator.CreateQrCode(element.Data, eccLevel);

    var moduleCount = qrCodeData.ModuleMatrix.Count;
    var moduleSize = targetSize / (float)moduleCount;

    var bitmap = new SKBitmap(targetSize, targetSize);
    using var canvas = new SKCanvas(bitmap);

    // Рисуем модули как прямоугольники на bitmap
    for (var y = 0; y < moduleCount; y++)
        for (var x = 0; x < moduleCount; x++)
            if (qrCodeData.ModuleMatrix[y][x])
                canvas.DrawRect(x * moduleSize, y * moduleSize, ...);

    return bitmap;
}
```

Видно, что `ModuleMatrix` -- это массив bool, доступный ДО растеризации. Мы можем использовать его напрямую для генерации SVG.

### 3.4 Архитектурные подходы

#### Подход A: SVG QR Provider в SvgRenderingEngine

Добавить метод `DrawQrElementAsSvg` напрямую в `SvgRenderingEngine`:

```csharp
case QrElement qr:
    DrawQrElementAsSvg(sb, qr, x, y, width, height);
    break;
```

Метод генерирует SVG `<rect>` элементы из `ModuleMatrix`:

```csharp
private static void DrawQrElementAsSvg(StringBuilder sb, QrElement qr,
    float x, float y, float width, float height)
{
    using var qrGenerator = new QRCodeGenerator();
    using var qrCodeData = qrGenerator.CreateQrCode(qr.Data, eccLevel);

    var moduleCount = qrCodeData.ModuleMatrix.Count;
    var moduleWidth = width / moduleCount;
    var moduleHeight = height / moduleCount;

    // Группа с фоном
    sb.Append("<g>");
    if (qr.Background is not null)
        sb.Append($"<rect x=\"{x}\" y=\"{y}\" width=\"{width}\" height=\"{height}\" fill=\"{qr.Background}\"/>");

    // Модули как <rect>
    for (var row = 0; row < moduleCount; row++)
        for (var col = 0; col < moduleCount; col++)
            if (qrCodeData.ModuleMatrix[row][col])
                sb.Append($"<rect x=\"{x + col * moduleWidth}\" y=\"{y + row * moduleHeight}\" " +
                          $"width=\"{moduleWidth}\" height=\"{moduleHeight}\" fill=\"{qr.Foreground}\"/>");
    sb.Append("</g>");
}
```

**Плюсы**: Простая реализация. Нет новых интерфейсов. Минимальные изменения.
**Минусы**: QR генерация дублируется (Skia и SVG). Зависимость от QRCoder в FlexRender.Svg.

#### Подход B: ISvgContentProvider<T> -- отдельный интерфейс для SVG

```csharp
// В FlexRender.Core или FlexRender.Svg
public interface ISvgContentProvider<in TElement>
{
    string GenerateSvg(TElement element, float width, float height);
}
```

QR provider реализует оба интерфейса:
```csharp
public sealed class QrProvider : IContentProvider<QrElement>, ISvgContentProvider<QrElement>
{
    public SKBitmap Generate(QrElement element) { ... }  // для Skia
    public string GenerateSvg(QrElement element, float width, float height) { ... }  // для SVG
}
```

SVG рендерер проверяет: если провайдер реализует `ISvgContentProvider`, использовать SVG; иначе fallback на bitmap.

**Плюсы**: Чистая архитектура. Расширяемо (бар-коды тоже могут генерировать SVG). Нет дублирования.
**Минусы**: Новый интерфейс. Больше изменений. Провайдер должен знать про оба мира.

#### Подход C: SVG генерация через path data

Вместо множества `<rect>` элементов, сгенерировать один `<path>` с оптимизированным path data:

```xml
<path d="M10,10h5v5h-5z M15,10h5v5h-5z ..." fill="#000000"/>
```

Это значительно компактнее, чем отдельные `<rect>` элементы.

**Плюсы**: Минимальный размер SVG. Один DOM-элемент вместо сотен.
**Минусы**: Более сложная генерация. Потенциально можно объединить смежные модули в полосы.

### 3.5 Рекомендация

**Подход A + элементы C** -- генерация SVG напрямую в SvgRenderingEngine, используя оптимизированный `<path>` вместо множества `<rect>`.

Обоснование:
1. Самый простой путь к результату
2. QRCoder -- чистый .NET, нет новых нативных зависимостей
3. Зависимость от QRCoder в FlexRender.Svg легко добавить (уже используется в FlexRender.QrCode)
4. Оптимизация через `<path>` -- важна, т.к. QR код V40 имеет 177x177 = 31,329 модулей
5. Fallback на bitmap через `DrawBitmapElement` можно оставить для случаев без QRCoder

**Однако** есть архитектурная проблема: `FlexRender.Svg` не зависит от `QRCoder`. Если мы добавим прямую зависимость, это нарушает модульность.

Лучшее решение: оставить QR генерацию в `FlexRender.QrCode`, но добавить метод, возвращающий SVG строку. SvgRenderingEngine будет проверять, поддерживает ли провайдер SVG выход:

```csharp
// Новый интерфейс в FlexRender.Skia (рядом с IContentProvider)
public interface ISvgContentProvider<in TElement>
{
    string GenerateSvg(TElement element, float x, float y, float width, float height);
}

// QrProvider реализует оба
public sealed class QrProvider : IContentProvider<QrElement>, ISvgContentProvider<QrElement>
```

### 3.6 Оценка трудоемкости

| Компонент | Сложность | Оценка |
|-----------|-----------|--------|
| ISvgContentProvider интерфейс | Низкая | 0.5 дня |
| QrProvider.GenerateSvg() | Средняя | 1 день |
| SvgRenderingEngine -- проверка ISvgContentProvider | Низкая | 0.5 дня |
| Path оптимизация (объединение смежных модулей) | Средняя | 1 день |
| Тесты | Средняя | 1 день |
| **Итого** | | **4 дня** |

### 3.7 Ожидаемый результат

QR код в SVG до (текущее):
```xml
<image x="10" y="10" width="200" height="200"
       href="data:image/png;base64,iVBORw0KGgo..."
       preserveAspectRatio="xMidYMid meet"/>
```
~10-20 КБ base64 данных

QR код в SVG после:
```xml
<path d="M12,12h8v8h-8zM20,12h8v8h-8z..." fill="#000000"/>
```
~1-3 КБ path data, полностью векторный, масштабируемый

---

## Часть 4: Зависимости и совместимость

### FlexRender.Svg зависимость от FlexRender.Skia

Текущая ситуация: `FlexRender.Svg` зависит от `FlexRender.Skia`:
- Использует `SKTypeface.FromFile()` для извлечения имен шрифтов
- Использует `SKBitmap`/`SKImage` для конвертации QR/Barcode в base64
- Использует `ColorParser`, `RotationHelper` из Skia

Для Feature 2 (QR SVG) это не проблема -- `FlexRender.Svg` уже зависит от Skia.

Для Feature 1 (ImageSharp) это означает: если пользователь хочет ImageSharp + SVG, ему все равно нужен SkiaSharp (для SVG шрифтов). Это **нежелательно**.

Долгосрочно: извлечь утилиты (ColorParser, RotationHelper) в Core, сделать SVG рендерер независимым от Skia. Но это за рамками текущих фич.

---

## Часть 5: Приоритизация

### Feature 2 (QR SVG) -- **высокий приоритет**
- Малый scope (4 дня)
- Немедленная польза: меньший размер SVG, лучшее качество, убирает ненужную растеризацию
- Устраняет зависимость SVG рендерера от QR растеризации
- Нет рисков с AOT

### Feature 1 (ImageSharp) -- **средний приоритет, высокий риск**
- Большой scope (15-22 дня)
- Риск AOT несовместимости SixLabors.Fonts
- Риск лицензионных проблем для коммерческих пользователей
- Нужны отдельные snapshot golden images
- Реальная потребность существует (пользователи жалуются на SkiaSharp нативные зависимости)

**Рекомендация**: начать с Feature 2, затем провести POC для Feature 1 (AOT тест + основные примитивы).

---

## Часть 6: Открытые вопросы

### Для Feature 1 (ImageSharp)
1. Готовы ли мы к тому, что ImageSharp рендерер будет выдавать визуально отличающийся результат от Skia? (разные алгоритмы антиалиасинга текста)
2. Нужна ли поддержка HarfBuzz для ImageSharp? (SixLabors.Fonts имеет встроенную поддержку OpenType shaping)
3. Какой минимальный набор фич для первого релиза? (текст + прямоугольники + изображения? Или все фичи сразу?)
4. Лицензия ImageSharp -- приемлема ли для коммерческих пользователей FlexRender?

### Для Feature 2 (QR SVG)
1. Нужен ли аналогичный подход для Barcode? (штрих-коды тоже могут быть SVG)
2. Оптимизация path: объединять смежные модули по горизонтали (полосы) или генерировать по одному rect?
3. Поддержка прозрачного фона QR кода в SVG?

---

## Резюме решений для валидации

| Решение | Выбор | Обоснование |
|---------|-------|-------------|
| Архитектура ImageSharp | Подход A: параллельный бэкенд | Минимальный риск, нет рефакторинга |
| QR SVG генерация | Подход A+C: прямая генерация через path | Простота + оптимальный размер |
| Интерфейс SVG контента | `ISvgContentProvider<T>` | Расширяемость для barcode |
| Приоритет | Feature 2 первая, Feature 1 как POC | Соотношение усилий/пользы |
