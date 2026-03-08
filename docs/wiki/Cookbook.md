# Cookbook

Practical recipes for common FlexRender use cases. Each recipe is a complete, copy-paste-ready YAML template with the data object needed to render it.

For element properties reference, see [[Element-Reference]].
For expression syntax, see [[Template-Expressions]].
For flexbox layout, see [[Flexbox-Layout]].
For CLI usage, see [[CLI-Reference]].

## Quick Start: CLI Rendering

Every recipe below can be rendered using the `flexrender` CLI. Save the template as a `.yaml` file and the data as a `.json` file, then run:

```bash
# Render to PNG
flexrender render template.yaml -d data.json -o output.png

# Render to JPEG (85% quality)
flexrender render template.yaml -d data.json -o output.jpg --quality 85

# BMP monochrome for thermal printers
flexrender render template.yaml -d data.json -o output.bmp --bmp-color monochrome1

# Validate template without rendering
flexrender validate template.yaml

# Watch for changes and re-render automatically
flexrender watch template.yaml -d data.json -o preview.png

# Debug layout (shows element bounds)
flexrender debug-layout template.yaml -d data.json

# With custom fonts directory
flexrender render template.yaml -d data.json -o output.png --fonts ./assets/fonts

# Scale 2x (retina)
flexrender render template.yaml -d data.json -o output.png --scale 2.0
```

If running from source (without installing the dotnet tool):

```bash
dotnet run --project src/FlexRender.Cli -- render template.yaml -d data.json -o output.png
```

---

## Receipts

### Simple Receipt with Header and Footer

A minimal thermal receipt with a shop header, static line items, a total row, and a footer. Uses a 380px canvas width, which is typical for 80mm thermal printers at ~203 DPI.

**Template:**

```yaml
template:
  name: "simple-receipt"
  version: 1

fonts:
  - "assets/fonts/Inter-Regular.ttf"

canvas:
  fixed: width
  width: 380
  background: "#ffffff"

layout:
  - type: flex
    padding: "24 20"
    gap: 12
    children:
      # Header
      - type: flex
        gap: 4
        align: center
        children:
          - type: text
            content: "{{shopName}}"
            fontWeight: bold
            size: 1.5em
            align: center
            color: "#1a1a1a"

          - type: text
            content: "{{address}}"
            size: 0.85em
            align: center
            color: "#888888"

      - type: separator
        style: dashed
        color: "#cccccc"

      # Line items
      - type: flex
        gap: 6
        children:
          - type: flex
            direction: row
            justify: space-between
            children:
              - type: text
                content: "Espresso"
                color: "#333333"
              - type: text
                content: "3.50 $"
                color: "#333333"

          - type: flex
            direction: row
            justify: space-between
            children:
              - type: text
                content: "Croissant"
                color: "#333333"
              - type: text
                content: "4.00 $"
                color: "#333333"

      - type: separator
        style: solid
        color: "#1a1a1a"

      # Total
      - type: flex
        direction: row
        justify: space-between
        children:
          - type: text
            content: "TOTAL"
            fontWeight: bold
            size: 1.2em
          - type: text
            content: "{{total}} $"
            fontWeight: bold
            size: 1.2em

      - type: separator
        style: dotted
        color: "#cccccc"

      # Footer
      - type: flex
        gap: 2
        children:
          - type: text
            content: "Thank you for your purchase!"
            size: 0.85em
            align: center
            color: "#666666"

          - type: text
            content: "{{date}}"
            size: 0.75em
            align: center
            color: "#999999"
```

**Data:**

```json
{
  "shopName": "Corner Cafe",
  "address": "123 Main St, Springfield",
  "total": "7.50",
  "date": "2026-03-08 14:30"
}
```

**CLI:**

```bash
flexrender render simple-receipt.yaml -d receipt-data.json -o receipt.png

# For thermal printer (monochrome BMP)
flexrender render simple-receipt.yaml -d receipt-data.json -o receipt.bmp --bmp-color monochrome1
```

---

### Receipt with Dynamic Items

Uses `type: each` to loop over a line-items array, so the same template works regardless of how many items are in the order. Each row is a flex container with `space-between` to push names and prices to opposite edges.

