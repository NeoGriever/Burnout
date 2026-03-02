using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Burnout.Windows;

public partial class BurnoutWindow
{
    private void DrawSettingsPage()
    {
        ImGui.TextUnformatted("Settings");
        ImGui.Separator();

        // ---- Timezone ----
        ImGui.TextUnformatted("Display Timezone");
        ImGui.SetNextItemWidth(400);

        var tzList = TimeZoneInfo.GetSystemTimeZones();
        var currentIdx = 0;
        var tzNames = new string[tzList.Count];
        for (int i = 0; i < tzList.Count; i++)
        {
            tzNames[i] = tzList[i].DisplayName;
            if (tzList[i].Id == _config.SelectedDisplayTimezone)
                currentIdx = i;
        }

        if (ImGui.Combo("##tz_settings", ref currentIdx, tzNames, tzNames.Length))
        {
            _config.SelectedDisplayTimezone = tzList[currentIdx].Id;
            _save();
            RefreshTimezone();
        }

        ImGui.Spacing();

        // ---- 2-Week Mode ----
        var twoWeek = _config.TwoWeekMode;
        if (ImGui.Checkbox("2-Week Cycle (Week A / Week B)", ref twoWeek))
        {
            _config.TwoWeekMode = twoWeek;
            if (!twoWeek)
            {
                // Reset all entries to week 0
                foreach (var e in _config.Entries)
                    e.Week = 0;
            }
            _save();
        }

        ImGui.Spacing();

        // ---- Confirm on Delete ----
        var confirmDel = _config.ConfirmOnDelete;
        if (ImGui.Checkbox("Confirm before deleting entries", ref confirmDel))
        {
            _config.ConfirmOnDelete = confirmDel;
            _save();
        }

        ImGui.Spacing();
        ImGui.Separator();

        // ---- Cell Size ----
        ImGui.TextUnformatted("Grid Cell Size");

        var cellH = _config.CellHeight;
        if (ImGui.SliderFloat("Cell Height", ref cellH, 20f, 60f))
        {
            _config.CellHeight = cellH;
            _save();
        }

        ImGui.Spacing();
        ImGui.Separator();

        // ---- Row Colors ----
        ImGui.TextUnformatted("Row Colors");

        var wdOdd = _config.WeekdayColorOdd;
        if (ImGui.ColorEdit4("Weekday Color Odd", ref wdOdd))
        {
            _config.WeekdayColorOdd = wdOdd;
            _save();
        }

        var wdEven = _config.WeekdayColorEven;
        if (ImGui.ColorEdit4("Weekday Color Even", ref wdEven))
        {
            _config.WeekdayColorEven = wdEven;
            _save();
        }

        var weOdd = _config.WeekendColorOdd;
        if (ImGui.ColorEdit4("Weekend Color Odd", ref weOdd))
        {
            _config.WeekendColorOdd = weOdd;
            _save();
        }

        var weEven = _config.WeekendColorEven;
        if (ImGui.ColorEdit4("Weekend Color Even", ref weEven))
        {
            _config.WeekendColorEven = weEven;
            _save();
        }

        ImGui.Spacing();
        ImGui.Separator();

        // ---- Clear All ----
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.15f, 0.15f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.2f, 0.2f, 1f));
        if (ImGui.Button("Clear All Entries", new Vector2(200, 30)))
        {
            ImGui.OpenPopup("ConfirmClear");
        }
        ImGui.PopStyleColor(2);

        if (ImGui.BeginPopup("ConfirmClear"))
        {
            ImGui.TextUnformatted("Really delete all entries?");
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.15f, 0.15f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.2f, 0.2f, 1f));
            if (ImGui.Button("Yes, delete all"))
            {
                _config.Entries.Clear();
                _save();
                ImGui.CloseCurrentPopup();
            }
            ImGui.PopStyleColor(2);
            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }
    }
}
