using System.Globalization;
using System.Text;

namespace FlexRender.Content.Ndc;

/// <summary>
/// Tokenizes NDC printer data streams into a sequence of <see cref="NdcToken"/> values.
/// </summary>
internal static class NdcTokenizer
{
    private const char ESC = '\x1B';
    private const char LF = '\n';
    private const char CR = '\r';
    private const char FF = '\x0C';
    private const char SO = '\x0E';
    private const char GS = '\x1D';
    private const char HT = '\x09';

    /// <summary>
    /// Tokenizes the input NDC data stream.
    /// </summary>
    internal static List<NdcToken> Tokenize(string input)
    {
        var tokens = new List<NdcToken>();
        if (input.Length == 0) return tokens;

        var textBuffer = new StringBuilder();
        var i = 0;

        while (i < input.Length)
        {
            var ch = input[i];

            switch (ch)
            {
                case ESC:
                    FlushText(tokens, textBuffer);
                    i++;
                    ParseEscSequence(input, ref i, tokens);
                    break;

                case LF:
                    FlushText(tokens, textBuffer);
                    tokens.Add(new NdcToken(NdcTokenType.LineFeed, ""));
                    i++;
                    break;

                case CR:
                    FlushText(tokens, textBuffer);
                    i++;
                    if (i < input.Length && input[i] == LF)
                        i++; // consume LF after CR
                    tokens.Add(new NdcToken(NdcTokenType.LineFeed, ""));
                    break;

                case FF:
                    FlushText(tokens, textBuffer);
                    tokens.Add(new NdcToken(NdcTokenType.FormFeed, ""));
                    i++;
                    break;

                case SO:
                    FlushText(tokens, textBuffer);
                    i++;
                    if (i < input.Length)
                    {
                        var spaceCount = GetSpaceCount(input[i]);
                        tokens.Add(new NdcToken(NdcTokenType.Spaces, spaceCount.ToString(CultureInfo.InvariantCulture)));
                        i++;
                    }
                    break;

                case GS:
                    FlushText(tokens, textBuffer);
                    i++;
                    var fieldId = i < input.Length ? input[i].ToString() : "";
                    if (i < input.Length) i++;
                    tokens.Add(new NdcToken(NdcTokenType.FieldSeparator, fieldId));
                    break;

                case HT:
                    FlushText(tokens, textBuffer);
                    tokens.Add(new NdcToken(NdcTokenType.HorizontalTab, ""));
                    i++;
                    break;

                default:
                    textBuffer.Append(ch);
                    i++;
                    break;
            }
        }

        FlushText(tokens, textBuffer);
        return tokens;
    }

    private static void FlushText(List<NdcToken> tokens, StringBuilder textBuffer)
    {
        if (textBuffer.Length > 0)
        {
            tokens.Add(new NdcToken(NdcTokenType.Text, textBuffer.ToString()));
            textBuffer.Clear();
        }
    }

