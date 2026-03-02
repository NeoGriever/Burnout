using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace Burnout.Windows;

public partial class BurnoutWindow : Window, IDisposable
{
    private enum Page { Schedule, Settings }
    private Page _page = Page.Schedule;

    private readonly Configuration _config;
    private readonly Action _save;
    private bool _isSidebarVisible = true;
    private bool _editModeActive;

    // Timezone helpers
    private static readonly TimeZoneInfo EstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
    private TimeZoneInfo _displayTimeZone = EstTimeZone;
    private int _tzOffsetHours;

    public BurnoutWindow(Configuration config, Action save)
        : base("Burnout###Burnout")
    {
        _config = config;
        _save = save;

        Size = new Vector2(1400, 620);
        SizeCondition = ImGuiCond.FirstUseEver;
        RespectCloseHotkey = false;

        RefreshTimezone();
    }

    public void Dispose() { }
    public void OpenMain() { _page = Page.Schedule; IsOpen = true; }
    public void OpenSettings() { _page = Page.Settings; IsOpen = true; }

    public void RefreshTimezone()
    {
        try
        {
            _displayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(_config.SelectedDisplayTimezone);
        }
        catch
        {
            _displayTimeZone = EstTimeZone;
        }

        var now = DateTimeOffset.UtcNow;
        var displayOffset = _displayTimeZone.GetUtcOffset(now);
        var estOffset = EstTimeZone.GetUtcOffset(now);
        var diff = displayOffset - estOffset;
        _tzOffsetHours = (int)Math.Round(diff.TotalHours);
    }

    /// <summary>
    /// Converts display-timezone (day, hour) back to EST (day, hour).
    /// </summary>
    public (DayOfWeek day, int hour) DisplayToEst(DayOfWeek displayDay, int displayHour)
    {
        int estHour = displayHour - _tzOffsetHours;
        int dayShift = 0;
        while (estHour < 0) { estHour += 24; dayShift--; }
        while (estHour >= 24) { estHour -= 24; dayShift++; }

        int dayIdx = ((int)displayDay + dayShift % 7 + 7) % 7;
        return ((DayOfWeek)dayIdx, estHour);
    }

    /// <summary>
    /// Converts EST (day, hour) to display-timezone (day, hour).
    /// </summary>
    public (DayOfWeek day, int hour) EstToDisplay(DayOfWeek estDay, int estHour)
    {
        int dispHour = estHour + _tzOffsetHours;
        int dayShift = 0;
        while (dispHour < 0) { dispHour += 24; dayShift--; }
        while (dispHour >= 24) { dispHour -= 24; dayShift++; }

        int dayIdx = ((int)estDay + dayShift % 7 + 7) % 7;
        return ((DayOfWeek)dayIdx, dispHour);
    }

    public override void Draw()
    {
        RefreshTimezone();
        var avail = ImGui.GetContentRegionAvail();
        var sidebarWidth = _isSidebarVisible ? 160f : 0f;

        // ---- SIDEBAR ----
        if (_isSidebarVisible)
        {
            ImGui.BeginChild("sidebar", new Vector2(sidebarWidth, avail.Y), true);
            if (ImGui.SmallButton("<##hide_sidebar")) _isSidebarVisible = false;
            ImGui.SameLine();
            ImGui.TextUnformatted("Burnout");

            ImGui.Separator();
            NavButton(Page.Schedule, "Schedule");
            ImGui.Separator();
            NavButton(Page.Settings, "Settings");

            ImGui.EndChild();
            ImGui.SameLine();
        }

        // ---- CONTENT ----
        ImGui.BeginChild("content", new Vector2(0, avail.Y), true);

        if (!_isSidebarVisible)
        {
            if (ImGui.SmallButton(">##show_sidebar")) _isSidebarVisible = true;
            ImGui.SameLine();
            ImGui.TextDisabled($"Page: {_page}");
            ImGui.Separator();
        }

        switch (_page)
        {
            case Page.Schedule: DrawSchedulePage(); break;
            case Page.Settings: DrawSettingsPage(); break;
        }

        ImGui.EndChild();
    }

    private void NavButton(Page page, string label)
    {
        var selected = _page == page;
        if (selected)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.15f, 0.35f, 0.65f, 0.9f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.2f, 0.4f, 0.75f, 1f));
        }

        if (ImGui.Button(label, new Vector2(-1, 40))) _page = page;

        if (selected)
        {
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar();
        }
    }
}
