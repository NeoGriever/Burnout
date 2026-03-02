using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Burnout.Windows;

public partial class BurnoutWindow
{
    private bool _showEntryDialog;
    private ShiftEntry? _editingEntry;
    private bool _isNewEntry;
    private string _dialogLabel = "";
    private string _dialogCommand = "";

    // Delete confirmation
    private bool _showDeleteConfirm;
    private ShiftEntry? _deleteTarget;

    // Dialog time fields (EST)
    private int _dialogStartDay;  // DayOfWeek cast (0=Sun..6=Sat)
    private int _dialogStartHour;
    private int _dialogStartMin;  // index into MinuteOptions (0..3)
    private int _dialogEndDay;    // DayOfWeek cast
    private int _dialogEndHour;
    private int _dialogEndMin;    // index into MinuteOptions (0..3)

    private static readonly string[] DayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
    private static readonly string[] MinuteOptions = { "00", "15", "30", "45" };

    private void OpenEntryDialog(ShiftEntry entry, bool isNew)
    {
        _editingEntry = entry;
        _isNewEntry = isNew;
        _showEntryDialog = true;
        _dialogLabel = entry.Label;
        _dialogCommand = entry.Command;

        // Populate time fields from entry (EST)
        _dialogStartDay = (int)entry.Day;
        _dialogStartHour = entry.StartHour;
        _dialogStartMin = entry.StartMinute / 15;

        // Compute end day + time from duration
        int totalEndMin = entry.StartHour * 60 + entry.StartMinute + entry.DurationMinutes;
        int endDayOffset = totalEndMin / (24 * 60);
        int endMinOfDay = totalEndMin % (24 * 60);

        int startDayLinear = MondayIndex(entry.Day);
        int endDayLinear = (startDayLinear + endDayOffset) % 7;
        _dialogEndDay = (int)DayOrder[endDayLinear];
        _dialogEndHour = endMinOfDay / 60;
        _dialogEndMin = (endMinOfDay % 60) / 15;
    }

    private void CloseEntryDialog()
    {
        _showEntryDialog = false;
        _editingEntry = null;
    }

    /// <summary>
    /// Requests deletion of an entry. Shows confirmation if enabled in config.
    /// Can be called from the entry dialog or from drag-outside-grid.
    /// </summary>
    private void RequestDeleteEntry(ShiftEntry entry)
    {
        if (_config.ConfirmOnDelete)
        {
            _deleteTarget = entry;
            _showDeleteConfirm = true;
        }
        else
        {
            _config.Entries.Remove(entry);
            _ghostEntryIds.Remove(entry.Id);
            _save();
            if (_showEntryDialog && _editingEntry == entry)
                CloseEntryDialog();
        }
    }

