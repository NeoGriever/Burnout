`v1.0.0.0`

# Burnout

A visual weekly schedule planner for FFXIV. Plan your shifts, venue hours, or recurring events on an interactive grid directly inside the game — no alt-tabbing to spreadsheets or calendars needed.

## Features

- **Weekly Grid** — 7 days × 24 hours with 15-minute precision.
- **Drag & Drop** — Create, move, and resize entries by dragging. Toggle Edit Mode on/off to prevent accidental changes.
- **Timezone Support** — Times are stored in EST and automatically converted to any display timezone you choose. DST is handled for both sides.
- **Two-Week Rotation** — Optional Week A / Week B cycle for alternating schedules.
- **Visual Styles** — Three looks per entry: Solid, Striped, or Dotted Border, each with a custom color.
- **Midnight Overflow** — Entries that cross midnight are visually split across days with `<` / `>` indicators.
- **Command Buttons** — Attach a chat command to any entry (e.g. `/li myclub`) and run it with one click.
- **Customizable Grid** — Adjustable row height and separate background colors for weekdays and weekends.

## Getting Started

Install via the Dalamud plugin installer. Open the schedule with any of these commands:

```
/burnout   /burn   /calendar   /schedule   /workplan
```

### Creating Entries

1. Enable **Edit Mode** (green Edit button, top-right).
2. **Double-click** an empty cell, or click **+ New** in the toolbar.
3. Set label, color, style, time, and an optional command in the dialog.
4. Click **OK** to save.

### Editing Entries

- **Double-click** or **right-click** an entry to open the edit dialog.
- **Drag the center** of an entry to move it to another day or time.
- **Drag the left/right edge** to resize.
- **Drag outside the grid** to delete (with optional confirmation).
- Press **Escape** during any drag to cancel.

### Settings

Found on the **Settings** page via the sidebar:

| Setting | Description |
|---|---|
| Display Timezone | Which timezone the grid shows (also in the toolbar). |
| Two-Week Mode | Enables Week A / Week B rotation. |
| Confirm on Delete | Ask before deleting entries. |
| Cell Height | Row height slider (20–60 px). |
| Row Colors | Background colors for weekday/weekend rows. |
| Clear All | Remove every entry at once (with confirmation). |

## License

This project is licensed under the AGPL-3.0-or-later License.
