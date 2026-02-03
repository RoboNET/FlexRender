using System.Text;
using FlexRender.Parsing.Ast;
using FlexRender.Rendering;
using SkiaSharp;

namespace FlexRender.Providers;

/// <summary>
/// Provides barcode generation using pure SkiaSharp drawing.
/// </summary>
public sealed class BarcodeProvider : IContentProvider<BarcodeElement>
{
    private const int TextHeight = 16;
    private const int TextPadding = 4;

    /// <summary>
    /// Code 128 encoding table for character set B (ASCII 32-127).
    /// </summary>
    private static readonly Dictionary<char, string> Code128BPatterns = new()
    {
        { ' ', "11011001100" }, { '!', "11001101100" }, { '"', "11001100110" },
        { '#', "10010011000" }, { '$', "10010001100" }, { '%', "10001001100" },
        { '&', "10011001000" }, { '\'', "10011000100" }, { '(', "10001100100" },
        { ')', "11001001000" }, { '*', "11001000100" }, { '+', "11000100100" },
        { ',', "10110011100" }, { '-', "10011011100" }, { '.', "10011001110" },
        { '/', "10111001100" }, { '0', "10011101100" }, { '1', "10011100110" },
        { '2', "11001110010" }, { '3', "11001011100" }, { '4', "11001001110" },
        { '5', "11011100100" }, { '6', "11001110100" }, { '7', "11101101110" },
        { '8', "11101001100" }, { '9', "11100101100" }, { ':', "11100100110" },
        { ';', "11101100100" }, { '<', "11100110100" }, { '=', "11100110010" },
        { '>', "11011011000" }, { '?', "11011000110" }, { '@', "11000110110" },
        { 'A', "10100011000" }, { 'B', "10001011000" }, { 'C', "10001000110" },
        { 'D', "10110001000" }, { 'E', "10001101000" }, { 'F', "10001100010" },
        { 'G', "11010001000" }, { 'H', "11000101000" }, { 'I', "11000100010" },
        { 'J', "10110111000" }, { 'K', "10110001110" }, { 'L', "10001101110" },
        { 'M', "10111011000" }, { 'N', "10111000110" }, { 'O', "10001110110" },
        { 'P', "11101110110" }, { 'Q', "11010001110" }, { 'R', "11000101110" },
        { 'S', "11011101000" }, { 'T', "11011100010" }, { 'U', "11011101110" },
        { 'V', "11101011000" }, { 'W', "11101000110" }, { 'X', "11100010110" },
        { 'Y', "11101101000" }, { 'Z', "11101100010" }, { '[', "11100011010" },
        { '\\', "11101111010" }, { ']', "11001000010" }, { '^', "11110001010" },
        { '_', "10100110000" }, { '`', "10100001100" }, { 'a', "10010110000" },
        { 'b', "10010000110" }, { 'c', "10000101100" }, { 'd', "10000100110" },
        { 'e', "10110010000" }, { 'f', "10110000100" }, { 'g', "10011010000" },
        { 'h', "10011000010" }, { 'i', "10000110100" }, { 'j', "10000110010" },
        { 'k', "11000010010" }, { 'l', "11001010000" }, { 'm', "11110111010" },
        { 'n', "11000010100" }, { 'o', "10001111010" }, { 'p', "10100111100" },
        { 'q', "10010111100" }, { 'r', "10010011110" }, { 's', "10111100100" },
        { 't', "10011110100" }, { 'u', "10011110010" }, { 'v', "11110100100" },
        { 'w', "11110010100" }, { 'x', "11110010010" }, { 'y', "11011011110" },
        { 'z', "11011110110" }, { '{', "11110110110" }, { '|', "10101111000" },
        { '}', "10100011110" }, { '~', "10001011110" }
    };

    /// <summary>
    /// Code 128 start pattern for code set B.
    /// </summary>
    private const string Code128StartB = "11010010000";

    /// <summary>
    /// Code 128 stop pattern.
    /// </summary>
    private const string Code128Stop = "1100011101011";