    private void DrawEntryDialog()
    {
        if (!_showEntryDialog || _editingEntry == null)
            return;

        ImGui.SetNextWindowSize(new Vector2(420, 0), ImGuiCond.Always);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;
        bool open = true;
        if (ImGui.Begin($"Shift Entry###entry_dialog", ref open, flags))
        {
            if (!open)
            {
                if (_isNewEntry)
                {
                    _config.Entries.Remove(_editingEntry);
                    _ghostEntryIds.Remove(_editingEntry.Id);
                }
                _save();
                CloseEntryDialog();
                ImGui.End();
                return;
            }

            // Label
            ImGui.InputText("Label", ref _dialogLabel, 128);

            // Color
            var color = _editingEntry.Color;
            if (ImGui.ColorEdit4("Color", ref color))
                _editingEntry.Color = color;

            // Style
            var styleNames = new[] { "Solid", "Striped", "Dotted Border" };
            var style = _editingEntry.Style;
            if (ImGui.Combo("Style", ref style, styleNames, styleNames.Length))
                _editingEntry.Style = style;

            // Command
            ImGui.InputText("Command", ref _dialogCommand, 256);
            ImGui.TextDisabled("e.g. /li specialclub");

            ImGui.Separator();

            // ---- EST Time Editors ----
            ImGui.TextUnformatted("Time (EST):");

            // Start: Day + Hour:Minute
            ImGui.TextUnformatted("Start:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120);
            if (ImGui.Combo("##start_day", ref _dialogStartDay, DayNames, DayNames.Length))
                ApplyDialogTime();
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            if (ImGui.InputInt("##start_h", ref _dialogStartHour, 1, 1))
            {
                _dialogStartHour = Math.Clamp(_dialogStartHour, 0, 23);
                ApplyDialogTime();
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(":");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(55);
            if (ImGui.Combo("##start_m", ref _dialogStartMin, MinuteOptions, MinuteOptions.Length))
                ApplyDialogTime();

            // End: Day + Hour:Minute
            ImGui.TextUnformatted("End:  ");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120);
            if (ImGui.Combo("##end_day", ref _dialogEndDay, DayNames, DayNames.Length))
                ApplyDialogTime();
            ImGui.SameLine();
            ImGui.SetNextItemWidth(50);
            if (ImGui.InputInt("##end_h", ref _dialogEndHour, 1, 1))
            {
                _dialogEndHour = Math.Clamp(_dialogEndHour, 0, 23);
                ApplyDialogTime();
            }
            ImGui.SameLine();
            ImGui.TextUnformatted(":");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(55);
            if (ImGui.Combo("##end_m", ref _dialogEndMin, MinuteOptions, MinuteOptions.Length))
                ApplyDialogTime();

            // Duration info
            int durH = _editingEntry.DurationMinutes / 60;
            int durM = _editingEntry.DurationMinutes % 60;
            ImGui.TextDisabled($"Duration: {durH}h {durM:00}m");

            ImGui.Spacing();

            // Display TZ preview
            var (dispDay, dispHour) = EstToDisplay(_editingEntry.Day, _editingEntry.StartHour);
            int dispStartMin = dispHour * 60 + _editingEntry.StartMinute;
            int dispEndMin = dispStartMin + _editingEntry.DurationMinutes;
            int dispEndDayOffset = dispEndMin / (24 * 60);
            int dispEndMinOfDay = dispEndMin % (24 * 60);

            int dispStartDayLinear = MondayIndex(dispDay);
            var dispEndDay = DayFromLinearSlot(dispStartDayLinear + dispEndDayOffset);
            int dispEndH = dispEndMinOfDay / 60;
            int dispEndM = dispEndMinOfDay % 60;

            ImGui.TextDisabled($"Display: {dispDay} {dispHour:00}:{_editingEntry.StartMinute:00} - {dispEndDay} {dispEndH:00}:{dispEndM:00}");

            ImGui.Separator();

            // OK button (green)
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.2f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.3f, 0.75f, 0.3f, 1f));
            if (ImGui.Button("OK", new Vector2(110, 30)))
            {
                _editingEntry!.Label = _dialogLabel;
                _editingEntry.Command = _dialogCommand;
                _ghostEntryIds.Remove(_editingEntry.Id);
                _save();
                CloseEntryDialog();
            }
            ImGui.PopStyleColor(2);

            ImGui.SameLine();

            // Cancel button
            if (ImGui.Button("Cancel", new Vector2(110, 30)))
            {
                if (_isNewEntry)
                {
                    _config.Entries.Remove(_editingEntry!);
                    _ghostEntryIds.Remove(_editingEntry!.Id);
                }
                _save();
                CloseEntryDialog();
            }

            // Delete button (red, only for existing entries)
            if (!_isNewEntry)
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.15f, 0.15f, 1f));
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.2f, 0.2f, 1f));
                if (ImGui.Button("Delete", new Vector2(110, 30)))
                    RequestDeleteEntry(_editingEntry!);
                ImGui.PopStyleColor(2);
            }

            ImGui.End();
        }
    }

    private void DrawDeleteConfirmDialog()
    {
        if (!_showDeleteConfirm || _deleteTarget == null)
            return;

        ImGui.SetNextWindowSize(new Vector2(320, 0), ImGuiCond.Always);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        var flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.AlwaysAutoResize;
        bool open = true;
        if (ImGui.Begin("Delete Entry?###delete_confirm", ref open, flags))
        {
            if (!open)
            {
                _showDeleteConfirm = false;
                _deleteTarget = null;
                ImGui.End();
                return;
            }

            ImGui.TextUnformatted($"Delete \"{_deleteTarget.Label}\"?");
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.6f, 0.15f, 0.15f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.2f, 0.2f, 1f));
            if (ImGui.Button("Yes, delete", new Vector2(130, 30)))
            {
                _config.Entries.Remove(_deleteTarget);
                _ghostEntryIds.Remove(_deleteTarget.Id);
                _save();
                if (_showEntryDialog && _editingEntry == _deleteTarget)
                    CloseEntryDialog();
                _showDeleteConfirm = false;
                _deleteTarget = null;
            }
            ImGui.PopStyleColor(2);

            ImGui.SameLine();

            if (ImGui.Button("Cancel", new Vector2(130, 30)))
            {
                _showDeleteConfirm = false;
                _deleteTarget = null;
            }

            ImGui.End();
        }
    }

    private void ApplyDialogTime()
    {
        if (_editingEntry == null) return;

        _editingEntry.Day = (DayOfWeek)_dialogStartDay;
        _editingEntry.StartHour = _dialogStartHour;
        _editingEntry.StartMinute = _dialogStartMin * 15;

        // Compute duration from start day+time to end day+time
        int startDayLinear = MondayIndex((DayOfWeek)_dialogStartDay);
        int endDayLinear = MondayIndex((DayOfWeek)_dialogEndDay);
        int dayDiff = endDayLinear - startDayLinear;
        if (dayDiff < 0) dayDiff += 7; // wrap forward

        int startMinOfDay = _dialogStartHour * 60 + _dialogStartMin * 15;
        int endMinOfDay = _dialogEndHour * 60 + _dialogEndMin * 15;
        int duration = dayDiff * 24 * 60 + endMinOfDay - startMinOfDay;

        // If same day and end <= start, treat as next-week wrap (7 days)
        if (duration <= 0) duration += 7 * 24 * 60;
        if (duration < 15) duration = 15;

        _editingEntry.DurationMinutes = duration;
    }
}
