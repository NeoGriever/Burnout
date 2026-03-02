`v1.0.0.0`

# Burnout

Burnout is a Dalamud plugin for FFXIV that provides a visual weekly work schedule planner directly inside the game. It renders an interactive grid of 7 days and 24 hours where you create, move, and resize shift entries via drag and drop. All times are stored internally in EST (Eastern Standard Time, DST-aware) but can be displayed in any system timezone. Three visual styles, a two-week rotation mode, per-entry chat commands, and 15-minute scheduling granularity make it suitable for organizing static schedules, venue hours, or recurring events.

## Table of Contents

- [1 Getting Started](#1-getting-started)
  - [1.1 Installation](#11-installation)
  - [1.2 Opening the Plugin](#12-opening-the-plugin)
  - [1.3 Sidebar Navigation](#13-sidebar-navigation)
- [2 Schedule Grid](#2-schedule-grid)
  - [2.1 Grid Layout](#21-grid-layout)
  - [2.2 Dual-Header (Timezone Display)](#22-dual-header-timezone-display)
  - [2.3 Day Rows and Sub-Rows](#23-day-rows-and-sub-rows)
  - [2.4 Two-Week Mode](#24-two-week-mode)
- [3 Shift Entries](#3-shift-entries)
  - [3.1 Entry Properties](#31-entry-properties)
  - [3.2 Visual Styles](#32-visual-styles)
  - [3.3 Midnight Overflow](#33-midnight-overflow)
  - [3.4 Ghost Entries](#34-ghost-entries)
  - [3.5 Command Buttons](#35-command-buttons)
- [4 Creating and Editing Entries](#4-creating-and-editing-entries)
  - [4.1 Creating via Double-Click](#41-creating-via-double-click)
  - [4.2 Creating via Toolbar Button](#42-creating-via-toolbar-button)
  - [4.3 Entry Dialog](#43-entry-dialog)
  - [4.4 Time Editing](#44-time-editing)
- [5 Drag and Drop](#5-drag-and-drop)
  - [5.1 Edit Mode](#51-edit-mode)
  - [5.2 Moving Entries](#52-moving-entries)
  - [5.3 Resizing Entries](#53-resizing-entries)
  - [5.4 Deleting via Drag](#54-deleting-via-drag)
  - [5.5 Drag State Machine](#55-drag-state-machine)
- [6 Timezone System](#6-timezone-system)
  - [6.1 Internal Storage (EST)](#61-internal-storage-est)
  - [6.2 Display Timezone](#62-display-timezone)
  - [6.3 Offset Calculation](#63-offset-calculation)
  - [6.4 DST Handling](#64-dst-handling)
- [7 Settings](#7-settings)
  - [7.1 Display Timezone](#71-display-timezone)
  - [7.2 Two-Week Mode](#72-two-week-mode)
  - [7.3 Confirm on Delete](#73-confirm-on-delete)
  - [7.4 Cell Height](#74-cell-height)
  - [7.5 Row Background Colors](#75-row-background-colors)
  - [7.6 Clear All Entries](#76-clear-all-entries)
- [Appendix](#appendix)
  - [A - Drag State Machine Diagram](#a---drag-state-machine-diagram)
  - [B - Cross-Reference Index](#b---cross-reference-index)
  - [C - Technical Notes](#c---technical-notes)
- [Commands](#commands)
- [License](#license)

---

## 1 Getting Started

### 1.1 Installation

Install through the Dalamud plugin installer. Burnout requires **ECommons** as a dependency (installed automatically).

### 1.2 Opening the Plugin

Use any of the following chat commands:

```
/burnout
/burn
/calendar
/schedule
/workplan
```

This opens the main window with the schedule grid.

### 1.3 Sidebar Navigation

The sidebar on the left provides navigation between pages:

| Page | Description |
|---|---|
| **Schedule** | Main grid view with the weekly schedule. |
| **Settings** | Configuration options for timezone, cell size, colors, and more. |

Click `<` to collapse the sidebar or `>` to expand it. When collapsed, the current page name is displayed at the top of the content area.

---

## 2 Schedule Grid

### 2.1 Grid Layout

The schedule grid displays 7 days (Monday through Sunday) across 24 hours (00:00-23:00). Each hour column is subdivided into 15-minute increments for precise entry placement.

- **Columns**: 24 (one per hour), width calculated automatically from available window space (minimum 20px per column).
- **Day Labels**: 50px-wide column on the left showing day abbreviations (Mon, Tue, Wed, Thu, Fri, Sat, Sun).
- **Row Height**: Each day row height equals `SubRowCount x CellHeight`, scaling vertically when entries overlap (see [2.3 Day Rows and Sub-Rows](#23-day-rows-and-sub-rows)).
- **Background Colors**: Weekday rows (Mon-Fri) use alternating dark blue shades. Weekend rows (Sat-Sun) use alternating dark green shades. Both are configurable (see [7.5 Row Background Colors](#75-row-background-colors)).

### 2.2 Dual-Header (Timezone Display)

The grid header shows hour labels (00:00 through 23:00) in the currently selected display timezone. The toolbar above the grid displays:

- **Display TZ**: A dropdown (280px wide) to select the display timezone.
- **Offset indicator**: Shows the offset from EST, e.g., `EST+2h` or `EST-3h`.

The header labels automatically shift when the display timezone changes, so entries appear at their correct local time regardless of which timezone is selected.

### 2.3 Day Rows and Sub-Rows

When multiple entries overlap on the same day and time range, Burnout stacks them vertically using sub-rows. Each day can have one or more sub-rows, and the row height grows to accommodate all stacked entries.

**Sub-Row Algorithm** (Greedy Interval Coloring):

1. All placements for a day are sorted by their display start minute.
2. For each placement, the algorithm finds the first sub-row where the previous entry has already ended.
3. If no existing sub-row is available, a new one is created.
4. The day's total sub-row count determines its rendered height.

This ensures that overlapping entries are always visible and never drawn on top of each other.

### 2.4 Two-Week Mode

When enabled (see [7.2 Two-Week Mode](#72-two-week-mode)), the grid displays two separate 7-day layouts stacked vertically:

- **Week A**: The first 7-day grid (top).
- **Week B**: The second 7-day grid (bottom), separated by an orange/gold divider labeled `--- Week B ---`.

Each entry belongs to either Week A (`Week = 0`) or Week B (`Week = 1`). Entries can be moved between weeks via drag and drop (see [5.2 Moving Entries](#52-moving-entries)). Disabling two-week mode resets all entries to Week A.

---

## 3 Shift Entries

### 3.1 Entry Properties

Each shift entry stores the following properties:

| Property | Type | Default | Description |
|---|---|---|---|
| **Id** | string | Auto-generated UUID | Unique identifier. |
| **Label** | string | `"New Shift"` | Display name shown on the entry block. |
| **Week** | int | `0` | `0` = Week A, `1` = Week B (see [2.4 Two-Week Mode](#24-two-week-mode)). |
| **Day** | DayOfWeek | Monday | Day of the week, stored in EST. |
| **StartHour** | int | `0` | Hour of day (0-23), stored in EST. |
| **StartMinute** | int | `0` | Minute within the hour (0, 15, 30, or 45). |
| **DurationMinutes** | int | `60` | Duration in minutes (minimum 15, step 15). |
| **Color** | Vector4 | `(0.3, 0.6, 0.9, 0.85)` | RGBA color for the entry block. |
| **Style** | int | `0` | Visual style (see [3.2 Visual Styles](#32-visual-styles)). |
| **Command** | string | `""` | Chat command executed when the command button is clicked (see [3.5 Command Buttons](#35-command-buttons)). |

### 3.2 Visual Styles

Three visual styles are available, selectable in the entry dialog (see [4.3 Entry Dialog](#43-entry-dialog)):

| Style | Name | Appearance |
|---|---|---|
| **0** | Solid | Filled rectangle with a brighter border (+0.2 brightness boost). |
| **1** | Striped | Filled rectangle overlaid with diagonal white stripes (8px spacing, 15% alpha). |
| **2** | Dotted Border | Filled rectangle with 2x2px dots along all four edges (6px spacing). |

### 3.3 Midnight Overflow

Entries that extend past midnight are automatically split across consecutive days. Each segment is rendered on its respective day row with visual indicators:

- **First segment** (ends at midnight): Displays `Label >` with a `>` suffix indicating continuation.
- **Continuation segment** (starts at midnight): Displays `< Label` with a `<` prefix indicating overflow from the previous day.
- **Middle segments** (if the entry spans more than two days): Display `< Label >` with both indicators.

The overflow rendering is purely visual — the underlying entry data remains a single record with its original start time and duration.

### 3.4 Ghost Entries

When a new entry is created (via double-click or toolbar button), it starts as a **ghost entry** — an unsaved, tentative shift that is visually distinct from committed entries:

- **Fill**: Entry color at 35% alpha (heavily faded).
- **Border**: Dashed line (6px dash, 4px gap) instead of solid.
- **Text**: 50% alpha (dimmed label).

A ghost entry becomes a regular entry when the user clicks **OK** in the entry dialog (see [4.3 Entry Dialog](#43-entry-dialog)). Clicking **Cancel** removes the ghost entry entirely.

### 3.5 Command Buttons

If an entry has a non-empty `Command` field, a small button with a `>` arrow appears on the right side of the entry block. Clicking this button executes the stored chat command (e.g., `/li specialclub` or `/teleport Limsa`).

Command buttons are only shown on committed entries (not ghosts) and not on overflow continuation segments. The button is 18px wide (or `CellHeight - 2`, whichever is smaller) with a dark background.

---

## 4 Creating and Editing Entries

### 4.1 Creating via Double-Click

Double-click on an empty cell in the grid to create a new entry at that position. This requires **Edit Mode** to be enabled (see [5.1 Edit Mode](#51-edit-mode)). The entry is created as a ghost (see [3.4 Ghost Entries](#34-ghost-entries)) and the entry dialog opens immediately.

### 4.2 Creating via Toolbar Button

When Edit Mode is enabled, a `+ New` button appears in the toolbar (top-right of the schedule page). Clicking it creates a new entry at a default position (Monday 00:00, 1-hour duration) and opens the entry dialog.

### 4.3 Entry Dialog

The entry dialog is a modal window for editing all entry properties. It opens when:

- A new entry is created (double-click or toolbar button).
- An existing entry is double-clicked.
- An existing entry is right-clicked.

| Field | Control | Description |
|---|---|---|
| **Label** | Text input (128 chars) | The display name for the entry. |
| **Color** | Color picker (RGBA) | The entry's fill and border color. |
| **Style** | Dropdown | Solid, Striped, or Dotted Border (see [3.2 Visual Styles](#32-visual-styles)). |
| **Command** | Text input (256 chars) | Chat command to execute on button click (see [3.5 Command Buttons](#35-command-buttons)). |
| **Start Time** | Day + Hour + Minute | EST start time (see [4.4 Time Editing](#44-time-editing)). |
| **End Time** | Day + Hour + Minute | EST end time. |
| **Duration** | Read-only text | Computed from start and end times. |
| **Display Preview** | Read-only text | Shows the same times converted to the current display timezone. |

**Buttons**:

| Button | Size | Color | Action |
|---|---|---|---|
| **OK** | 110x30px | Green | Saves changes, removes ghost status, closes dialog. |
| **Cancel** | 110x30px | Gray | Discards changes. If the entry is a ghost, removes it. |
| **Delete** | 110x30px | Red | Deletes the entry. Only shown for existing (non-ghost) entries. Respects the delete confirmation setting (see [7.3 Confirm on Delete](#73-confirm-on-delete)). |

### 4.4 Time Editing

Start and end times are set using three controls each:

- **Day**: Dropdown (Monday through Sunday).
- **Hour**: Integer input (0-23).
- **Minute**: Dropdown (0, 15, 30, 45).

All times in the dialog are in EST. A display preview below shows the equivalent times in the currently selected display timezone.

**Duration Calculation**: The duration is computed as the difference between start and end times. If the end time is earlier than the start time on the same day, the entry wraps around to the following week (adding 7 days). The minimum duration is 15 minutes.

---

## 5 Drag and Drop

### 5.1 Edit Mode

Drag and drop operations require **Edit Mode** to be enabled. Toggle it using the **Edit** button in the toolbar (top-right of the schedule page):

- **OFF** (red background): Drag operations are disabled. No resize or move cursors appear. Double-click and right-click still open the entry dialog. Command buttons remain clickable. The `+ New` toolbar button is hidden.
- **ON** (green background): All drag operations are enabled. Resize and move cursors appear on entry edges and body. The `+ New` toolbar button is visible.

### 5.2 Moving Entries

Click and drag the middle area of an entry to move it to a different time slot or day. The entry preserves its duration and maintains the grab offset (the relative position where you initially clicked within the entry).

In two-week mode (see [2.4 Two-Week Mode](#24-two-week-mode)), entries can be dragged between Week A and Week B.

**Cursor**: `Hand` when hovering over the middle of an entry in Edit Mode.

### 5.3 Resizing Entries

Click and drag the left or right edge of an entry (within 6px of the border) to resize it:

- **Left edge**: Adjusts the start time. Duration grows or shrinks accordingly.
- **Right edge**: Adjusts the end time. Only the duration changes.

Resizing snaps to hour boundaries. The minimum duration is 15 minutes and the maximum is 24 hours (right edge) or 48 hours (left edge).

**Cursor**: `ResizeEW` (horizontal double arrow) when hovering over an entry edge in Edit Mode.

### 5.4 Deleting via Drag

If an entry is dragged outside the grid bounds (above, below, or to the sides of the grid area), it triggers a deletion. If **Confirm on Delete** is enabled (see [7.3 Confirm on Delete](#73-confirm-on-delete)), a confirmation dialog appears. Otherwise, the entry is deleted immediately.

If the deletion is canceled, the entry returns to its original position.

### 5.5 Drag State Machine

All drag interactions follow a state machine with a 4-pixel movement threshold to distinguish clicks from drags:

| State | Trigger | Next State |
|---|---|---|
| **Idle** | Click on left edge (Edit Mode) | PendingResizeLeft |
| **Idle** | Click on right edge (Edit Mode) | PendingResizeRight |
| **Idle** | Click on middle (Edit Mode) | PendingMove |
| **Idle** | Double-click on empty cell | Create entry + open dialog |
| **Idle** | Double-click on entry | Open dialog |
| **Idle** | Right-click on entry | Open dialog |
| **PendingResize\*** | Mouse released (< 4px) | Idle |
| **PendingResize\*** | Mouse moved >= 4px | ActiveResize* |
| **PendingMove** | Mouse released (< 4px) | Idle |
| **PendingMove** | Mouse moved >= 4px | ActiveMove |
| **ActiveResize\*** | Mouse released | Commit resize, Idle |
| **ActiveResize\*** | Escape pressed | Revert to original, Idle |
| **ActiveMove** | Mouse released | Commit move, Idle |
| **ActiveMove** | Mouse released outside grid | Delete entry (with confirmation), Idle |
| **ActiveMove** | Escape pressed | Revert to original, Idle |

See [Appendix A](#a---drag-state-machine-diagram) for a visual diagram.

---

## 6 Timezone System

### 6.1 Internal Storage (EST)

All entry times (day, hour, minute) are stored in **Eastern Standard Time** (EST). The .NET timezone ID `"Eastern Standard Time"` is used, which is DST-aware — it automatically switches between EST (UTC-5) and EDT (UTC-4) based on the current date.

This means the stored hour values represent the actual Eastern time, and the plugin handles daylight saving transitions transparently.

### 6.2 Display Timezone

The display timezone determines how entry times are shown on the grid. It can be any timezone installed on the system, selectable via:

- The **Display TZ** dropdown in the schedule toolbar (see [2.2 Dual-Header](#22-dual-header-timezone-display)).
- The **Display Timezone** dropdown in Settings (see [7.1 Display Timezone](#71-display-timezone)).

Both controls share the same setting. The grid header labels and entry positions update immediately when the display timezone changes.

### 6.3 Offset Calculation

The offset between EST and the display timezone is calculated as:

```
offset = displayTz.GetUtcOffset(now) - estTz.GetUtcOffset(now)
```

This produces an integer hour offset (half-hour timezones like UTC+5:30 are rounded to the nearest whole hour). The offset is applied to all display coordinates, shifting entries left or right on the grid. Day boundaries are handled correctly — an entry at EST 23:00 displayed in a UTC+2 timezone appears at 04:00 the next day.

### 6.4 DST Handling

Both the source timezone (EST) and the display timezone participate in DST calculations independently. The offset formula uses `GetUtcOffset(DateTimeOffset.Now)`, which returns the currently active offset including any DST adjustments.

This means:
- When EST transitions to EDT, the offset changes automatically.
- When the display timezone transitions (e.g., CET to CEST), the offset also adjusts.
- No manual DST configuration is needed.

---

## 7 Settings

### 7.1 Display Timezone

A dropdown listing all system timezones with their UTC offset labels (e.g., `(UTC-05:00) Eastern Time`). Changing this setting immediately updates the grid display. The same timezone can also be selected from the schedule toolbar (see [2.2 Dual-Header](#22-dual-header-timezone-display)).

### 7.2 Two-Week Mode

A checkbox labeled **"2-Week Cycle (Week A / Week B)"**. When enabled, the grid shows two 7-day layouts with separate entry sets (see [2.4 Two-Week Mode](#24-two-week-mode)). When disabled, all entries are reset to Week A (`Week = 0`) and the second grid is hidden.

### 7.3 Confirm on Delete

A checkbox labeled **"Confirm before deleting entries"** (default: enabled). When on, a confirmation dialog appears before any entry is deleted — whether via the Delete button in the entry dialog (see [4.3 Entry Dialog](#43-entry-dialog)) or by dragging an entry outside the grid (see [5.4 Deleting via Drag](#54-deleting-via-drag)). When off, deletions happen immediately.

### 7.4 Cell Height

A slider controlling the height of each sub-row in pixels. Range: 20px to 60px. Default: 30px. Lower values show more days on screen; higher values make entry labels easier to read.

### 7.5 Row Background Colors

Four color pickers for customizing the grid row backgrounds:

| Setting | Default | Applies To |
|---|---|---|
| **Weekday Color Odd** | Dark blue `(0.04, 0.06, 0.12)` | Monday, Wednesday, Friday rows. |
| **Weekday Color Even** | Slightly lighter blue `(0.06, 0.08, 0.16)` | Tuesday, Thursday rows. |
| **Weekend Color Odd** | Dark green `(0.04, 0.10, 0.06)` | Saturday row. |
| **Weekend Color Even** | Slightly lighter green `(0.06, 0.13, 0.08)` | Sunday row. |

### 7.6 Clear All Entries

A red button labeled **"Clear All Entries"**. Clicking it opens a confirmation popup asking `"Really delete all entries?"` with **Yes** and **Cancel** buttons. Confirming removes every entry from the configuration.

---

## Appendix

### A - Drag State Machine Diagram

```
                        +------ Idle ------+
                        |    |    |    |    |
                  left  |  right  | middle  |  double-click / right-click
                  edge  |  edge   |         |
                   v    |   v     v         v
          PendingResizeL | PendingResizeR  PendingMove    Open Dialog
                |    |   |   |    |        |    |
            <4px|  >=4px | <4px >=4px   <4px  >=4px
                v    v   |   v    v      v      v
              Idle  ActiveResizeL |  Idle  ActiveResizeR  Idle  ActiveMove
                     |    |      |        |    |               |    |    |
                release  Esc     |     release  Esc         release Esc outside
                  |       |      |       |       |            |      |    |
                  v       v      |       v       v            v      v    v
               Commit   Revert  |    Commit   Revert       Commit Revert Delete
                  |       |     |       |       |            |      |    |
                  +---+---+     |       +---+---+            +--+---+----+
                      |         |           |                   |
                      v         v           v                   v
                              Idle                            Idle
```

### B - Cross-Reference Index

| Section | References |
|---|---|
| [2.2 Dual-Header](#22-dual-header-timezone-display) | [6 Timezone System](#6-timezone-system) (offset calculation), [7.1 Display Timezone](#71-display-timezone) (settings) |
| [2.3 Sub-Rows](#23-day-rows-and-sub-rows) | [Appendix C](#c---technical-notes) (algorithm details) |
| [2.4 Two-Week Mode](#24-two-week-mode) | [7.2 Two-Week Mode](#72-two-week-mode) (settings toggle), [5.2 Moving Entries](#52-moving-entries) (cross-week drag) |
| [3.2 Visual Styles](#32-visual-styles) | [4.3 Entry Dialog](#43-entry-dialog) (style selection dropdown) |
| [3.3 Midnight Overflow](#33-midnight-overflow) | [2.1 Grid Layout](#21-grid-layout) (rendering across day rows) |
| [3.4 Ghost Entries](#34-ghost-entries) | [4.3 Entry Dialog](#43-entry-dialog) (OK commits, Cancel removes ghost) |
| [3.5 Command Buttons](#35-command-buttons) | [4.3 Entry Dialog](#43-entry-dialog) (Command field) |
| [4.1 Creating via Double-Click](#41-creating-via-double-click) | [5.1 Edit Mode](#51-edit-mode) (requires Edit Mode on) |
| [4.2 Creating via Toolbar Button](#42-creating-via-toolbar-button) | [5.1 Edit Mode](#51-edit-mode) (button only visible in Edit Mode) |
| [5.1 Edit Mode](#51-edit-mode) | [5.2 Moving](#52-moving-entries), [5.3 Resizing](#53-resizing-entries), [5.4 Deleting via Drag](#54-deleting-via-drag) (all require Edit Mode) |
| [5.4 Deleting via Drag](#54-deleting-via-drag) | [7.3 Confirm on Delete](#73-confirm-on-delete) (controls confirmation dialog) |
| [5.5 Drag State Machine](#55-drag-state-machine) | [Appendix A](#a---drag-state-machine-diagram) (visual diagram) |
| [6.1 EST Storage](#61-internal-storage-est) | [4.4 Time Editing](#44-time-editing) (dialog times are in EST) |
| [6.2 Display Timezone](#62-display-timezone) | [2.2 Dual-Header](#22-dual-header-timezone-display) (toolbar dropdown), [7.1 Display Timezone](#71-display-timezone) (settings dropdown) |
| [7.2 Two-Week Mode](#72-two-week-mode) | [2.4 Two-Week Mode](#24-two-week-mode) (grid layout changes) |
| [7.3 Confirm on Delete](#73-confirm-on-delete) | [4.3 Entry Dialog](#43-entry-dialog) (Delete button), [5.4 Deleting via Drag](#54-deleting-via-drag) (drag outside grid) |
| [7.5 Row Background Colors](#75-row-background-colors) | [2.1 Grid Layout](#21-grid-layout) (weekday/weekend row coloring) |
| [7.6 Clear All Entries](#76-clear-all-entries) | [3.1 Entry Properties](#31-entry-properties) (removes all entries from config) |

### C - Technical Notes

- **EST Storage**: All times are stored in Eastern Standard Time using `TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time")`. Despite the name containing "Standard", this .NET identifier is DST-aware and automatically handles EST/EDT transitions.
- **15-Minute Granularity**: Entry start times and durations snap to 15-minute increments internally. This provides finer control than full-hour blocks while keeping the grid readable.
- **Sub-Row Algorithm**: Uses greedy interval coloring on minute-based intervals. Placements are sorted by start minute, then assigned to the first available sub-row where the previous entry has ended. This guarantees minimal row expansion while keeping all entries visible.
- **Coordinate Conversion**: Two helper methods (`DisplayToEst` and `EstToDisplay`) handle all timezone conversions, including day-of-week wrapping when the offset pushes times across midnight boundaries.
- **Dependencies**: Requires ECommons (installed automatically via Dalamud). ECommons provides the `Chat.SendMessage()` API used for executing entry commands.
- **Persistence**: Configuration is saved via `PluginInterface.SavePluginConfig()`. All entries persist across plugin close/reopen and game restarts.
- **Default Window Size**: 1400x620 pixels on first use, freely resizable afterward.

## Commands

- `/burnout` - Opens the Burnout schedule window.
- `/burn` - Alias for `/burnout`.
- `/calendar` - Alias for `/burnout`.
- `/schedule` - Alias for `/burnout`.
- `/workplan` - Alias for `/burnout`.

## License

This project is licensed under the AGPL-3.0-or-later License.