**Template:**

```yaml
template:
  name: "dynamic-receipt"
  version: 1

fonts:
  - "assets/fonts/Inter-Regular.ttf"

canvas:
  fixed: width
  width: 380
  background: "#ffffff"

layout:
  - type: flex
    padding: "24 20"
    gap: 12
    children:
      - type: text
        content: "{{shopName}}"
        fontWeight: bold
        size: 1.5em
        align: center

      - type: separator
        style: dashed
        color: "#cccccc"

      # Dynamic line items
      - type: each
        array: items
        as: item
        children:
          - type: flex
            direction: row
            justify: space-between
            children:
              - type: flex
                gap: 2
                shrink: 1
                children:
                  - type: text
                    content: "{{item.name}}"
                    color: "#333333"
                  - type: if
                    condition: item.qty
                    greaterThan: 1
                    then:
                      - type: text
                        content: "x{{item.qty}}"
                        size: 0.8em
                        color: "#888888"
              - type: text
                content: "{{item.price}} $"
                color: "#333333"

      - type: separator
        style: solid
        color: "#1a1a1a"

      # Subtotal, discount, total
      - type: if
        condition: discount
        greaterThan: 0
        then:
          - type: flex
            gap: 4
            children:
              - type: flex
                direction: row
                justify: space-between
                children:
                  - type: text
                    content: "Subtotal"
                    size: 0.9em
                    color: "#666666"
                  - type: text
                    content: "{{subtotal}} $"
                    size: 0.9em
                    color: "#666666"
              - type: flex
                direction: row
                justify: space-between
                children:
                  - type: text
                    content: "Discount"
                    size: 0.9em
                    color: "#22c55e"
                  - type: text
                    content: "-{{discount}} $"
                    size: 0.9em
                    color: "#22c55e"
              - type: separator
                style: dashed
                color: "#cccccc"

      - type: flex
        direction: row
        justify: space-between
        children:
          - type: text
            content: "TOTAL"
            fontWeight: bold
            size: 1.2em
          - type: text
            content: "{{total}} $"
            fontWeight: bold
            size: 1.2em

      - type: separator
        style: dotted
        color: "#cccccc"

      - type: text
        content: "{{date}}"
        size: 0.75em
        align: center
        color: "#999999"
```

**Data:**

```json
{
  "shopName": "Corner Cafe",
  "items": [
    { "name": "Espresso", "qty": 2, "price": "7.00" },
    { "name": "Croissant", "qty": 1, "price": "4.00" },
    { "name": "Orange Juice", "qty": 1, "price": "5.50" }
  ],
  "subtotal": "16.50",
  "discount": "1.50",
  "total": "15.00",
  "date": "2026-03-08 14:30"
}
```

**CLI:**

```bash
flexrender render dynamic-receipt.yaml -d order-data.json -o receipt.png
```

---

### Receipt with Table

Uses `type: table` for a cleaner column-aligned layout. The table element is expanded into flex rows at render time, so no additional packages are needed. Includes a static summary table for subtotal, tax, and total.

**Template:**

```yaml
template:
  name: "table-receipt"
  version: 1

fonts:
  - "assets/fonts/Inter-Regular.ttf"

canvas:
  fixed: width
  width: 380
  background: "#ffffff"

layout:
  - type: flex
    padding: "24 20"
    gap: 14
    children:
      - type: flex
        gap: 4
        align: center
        children:
          - type: text
            content: "{{companyName}}"
            fontWeight: bold
            size: 1.4em
            align: center
          - type: text
            content: "Invoice #{{invoiceNumber}}"
            size: 0.9em
            align: center
            color: "#666666"

      - type: separator
        style: solid
        color: "#e0e0e0"

      # Dynamic line items table
      - type: table
        array: items
        as: item
        size: 0.9em
        color: "#333333"
        row-gap: "2"
        column-gap: "8"
        header-fontWeight: semi-bold
        header-color: "#1a1a1a"
        header-size: 0.85em
        header-border-bottom: dashed
        columns:
          - key: description
            label: "Item"
            grow: 1
          - key: qty
            label: "Qty"
            width: "36"
            align: center
          - key: price
            label: "Price"
            width: "64"
            align: right

      - type: separator
        style: solid
        color: "#e0e0e0"

      # Summary (static rows)
      - type: table
        size: 0.9em
        color: "#555555"
        row-gap: "4"
        columns:
          - key: label
            grow: 1
          - key: value
            width: "80"
            align: right
        rows:
          - label: "Subtotal"
            value: "{{subtotal}}"
          - label: "Tax ({{taxRate}})"
            value: "{{tax}}"
          - label: "TOTAL"
            value: "{{total}}"
            fontWeight: bold
            color: "#1a1a1a"
            size: "1.1em"

      - type: text
        content: "Thank you for your business!"
        size: 0.8em
        align: center
        color: "#999999"
```

