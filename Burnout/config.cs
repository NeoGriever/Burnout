using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;

namespace Burnout;

[Serializable]
public class ShiftEntry
{
    public string Id = Guid.NewGuid().ToString();
    public string Label = "New Shift";
    public int Week = 0;
    public DayOfWeek Day;
    public int StartHour;
    public int StartMinute = 0;         // 0, 15, 30, 45
    public int DurationMinutes = 60;    // minimum 15, step 15
    public Vector4 Color = new(0.3f, 0.6f, 0.9f, 0.85f);
    public int Style = 0;
    public string Command = "";
}

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public List<ShiftEntry> Entries { get; set; } = new();
    public string SelectedDisplayTimezone { get; set; } = "Eastern Standard Time";
    public bool TwoWeekMode { get; set; } = false;
    public float CellHeight { get; set; } = 30f;
    public bool ConfirmOnDelete { get; set; } = true;

    // Row background colors
    public Vector4 WeekdayColorOdd { get; set; } = new(0.04f, 0.06f, 0.12f, 1f);
    public Vector4 WeekdayColorEven { get; set; } = new(0.06f, 0.08f, 0.16f, 1f);
    public Vector4 WeekendColorOdd { get; set; } = new(0.04f, 0.10f, 0.06f, 1f);
    public Vector4 WeekendColorEven { get; set; } = new(0.06f, 0.13f, 0.08f, 1f);

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
