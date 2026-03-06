namespace FlexRender.Content.Ndc;

/// <summary>
/// Types of tokens produced by the NDC tokenizer.
/// </summary>
internal enum NdcTokenType
{
    Text,
    LineFeed,
    FormFeed,
    CharsetSwitch,
    Spaces,
    FieldSeparator,
    Barcode,
    HorizontalTab,
    SetLeftMargin,
    SetRightMargin,
    SetLinesPerInch,
    SelectCodePage,
    SelectInternationalCharset,
    SelectArabicCharset,
    BarcodeHriPosition,
    BarcodeWidth,
    BarcodeHeight,
    PrintGraphics,
    PrintBitImage,
    PrintChequeImage,
    DefineCharset,
    DefineBitImage,
    DualSidedPrinting
}

/// <summary>
/// A single token from the NDC data stream.
/// </summary>
/// <param name="Type">The token type.</param>
/// <param name="Value">The token value (text content, charset designator, space count, barcode type:data).</param>
internal readonly record struct NdcToken(NdcTokenType Type, string Value);
