using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Burnout.Windows;

internal static class BurnoutGui
{
    public static Vector4 ButtonTextColor = new(1f, 1f, 1f, 1f);

    public static bool Button(string label)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.Button(label);
        ImGui.PopStyleColor();
        return r;
    }

    public static bool Button(string label, Vector2 size)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ButtonTextColor);
        var r = ImGui.Button(label, size);
        ImGui.PopStyleColor();
        return r;
    }
}
