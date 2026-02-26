# Template Expressions

FlexRender provides a template engine with variable substitution, loops, and conditionals. Expressions are processed in three phases:

1. **AST-level** (`TemplateExpander`) -- expands `type: each` and `type: if` elements into concrete elements based on data. This enables template caching.
2. **Inline** (`TemplatePipeline`) -- resolves `{{variable}}` expressions in all element property values after expansion.
3. **Materialization** -- resolved strings are parsed into their target types (float, int, bool, enum). This allows expressions to work in all property types, not just strings.

## Variable Substitution

Use `{{variable}}` syntax to insert data values into text and properties:

```yaml
# Simple variable
- type: text
  content: "Hello, {{name}}!"

# Dot notation for nested access
- type: text
  content: "City: {{user.address.city}}"

# Array index access
- type: text
  content: "First item: {{items[0].name}}"

# Combined path and index
- type: text
  content: "{{orders[0].items[2].name}}"

# Computed key access (dynamic key from variable)
- type: text
  content: "{{translations[lang]}}"

# String literal key
- type: text
  content: "{{translations[\"en\"]}}"

# Chained access
- type: text
  content: "{{sections[current].title}}"

# Nested computed access
- type: text
  content: "{{dict[keys[0]]}}"

# Expression as key
- type: text
  content: "Item: {{arr[base + offset]}}"
```

Variables can be used in **all** element properties -- including typed properties like numbers (`opacity`, `maxLines`, `size`), booleans (`wrap`, `showText`), and enums (`align`, `display`, `position`). When a typed property contains `{{`, the value is preserved as an expression during parsing, resolved at render time, and then parsed into the target type.

## Inline Expressions

FlexRender supports inline expressions within `{{ }}` delimiters. Expressions extend simple variable substitution with arithmetic, comparison operators, logical NOT, null coalescing, and filters.

### Arithmetic

Arithmetic operators work on numeric values:

| Operator | Description | Example |
|----------|-------------|---------|
| `+` | Addition | `{{price + tax}}` |
| `-` | Subtraction | `{{total - discount}}` |
| `*` | Multiplication | `{{price * quantity}}` |
| `/` | Division | `{{total / count}}` |
| `-` (unary) | Negation | `{{-balance}}` |

```yaml
# Compute line total
- type: text
  content: "Line total: {{price * quantity}} $"

# Compute discount
- type: text
  content: "After discount: {{total - total * discountPercent / 100}} $"
```

Both operands must be numeric (NumberValue). Division by zero returns a null value.

### Comparison Operators

Comparison operators return boolean values and are primarily used in `{{#if}}` conditions:

| Operator | Description | Example |
|----------|-------------|---------|
| `==` | Equal | `{{#if status == 'paid'}}...{{/if}}` |
| `!=` | Not equal | `{{#if status != 'cancelled'}}...{{/if}}` |
| `<` | Less than | `{{#if stock < 5}}...{{/if}}` |
| `>` | Greater than | `{{#if total > 1000}}...{{/if}}` |
| `<=` | Less than or equal | `{{#if quantity <= 10}}...{{/if}}` |
| `>=` | Greater than or equal | `{{#if rating >= 4}}...{{/if}}` |

```yaml
# Show status based on comparison
- type: text
  content: "{{#if total > 1000}}Free shipping!{{else}}Shipping: 10${{/if}}"

# Check string equality
- type: text
  content: "{{#if status == 'paid'}}Payment received{{else}}Awaiting payment{{/if}}"
```

The expressions support `true`, `false`, and `null` literals:

```yaml
# Compare with boolean literal
- type: text
  content: "{{#if active == true}}Online{{else}}Offline{{/if}}"

# Check for null
- type: text
  content: "{{#if email != null}}{{email}}{{else}}No email{{/if}}"
```

Comparison rules:
- **Numbers**: compared by value (`100 == 100.0` is true)
- **Strings**: compared using ordinal (case-sensitive) comparison
- **Booleans**: `==` and `!=` supported with `true`/`false` literals; ordered comparisons return false
- **Null**: `null == null` is true; `null != <anything>` is true; ordered comparisons with null return false
- **Mixed types** (e.g., string vs number): `==` is false, `!=` is true, ordered comparisons return false
- **Chained comparisons** (e.g., `a < b < c`) are not supported and will produce a parse error