    /// <summary>
    /// Generates a barcode bitmap from the specified element configuration.
    /// </summary>
    /// <param name="element">The barcode element configuration.</param>
    /// <returns>A bitmap containing the rendered barcode.</returns>
    /// <exception cref="ArgumentNullException">Thrown when element is null.</exception>
    /// <exception cref="ArgumentException">Thrown when element data is empty or dimensions are invalid.</exception>
    /// <exception cref="NotSupportedException">Thrown when the barcode format is not supported.</exception>
    public SKBitmap Generate(BarcodeElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        if (string.IsNullOrEmpty(element.Data))
        {
            throw new ArgumentException("Barcode data cannot be empty.", nameof(element));
        }

        if (element.BarcodeWidth <= 0 || element.BarcodeHeight <= 0)
        {
            throw new ArgumentException("Barcode dimensions must be positive.", nameof(element));
        }

        return element.Format switch
        {
            BarcodeFormat.Code128 => GenerateCode128(element),
            _ => throw new NotSupportedException($"Barcode format '{element.Format}' is not yet supported.")
        };
    }

    /// <summary>
    /// Generates a Code 128 barcode.
    /// </summary>
    /// <param name="element">The barcode element configuration.</param>
    /// <returns>A bitmap containing the rendered Code 128 barcode.</returns>
    private static SKBitmap GenerateCode128(BarcodeElement element)
    {
        // Validate that all characters are supported
        foreach (var c in element.Data)
        {
            if (!Code128BPatterns.ContainsKey(c))
            {
                throw new ArgumentException(
                    $"Character '{c}' (ASCII {(int)c}) is not supported in Code 128B. Supported range: ASCII 32-126.",
                    nameof(element));
            }
        }

        // Build the barcode pattern using StringBuilder for performance
        var patternBuilder = new StringBuilder(Code128StartB);

        // Calculate checksum while building pattern
        var checksum = 104; // Start B code value
        var position = 1;

        foreach (var c in element.Data)
        {
            patternBuilder.Append(Code128BPatterns[c]);
            var codeValue = c - 32; // Convert ASCII to Code 128 value
            checksum += codeValue * position;
            position++;
        }

        // Add checksum character
        checksum %= 103;
        var checksumChar = (char)(checksum + 32);
        if (Code128BPatterns.TryGetValue(checksumChar, out var checksumPattern))
        {
            patternBuilder.Append(checksumPattern);
        }

        patternBuilder.Append(Code128Stop);
        var pattern = patternBuilder.ToString();

        // Calculate bar dimensions
        var totalUnits = pattern.Length;
        var barWidth = element.BarcodeWidth / (float)totalUnits;
        var barcodeHeight = element.ShowText
            ? element.BarcodeHeight - TextHeight - TextPadding
            : element.BarcodeHeight;

        var bitmap = new SKBitmap(element.BarcodeWidth, element.BarcodeHeight);
        using var canvas = new SKCanvas(bitmap);

        var foreground = ColorParser.Parse(element.Foreground);
        var background = element.Background is not null
            ? ColorParser.Parse(element.Background)
            : SKColors.White;

        // Fill background
        canvas.Clear(background);

        using var barPaint = new SKPaint
        {
            Color = foreground,
            IsAntialias = false,
            Style = SKPaintStyle.Fill
        };

        // Draw bars
        var x = 0f;
        foreach (var bit in pattern)
        {
            if (bit == '1')
            {
                canvas.DrawRect(x, 0, barWidth, barcodeHeight, barPaint);
            }
            x += barWidth;
        }

        // Draw text if enabled
        if (element.ShowText)
        {
            using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal) ?? SKTypeface.Default;
            using var textFont = new SKFont(typeface, TextHeight - 2)
            {
                Subpixel = true
            };
            using var textPaint = new SKPaint
            {
                Color = foreground,
                IsAntialias = true
            };

            var textY = barcodeHeight + TextPadding + TextHeight - 2;
            canvas.DrawText(element.Data, element.BarcodeWidth / 2f, textY, SKTextAlign.Center, textFont, textPaint);
        }

        return bitmap;
    }
}
