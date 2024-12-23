using ImGuiNET;
using System.Runtime.InteropServices;

namespace ImGuiNET
{
    public static class ImGuiDocking
    {
        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint igDockBuilderGetNode(uint node_id);

        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint igDockBuilderGetCentralNode(uint node_id);

        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint igDockBuilderSplitNode(uint node_id, ImGuiDir split_dir,
            float ratio, out uint out_id_first, out uint out_id_second);

        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        public static extern void igDockBuilderDockWindow(string label, uint node_id);


        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint igDockBuilderAddNode(uint node_id, ImGuiDockNodeFlags flags);

        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        public static extern void igDockBuilderRemoveNode(uint node_id);

        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        public static extern void igDockBuilderSetNodePos(uint node_id, float posX, float posY);

        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        public static extern void igDockBuilderSetNodeSize(uint node_id, float sizeX, float sizeY);

        [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
        public static extern void igDockBuilderFinish(uint node_id);

        public static uint first { get; private set; }
        public static uint second { get; private set; }

        public static void BuildDockSpace(uint id, string label_one, string label_two, ImGuiDir dir, float ratio)
        {
            if (igDockBuilderGetNode(id) == 0)
            {
                igDockBuilderRemoveNode(id);
                igDockBuilderAddNode(id, ImGuiDockNodeFlags.None);
            }

            igDockBuilderSplitNode(id, ImGuiDir.Right, ratio, out uint split, out uint _first);
            igDockBuilderSplitNode(split, ImGuiDir.Left, ratio, out _, out uint _second);
            first = _first;
            second = _second;

            //igDockBuilderDockWindow(label, id);
            igDockBuilderDockWindow(label_one, first);
            igDockBuilderDockWindow(label_two, second);

            igDockBuilderFinish(id);
        }
    }
}