**Data:**

```json
{
  "companyName": "Acme Corp",
  "invoiceNumber": "INV-2026-0042",
  "items": [
    { "description": "Widget A", "qty": "3", "price": "$29.97" },
    { "description": "Gadget B", "qty": "1", "price": "$24.99" },
    { "description": "Cable C", "qty": "5", "price": "$14.95" }
  ],
  "subtotal": "$69.91",
  "taxRate": "8%",
  "tax": "$5.59",
  "total": "$75.50"
}
```

**CLI:**

```bash
flexrender render table-receipt.yaml -d invoice-data.json -o invoice.png

# JPEG for email attachment
flexrender render table-receipt.yaml -d invoice-data.json -o invoice.jpg --quality 90
```

---

### Receipt with NDC Content

NDC (NCR Direct Connect) is a binary protocol used by ATM terminals to format printer output. The `content` element with `format: ndc` parses these binary data streams into FlexRender elements. This is useful when rendering ATM receipt images from raw transaction data captured by banking middleware.

The NDC parser requires `FlexRender.Content.Ndc` and `.WithNdc()` on the builder. A monospaced font (such as JetBrains Mono or Courier) is recommended for accurate column alignment.

**Template:**

```yaml
fonts:
  default: "assets/fonts/JetBrainsMono-Regular.ttf"
  bold: "assets/fonts/JetBrainsMono-Bold.ttf"

canvas:
  fixed: width
  width: 576
  background: "#ffffff"

layout:
  # Bank header (static)
  - type: flex
    padding: "16 20"
    gap: 4
    align: center
    children:
      - type: text
        content: "{{bankName}}"
        fontWeight: bold
        size: 1.2em
        align: center
      - type: text
        content: "ATM #{{atmId}}"
        size: 0.8em
        color: "#666666"
        align: center

  - type: separator
    style: solid
    color: "#cccccc"

  # NDC receipt body (parsed from binary data)
  - type: content
    source: "{{receiptData}}"
    format: ndc
    options:
      columns: 40
      input_encoding: latin1
      font_family: "JetBrains Mono"
      charsets:
        "1":
          encoding: "qwerty-jcuken"
          font_style: bold
        "I":
          font: bold
          uppercase: true

  - type: separator
    style: solid
    color: "#cccccc"

  # Footer (static)
  - type: flex
    padding: "12 20"
    gap: 2
    children:
      - type: text
        content: "{{date}}"
        size: 0.75em
        align: center
        color: "#999999"
      - type: text
        content: "Please retain this receipt"
        size: 0.75em
        align: center
        color: "#999999"
```