    private static void ParseEscSequence(string input, ref int i, List<NdcToken> tokens)
    {
        if (i >= input.Length) return;

        var seqId = input[i];
        switch (seqId)
        {
            case '(': // ESC ( X -- Select Primary Print Page
                i++;
                if (i < input.Length)
                {
                    tokens.Add(new NdcToken(NdcTokenType.CharsetSwitch, input[i].ToString()));
                    i++;
                }
                break;

            case ')': // ESC ) X -- Select Secondary Print Page
                i++;
                if (i < input.Length)
                {
                    tokens.Add(new NdcToken(NdcTokenType.CharsetSwitch, input[i].ToString()));
                    i++;
                }
                break;

            case 'k': // ESC k <type> <data> ESC \ -- Print Barcode
                i++;
                if (i < input.Length)
                {
                    var barcodeType = input[i];
                    i++;
                    var dataBuf = new StringBuilder();
                    while (i < input.Length)
                    {
                        if (input[i] == ESC && i + 1 < input.Length && input[i + 1] == '\\')
                        {
                            i += 2; // skip ESC backslash
                            break;
                        }
                        dataBuf.Append(input[i]);
                        i++;
                    }
                    tokens.Add(new NdcToken(NdcTokenType.Barcode, $"{barcodeType}:{dataBuf}"));
                }
                break;

            case '/': // ESC / <x> <y> -- Print Downloadable Bit Image
                i++;
                var imgX = i < input.Length ? input[i].ToString() : "";
                if (i < input.Length) i++;
                var imgY = i < input.Length ? input[i].ToString() : "";
                if (i < input.Length) i++;
                tokens.Add(new NdcToken(NdcTokenType.PrintBitImage, $"{imgX}:{imgY}"));
                break;

            case 'G': // ESC G <filename> ESC \ -- Print Graphics
            {
                i++;
                var filenameBuf = new StringBuilder();
                while (i < input.Length)
                {
                    if (input[i] == ESC && i + 1 < input.Length && input[i + 1] == '\\')
                    {
                        i += 2;
                        break;
                    }
                    filenameBuf.Append(input[i]);
                    i++;
                }
                tokens.Add(new NdcToken(NdcTokenType.PrintGraphics, filenameBuf.ToString()));
                break;
            }

            case '%': // ESC % <3-digit-codepage> -- Select OS/2 Code Page
            {
                i++;
                var cpBuf = new StringBuilder();
                for (var j = 0; j < 3 && i < input.Length; j++)
                {
                    cpBuf.Append(input[i]);
                    i++;
                }
                tokens.Add(new NdcToken(NdcTokenType.SelectCodePage, cpBuf.ToString()));
                break;
            }

            case '2': // ESC 2 -- Select International Character Sets
                i++;
                tokens.Add(new NdcToken(NdcTokenType.SelectInternationalCharset, ""));
                break;

            case '3': // ESC 3 -- Select Arabic Character Sets
                i++;
                tokens.Add(new NdcToken(NdcTokenType.SelectArabicCharset, ""));
                break;

            case '[': // ESC [ <col> p/q/r -- Statement printer controls
            {
                i++;
                var valueBuf = new StringBuilder();
                while (i < input.Length)
                {
                    var c = input[i];
                    i++;
                    if (c == 'p')
                    {
                        tokens.Add(new NdcToken(NdcTokenType.SetLeftMargin, valueBuf.ToString()));
                        break;
                    }
                    if (c == 'q')
                    {
                        tokens.Add(new NdcToken(NdcTokenType.SetRightMargin, valueBuf.ToString()));
                        break;
                    }
                    if (c == 'r')
                    {
                        tokens.Add(new NdcToken(NdcTokenType.SetLinesPerInch, valueBuf.ToString()));
                        break;
                    }
                    valueBuf.Append(c);
                }
                break;
            }

            case 'p': // ESC p <side> <codeline> [<image>] [<chequeID>] ESC \ -- Print Cheque Image
            {
                i++;
                var chequeBuf = new StringBuilder();
                while (i < input.Length)
                {
                    if (input[i] == ESC && i + 1 < input.Length && input[i + 1] == '\\')
                    {
                        i += 2;
                        break;
                    }
                    chequeBuf.Append(input[i]);
                    i++;
                }
                tokens.Add(new NdcToken(NdcTokenType.PrintChequeImage, chequeBuf.ToString()));
                break;
            }

            case '&': // ESC & <filename> ESC \ -- Define Downloadable Character Set
            {
                i++;
                var defBuf = new StringBuilder();
                while (i < input.Length)
                {
                    if (input[i] == ESC && i + 1 < input.Length && input[i + 1] == '\\')
                    {
                        i += 2;
                        break;
                    }
                    defBuf.Append(input[i]);
                    i++;
                }
                tokens.Add(new NdcToken(NdcTokenType.DefineCharset, defBuf.ToString()));
                break;
            }

            case '*': // ESC * <1/2> <filename> ESC \ -- Define Downloadable Bit Image
            {
                i++;
                var defImgBuf = new StringBuilder();
                while (i < input.Length)
                {
                    if (input[i] == ESC && i + 1 < input.Length && input[i + 1] == '\\')
                    {
                        i += 2;
                        break;
                    }
                    defImgBuf.Append(input[i]);
                    i++;
                }
                tokens.Add(new NdcToken(NdcTokenType.DefineBitImage, defImgBuf.ToString()));
                break;
            }

            case 'e': // ESC e <pos> -- Select HRI Character Printing Position
                i++;
                if (i < input.Length)
                {
                    tokens.Add(new NdcToken(NdcTokenType.BarcodeHriPosition, input[i].ToString()));
                    i++;
                }
                break;

            case 'w': // ESC w <width> -- Select Width of Barcode
                i++;
                if (i < input.Length)
                {
                    tokens.Add(new NdcToken(NdcTokenType.BarcodeWidth, input[i].ToString()));
                    i++;
                }
                break;

            case 'h': // ESC h <3-digit-height> -- Select Horizontal Height of Barcode
            {
                i++;
                var heightBuf = new StringBuilder();
                for (var j = 0; j < 3 && i < input.Length; j++)
                {
                    heightBuf.Append(input[i]);
                    i++;
                }
                tokens.Add(new NdcToken(NdcTokenType.BarcodeHeight, heightBuf.ToString()));
                break;
            }

            case 'q': // ESC q <0/1> -- Select Dual-sided Printing
                i++;
                if (i < input.Length)
                {
                    tokens.Add(new NdcToken(NdcTokenType.DualSidedPrinting, input[i].ToString()));
                    i++;
                }
                break;

            default:
                // Unknown ESC sequence -- skip the sequence ID character
                i++;
                break;
        }
    }

    private static int GetSpaceCount(char ch) => ch switch
    {
        >= '1' and <= '9' => ch - '0',
        ':' => 10,
        ';' => 11,
        '<' => 12,
        '=' => 13,
        '>' => 14,
        '?' => 15,
        _ => 1
    };
}
