using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImGuiNET
{
    public struct ImGuiDockNode
    {
        public uint ClassID;
        public ImGuiDockNodeFlags SharedFlags;
        public ImGuiDockNodeFlags LocalFlags;
        public ImGuiDockNodeFlags LocalFlagsInWindows;
        public ImGuiDockNodeFlags MergedFlags;
        public ImGuiDockNodeState State;
        public unsafe ImGuiDockNode* ParentNode;
        public unsafe ImGuiDockNode[]* ChildNodes;
    }
    public enum ImGuiDockNodeState
    {
        Unknown,
        HostWindowHiddenBecauseSingleWindow,
        HostWindowHiddenBecauseWindowsAreResizing,
        HostWindowVisible
    }
}
