using ImGuiNET;

namespace CHPEditor
{
    static class UIManager
    {
        public static Highlighter Highlight { get; private set; } = new(0xff, 0x3c, 0xff, 0xff);

        private static readonly ImGuiWindowFlags flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoCollapse;
        private static bool firstDraw = true;

        public static void Draw()
        {
            uint dockspace = ImGui.DockSpaceOverViewport();

            if (firstDraw)
            {
                ImGuiDocking.BuildDockSpace(dockspace, "Test1", "Test2", ImGuiDir.Left, 0.5f);
                firstDraw = false;
            }

            ImGui.SetNextWindowDockID(ImGuiDocking.first, ImGuiCond.Appearing);
            ImGui.Begin("Test1", flags | ImGuiWindowFlags.NoBackground);
            ImGui.End();

            ImGui.SetNextWindowDockID(ImGuiDocking.second, ImGuiCond.Appearing);
            ImGui.Begin("Test2", flags);
            ImGui.End();
        }
    }
}