### Logical NOT

The `!` operator inverts the truthiness of a value:

```yaml
# Show when NOT active
- type: text
  content: "{{#if !active}}Account is inactive{{/if}}"

# NOT with comparison
- type: text
  content: "{{#if !(total > 1000)}}Standard shipping{{/if}}"
```

The `!` operator evaluates the operand for [truthiness](#conditional-blocks) and returns the opposite boolean value.

### Logical Operators

The `||` and `&&` operators provide JavaScript-style short-circuit logic. They return **values**, not booleans.

| Operator | Description | Returns |
|----------|-------------|---------|
| `\|\|` | Logical OR / truthy coalescing | First truthy operand, or last operand |
| `&&` | Logical AND | First falsy operand, or last operand |

`||` is ideal for providing fallbacks when a value may be empty, null, zero, or false — unlike `??` which only catches null:

```yaml
# || catches null AND empty string AND zero AND false
- type: text
  content: "{{name || 'Guest'}}"         # "" -> "Guest", null -> "Guest"

# ?? catches only null
- type: text
  content: "{{name ?? 'Guest'}}"         # "" -> "", null -> "Guest"
```

`&&` is useful for guarding access or combining conditions:

```yaml
# Only show value if both conditions met
- type: text
  content: "{{#if isPremium && total > 100}}VIP discount!{{/if}}"

# Chain conditions in {{#if}}
- type: text
  content: "{{#if role == 'admin' && active}}Admin panel{{/if}}"
```

Both operators use [truthiness](#conditional-blocks) rules. See the truthiness table for what values are truthy/falsy.

### Null Coalescing

The `??` operator provides a fallback when the left side is null or missing:

```yaml
# Fallback to default text
- type: text
  content: "{{nickname ?? name ?? 'Anonymous'}}"

# Fallback for missing data
- type: text
  content: "Phone: {{user.phone ?? 'Not provided'}}"
```

### Filters

Filters transform values using the pipe (`|`) syntax. Filters support a positional argument, named parameters (`key:value`), and boolean flags:

```yaml
{{value | filterName}}
{{value | filterName:argument}}
{{value | filterName:positional key1:value1 key2:'string' flag}}
{{value | filterName key1:value1 flag}}
```

**Three modes:**
- Positional only: `{{value | truncate:30}}`
- Named only: `{{value | truncate length:30 suffix:'…'}}`
- Mixed: `{{value | truncate:30 suffix:'…' fromEnd}}`

Named parameters use `key:value` syntax. Boolean flags are just the key name without a value.

#### Built-in Filters

All 8 built-in filters are enabled by default. Use `WithoutDefaultFilters()` on `FlexRenderBuilder` to disable them if needed.

| Filter | Argument | Description | Example |
|--------|----------|-------------|---------|
| `currency` | -- | Format number as currency (2 decimal places) | `{{price \| currency}}` -> `"1234.50"` |
| `number` | decimal places (0-20) | Format number with specific decimal places | `{{rate \| number:4}}` -> `"3.1416"` |
| `upper` | -- | Convert string to uppercase | `{{name \| upper}}` -> `"JOHN"` |
| `lower` | -- | Convert string to lowercase | `{{name \| lower}}` -> `"john"` |
| `trim` | -- | Remove leading/trailing whitespace | `{{input \| trim}}` |
| `truncate` | `length` (positional, default: 50), `suffix` (default: "..."), `fromEnd` (flag) | Truncate string with configurable suffix and direction | `{{desc \| truncate:20}}`, `{{path \| truncate:20 fromEnd suffix:'…'}}` |
| `format` | format string | Format number or date with .NET format string | `{{date \| format:"dd.MM.yyyy"}}` |
| `currencySymbol` | -- | Convert ISO 4217 currency code (alphabetic or numeric) to symbol | `{{currency \| currencySymbol}}` -> `"$"`, `{{840 \| currencySymbol}}` -> `"$"` |

```yaml
# Price with currency formatting
- type: text
  content: "Total: {{subtotal * 1.1 | currency}} $"

# Currency symbol from alphabetic code ("USD" -> "$")
- type: text
  content: "{{currencyCode | currencySymbol}} {{amount | currency}}"

# Currency symbol from numeric ISO 4217 code (840 -> "$")
- type: text
  content: "{{numericCode | currencySymbol}} {{amount | currency}}"

# Uppercase label
- type: text
  content: "{{status | upper}}"

# Date formatting
- type: text
  content: "Date: {{orderDate | format:\"dd.MM.yyyy\"}}"

# Truncated description
- type: text
  content: "{{product.description | truncate:50}}"

# Truncated description with custom suffix
- type: text
  content: "{{product.description | truncate:30 suffix:'…'}}"

# Keep last 20 chars of file path
- type: text
  content: "{{file.path | truncate:20 fromEnd}}"

# Truncate with all named parameters
- type: text
  content: "{{text | truncate length:25 suffix:'...' fromEnd}}"
```

### Expression Precedence

Operators are evaluated in this order (highest to lowest):

| Precedence | Operators |
|------------|-----------|
| 0 (highest) | Index access (`[]`), Member access (`.`) |
| 1 | Logical NOT (`!x`), Unary minus (`-x`) |
| 2 | Multiplication, Division (`*`, `/`) |
| 3 | Addition, Subtraction (`+`, `-`) |
| 4 | Comparison (`==`, `!=`, `<`, `>`, `<=`, `>=`) |
| 5 | Logical AND (`&&`) |
| 6 | Logical OR (`\|\|`) |
| 7 | Null coalescing (`??`) |
| 8 (lowest) | Filter pipe (`\|`) |

### Expression Limits

Expressions have safety limits to prevent abuse:

| Limit | Value | Description |
|-------|-------|-------------|
| Max expression length | 2000 characters | Maximum length of the expression string inside `{{ }}` |
| Max expression depth | 50 | Maximum nesting depth of the expression AST |

### Custom Filters

You can register custom filters via the builder API. Custom filters work alongside the 8 built-in filters (which are enabled by default):

```csharp
var render = new FlexRenderBuilder()
    .WithFilter(new MyCustomFilter())
    .WithSkia()
    .Build();
```

To use only custom filters (no built-in filters):

```csharp
var render = new FlexRenderBuilder()
    .WithoutDefaultFilters()  // Removes all 8 built-in filters
    .WithFilter(new MyCustomFilter())
    .WithSkia()
    .Build();
```

Custom filters implement `ITemplateFilter`:

```csharp
public interface ITemplateFilter
{
    string Name { get; }
    TemplateValue Apply(TemplateValue input, FilterArguments arguments, CultureInfo culture);
}
```

The `FilterArguments` class provides:
- `Positional` -- the first (unnamed) argument, or null
- `GetNamed(name, defaultValue)` -- get a named parameter by key
- `HasFlag(name)` -- check if a boolean flag is present

---

## Text Blocks

Text blocks provide control flow inside `content` strings. They are processed by the template engine at render time.

> **Note:** Text blocks (`{{#if}}`, `{{#each}}`) work inside text `content` values. For element-level conditions and loops, use the `if` and `for-each` element properties instead.

### Conditional Blocks

```
{{#if condition}}...{{/if}}
{{#if condition}}...{{else}}...{{/if}}
```

The condition is evaluated for truthiness:

| Value | Truthy? |
|-------|---------|
| Non-empty string | Yes |
| Non-zero number | Yes |
| `true` | Yes |
| Non-empty array | Yes |
| Non-empty object | Yes |
| `null` / missing key | No |
| Empty string `""` | No |
| `0` | No |
| `false` | No |
| Empty array `[]` | No |

Conditions support full expressions including comparison operators, logical NOT, null coalescing, arithmetic, and filters:

```yaml
- type: text
  content: "{{#if name}}Hello {{name}}{{else}}Hello guest{{/if}}"

- type: text
  content: "{{#if name ?? nickname}}Hi {{name ?? nickname}}{{/if}}"

- type: text
  content: "{{#if count}}{{count}} items{{else}}No items{{/if}}"

# Comparison in conditions
- type: text
  content: "{{#if count > 0}}{{count}} items{{else}}No items{{/if}}"

- type: text
  content: "{{#if status == 'active'}}Online{{else}}Offline{{/if}}"

# Logical NOT in conditions
- type: text
  content: "{{#if !disabled}}Feature enabled{{/if}}"

# Logical operators in conditions
- type: text
  content: "{{#if role == 'admin' || role == 'moderator'}}Staff{{else}}User{{/if}}"

- type: text
  content: "{{#if active && verified}}Full access{{/if}}"
```

### Loop Blocks

```
{{#each arrayPath}}...{{/each}}
```

Iterates over an array or object. Inside the loop body, the current item's properties are accessible directly. Loop variables:

| Variable | Type | Description |
|----------|------|-------------|
| `@index` | number | 0-based iteration index |
| `@first` | bool | `true` for the first item |
| `@last` | bool | `true` for the last item |
| `@key` | string | Key name when iterating over an object (null for arrays) |

```yaml
- type: text
  content: "{{#each items}}{{name}}{{#if @last}}.{{else}}, {{/if}}{{/each}}"

# Output with items=[{name:"A"},{name:"B"},{name:"C"}]: "A, B, C."
```

```yaml
# Iterate over object key-value pairs
- type: text
  content: "{{#each specs}}{{@key}}: {{.}}, {{/each}}"

# Output with specs={"Color":"Red","Size":"XL"}: "Color: Red, Size: XL, "

# Access nested properties during object iteration
- type: text
  content: "{{#each people}}{{@key}} is {{age}}, {{/each}}"

# Output with people={"alice":{"age":30},"bob":{"age":25}}: "alice is 30, bob is 25, "
```

### Nesting

Text blocks can be nested. The maximum nesting depth is controlled by `ResourceLimits.MaxTemplateNestingDepth` (default: 100).

```yaml
- type: text
  content: "{{#each groups}}[{{#each items}}{{val}}{{/each}}]{{/each}}"

- type: text
  content: "{{#each users}}{{#if active}}{{name}} {{/if}}{{/each}}"
```

### String Literals and Escape Sequences

String literals in expressions support both single and double quotes:

```
{{name ?? "default"}}
{{name ?? 'default'}}
```

Escape sequences are supported inside string literals:

| Escape | Result |
|--------|--------|
| `\\` | `\` |
| `\"` | `"` |
| `\'` | `'` |
| `\n` | newline |
| `\t` | tab |

```yaml
- type: text
  content: "{{greeting ?? 'it\\'s a default'}}"
```

---

### Data Structure

Data is passed as `ObjectValue`:

```csharp
var data = new ObjectValue
{
    ["name"] = "John",
    ["user"] = new ObjectValue
    {
        ["address"] = new ObjectValue
        {
            ["city"] = "Moscow"
        }
    },
    ["items"] = new ArrayValue(
        new ObjectValue { ["name"] = "Product 1" },
        new ObjectValue { ["name"] = "Product 2" }
    )
};
```

---

## Loops (type: each)

The `each` element iterates over an array or object in the data, creating child elements for each item.

```yaml
- type: each
  array: items              # Path to array in data (required)
  as: item                  # Variable name for current item (optional)
  children:
    - type: text
      content: "{{item.name}}: {{item.price}} $"
```

### Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `array` | string | Yes | Path to array or object in data (e.g., `"items"`, `"order.lines"`) |
| `as` | string | No | Variable name for each item (default: items are accessible at root) |
| `children` | element[] | Yes | Template elements to render per item |

### Loop Variables

Inside `each` children, these special variables are available:

| Variable | Type | Description |
|----------|------|-------------|
| `{{@index}}` | int | Zero-based index of current item |
| `{{@first}}` | bool | `true` for the first item |
| `{{@last}}` | bool | `true` for the last item |
| `{{@key}}` | string | Key name when iterating over an object (`null` for arrays) |

### Loop Examples

**Basic item list:**

```yaml
- type: each
  array: items
  as: item
  children:
    - type: text
      content: "{{item.name}}"
```

**Numbered list with index:**

```yaml
- type: each
  array: items
  as: item
  children:
    - type: text
      content: "{{@index}}. {{item.name}}"
```

**Conditional separator between items:**

```yaml
- type: each
  array: items
  as: item
  children:
    - type: flex
      gap: 4
      children:
        - type: text
          content: "{{item.name}}: {{item.price}} $"
        - type: if
          condition: "@last"
          notEquals: true
          then:
            - type: separator
              style: dashed
              color: "#cccccc"
```

**Nested loop (deep path):**

```yaml
- type: each
  array: order.lines
  as: line
  children:
    - type: flex
      direction: row
      justify: space-between
      children:
        - type: text
          content: "{{line.product}}"
        - type: text
          content: "{{line.qty}} x {{line.unitPrice}}"
```

**Dictionary iteration (object key-value pairs):**

```yaml
# Data: {"specs": {"Color": "Red", "Size": "XL", "Material": "Cotton"}}

- type: each
  array: specs
  as: val
  children:
    - type: flex
      direction: row
      children:
        - type: text
          content: "{{@key}}:"
        - type: text
          content: "{{val}}"
```

**Cross-dictionary lookup with @key:**

```yaml
# Data: {"labels": {"name": "Name", "price": "Price"}, "values": {"name": "Widget", "price": "$9.99"}}

- type: each
  array: labels
  as: label
  children:
    - type: text
      content: "{{label}}: {{values[@key]}}"
```

**Nested object values:**

```yaml
# Data: {"sections": {"header": {"title": "Hello", "color": "#000"}}}

- type: each
  array: sections
  as: section
  children:
    - type: text
      content: "{{@key}}: {{section.title}}"
      color: "{{section.color}}"
```

---

## Conditionals (type: if)

The `if` element conditionally renders children based on data values. It supports 13 comparison operators.

### Basic Structure

```yaml
- type: if
  condition: isPremium       # Path to value in data
  then:                      # Rendered when condition is true
    - type: text
      content: "Premium member"
  else:                      # Rendered when condition is false (optional)
    - type: text
      content: "Standard member"
```

### All 13 Operators

| Operator | YAML Key | Value Type | Description |
|----------|----------|------------|-------------|
| Truthy | _(none)_ | -- | Value exists and is non-empty/non-zero/non-false |
| Equals | `equals` | any | Value equals the operand |
| NotEquals | `notEquals` | any | Value does not equal the operand |
| In | `in` | array | Value is in the list |
| NotIn | `notIn` | array | Value is not in the list |
| Contains | `contains` | any | Array contains the element |
| GreaterThan | `greaterThan` | number | Numeric greater than |
| GreaterThanOrEqual | `greaterThanOrEqual` | number | Numeric >= |
| LessThan | `lessThan` | number | Numeric less than |
| LessThanOrEqual | `lessThanOrEqual` | number | Numeric <= |
| HasItems | `hasItems` | bool | Array is non-empty (true) or empty (false) |
| CountEquals | `countEquals` | number | Array length equals N |
| CountGreaterThan | `countGreaterThan` | number | Array length > N |

### Operator Examples

**Truthy check** (value exists and is non-empty/non-zero/non-false):

```yaml
- type: if
  condition: discount
  then:
    - type: text
      content: "Discount: {{discount}}%"
```

**Equals** (works with strings, numbers, booleans, null):

```yaml
- type: if
  condition: status
  equals: "paid"
  then:
    - type: text
      content: "Payment received"
      color: "#22c55e"
```

**NotEquals:**

```yaml
- type: if
  condition: status
  notEquals: "cancelled"
  then:
    - type: text
      content: "Order active"
```

**In list:**

```yaml
- type: if
  condition: role
  in: ["admin", "moderator"]
  then:
    - type: text
      content: "Staff member"
```

**NotIn list:**

```yaml
- type: if
  condition: status
  notIn: ["cancelled", "refunded"]
  then:
    - type: text
      content: "Active order"
```

**Contains** (check if array contains a value):

```yaml
- type: if
  condition: tags
  contains: "urgent"
  then:
    - type: text
      content: "URGENT"
      color: "#ef4444"
```

**Numeric comparisons:**

```yaml
# Greater than
- type: if
  condition: total
  greaterThan: 1000
  then:
    - type: text
      content: "Free shipping!"

# Greater than or equal
- type: if
  condition: quantity
  greaterThanOrEqual: 10
  then:
    - type: text
      content: "Bulk discount applied"

# Less than
- type: if
  condition: stock
  lessThan: 5
  then:
    - type: text
      content: "Low stock!"
      color: "#ef4444"

# Less than or equal
- type: if
  condition: rating
  lessThanOrEqual: 2
  then:
    - type: text
      content: "Needs improvement"
```

**Array checks:**

```yaml
# Has items (array is non-empty)
- type: if
  condition: items
  hasItems: true
  then:
    - type: each
      array: items
      as: item
      children:
        - type: text
          content: "{{item.name}}"

# Count equals
- type: if
  condition: items
  countEquals: 1
  then:
    - type: text
      content: "Single item order"

# Count greater than
- type: if
  condition: items
  countGreaterThan: 5
  then:
    - type: text
      content: "Bulk order discount applied"
```

### Else-If Chains

Chain multiple conditions with `elseIf`:

```yaml
- type: if
  condition: status
  equals: "paid"
  then:
    - type: text
      content: "PAID"
      color: "#22c55e"
  elseIf:
    condition: status
    equals: "pending"
    then:
      - type: flex
        align: center
        children:
          - type: qr
            data: "{{paymentUrl}}"
            size: 100
          - type: text
            content: "Scan to pay"
            size: 0.85em
  else:
    - type: text
      content: "Payment required"
      color: "#ef4444"
```

Else-if chains can be nested to any depth, though keeping them shallow improves readability.

### Combining Conditions

For AND/OR logic, nest `if` elements:

```yaml
# AND: isPremium AND total > 100
- type: if
  condition: isPremium
  then:
    - type: if
      condition: total
      greaterThan: 100
      then:
        - type: text
          content: "Premium + large order discount!"
```

### Filters in Conditions

The `condition` field supports inline expressions with filters. This enables case-insensitive comparisons, formatting before comparison, and other transformations:

**Case-insensitive comparison:**

```yaml
- type: if
  condition: "{{ status | lower }}"
  equals: "active"
  then:
    - type: text
      content: "Active"
```

**Works with any filter:**

```yaml
- type: if
  condition: "{{ name | trim }}"
  notEquals: ""
  then:
    - type: text
      content: "Hello, {{name}}!"
```

> **Note:** When using inline expressions in `condition`, wrap the expression in quotes and double curly braces: `"{{ path | filter }}"`. Without `{{ }}`, the value is resolved as a plain dot-path without filter support.

---

## Expressions in Typed Properties

All element properties accept `{{expressions}}`, including typed properties like floats, integers, booleans, and enums. This enables fully data-driven templates where any aspect of the layout can be controlled by data.

```yaml
# Expressions in numeric properties
- type: text
  content: "Dynamic opacity"
  opacity: "{{theme.textOpacity}}"
  maxLines: "{{layout.maxLines}}"

# Expressions in boolean properties
- type: barcode
  data: "{{product.sku}}"
  showText: "{{settings.showBarcodeText}}"

# Expressions in enum properties
- type: text
  content: "Dynamic alignment"
  align: "{{theme.alignment}}"

# Expressions in size properties
- type: qr
  data: "{{payment.url}}"
  size: "{{layout.qrSize}}"
```

How typed expressions work:

1. When a typed property contains `{{`, the parser preserves the raw string as an `ExprValue<T>` expression instead of parsing it immediately
2. After template expansion, the expression is resolved to a concrete string using the data context
3. The resolved string is then parsed into the target type (e.g., `"0.5"` becomes `float 0.5`, `"true"` becomes `bool true`, `"center"` becomes `TextAlign.Center`)
4. If parsing fails, the default value for that type is used (e.g., `1.0` for opacity, `null` for nullable properties)

This works with all expression features -- arithmetic, filters, conditionals, and null coalescing:

```yaml
# Computed opacity with fallback
- type: text
  content: "Styled text"
  opacity: "{{theme.opacity ?? 1}}"

# Conditional boolean via expression
- type: barcode
  data: "{{sku}}"
  showText: "{{#if printMode}}true{{else}}false{{/if}}"
```

---

## Processing Order

Understanding the processing order helps with debugging:

1. **Parse** -- YAML is parsed into an AST. Typed properties containing `{{` are preserved as expressions
2. **Expand** -- `type: each` and `type: if` elements are expanded based on data
3. **Resolve** -- `{{variable}}` expressions are resolved to concrete strings in all properties
4. **Materialize** -- resolved strings are parsed into typed values (float, int, bool, enum)
5. **Layout** -- the flexbox engine computes positions and sizes
6. **Render** -- elements are drawn to the output image

Template caching works because step 1 (parse) is separate from steps 2-6 (expand/resolve/materialize/layout/render). Parse once, then process with different data for each render.

## See Also

- [[Template-Syntax]] -- element types and properties
- [[Flexbox-Layout]] -- layout engine details
