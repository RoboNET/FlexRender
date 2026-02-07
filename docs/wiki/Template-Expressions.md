# Template Expressions

FlexRender provides a template engine with variable substitution, loops, and conditionals. Expressions are processed in two layers:

1. **AST-level** (`TemplateExpander`) -- expands `type: each` and `type: if` elements into concrete elements based on data. This enables template caching.
2. **Inline** (`TemplateProcessor`) -- resolves `{{variable}}` expressions in element property values after expansion.

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
```

Variables can be used in most string properties: `content`, `data`, `src`, `color`, and others.

## Inline Expressions

FlexRender supports inline expressions within `{{ }}` delimiters. Expressions extend simple variable substitution with arithmetic, null coalescing, and filters.

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

Filters transform values using the pipe (`|`) syntax. Filters can take an optional argument after a colon:

```yaml
{{value | filterName}}
{{value | filterName:argument}}
```

#### Built-in Filters

| Filter | Argument | Description | Example |
|--------|----------|-------------|---------|
| `currency` | -- | Format number as currency (2 decimal places) | `{{price \| currency}}` -> `"1234.50"` |
| `number` | decimal places (0-20) | Format number with specific decimal places | `{{rate \| number:4}}` -> `"3.1416"` |
| `upper` | -- | Convert string to uppercase | `{{name \| upper}}` -> `"JOHN"` |
| `lower` | -- | Convert string to lowercase | `{{name \| lower}}` -> `"john"` |
| `trim` | -- | Remove leading/trailing whitespace | `{{input \| trim}}` |
| `truncate` | max length (default: 50) | Truncate string with "..." suffix | `{{desc \| truncate:20}}` |
| `format` | format string | Format number or date with .NET format string | `{{date \| format:"dd.MM.yyyy"}}` |
| `currencySymbol` | -- | Convert ISO 4217 currency code to symbol | `{{currency \| currencySymbol}}` -> `"$"` |

```yaml
# Price with currency formatting
- type: text
  content: "Total: {{subtotal * 1.1 | currency}} $"

# Uppercase label
- type: text
  content: "{{status | upper}}"

# Date formatting
- type: text
  content: "Date: {{orderDate | format:\"dd.MM.yyyy\"}}"

# Truncated description
- type: text
  content: "{{product.description | truncate:50}}"
```

### Expression Precedence

Operators are evaluated in this order (highest to lowest):

| Precedence | Operators |
|------------|-----------|
| 1 (highest) | Unary minus (`-x`) |
| 2 | Multiplication, Division (`*`, `/`) |
| 3 | Addition, Subtraction (`+`, `-`) |
| 4 | Null coalescing (`??`) |
| 5 (lowest) | Filter pipe (`\|`) |

### Expression Limits

Expressions have safety limits to prevent abuse:

| Limit | Value | Description |
|-------|-------|-------------|
| Max expression length | 2000 characters | Maximum length of the expression string inside `{{ }}` |
| Max expression depth | 50 | Maximum nesting depth of the expression AST |

### Custom Filters

You can register custom filters via the builder API:

```csharp
var render = new FlexRenderBuilder()
    .WithFilter("myFilter", new MyCustomFilter())
    .WithSkia()
    .Build();
```

Custom filters implement `ITemplateFilter`:

```csharp
public interface ITemplateFilter
{
    string Name { get; }
    TemplateValue Apply(TemplateValue input, TemplateValue? argument);
}
```

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

The `each` element iterates over an array in the data, creating child elements for each item.

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
| `array` | string | Yes | Path to array in data (e.g., `"items"`, `"order.lines"`) |
| `as` | string | No | Variable name for each item (default: items are accessible at root) |
| `children` | element[] | Yes | Template elements to render per item |

### Loop Variables

Inside `each` children, these special variables are available:

| Variable | Type | Description |
|----------|------|-------------|
| `{{@index}}` | int | Zero-based index of current item |
| `{{@first}}` | bool | `true` for the first item |
| `{{@last}}` | bool | `true` for the last item |

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

## Processing Order

Understanding the processing order helps with debugging:

1. **Parse** -- YAML is parsed into an AST (Template with CanvasSettings + TemplateElement tree)
2. **Expand** -- `type: each` and `type: if` elements are expanded based on data
3. **Process** -- `{{variable}}` expressions are resolved in element properties
4. **Layout** -- the flexbox engine computes positions and sizes
5. **Render** -- elements are drawn to the output image

Template caching works because steps 1 (parse) and 2-5 (expand/process/layout/render) are separate. Parse once, then render many times with different data.

## See Also

- [[Template-Syntax]] -- element types and properties
- [[Flexbox-Layout]] -- layout engine details
