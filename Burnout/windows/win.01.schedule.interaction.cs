using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Burnout.Windows;

public partial class BurnoutWindow
{
    private enum DragState
    {
        Idle,
        PendingResizeLeft,
        PendingResizeRight,
        PendingMove,
        ActiveResizeLeft,
        ActiveResizeRight,
        ActiveMove,
    }

    private DragState _dragState = DragState.Idle;
    private Vector2 _dragStartMouse;
    private DayOfWeek _dragStartDay;
    private int _dragStartHour;
    private int _dragStartWeek;
    private ShiftEntry? _dragEntry;
    private int _dragOrigStartHour;
    private int _dragOrigStartMinute;
    private DayOfWeek _dragOrigDay;
    private int _dragOrigDurationMinutes;
    private int _dragOrigWeek;

    private const float DragThreshold = 4f;
    private const float EdgePixels = 6f;

    private void HandleInteraction(Vector2 origin, float labelW, float cw, float ch,
        float dualHeaderH, float gridBottomY)
    {
        // Don't process interactions when any dialog is open
        if (_showEntryDialog || _showDeleteConfirm)
        {
            if (_dragState != DragState.Idle)
                CancelDrag();
            return;
        }

        // Don't process interactions when window isn't focused
        if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
        {
            if (_dragState != DragState.Idle)
                CancelDrag();
            return;
        }

        var mouse = ImGui.GetMousePos();

        // Strict grid bounds
        float gridLeft = origin.X + labelW;
        float gridTop = origin.Y + dualHeaderH;
        float gridRight = origin.X + labelW + 24 * cw;
        float gridBottom = gridBottomY;

        bool mouseInCellArea = mouse.X >= gridLeft && mouse.X < gridRight
                            && mouse.Y >= gridTop && mouse.Y < gridBottom;

        // Suppress window drag during interaction
        if (_dragState != DragState.Idle)
            ImGui.SetWindowFocus();

        // Escape cancels
        if (ImGui.IsKeyPressed(ImGuiKey.Escape) && _dragState != DragState.Idle)
        {
            CancelDrag();
            return;
        }

        // Grid coords under mouse
        int mouseDispHour = (int)((mouse.X - gridLeft) / cw);
        mouseDispHour = Math.Clamp(mouseDispHour, 0, 23);

        int mouseWeek = 0;
        DayOfWeek mouseDay = DayOfWeek.Monday;
        bool foundRow = false;
        {
            int weekCount = _config.TwoWeekMode ? 2 : 1;
            for (int w = 0; w < weekCount && !foundRow; w++)
            {
                for (int d = 0; d < DayOrder.Length; d++)
                {
                    var key = (w, DayOrder[d]);
                    if (!_rowYPositions.ContainsKey(key)) continue;
                    float ry = _rowYPositions[key];
                    float rh = _daySubRowCounts[key] * ch;
                    if (mouse.Y >= ry && mouse.Y < ry + rh)
                    {
                        mouseWeek = w;
                        mouseDay = DayOrder[d];
                        foundRow = true;
                        break;
                    }
                }
            }
        }

        bool inGrid = mouseInCellArea && foundRow;

        if (!inGrid && _dragState == DragState.Idle)
            return;

        // Hit-test using minute-based positions
        ShiftPlacement? hitPlacement = null;
        bool hitLeftEdge = false;
        bool hitRightEdge = false;
        bool hitCommandBtn = false;

        if (inGrid)
        {
            var key = (mouseWeek, mouseDay);
            var placements = _dayLayouts.ContainsKey(key) ? _dayLayouts[key] : null;
            if (placements != null && _rowYPositions.ContainsKey(key))
            {
                float dayY = _rowYPositions[key];
                foreach (var p in placements)
                {
                    float x1 = gridLeft + (p.DisplayStartMinute / 60f) * cw;
                    float x2 = gridLeft + ((p.DisplayStartMinute + p.DisplayDurationMinutes) / 60f) * cw;
                    float y1 = dayY + p.SubRow * ch;
                    float y2 = y1 + ch;

                    if (mouse.X >= x1 && mouse.X < x2 && mouse.Y >= y1 && mouse.Y < y2)
                    {
                        hitPlacement = p;
                        hitLeftEdge = _editModeActive && mouse.X - x1 <= EdgePixels && !p.IsOverflowStart;
                        hitRightEdge = _editModeActive && x2 - mouse.X <= EdgePixels && !p.IsOverflowEnd;

                        // Command button hit-test
                        bool hasCmd = !string.IsNullOrWhiteSpace(p.Entry.Command) && !p.IsOverflowStart
                                      && !_ghostEntryIds.Contains(p.Entry.Id);
                        if (hasCmd)
                        {
                            float cmdBtnW = Math.Min(ch - 2, 18f);
                            float cbx1 = x2 - cmdBtnW - 2;
                            if (mouse.X >= cbx1 && mouse.X < x2)
                                hitCommandBtn = true;
                        }
                        break;
                    }
                }
            }
        }

        // EST tooltip on hover — entries are stored in EST, show directly
        if (_dragState == DragState.Idle && hitPlacement != null && !hitCommandBtn)
        {
            var entry = hitPlacement.Entry;
            ImGui.BeginTooltip();
            ImGui.TextUnformatted($"EST: {entry.Day} {entry.StartHour:00}:{entry.StartMinute:00}");
            ImGui.EndTooltip();
        }

        // Cursor feedback
        if (_dragState == DragState.Idle)
        {
            if (hitCommandBtn)
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            else if (_editModeActive && (hitLeftEdge || hitRightEdge))
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
            else if (_editModeActive && hitPlacement != null)
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }
        else if (_dragState is DragState.ActiveResizeLeft or DragState.ActiveResizeRight
                 or DragState.PendingResizeLeft or DragState.PendingResizeRight)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        }
        else if (_dragState is DragState.ActiveMove or DragState.PendingMove)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        // State machine
        switch (_dragState)
        {
            case DragState.Idle:
                // Command button click (always active, not just edit mode)
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && hitCommandBtn && hitPlacement != null)
                {
                    var cmd = hitPlacement.Entry.Command.Trim();
                    if (!string.IsNullOrEmpty(cmd))
                        Plugin.SendChatCommand(cmd);
                    break;
                }

