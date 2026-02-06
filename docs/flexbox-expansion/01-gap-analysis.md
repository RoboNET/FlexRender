# Gap Analysis: FlexRender vs Yoga Layout Engine

## Container Properties

| Property | FlexRender | Yoga | Gap |
|---|---|---|---|
| `flex-direction: row` | YES | YES | -- |
| `flex-direction: column` | YES | YES | -- |
| `flex-direction: row-reverse` | NO | YES | MISSING |
| `flex-direction: column-reverse` | NO | YES | MISSING |
| `flex-wrap: nowrap` | YES (default, only mode) | YES | -- |
| `flex-wrap: wrap` | ENUM ONLY (not implemented in layout) | YES | MISSING IMPL |
| `flex-wrap: wrap-reverse` | ENUM ONLY (not implemented) | YES | MISSING IMPL |
| `justify-content: start` | YES | YES | -- |
| `justify-content: center` | YES | YES | -- |
| `justify-content: end` | YES | YES | -- |
| `justify-content: space-between` | YES | YES | -- |
| `justify-content: space-around` | YES | YES | -- |
| `justify-content: space-evenly` | YES | YES | -- |
| `align-items: start` | YES | YES | -- |
| `align-items: center` | YES | YES | -- |
| `align-items: end` | YES | YES | -- |
| `align-items: stretch` | YES | YES | -- |
| `align-items: baseline` | ENUM ONLY (not implemented) | YES | MISSING IMPL |
| `align-content: *` | ENUM ONLY (6 values, none implemented) | YES (7 values incl SpaceEvenly) | MISSING IMPL |
| `gap` (uniform) | YES | YES | -- |
| `row-gap` | NO (single gap only) | YES | MISSING |
| `column-gap` | NO (single gap only) | YES | MISSING |

## Item Properties

| Property | FlexRender | Yoga | Gap |
|---|---|---|---|
| `flex-grow` | YES | YES | -- |
| `flex-shrink` | YES (weighted, NOT scaled by basis) | YES (scaled by basis) | WRONG FORMULA |
| `flex-basis` | PROP ONLY (not used in layout) | YES | MISSING IMPL |
| `align-self` | PROP ONLY (not used in layout) | YES | MISSING IMPL |
| `order` | PROP ONLY (not used in layout) | NO (Yoga does not support) | LOW PRIORITY |

## Sizing Properties

| Property | FlexRender | Yoga | Gap |
|---|---|---|---|
| `width` | YES (px, %, em, auto) | YES (px, %, auto) | -- |
| `height` | YES (px, %, em, auto) | YES (px, %, auto) | -- |
| `min-width` | NO | YES | MISSING |
| `max-width` | NO | YES | MISSING |
| `min-height` | NO | YES | MISSING |
| `max-height` | NO | YES | MISSING |
| `aspect-ratio` | NO | YES | MISSING |

## Spacing Properties

| Property | FlexRender | Yoga | Gap |
|---|---|---|---|
| `padding` (4-side) | YES (top/right/bottom/left) | YES | -- |
| `margin` (uniform) | YES (single value) | YES (4-side + auto) | PARTIAL |
| `margin` (4-side) | NO | YES | MISSING |
| `margin: auto` | NO | YES | MISSING |
| `border` (width affecting layout) | NO | YES | MISSING |

## Positioning Properties

| Property | FlexRender | Yoga | Gap |
|---|---|---|---|
| `position: relative` | NO | YES | MISSING |
| `position: absolute` | NO | YES | MISSING |
| `position: static` | implicit default | YES (default) | -- |
| `top/right/bottom/left` | NO | YES | MISSING |

## Display & Overflow

| Property | FlexRender | Yoga | Gap |
|---|---|---|---|
| `display: flex` | YES (implicit) | YES | -- |
| `display: none` | NO | YES | MISSING |
| `overflow: visible` | default behavior | YES | -- |
| `overflow: hidden` | NO | YES | MISSING |

## Direction

| Property | FlexRender | Yoga | Gap |
|---|---|---|---|
| `direction: LTR` | YES (implicit, only mode) | YES | -- |
| `direction: RTL` | NO | YES | MISSING |

## Yoga Limitations (NOT in CSS spec)

- NO `order` property -- order always determined by document order
- NO z-index
- NO visibility (always visible)
- NO forced breaks
- Default flexDirection = Column (CSS = Row)
- Default flexShrink = 0 (CSS = 1)
- Default alignContent = FlexStart (CSS spec: Stretch)
- Simplified two-pass flex resolution instead of variable-pass from spec
