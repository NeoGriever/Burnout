using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Burnout.Windows;

public partial class BurnoutWindow
{
    private static readonly DayOfWeek[] DayOrder =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
        DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
    };

    private static readonly string[] DayLabels = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

    private readonly Dictionary<(int week, DayOfWeek day), List<ShiftPlacement>> _dayLayouts = new();
    private readonly Dictionary<(int week, DayOfWeek day), int> _daySubRowCounts = new();
    private readonly HashSet<string> _ghostEntryIds = new();

    internal class ShiftPlacement
    {
        public ShiftEntry Entry = null!;
        public int Week;
        public DayOfWeek DisplayDay;
        public int DisplayStartMinute;    // 0-1439 within the day
        public int DisplayDurationMinutes;
        public int SubRow;
        public bool IsOverflowStart;
        public bool IsOverflowEnd;
    }

    private Vector2 _gridOrigin;
    private float _gridLabelWidth;
    private float _gridCellWidth;
    private float _gridCellHeight;
    private float _gridHeaderHeight;
    private float _gridTotalRowsHeight;
    private readonly Dictionary<(int week, DayOfWeek day), float> _rowYPositions = new();

    /// <summary>Monday=0 .. Sunday=6</summary>
    internal static int MondayIndex(DayOfWeek d) => ((int)d + 6) % 7;

    private static DayOfWeek DayFromLinearSlot(int slot)
    {
        int idx = ((slot % 7) + 7) % 7; // 0=Mon .. 6=Sun
        return DayOrder[idx];
    }

    private void DrawSchedulePage()
    {
        DrawToolbar();
        ComputeAllLayouts();
        DrawGrid();
        DrawEntryDialog();
        DrawDeleteConfirmDialog();
    }

    private void DrawToolbar()
    {
        ImGui.TextUnformatted("Display TZ:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(280);

        var tzList = TimeZoneInfo.GetSystemTimeZones();
        var currentIdx = 0;
        var tzNames = new string[tzList.Count];
        for (int i = 0; i < tzList.Count; i++)
        {
            tzNames[i] = tzList[i].DisplayName;
            if (tzList[i].Id == _config.SelectedDisplayTimezone)
                currentIdx = i;
        }

        if (ImGui.Combo("##tz_toolbar", ref currentIdx, tzNames, tzNames.Length))
        {
            _config.SelectedDisplayTimezone = tzList[currentIdx].Id;
            _save();
            RefreshTimezone();
        }

        ImGui.SameLine();
        var offsetLabel = _tzOffsetHours == 0
            ? "EST"
            : $"EST{(_tzOffsetHours > 0 ? "+" : "")}{_tzOffsetHours}h";
        ImGui.TextDisabled($"(Offset: {offsetLabel})");

        // Right-aligned buttons: New Entry + Edit toggle
        float rightX = ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX();

        // Edit mode toggle
        float editBtnW = 50f;
        ImGui.SameLine(rightX - editBtnW);
        var editCol = _editModeActive
            ? new Vector4(0.2f, 0.6f, 0.2f, 1f)
            : new Vector4(0.5f, 0.15f, 0.15f, 1f);
        var editColHover = _editModeActive
            ? new Vector4(0.3f, 0.75f, 0.3f, 1f)
            : new Vector4(0.65f, 0.2f, 0.2f, 1f);
        ImGui.PushStyleColor(ImGuiCol.Button, editCol);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, editColHover);
        if (ImGui.Button(_editModeActive ? "Edit" : "Edit", new Vector2(editBtnW, 0)))
            _editModeActive = !_editModeActive;
        ImGui.PopStyleColor(2);

        // New Entry button (only in edit mode)
        if (_editModeActive)
        {
            float newBtnW = 80f;
            ImGui.SameLine(rightX - editBtnW - newBtnW - 4);
            if (ImGui.Button("+ New", new Vector2(newBtnW, 0)))
            {
                var newEntry = new ShiftEntry();
                _config.Entries.Add(newEntry);
                _ghostEntryIds.Add(newEntry.Id);
                _save();
                OpenEntryDialog(newEntry, isNew: true);
            }
        }

        ImGui.Separator();
    }

    private void ComputeAllLayouts()
    {
        _dayLayouts.Clear();
        _daySubRowCounts.Clear();

        int weekCount = _config.TwoWeekMode ? 2 : 1;
        int totalSlots = weekCount * 7;

        for (int w = 0; w < weekCount; w++)
            foreach (var dow in DayOrder)
            {
                var key = (w, dow);
                _dayLayouts[key] = new List<ShiftPlacement>();
                _daySubRowCounts[key] = 1;
            }

        foreach (var entry in _config.Entries)
        {
            if (entry.Week >= weekCount) continue;

            var (dispDay, dispHour) = EstToDisplay(entry.Day, entry.StartHour);
            int dispStartMinute = dispHour * 60 + entry.StartMinute;

            // Compute linear slot of display start
            int ctDayIdx = MondayIndex(entry.Day);
            int dispDayIdx = MondayIndex(dispDay);
            int dayDelta = dispDayIdx - ctDayIdx;
            if (dayDelta > 3) dayDelta -= 7;
            if (dayDelta < -3) dayDelta += 7;

            int startLinear = entry.Week * 7 + ctDayIdx + dayDelta;

            int remaining = entry.DurationMinutes;
            int curLinear = startLinear;
            int curStartMin = dispStartMinute;
            bool isFirst = true;
            int maxSegs = totalSlots + 1; // safety limit
            int segCount = 0;

            while (remaining > 0 && segCount < maxSegs)
            {
                // Wrap across the entire cycle (1-week or 2-week)
                int effectiveLinear = ((curLinear % totalSlots) + totalSlots) % totalSlots;

                int segLen = Math.Min(remaining, 24 * 60 - curStartMin);
                int segWeek = effectiveLinear / 7;
                var segDay = DayFromLinearSlot(effectiveLinear);
                var key = (segWeek, segDay);

                if (_dayLayouts.ContainsKey(key))
                {
                    _dayLayouts[key].Add(new ShiftPlacement
                    {
                        Entry = entry,
                        Week = segWeek,
                        DisplayDay = segDay,
                        DisplayStartMinute = curStartMin,
                        DisplayDurationMinutes = segLen,
                        IsOverflowStart = !isFirst,
                        IsOverflowEnd = remaining > segLen,
                    });
                }

                remaining -= segLen;
                curLinear++;
                curStartMin = 0;
                isFirst = false;
                segCount++;
            }
        }

        // Sub-rows (minute-based overlap detection)
        foreach (var key in _dayLayouts.Keys)
        {
            var placements = _dayLayouts[key].OrderBy(p => p.DisplayStartMinute).ToList();
            var subRowEnds = new List<int>(); // end in minutes

            foreach (var p in placements)
            {
                int assigned = -1;
                for (int sr = 0; sr < subRowEnds.Count; sr++)
                {
                    if (subRowEnds[sr] <= p.DisplayStartMinute)
                    {
                        assigned = sr;
                        break;
                    }
                }
                if (assigned < 0)
                {
                    assigned = subRowEnds.Count;
                    subRowEnds.Add(0);
                }
                p.SubRow = assigned;
                subRowEnds[assigned] = p.DisplayStartMinute + p.DisplayDurationMinutes;
            }

            _daySubRowCounts[key] = Math.Max(1, subRowEnds.Count);
        }
    }

    private void DrawGrid()
    {
        var ch = _config.CellHeight;
        var labelWidth = 50f;
        var headerHeight = 22f;
        var weekSepHeight = 24f;

        var availWidth = ImGui.GetContentRegionAvail().X;
        var cw = (availWidth - labelWidth) / 24f;
        if (cw < 20f) cw = 20f;

        int weekCount = _config.TwoWeekMode ? 2 : 1;
        float totalW = labelWidth + 24 * cw;

        float totalRowsH = 0;
        for (int w = 0; w < weekCount; w++)
        {
            if (w > 0) totalRowsH += weekSepHeight;
            foreach (var dow in DayOrder)
                totalRowsH += _daySubRowCounts[(w, dow)] * ch;
        }
        float totalH = headerHeight + totalRowsH + 10;

        ImGui.BeginChild("grid_scroll", new Vector2(0, 0), false,
            ImGuiWindowFlags.HorizontalScrollbar);

        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();

        _gridOrigin = origin;
        _gridLabelWidth = labelWidth;
        _gridCellWidth = cw;
        _gridCellHeight = ch;
        _gridHeaderHeight = headerHeight;
        _rowYPositions.Clear();

        var gridColor = ImGui.GetColorU32(new Vector4(0.3f, 0.3f, 0.3f, 1f));
        var separatorColor = ImGui.GetColorU32(new Vector4(0.45f, 0.45f, 0.45f, 1f));
        var headerBg = ImGui.GetColorU32(new Vector4(0.12f, 0.12f, 0.16f, 1f));
        var headerText = ImGui.GetColorU32(new Vector4(0.85f, 0.85f, 0.85f, 1f));
        var weekdayOdd = ImGui.GetColorU32(_config.WeekdayColorOdd);
        var weekdayEven = ImGui.GetColorU32(_config.WeekdayColorEven);
        var weekendOdd = ImGui.GetColorU32(_config.WeekendColorOdd);
        var weekendEven = ImGui.GetColorU32(_config.WeekendColorEven);

        // ---- Single header row (display TZ) ----
        drawList.AddRectFilled(origin, origin + new Vector2(totalW, headerHeight), headerBg);

        // TZ label in the label column
        var tzShort = _displayTimeZone.Id == EstTimeZone.Id ? "EST" : "TZ";
        drawList.AddText(new Vector2(origin.X + 4, origin.Y + 3), headerText, tzShort);

        // Hour labels - display timezone hours, centered
        for (int h = 0; h < 24; h++)
        {
            float x = origin.X + labelWidth + h * cw;
            var hourLabel = $"{h:00}:00";
            var labelSize = ImGui.CalcTextSize(hourLabel);
            drawList.AddText(new Vector2(x + (cw - labelSize.X) * 0.5f, origin.Y + 3), headerText, hourLabel);
            drawList.AddLine(new Vector2(x, origin.Y), new Vector2(x, origin.Y + headerHeight), gridColor);
        }

        drawList.AddLine(
            new Vector2(origin.X, origin.Y + headerHeight),
            new Vector2(origin.X + totalW, origin.Y + headerHeight), separatorColor, 2f);

        // ---- Day rows ----
        float rowY = origin.Y + headerHeight;
        for (int w = 0; w < weekCount; w++)
        {
            if (w > 0)
            {
                var weekSepBg = ImGui.GetColorU32(new Vector4(0.18f, 0.12f, 0.04f, 1f));
                var weekSepBorder = ImGui.GetColorU32(new Vector4(0.7f, 0.45f, 0.1f, 1f));
                drawList.AddRectFilled(
                    new Vector2(origin.X, rowY),
                    new Vector2(origin.X + totalW, rowY + weekSepHeight), weekSepBg);
                drawList.AddLine(
                    new Vector2(origin.X, rowY),
                    new Vector2(origin.X + totalW, rowY), weekSepBorder, 2f);
                drawList.AddLine(
                    new Vector2(origin.X, rowY + weekSepHeight),
                    new Vector2(origin.X + totalW, rowY + weekSepHeight), weekSepBorder, 2f);
                var wkLabel = "--- Week B ---";
                var wlSize = ImGui.CalcTextSize(wkLabel);
                drawList.AddText(
                    new Vector2(origin.X + (totalW - wlSize.X) * 0.5f,
                                rowY + (weekSepHeight - wlSize.Y) * 0.5f),
                    ImGui.GetColorU32(new Vector4(1f, 0.8f, 0.3f, 1f)), wkLabel);
                rowY += weekSepHeight;
            }

            for (int d = 0; d < DayOrder.Length; d++)
            {
                var dow = DayOrder[d];
                var key = (w, dow);
                int subRows = _daySubRowCounts[key];
                float rowH = subRows * ch;

                // Separator line at the top of every day row
                drawList.AddLine(
                    new Vector2(origin.X, rowY),
                    new Vector2(origin.X + totalW, rowY), separatorColor);

                bool isWeekend = dow == DayOfWeek.Saturday || dow == DayOfWeek.Sunday;
                var bg = isWeekend
                    ? (d % 2 == 0 ? weekendEven : weekendOdd)
                    : (d % 2 == 0 ? weekdayEven : weekdayOdd);
                drawList.AddRectFilled(
                    new Vector2(origin.X, rowY),
                    new Vector2(origin.X + totalW, rowY + rowH), bg);

                // Day label
                drawList.AddText(new Vector2(origin.X + 4, rowY + 2), headerText, DayLabels[d]);

                for (int h = 0; h <= 24; h++)
                {
                    float x = origin.X + labelWidth + h * cw;
                    var lineCol = h == 0 || h == 24 ? separatorColor : gridColor;
                    drawList.AddLine(new Vector2(x, rowY), new Vector2(x, rowY + rowH), lineCol);
                }

                drawList.AddLine(
                    new Vector2(origin.X, rowY + rowH),
                    new Vector2(origin.X + totalW, rowY + rowH), separatorColor);

                foreach (var p in _dayLayouts[key])
                {
                    bool isGhost = _ghostEntryIds.Contains(p.Entry.Id);
                    DrawShiftBlock(drawList, origin, labelWidth, cw, ch, rowY, p, isGhost);
                }

                _rowYPositions[key] = rowY;
                rowY += rowH;
            }
        }

        _gridTotalRowsHeight = rowY - (origin.Y + headerHeight);

        ImGui.Dummy(new Vector2(totalW, totalH));
        HandleInteraction(origin, labelWidth, cw, ch, headerHeight, rowY);
        ImGui.EndChild();
    }

    private void DrawShiftBlock(ImDrawListPtr drawList, Vector2 origin, float labelW,
        float cw, float ch, float rowY, ShiftPlacement p, bool isGhost)
    {
        // Minute-based pixel positions
        float x1 = origin.X + labelW + (p.DisplayStartMinute / 60f) * cw + 1;
        float x2 = origin.X + labelW + ((p.DisplayStartMinute + p.DisplayDurationMinutes) / 60f) * cw - 1;
        float y1 = rowY + p.SubRow * ch + 1;
        float y2 = y1 + ch - 2;

        if (isGhost)
        {
            var ghostFill = ImGui.GetColorU32(new Vector4(
                p.Entry.Color.X, p.Entry.Color.Y, p.Entry.Color.Z,
                p.Entry.Color.W * 0.35f));
            var ghostBorder = ImGui.GetColorU32(new Vector4(
                Math.Min(p.Entry.Color.X + 0.3f, 1f),
                Math.Min(p.Entry.Color.Y + 0.3f, 1f),
                Math.Min(p.Entry.Color.Z + 0.3f, 1f),
                0.7f));

            drawList.AddRectFilled(new Vector2(x1, y1), new Vector2(x2, y2), ghostFill, 3f);
            DrawDashedRect(drawList, x1, y1, x2, y2, ghostBorder, 6f, 4f);
        }
        else
        {
            var col = ImGui.GetColorU32(p.Entry.Color);
            var borderCol = ImGui.GetColorU32(new Vector4(
                Math.Min(p.Entry.Color.X + 0.2f, 1f),
                Math.Min(p.Entry.Color.Y + 0.2f, 1f),
                Math.Min(p.Entry.Color.Z + 0.2f, 1f),
                1f));

            switch (p.Entry.Style)
            {
                case 0:
                    drawList.AddRectFilled(new Vector2(x1, y1), new Vector2(x2, y2), col, 3f);
                    drawList.AddRect(new Vector2(x1, y1), new Vector2(x2, y2), borderCol, 3f);
                    break;
                case 1:
                    drawList.AddRectFilled(new Vector2(x1, y1), new Vector2(x2, y2), col, 3f);
                    for (float sx = x1 - (y2 - y1); sx < x2; sx += 8)
                    {
                        drawList.AddLine(
                            new Vector2(Math.Max(sx, x1), y1),
                            new Vector2(Math.Max(sx + (y2 - y1), x1), y2),
                            ImGui.GetColorU32(new Vector4(1, 1, 1, 0.15f)), 1f);
                    }
                    drawList.AddRect(new Vector2(x1, y1), new Vector2(x2, y2), borderCol, 3f);
                    break;
                case 2:
                    drawList.AddRectFilled(new Vector2(x1, y1), new Vector2(x2, y2), col, 3f);
                    var dotCol = borderCol;
                    float dotSize = 2f;
                    for (float dx = x1; dx < x2; dx += 6)
                    {
                        drawList.AddRectFilled(new Vector2(dx, y1), new Vector2(dx + dotSize, y1 + dotSize), dotCol);
                        drawList.AddRectFilled(new Vector2(dx, y2 - dotSize), new Vector2(dx + dotSize, y2), dotCol);
                    }
                    for (float dy = y1; dy < y2; dy += 6)
                    {
                        drawList.AddRectFilled(new Vector2(x1, dy), new Vector2(x1 + dotSize, dy + dotSize), dotCol);
                        drawList.AddRectFilled(new Vector2(x2 - dotSize, dy), new Vector2(x2, dy + dotSize), dotCol);
                    }
                    break;
            }
        }

        // Command button ">" on the right side (if command is set and not ghost)
        bool hasCmd = !isGhost && !string.IsNullOrWhiteSpace(p.Entry.Command) && !p.IsOverflowStart;
        float cmdBtnW = hasCmd ? Math.Min(ch - 2, 18f) : 0f;
        float cmdBtnPad = hasCmd ? 2f : 0f;

        if (hasCmd)
        {
            float cbx1 = x2 - cmdBtnW - 2;
            float cby1 = y1 + 1;
            float cbx2 = x2 - 2;
            float cby2 = y2 - 1;
            var cmdBg = ImGui.GetColorU32(new Vector4(0.15f, 0.15f, 0.15f, 0.7f));
            var cmdBorder = ImGui.GetColorU32(new Vector4(0.8f, 0.8f, 0.8f, 0.6f));
            drawList.AddRectFilled(new Vector2(cbx1, cby1), new Vector2(cbx2, cby2), cmdBg, 2f);
            drawList.AddRect(new Vector2(cbx1, cby1), new Vector2(cbx2, cby2), cmdBorder, 2f);
            var arrowSize = ImGui.CalcTextSize(">");
            drawList.AddText(
                new Vector2(cbx1 + (cmdBtnW - arrowSize.X) * 0.5f, cby1 + (cby2 - cby1 - arrowSize.Y) * 0.5f),
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)), ">");
        }

        // Label - centered horizontally and vertically
        var label = p.Entry.Label;
        if (p.IsOverflowStart) label = "< " + label;
        if (p.IsOverflowEnd) label += " >";

        var textSize = ImGui.CalcTextSize(label);
        float labelAreaRight = x2 - cmdBtnW - cmdBtnPad;
        float availW = labelAreaRight - x1 - 4;
        if (textSize.X > availW && label.Length > 3)
        {
            while (ImGui.CalcTextSize(label + "...").X > availW && label.Length > 1)
                label = label[..^1];
            label += "...";
            textSize = ImGui.CalcTextSize(label);
        }

        var textAlpha = isGhost ? 0.5f : 1f;
        var textCol = ImGui.GetColorU32(new Vector4(1, 1, 1, textAlpha));
        float textX = x1 + (labelAreaRight - x1 - textSize.X) * 0.5f;
        float textY = y1 + (ch - 2 - textSize.Y) * 0.5f;
        drawList.AddText(new Vector2(textX, textY), textCol, label);
    }

    private static void DrawDashedRect(ImDrawListPtr drawList,
        float x1, float y1, float x2, float y2, uint color, float dashLen, float gapLen)
    {
        DrawDashedLine(drawList, x1, y1, x2, y1, color, dashLen, gapLen);
        DrawDashedLine(drawList, x2, y1, x2, y2, color, dashLen, gapLen);
        DrawDashedLine(drawList, x2, y2, x1, y2, color, dashLen, gapLen);
        DrawDashedLine(drawList, x1, y2, x1, y1, color, dashLen, gapLen);
    }

    private static void DrawDashedLine(ImDrawListPtr drawList,
        float x1, float y1, float x2, float y2, uint color, float dashLen, float gapLen)
    {
        float dx = x2 - x1, dy = y2 - y1;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1f) return;
        float nx = dx / len, ny = dy / len;
        float pos = 0;
        bool draw = true;
        while (pos < len)
        {
            float segLen = draw ? dashLen : gapLen;
            if (pos + segLen > len) segLen = len - pos;
            if (draw)
            {
                drawList.AddLine(
                    new Vector2(x1 + nx * pos, y1 + ny * pos),
                    new Vector2(x1 + nx * (pos + segLen), y1 + ny * (pos + segLen)),
                    color, 1.5f);
            }
            pos += segLen;
            draw = !draw;
        }
    }
}