                // Double-click on entry -> open dialog (always active)
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && inGrid && hitPlacement != null)
                {
                    OpenEntryDialog(hitPlacement.Entry, isNew: false);
                    break;
                }

                // Double-click on empty area -> create new entry (always active)
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && inGrid && hitPlacement == null)
                {
                    var (estDay, estHour) = DisplayToEst(mouseDay, mouseDispHour);
                    var newEntry = new ShiftEntry
                    {
                        Day = estDay,
                        StartHour = estHour,
                        StartMinute = 0,
                        DurationMinutes = 60,
                        Week = mouseWeek,
                    };
                    _config.Entries.Add(newEntry);
                    _ghostEntryIds.Add(newEntry.Id);
                    _save();
                    OpenEntryDialog(newEntry, isNew: true);
                    break;
                }

                // Right-click on entry -> open dialog (always active)
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && hitPlacement != null)
                {
                    OpenEntryDialog(hitPlacement.Entry, isNew: false);
                    break;
                }

                // Drag operations only in edit mode
                if (_editModeActive && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && inGrid)
                {
                    _dragStartMouse = mouse;
                    _dragStartDay = mouseDay;
                    _dragStartHour = mouseDispHour;
                    _dragStartWeek = mouseWeek;

                    if (hitPlacement != null && !hitCommandBtn)
                    {
                        _dragEntry = hitPlacement.Entry;
                        _dragOrigStartHour = hitPlacement.Entry.StartHour;
                        _dragOrigStartMinute = hitPlacement.Entry.StartMinute;
                        _dragOrigDay = hitPlacement.Entry.Day;
                        _dragOrigDurationMinutes = hitPlacement.Entry.DurationMinutes;
                        _dragOrigWeek = hitPlacement.Entry.Week;

                        if (hitLeftEdge)
                            _dragState = DragState.PendingResizeLeft;
                        else if (hitRightEdge)
                            _dragState = DragState.PendingResizeRight;
                        else
                            _dragState = DragState.PendingMove;
                    }
                }
                break;

            case DragState.PendingResizeLeft:
            case DragState.PendingResizeRight:
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    _dragState = DragState.Idle;
                else if (Vector2.Distance(mouse, _dragStartMouse) >= DragThreshold)
                    _dragState = _dragState == DragState.PendingResizeLeft
                        ? DragState.ActiveResizeLeft
                        : DragState.ActiveResizeRight;
                break;

            case DragState.ActiveResizeLeft:
                if (_dragEntry != null)
                {
                    int delta = mouseDispHour - _dragStartHour;
                    var (estDay, estHour) = DisplayToEst(_dragStartDay, _dragStartHour + delta);

                    // Compute correct duration across day boundaries
                    int origDayLinear = MondayIndex(_dragOrigDay);
                    int newDayLinear = MondayIndex(estDay);
                    int dayDiff = origDayLinear - newDayLinear;
                    if (dayDiff < -3) dayDiff += 7;
                    if (dayDiff > 3) dayDiff -= 7;

                    // Original end in total minutes from day start
                    int origEndTotalMin = _dragOrigStartHour * 60 + _dragOrigStartMinute + _dragOrigDurationMinutes;
                    int newStartTotalMin = estHour * 60; // resize snaps to hour boundary
                    int newDuration = dayDiff * 24 * 60 + origEndTotalMin - newStartTotalMin;
                    if (newDuration < 15) newDuration = 15;
                    if (newDuration > 48 * 60) newDuration = 48 * 60;

                    _dragEntry.Day = estDay;
                    _dragEntry.StartHour = estHour;
                    _dragEntry.StartMinute = 0; // resize snaps to hour
                    _dragEntry.DurationMinutes = newDuration;

                    // If day crossed backward past Monday in 2-week mode, adjust week
                    if (_config.TwoWeekMode)
                    {
                        int origLinear = _dragOrigWeek * 7 + MondayIndex(_dragOrigDay);
                        int newLinear = origLinear + (newDayLinear - origDayLinear);
                        if (newDayLinear - origDayLinear > 3) newLinear -= 7;
                        if (newDayLinear - origDayLinear < -3) newLinear += 7;
                        int newWeek = Math.Clamp(newLinear / 7, 0, 1);
                        _dragEntry.Week = newWeek;
                    }

                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    {
                        _save();
                        _dragState = DragState.Idle;
                    }
                }
                break;

            case DragState.ActiveResizeRight:
                if (_dragEntry != null)
                {
                    int delta = mouseDispHour - _dragStartHour;
                    int newDuration = _dragOrigDurationMinutes + delta * 60;
                    if (newDuration < 15) newDuration = 15;
                    if (newDuration > 24 * 60) newDuration = 24 * 60;
                    _dragEntry.DurationMinutes = newDuration;

                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    {
                        _save();
                        _dragState = DragState.Idle;
                    }
                }
                break;

            case DragState.PendingMove:
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    _dragState = DragState.Idle;
                else if (Vector2.Distance(mouse, _dragStartMouse) >= DragThreshold)
                    _dragState = DragState.ActiveMove;
                break;

            case DragState.ActiveMove:
                if (_dragEntry != null)
                {
                    if (inGrid)
                    {
                        // The grab offset: where in the entry the user clicked (display coords)
                        // _dragStartHour = display hour of mouse at click time
                        // We need the display start hour of the entry at click time
                        var (_, origDispStartH) = EstToDisplay(_dragOrigDay, _dragOrigStartHour);
                        int grabOffset = _dragStartHour - origDispStartH;

                        // New display start = mouse position minus grab offset
                        int newDispStartHour = mouseDispHour - grabOffset;

                        // Convert display position to CT
                        var (newEstDay, newEstHour) = DisplayToEst(mouseDay, newDispStartHour);
                        _dragEntry.Day = newEstDay;
                        _dragEntry.StartHour = newEstHour;
                        _dragEntry.StartMinute = _dragOrigStartMinute;
                        _dragEntry.Week = mouseWeek;
                    }

                    if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                    {
                        if (!inGrid)
                        {
                            // Dropped outside -> restore position, then ask to delete
                            _dragEntry.StartHour = _dragOrigStartHour;
                            _dragEntry.StartMinute = _dragOrigStartMinute;
                            _dragEntry.Day = _dragOrigDay;
                            _dragEntry.Week = _dragOrigWeek;
                            RequestDeleteEntry(_dragEntry);
                        }
                        _save();
                        _dragState = DragState.Idle;
                        _dragEntry = null;
                    }
                }
                break;
        }

        // Absorb mouse to prevent window drag
        if (_dragState != DragState.Idle)
        {
            var cursor = ImGui.GetCursorPos();
            ImGui.SetCursorScreenPos(_gridOrigin);
            ImGui.InvisibleButton("##grid_drag_block",
                new Vector2(_gridLabelWidth + 24 * _gridCellWidth, _gridHeaderHeight + _gridTotalRowsHeight));
            ImGui.SetCursorPos(cursor);
        }
    }

    private void CancelDrag()
    {
        if (_dragEntry != null && _dragState is DragState.ActiveResizeLeft or DragState.ActiveResizeRight)
        {
            _dragEntry.Day = _dragOrigDay;
            _dragEntry.StartHour = _dragOrigStartHour;
            _dragEntry.StartMinute = _dragOrigStartMinute;
            _dragEntry.DurationMinutes = _dragOrigDurationMinutes;
            _dragEntry.Week = _dragOrigWeek;
        }
        else if (_dragEntry != null && _dragState == DragState.ActiveMove)
        {
            _dragEntry.StartHour = _dragOrigStartHour;
            _dragEntry.StartMinute = _dragOrigStartMinute;
            _dragEntry.Day = _dragOrigDay;
            _dragEntry.Week = _dragOrigWeek;
        }

        _dragState = DragState.Idle;
        _dragEntry = null;
    }
}