**Data (C#):**

```csharp
var data = new ObjectValue
{
    ["bankName"] = "First National Bank",
    ["atmId"] = "ATM-0042",
    ["receiptData"] = new BytesValue(ndcBinaryBytes),
    ["date"] = "2026-03-08 09:15:33"
};
```

**Builder setup:**

```csharp
var render = new FlexRenderBuilder()
    .WithNdc()
    .WithSkia()
    .Build();
```

> **Note:** NDC receipts use binary data (`BytesValue`), so they must be rendered through the C# API. The CLI does not support binary data inputs. For text-based NDC content, you can use `base64:` prefix in the JSON data:
>
> ```json
> { "receiptData": "base64:PFN0YXJ0PjxOREMgZGF0YT4..." }
> ```
>
> ```bash
> flexrender render ndc-receipt.yaml -d ndc-data.json -o atm-receipt.png
> ```

---

### Receipt with QR Code

Adds a scannable QR code at the bottom for a payment link or digital receipt URL. The QR code is centered using a flex container with `align: center`.

**Template:**

```yaml
template:
  name: "receipt-qr"
  version: 1

fonts:
  - "assets/fonts/Inter-Regular.ttf"

canvas:
  fixed: width
  width: 380
  background: "#ffffff"

layout:
  - type: flex
    padding: "24 20"
    gap: 12
    children:
      - type: text
        content: "{{shopName}}"
        fontWeight: bold
        size: 1.5em
        align: center

      - type: separator
        style: dashed
        color: "#cccccc"

      - type: each
        array: items
        as: item
        children:
          - type: flex
            direction: row
            justify: space-between
            children:
              - type: text
                content: "{{item.name}}"
                color: "#333333"
              - type: text
                content: "{{item.price}} $"
                color: "#333333"

      - type: separator
        style: solid
        color: "#1a1a1a"

      - type: flex
        direction: row
        justify: space-between
        children:
          - type: text
            content: "TOTAL"
            fontWeight: bold
            size: 1.2em
          - type: text
            content: "{{total}} $"
            fontWeight: bold
            size: 1.2em

      - type: separator
        style: dotted
        color: "#cccccc"

      # QR code centered
      - type: flex
        align: center
        gap: 6
        children:
          - type: qr
            data: "{{paymentUrl}}"
            size: 140
            errorCorrection: M

          - type: text
            content: "Scan to pay"
            size: 0.75em
            color: "#999999"
            align: center

      - type: separator
        style: dotted
        color: "#cccccc"

      - type: text
        content: "Thank you!"
        size: 0.85em
        align: center
        color: "#666666"
```

**Data:**

```json
{
  "shopName": "Corner Cafe",
  "items": [
    { "name": "Espresso", "price": "3.50" },
    { "name": "Croissant", "price": "4.00" }
  ],
  "total": "7.50",
  "paymentUrl": "https://pay.example.com/invoice/abc123"
}
```

**Builder setup** (QR requires `FlexRender.QrCode`):

```csharp
var render = new FlexRenderBuilder()
    .WithSkia(skia => skia.WithQr())
    .Build();
```

**CLI:**

```bash
flexrender render receipt-qr.yaml -d payment-data.json -o receipt-qr.png
```

---

## Labels and Tickets

### Product Label with Barcode

A compact product label with name, description, price, and a Code128 barcode. Uses `margin: "0 auto"` to center the barcode horizontally. The barcode requires `FlexRender.Barcode` and `.WithBarcode()`.

**Template:**

```yaml
template:
  name: "product-label"
  version: 1

canvas:
  fixed: width
  width: 200
  background: "#ffffff"

layout:
  - type: flex
    padding: "12 10"
    gap: 6
    children:
      - type: text
        content: "{{productName}}"
        fontWeight: bold
        size: 1.1em
        align: center
        maxLines: 2
        overflow: ellipsis

      - type: text
        content: "{{description}}"
        size: 0.85em
        color: "#666666"
        align: center
        maxLines: 2

      - type: text
        content: "{{price}}"
        fontWeight: bold
        size: 1.3em
        color: "#cc0000"
        align: center

      - type: barcode
        data: "{{sku}}"
        format: code128
        width: 180
        height: 40
        showText: true
        margin: "0 auto"
```

**Data:**

```json
{
  "productName": "Organic Green Tea",
  "description": "Premium loose leaf, 100g",
  "price": "$12.99",
  "sku": "TEA-GRN-100"
}
```

**CLI:**

```bash
flexrender render product-label.yaml -d product-data.json -o label.png

# Scale 2x for high-DPI label printers
flexrender render product-label.yaml -d product-data.json -o label.png --scale 2.0
```

---

### Event Ticket

A two-section ticket with a dark header for the event name, a white body for date/time/seat details, and a QR code section separated by a dashed tear line. Section/row/seat info uses small info cards with a label-value pattern.

**Template:**

```yaml
template:
  name: "event-ticket"
  version: 1

fonts:
  - "assets/fonts/Inter-Regular.ttf"

canvas:
  fixed: width
  width: 360
  background: "#f0f0f5"

layout:
  # Event header
  - type: flex
    background: "#1a1a2e"
    padding: "24 28 20 28"
    gap: 6
    children:
      - type: text
        content: "{{eventName}}"
        fontWeight: bold
        size: 1.5em
        align: center
        color: "#ffffff"
        maxLines: 2

      - type: text
        content: "{{venue}}"
        size: 0.95em
        align: center
        color: "#8888aa"

  # Details
  - type: flex
    padding: "16 28"
    gap: 12
    background: "#ffffff"
    children:
      - type: flex
        direction: row
        gap: 12
        children:
          - type: flex
            grow: 1
            background: "#f5f5fa"
            padding: "10 14"
            gap: 2
            children:
              - type: text
                content: "DATE"
                size: 0.7em
                color: "#888888"
              - type: text
                content: "{{date}}"
                fontWeight: semi-bold
                size: 1.05em

          - type: flex
            grow: 1
            background: "#f5f5fa"
            padding: "10 14"
            gap: 2
            children:
              - type: text
                content: "TIME"
                size: 0.7em
                color: "#888888"
              - type: text
                content: "{{time}}"
                fontWeight: semi-bold
                size: 1.05em

      - type: flex
        direction: row
        gap: 8
        children:
          - type: flex
            grow: 1
            background: "#f5f5fa"
            padding: "10 14"
            gap: 2
            align: center
            children:
              - type: text
                content: "SECTION"
                size: 0.7em
                color: "#888888"
              - type: text
                content: "{{section}}"
                fontWeight: bold
                size: 1.3em

          - type: flex
            grow: 1
            background: "#f5f5fa"
            padding: "10 14"
            gap: 2
            align: center
            children:
              - type: text
                content: "ROW"
                size: 0.7em
                color: "#888888"
              - type: text
                content: "{{row}}"
                fontWeight: bold
                size: 1.3em

          - type: flex
            grow: 1
            background: "#f5f5fa"
            padding: "10 14"
            gap: 2
            align: center
            children:
              - type: text
                content: "SEAT"
                size: 0.7em
                color: "#888888"
              - type: text
                content: "{{seat}}"
                fontWeight: bold
                size: 1.3em

  # Tear line
  - type: separator
    style: dashed
    color: "#cccccc"

  # QR code
  - type: flex
    padding: "16 28 20 28"
    gap: 8
    align: center
    background: "#ffffff"
    children:
      - type: qr
        data: "{{ticketId}}"
        size: 140
        errorCorrection: H

      - type: text
        content: "{{ticketId}}"
        size: 0.7em
        color: "#aaaaaa"
        align: center

      - type: separator
        style: dotted
        color: "#dddddd"

      - type: text
        content: "Present this ticket at the entrance"
        size: 0.75em
        align: center
        color: "#888888"
```

**Data:**

```json
{
  "eventName": "Symphony Orchestra: Beethoven's 9th",
  "venue": "Grand Concert Hall",
  "date": "Mar 15, 2026",
  "time": "7:30 PM",
  "section": "A",
  "row": "12",
  "seat": "7",
  "ticketId": "TKT-2026-0315-A12S07"
}
```

**CLI:**

```bash
flexrender render event-ticket.yaml -d ticket-data.json -o ticket.png

# JPEG for web/email
flexrender render event-ticket.yaml -d ticket-data.json -o ticket.jpg --quality 95
```

---

## Advanced Patterns

### Conditional Content

Uses `type: if` with `elseIf` to render different content based on data values. This example shows a payment status indicator that changes appearance depending on whether the payment is complete, pending, or failed.

**Template:**

```yaml
template:
  name: "conditional-receipt"
  version: 1

fonts:
  - "assets/fonts/Inter-Regular.ttf"

canvas:
  fixed: width
  width: 380
  background: "#ffffff"

layout:
  - type: flex
    padding: "24 20"
    gap: 12
    children:
      - type: text
        content: "Order #{{orderId}}"
        fontWeight: bold
        size: 1.3em
        align: center

      - type: separator
        style: dashed
        color: "#cccccc"

      - type: each
        array: items
        as: item
        children:
          - type: flex
            direction: row
            justify: space-between
            children:
              - type: text
                content: "{{item.name}}"
                color: "#333333"
              - type: text
                content: "{{item.price}} $"
                color: "#333333"

      - type: separator
        style: solid
        color: "#1a1a1a"

      - type: flex
        direction: row
        justify: space-between
        children:
          - type: text
            content: "TOTAL"
            fontWeight: bold
            size: 1.2em
          - type: text
            content: "{{total}} $"
            fontWeight: bold
            size: 1.2em

      - type: separator
        style: dotted
        color: "#cccccc"

      # Payment status -- changes based on data
      - type: if
        condition: status
        equals: "paid"
        then:
          - type: flex
            padding: "10"
            background: "#d4edda"
            border-radius: "6"
            align: center
            children:
              - type: text
                content: "PAID"
                fontWeight: bold
                color: "#155724"
                align: center
        elseIf:
          condition: status
          equals: "pending"
          then:
            - type: flex
              padding: "10"
              align: center
              gap: 8
              children:
                - type: flex
                  padding: "8"
                  background: "#fff3cd"
                  border-radius: "6"
                  align: center
                  children:
                    - type: text
                      content: "Payment Pending"
                      fontWeight: bold
                      color: "#856404"
                      align: center
                - type: qr
                  data: "{{paymentUrl}}"
                  size: 120
                  errorCorrection: M
                - type: text
                  content: "Scan to complete payment"
                  size: 0.75em
                  color: "#999999"
                  align: center
          else:
            - type: flex
              padding: "10"
              background: "#f8d7da"
              border-radius: "6"
              align: center
              children:
                - type: text
                  content: "Payment Failed"
                  fontWeight: bold
                  color: "#721c24"
                  align: center
                - type: text
                  content: "Please contact support"
                  size: 0.85em
                  color: "#721c24"
                  align: center
```

**Data (paid):**

```json
{
  "orderId": "ORD-9876",
  "items": [
    { "name": "Widget", "price": "19.99" },
    { "name": "Gadget", "price": "34.99" }
  ],
  "total": "54.98",
  "status": "paid"
}
```

**Data (pending):**

```json
{
  "orderId": "ORD-9877",
  "items": [
    { "name": "Widget", "price": "19.99" }
  ],
  "total": "19.99",
  "status": "pending",
  "paymentUrl": "https://pay.example.com/ORD-9877"
}
```

**CLI:**

```bash
# Render with "paid" status
flexrender render conditional-receipt.yaml -d paid-order.json -o receipt-paid.png

# Render with "pending" status (includes QR code)
flexrender render conditional-receipt.yaml -d pending-order.json -o receipt-pending.png
```

---

### Markdown Content in a Receipt

Uses the `content` element with `format: markdown` to render a free-form body from data. This is useful when the receipt body is authored elsewhere (CMS, API, database) and arrives as Markdown text. Requires `FlexRender.Content.Markdown` and `.WithMarkdown()`.

**Template:**

```yaml
template:
  name: "markdown-receipt"
  version: 1

canvas:
  fixed: width
  width: 400
  background: "#ffffff"

layout:
  - type: text
    content: "{{title}}"
    fontWeight: bold
    size: 1.5em
    align: center
    padding: "20 16 8 16"

  - type: separator
    color: "#e0e0e0"

  # Dynamic markdown body
  - type: content
    source: "{{body}}"
    format: markdown
    padding: "12 16"

  - type: separator
    color: "#e0e0e0"

  - type: text
    content: "Generated by FlexRender"
    size: 0.8em
    color: "#999999"
    align: center
    padding: "8 16 16 16"
```

**Data:**

```json
{
  "title": "Order Summary",
  "body": "## Items\n\n- Espresso x2 -- $7.00\n- **Croissant** -- $4.00\n\n---\n\n> **Total: $11.00**\n\nThank you for your order!"
}
```

**CLI:**

```bash
flexrender render markdown-receipt.yaml -d markdown-data.json -o receipt-md.png
```

---

### Multi-language Receipt

Sets the `culture` property on the template metadata to control how numbers and dates are formatted by expression filters. The `currency` and `number` filters respect the active culture, so `{{amount | currency}}` produces locale-appropriate output without template changes.

**Template:**

```yaml
template:
  name: "multi-lang-receipt"
  version: 1
  culture: "de-DE"

fonts:
  - "assets/fonts/Inter-Regular.ttf"

canvas:
  fixed: width
  width: 380
  background: "#ffffff"

layout:
  - type: flex
    padding: "24 20"
    gap: 12
    children:
      - type: text
        content: "{{shopName}}"
        fontWeight: bold
        size: 1.5em
        align: center

      - type: separator
        style: dashed
        color: "#cccccc"

      - type: each
        array: items
        as: item
        children:
          - type: flex
            direction: row
            justify: space-between
            children:
              - type: text
                content: "{{item.name}}"
                color: "#333333"
              - type: text
                content: "{{item.price | currency}}"
                color: "#333333"

      - type: separator
        style: solid
        color: "#1a1a1a"

      - type: flex
        direction: row
        justify: space-between
        children:
          - type: text
            content: "GESAMT"
            fontWeight: bold
            size: 1.2em
          - type: text
            content: "{{total | currency}}"
            fontWeight: bold
            size: 1.2em

      - type: text
        content: "Vielen Dank!"
        size: 0.85em
        align: center
        color: "#666666"
```

**Data:**

```json
{
  "shopName": "Berliner Kaffeehaus",
  "items": [
    { "name": "Espresso", "price": 3.50 },
    { "name": "Berliner", "price": 2.80 }
  ],
  "total": 6.30
}
```

With `culture: "de-DE"`, the `currency` filter formats `3.50` as `3,50 EUR` (locale-dependent). Changing `culture` to `"en-US"` would produce `$3.50` instead -- no template edits needed.

**CLI:**

```bash
flexrender render multi-lang-receipt.yaml -d german-data.json -o receipt-de.png
```

---

## Images

### Image Sources Overview

FlexRender supports four ways to load images in `type: image` elements. All sources are resolved through a chain of loaders in priority order.

| Source | Syntax | Requires |
|--------|--------|----------|
| Local file | `src: "logo.png"` or `src: "/path/to/logo.png"` | Default (FileResourceLoader) |
| HTTP/HTTPS | `src: "https://example.com/logo.png"` | `.WithHttpLoader()` on builder |
| Base64 data URL | `src: "data:image/png;base64,iVBOR..."` | Default (Base64ResourceLoader) |
| Embedded resource | `src: "embedded://MyApp.Assets.logo.png"` | `.WithEmbeddedLoader(assembly)` |

> **Important:** For `data:` URIs, the MIME type is **required** (e.g., `data:image/png;base64,...`). For the `base64:` prefix in content sources, MIME type is **not required** (e.g., `base64:SGVsbG8=`).

---

### Local File Image

The simplest case -- image file relative to the template base path.

**Template:**

```yaml
canvas:
  fixed: width
  width: 300
  background: "#ffffff"

layout:
  - type: flex
    padding: "16"
    gap: 12
    align: center
    children:
      # Relative path (resolved from base path)
      - type: image
        src: "assets/images/logo.png"
        width: 200
        fit: contain

      - type: text
        content: "Company Name"
        fontWeight: bold
        size: 1.2em
        align: center
```

**CLI:**

```bash
# --base-path tells the renderer where to find relative file references
flexrender render card.yaml -d data.json -o card.png --base-path ./templates
```

---

### HTTP Image

Load images from URLs at render time. Requires `.WithHttpLoader()` in the builder or the CLI (enabled by default in CLI).

**Template:**

```yaml
canvas:
  fixed: width
  width: 400
  background: "#f5f5f5"

layout:
  - type: flex
    padding: "20"
    gap: 16
    children:
      - type: flex
        direction: row
        gap: 16
        children:
          # Image loaded from HTTPS URL
          - type: image
            src: "https://avatars.githubusercontent.com/u/12345"
            width: 80
            height: 80
            fit: cover
            border-radius: "40"

          - type: flex
            gap: 4
            justify: center
            children:
              - type: text
                content: "{{userName}}"
                fontWeight: bold
                size: 1.1em
              - type: text
                content: "{{bio}}"
                size: 0.85em
                color: "#666666"
                maxLines: 2
                overflow: ellipsis

      - type: separator
        color: "#e0e0e0"

      - type: text
        content: "{{stats.repos}} repos · {{stats.followers}} followers"
        size: 0.85em
        color: "#888888"
        align: center
```

**Data:**

```json
{
  "userName": "Jane Developer",
  "bio": "Full-stack engineer, open source contributor",
  "stats": { "repos": 42, "followers": 1200 }
}
```

**CLI:**

```bash
flexrender render profile-card.yaml -d profile.json -o profile.png
```

**C# builder** (HTTP loader must be explicitly registered):

```csharp
var render = new FlexRenderBuilder()
    .WithHttpLoader(opts => {
        opts.Timeout = TimeSpan.FromSeconds(30);
        opts.MaxResourceSize = 10 * 1024 * 1024;
    })
    .WithSkia()
    .Build();
```

---

### Base64 Inline Image

Embed images directly in the template or data as base64 data URLs. Useful when images come from a database or API, or when you want a self-contained template with no external dependencies.

**Template:**

```yaml
canvas:
  fixed: width
  width: 300
  background: "#ffffff"

layout:
  - type: flex
    padding: "16"
    gap: 12
    align: center
    children:
      # Static base64 image (inline in template)
      - type: image
        src: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
        width: 100
        height: 100

      # Dynamic base64 image (from data)
      - type: image
        src: "{{logoBase64}}"
        width: 200
        fit: contain

      - type: text
        content: "{{title}}"
        fontWeight: bold
        align: center
```

**Data:**

```json
{
  "title": "Product Card",
  "logoBase64": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg=="
}
```

> **Syntax:** `data:image/<format>;base64,<data>` — the MIME type (`image/png`, `image/jpeg`, `image/webp`, etc.) is **required**. The comma after `base64` is the mandatory separator.

**CLI:**

```bash
flexrender render inline-image.yaml -d image-data.json -o output.png
```

---

### Image Fit Modes

The `fit` property controls how an image is scaled within its container:

**Template:**

```yaml
canvas:
  fixed: width
  width: 500
  background: "#f0f0f0"

layout:
  - type: flex
    padding: "16"
    gap: 16
    children:
      - type: text
        content: "Image Fit Modes"
        fontWeight: bold
        size: 1.2em
        align: center

      - type: flex
        direction: row
        gap: 12
        wrap: wrap
        children:
          # contain -- scales to fit, preserves aspect ratio
          - type: flex
            gap: 4
            align: center
            children:
              - type: image
                src: "{{imageUrl}}"
                width: 100
                height: 100
                fit: contain
                background: "#dddddd"
              - type: text
                content: "contain"
                size: 0.8em
                color: "#666666"

          # cover -- scales to fill, crops excess
          - type: flex
            gap: 4
            align: center
            children:
              - type: image
                src: "{{imageUrl}}"
                width: 100
                height: 100
                fit: cover
              - type: text
                content: "cover"
                size: 0.8em
                color: "#666666"

          # fill -- stretches to fill (may distort)
          - type: flex
            gap: 4
            align: center
            children:
              - type: image
                src: "{{imageUrl}}"
                width: 100
                height: 100
                fit: fill
              - type: text
                content: "fill"
                size: 0.8em
                color: "#666666"

          # none -- original size, no scaling
          - type: flex
            gap: 4
            align: center
            children:
              - type: image
                src: "{{imageUrl}}"
                width: 100
                height: 100
                fit: none
              - type: text
                content: "none"
                size: 0.8em
                color: "#666666"
```

**Data:**

```json
{
  "imageUrl": "assets/images/sample.png"
}
```

**CLI:**

```bash
flexrender render fit-modes.yaml -d data.json -o fit-modes.png --base-path ./templates
```

---

## See Also

- [[Template-Syntax]] -- canvas, element types, units
- [[Element-Reference]] -- complete property reference
- [[Template-Expressions]] -- variables, filters, loops, conditionals
- [[Flexbox-Layout]] -- layout engine details
- [[Render-Options]] -- output formats and rendering settings
- [[CLI-Reference]] -- render templates from the command line
