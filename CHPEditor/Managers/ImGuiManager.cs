using ImGuiNET;
using Silk.NET.Maths;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;

namespace CHPEditor
{
    static class ImGuiManager
    {
        public static Highlighter Highlight { get; private set; } = new Highlighter(0xff, 0x3c, 0xff, 0xff);
        public static Vector4 Color = new Vector4(1.0f, 0.235f, 1.0f, 1.0f);

        public static bool ImGuiIsActive = false;
        public static Vector2 BackgroundOffset = new Vector2(0, 0);
        public static float BackgroundZoom
        {
            get { return zoom; }
            set { zoom = float.Clamp(value, 0.1f, 20.0f); }
        }
        private static float zoom = 1.0f;

        public static bool[] PatternDisabled = [];
        public static bool[] TextureDisabled = [];
        public static bool[] LayerDisabled = [];

        public static bool HighlightRect = false;

        private static bool[] UsedRects = [];
        private static string[] UsedRectsPreview = [];
        public static int SelectedRect = 0;

        private static string[] StateNames = new string[18];
        private static int SelectedPattern = 0;
        private static int SelectedTexture = 0;
        private static int SelectedLayer = 0;

        private static int CurrentFrame = 0;

        private static ImGuiWindowFlags flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking;

        public static void Initialize()
        {
            for (int i = 1; i <= 18; i++) { StateNames[i-1] = CHPEditor.Lang.GetValue("STATE_FULL_INDEXED", i, CHPEditor.Lang.GetValue(string.Format("STATE{0}_TITLE", i))); }
        }
        public static void Draw()
        {
            // Unfortunately, it looks like ImGuiNET lacks C# wrappers for the dock builder.
            // For now, I'll just force window positions/sizes ¯\_(ツ)_/¯

            //ImGui.DockSpaceOverViewport(ImGui.GetMainViewport(), ImGuiDockNodeFlags.AutoHideTabBar | ImGuiDockNodeFlags.PassthruCentralNode);

            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File###MENU_FILE"))
                {
                    ImGui.MenuItem("New###MENU_FILE_NEW", "CTRL+N");
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

            #region CHP Selector
#if DEBUG
            if (CHPEditor.showDebug)
                ImGui.ShowDemoWindow();
#endif

            ImGui.Begin(CHPEditor.Lang.GetValue("WINDOW_PREVIEW_SELECTOR_TITLE") + "###SELECT", flags);
            ImGui.SetWindowPos(new Vector2(0,21) + RatioFromWindowSize(0.75f, 0));
            ImGui.SetWindowSize(RatioFromWindowSize(0.25f, 0.5f));

            if (ImGui.BeginTabBar("DisplayMode"))
            {
                if (ImGui.BeginTabItem(CHPEditor.Lang.GetValue("TAB_BITMAPS")))
                {
                    if (ImGui.IsItemActive()) { BackgroundOffset = new Vector2(0, 0); BackgroundZoom = 1.0f; }

                    CHPEditor.anitoggle = false;
                    string[] bmpnames = ["CharBMP", "CharBMP2P", "CharFace", "CharFace2P", "SelectCG", "SelectCG2P", "CharTex", "CharTex2P"];
                    for (int i = 1; i <= 8; i++)
                    {
                        if (ImGui.Selectable(bmpnames[i - 1], CHPEditor.bmpshow == i))
                        {
                            Trace.TraceInformation("Displaying " + bmpnames[i - 1]);
                            CHPEditor.bmpshow = i;
                        }
                    }
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem(CHPEditor.Lang.GetValue("TAB_ANIMATIONS")))
                {
                    if (ImGui.IsItemActive()) { BackgroundOffset = new Vector2(0, 0); BackgroundZoom = 1.0f; }

                    CHPEditor.anitoggle = true;

                    //Initialize();
                    //for (int i = 1; i <= 18; i++)
                    //{
                    //    //string text = CHPEditor.Lang.GetValue("STATE_FULL_INDEXED", i, CHPEditor.Lang.GetValue(string.Format("STATE{0}_TITLE", i)));
                    //    if (ImGui.Selectable(StateNames[i-1], CHPEditor.anishow == i))
                    //    {
                    //        Trace.TraceInformation("Previewing " + StateNames[i-1]);
                    //        CHPEditor.anishow = i;
                    //        CHPEditor.tick = 0;
                    //        CurrentFrame = 0;
                    //        Timeline.Clear();

                    //        PatternDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[i - 1].Pattern.Count];
                    //        TextureDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[i - 1].Texture.Count];
                    //        LayerDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[i - 1].Layer.Count];

                    //        SelectedPattern = 0;
                    //        SelectedTexture = 0;
                    //        SelectedLayer = 0;
                    //    }
                    //}
                    AnimationSelectables();

                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.End();
            #endregion

            #region CHP Info

            ImGui.Begin(CHPEditor.Lang.GetValue("WINDOW_CHP_INFO_TITLE") + "###INFO", flags);
            ImGui.SetWindowPos(RatioFromWindowSize(0.75f, 0.5f) + new Vector2(0,21));
            ImGui.SetWindowSize(RatioFromWindowSize(0.25f, 0.5f));

            ImGui.InputTextWithHint(CHPEditor.Lang.GetValue("CHP_PATH_PROMPT"), Path.Combine("chara", "chara.chp"), ref CHPEditor.Config.Path, 1024);
            if (ImGui.Button(CHPEditor.Lang.GetValue("CHP_RELOAD_PROMPT")))
            {
                CHPEditor.ChpFile.Dispose();
                //CHPEditor.ChpFile = null;
                CHPEditor.ChpFile = new CHPFile(CHPEditor.Config.Path);
                Timeline.Clear();

                UpdateCHPStats();
            }
#if DEBUG
            ImGui.Checkbox("DEBUG ONLY! Show ImGUI Demo Window", ref CHPEditor.showDebug);
#endif
            ImGui.Separator();
            if (CHPEditor.ChpFile.Loaded)
            {
                ImGui.Text(CHPEditor.Lang.GetValue("CHP_FILE_INFO", CHPEditor.ChpFile.FileName, CHPEditor.ChpFile.FileEncoding.WebName));

                if (!string.IsNullOrEmpty(CHPEditor.ChpFile.CharName)) ImGui.Text(CHPEditor.Lang.GetValue("CHP_CHARA_NAME", CHPEditor.ChpFile.CharName));
                if (!string.IsNullOrEmpty(CHPEditor.ChpFile.Artist)) ImGui.Text(CHPEditor.Lang.GetValue("CHP_CHARA_ARTIST", CHPEditor.ChpFile.Artist));

                ImGui.Separator();

                if (!CHPEditor.anitoggle)
                {
                    string bmpname = CHPEditor.Lang.GetValue("CHP_BMP_PATH_NONE");
                    Vector2D<int> bmpsize = new Vector2D<int>(0, 0);
                    System.Drawing.Color bmpcolor = System.Drawing.Color.Transparent;
                    switch (CHPEditor.bmpstate)
                    {
                        case 1:
                            if (CHPEditor.ChpFile.CharBMP.Loaded)
                            {
                                bmpname = CHPEditor.ChpFile.CharBMP.Path;
                                bmpsize = CHPEditor.ChpFile.CharBMP.Bounds;
                                bmpcolor = CHPEditor.ChpFile.CharBMP.ColorKey;
                            }
                            break;
                        case 2:
                            if (CHPEditor.ChpFile.CharBMP2P.Loaded)
                            {
                                bmpname = CHPEditor.ChpFile.CharBMP2P.Path;
                                bmpsize = CHPEditor.ChpFile.CharBMP2P.Bounds;
                                bmpcolor = CHPEditor.ChpFile.CharBMP2P.ColorKey;
                            }
                            break;
                        case 3:
                            if (CHPEditor.ChpFile.CharFace.Loaded)
                            {
                                bmpname = CHPEditor.ChpFile.CharFace.Path;
                                bmpsize = CHPEditor.ChpFile.CharFace.Bounds;
                                bmpcolor = CHPEditor.ChpFile.CharFace.ColorKey;
                            }
                            break;
                        case 4:
                            if (CHPEditor.ChpFile.CharFace2P.Loaded)
                            {
                                bmpname = CHPEditor.ChpFile.CharFace2P.Path;
                                bmpsize = CHPEditor.ChpFile.CharFace2P.Bounds;
                                bmpcolor = CHPEditor.ChpFile.CharFace2P.ColorKey;
                            }
                            break;
                        case 5:
                            if (CHPEditor.ChpFile.SelectCG.Loaded)
                            {
                                bmpname = CHPEditor.ChpFile.SelectCG.Path;
                                bmpsize = CHPEditor.ChpFile.SelectCG.Bounds;
                                bmpcolor = CHPEditor.ChpFile.SelectCG.ColorKey;
                            }
                            break;
                        case 6:
                            if (CHPEditor.ChpFile.SelectCG2P.Loaded)
                            {
                                bmpname = CHPEditor.ChpFile.SelectCG2P.Path;
                                bmpsize = CHPEditor.ChpFile.SelectCG2P.Bounds;
                                bmpcolor = CHPEditor.ChpFile.SelectCG2P.ColorKey;
                            }
                            break;
                        case 7:
                            if (CHPEditor.ChpFile.CharTex.Loaded)
                            {
                                bmpname = CHPEditor.ChpFile.CharTex.Path;
                                bmpsize = CHPEditor.ChpFile.CharTex.Bounds;
                                bmpcolor = CHPEditor.ChpFile.CharTex.ColorKey;
                            }
                            break;
                        case 8:
                            if (CHPEditor.ChpFile.CharTex2P.Loaded)
                            {
                                bmpname = CHPEditor.ChpFile.CharTex2P.Path;
                                bmpsize = CHPEditor.ChpFile.CharTex2P.Bounds;
                                bmpcolor = CHPEditor.ChpFile.CharTex2P.ColorKey;
                            }
                            break;
                    }
                    ImGui.Text(CHPEditor.Lang.GetValue("CHP_BMP_PATH", bmpname));
                    ImGui.Text(CHPEditor.Lang.GetValue("CHP_BMP_SIZE", bmpsize.X, bmpsize.Y));
                    ImGui.Text(CHPEditor.Lang.GetValue("CHP_BMP_COLORKEY", bmpcolor.R, bmpcolor.G, bmpcolor.B, bmpcolor.A));
                }
                else
                {
                    ImGui.Text(CHPEditor.Lang.GetValue("STATE_INDEXED", CHPEditor.anishow) + "\n\n");
                    if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Loaded)
                    {
                        if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Frame != 0)
                        {
                            ImGui.Text(CHPEditor.Lang.GetValue("CHP_CHARA_TIMELINE",
                            Timeline.CurrentFrame,
                            CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount - 1,
                            Math.Round(Timeline.CurrentTime / 1000.0, 2),
                            (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Frame * CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount) / 1000.0
                            ));
                            ImGui.Text(CHPEditor.Lang.GetValue("CHP_CHARA_FPS", 1000.0f / CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Frame, CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Frame));
                        }
                        else
                        {
                            ImGui.Text(CHPEditor.Lang.GetValue("CHP_CHARA_TIMELINE",
                            Timeline.CurrentFrame,
                            CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount - 1,
                            Math.Round(Timeline.CurrentTime / 1000.0, 2),
                            (CHPEditor.ChpFile.Anime * CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount) / 1000.0
                                ));
                            ImGui.Text(CHPEditor.Lang.GetValue("CHP_CHARA_FPS", 1000.0f / CHPEditor.ChpFile.Anime, CHPEditor.ChpFile.Anime));
                        }

                        if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Loop > 0)
                        {
                            bool loopIsExceeding = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Loop >= CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount; // True if Loop exceeds index of last frame
                            ImGui.TextColored( loopIsExceeding ? new Vector4(1, 0, 0, 1) : new Vector4(1),
                                loopIsExceeding ? 
                                CHPEditor.Lang.GetValue("CHP_CHARA_LOOP_WARN_BOUNDS", CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Loop) :
                                CHPEditor.Lang.GetValue("CHP_CHARA_LOOP", CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Loop));
                        }

                        ImGui.Columns(3);

                        ImGui.Checkbox(CHPEditor.Lang.GetValue("ANIMATIONS_PAUSE_PROMPT"), ref CHPEditor.pause);

                        ImGui.NextColumn();

                        if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Loop > 0)
                        {
                            ImGui.Checkbox(CHPEditor.Lang.GetValue("CHP_CHARA_LOOP_PROMPT"), ref Timeline.UseLoop);
                        }
                        else
                            ImGui.TextDisabled(CHPEditor.Lang.GetValue("CHP_CHARA_LOOP_PROMPT_DISABLED"));

                        ImGui.NextColumn();

                        if (CHPEditor.ChpFile.CharBMP2P.Loaded)
                            ImGui.Checkbox(CHPEditor.Lang.GetValue("ANIMATIONS_USE2P_PROMPT"), ref CHPEditor.use2P);
                        else
                            ImGui.TextDisabled(CHPEditor.Lang.GetValue("ANIMATIONS_USE2P_PROMPT_DISABLED"));

                        ImGui.NextColumn();

                        ImGui.Columns();

                        ImGui.Separator();

                        if (CHPEditor.anishow != 14)
                            ImGui.Checkbox(CHPEditor.Lang.GetValue("CHP_CHARA_HIDE_BG_PROMPT"), ref CHPEditor.hideBg);

                        if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern.Count > 0)
                        {
                            //ImGui.Text(CHPEditor.Lang.GetValue("CHP_CHARA_PATTERN_ACTIVE", CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern.Count));
                            
                            if (ImGui.TreeNodeEx(CHPEditor.Lang.GetValue("CHP_CHARA_PATTERN_ACTIVE", CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern.Count) + "###PATTERN_TREE", ImGuiTreeNodeFlags.DefaultOpen))
                            {
                                for (int i = 0; i < PatternDisabled.Length; i++)
                                    if (ImGui.Selectable(CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[i].Comment != "" ?
                                        CHPEditor.Lang.GetValue("CHP_CHARA_ITEM_DETAIL", i + 1, CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[i].Comment) :
                                        CHPEditor.Lang.GetValue("CHP_CHARA_ITEM", i + 1), !PatternDisabled[i]))
                                        PatternDisabled[i] = !PatternDisabled[i];
                                ImGui.TreePop();
                            }

                        }
                        if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture.Count > 0)
                        {
                            //ImGui.Text(CHPEditor.Lang.GetValue("CHP_CHARA_TEXTURE_ACTIVE", CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture.Count));

                            if (ImGui.TreeNodeEx(CHPEditor.Lang.GetValue("CHP_CHARA_TEXTURE_ACTIVE", CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture.Count) + "###TEXTURE_TREE", ImGuiTreeNodeFlags.DefaultOpen))
                            {
                                for (int i = 0; i < TextureDisabled.Length; i++)
                                    if (ImGui.Selectable(CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[i].Comment != "" ?
                                        CHPEditor.Lang.GetValue("CHP_CHARA_ITEM_DETAIL", i + 1, CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[i].Comment) :
                                        CHPEditor.Lang.GetValue("CHP_CHARA_ITEM", i + 1), !TextureDisabled[i]))
                                        TextureDisabled[i] = !TextureDisabled[i];
                                ImGui.TreePop();
                            }
                        }
                        if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer.Count > 0)
                        {
                            //ImGui.Text(CHPEditor.Lang.GetValue("CHP_CHARA_LAYER_ACTIVE", CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer.Count));

                            if (ImGui.TreeNodeEx(CHPEditor.Lang.GetValue("CHP_CHARA_LAYER_ACTIVE", CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer.Count) + "###LAYER_TREE", ImGuiTreeNodeFlags.DefaultOpen))
                            {
                                for (int i = 0; i < LayerDisabled.Length; i++)
                                    if (ImGui.Selectable(CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[i].Comment != "" ?
                                        CHPEditor.Lang.GetValue("CHP_CHARA_ITEM_DETAIL", i + 1, CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[i].Comment) :
                                        CHPEditor.Lang.GetValue("CHP_CHARA_ITEM", i + 1), !LayerDisabled[i]))
                                        LayerDisabled[i] = !LayerDisabled[i];
                                ImGui.TreePop();
                            }
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled(CHPEditor.Lang.GetValue("CHP_CHARA_ANIMATION_NONE"));
                    }
                }

            }
            else
            {
                ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.5f, 0.5f, 1.0f), CHPEditor.Lang.GetValue("CHP_FILE_LOAD_FAIL"));
                ImGui.TextWrapped(CHPEditor.ChpFile.Error);
            }
            ImGui.End();
            #endregion

            #region Editor
            ImGui.Begin("Properties###PROPERTIES", flags);
            ImGui.SetWindowPos(RatioFromWindowSize(0, 0) + new Vector2(0,21));
            ImGui.SetWindowSize(RatioFromWindowSize(0.25f, 1f));

            if (CHPEditor.ChpFile.Loaded)
            {
                ImGui.BeginTabBar("Properties");

                #region Rects
                if (ImGui.BeginTabItem("Rects"))
                {
                    if (CHPEditor.anitoggle || (CHPEditor.bmpshow == 1 || CHPEditor.bmpshow == 2 || CHPEditor.bmpshow == 7 || CHPEditor.bmpshow == 8)) // CharBMP/CharTex
                    {
                        ImGui.Text("Rects Used: " + UsedRects.Count(value => value) + "/" + UsedRects.Length);

                        ImGui.Combo("Active Rect###ACTIVE_RECT", ref SelectedRect, UsedRectsPreview, UsedRectsPreview.Length);

                        ImGui.Checkbox("Highlight Rect", ref HighlightRect);
                        if (HighlightRect)
                            if (ImGui.ColorPicker4("Highlight Color", ref Color, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.DisplayRGB | ImGuiColorEditFlags.DisplayHex))
                                Highlight.UpdateColor((byte)(Color.X * 255), (byte)(Color.Y * 255), (byte)(Color.Z * 255), (byte)(Color.W * 255));

                            if (UsedRects.Length > 0)
                            {
                                ImGui.Separator();

                                // Weird setup to ensure any inputs after the currently modifying input don't vanish
                                bool input = ImGui.InputText("Label", ref CHPEditor.ChpFile.RectComments[SelectedRect], 1024);
                                input = ImGui.InputInt("X", ref CHPEditor.ChpFile.RectCollection[SelectedRect].Origin.X, 1, 10) || input;
                                input = ImGui.InputInt("Y", ref CHPEditor.ChpFile.RectCollection[SelectedRect].Origin.Y, 1, 10) || input;
                                input = ImGui.InputInt("W", ref CHPEditor.ChpFile.RectCollection[SelectedRect].Size.X, 1, 10) || input;
                                input = ImGui.InputInt("H", ref CHPEditor.ChpFile.RectCollection[SelectedRect].Size.Y, 1, 10) || input;

                                if (input)
                                {
                                    CHPEditor.ChpFile.RectCollection[SelectedRect].Size.X = int.Clamp(CHPEditor.ChpFile.RectCollection[SelectedRect].Size.X, 0, int.MaxValue);
                                    CHPEditor.ChpFile.RectCollection[SelectedRect].Size.Y = int.Clamp(CHPEditor.ChpFile.RectCollection[SelectedRect].Size.Y, 0, int.MaxValue);
                                    UpdateUsedRect(SelectedRect);
                                }
                            }
                    }
                    else if (CHPEditor.bmpshow == 3 || CHPEditor.bmpshow == 4) // CharFace
                    {
                        ImGui.Checkbox("Highlight Rect", ref HighlightRect);
                        if (HighlightRect)
                            if (ImGui.ColorPicker4("Highlight Color", ref Color, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.DisplayRGB | ImGuiColorEditFlags.DisplayHex))
                                Highlight.UpdateColor((byte)(Color.X * 255), (byte)(Color.Y * 255), (byte)(Color.Z * 255), (byte)(Color.W * 255));

                        ImGui.Text("CharFaceAllSize");
                        bool input = ImGui.InputInt("X", ref CHPEditor.ChpFile.CharFaceAllSize.Origin.X);
                        input = ImGui.InputInt("Y", ref CHPEditor.ChpFile.CharFaceAllSize.Origin.Y) || input;
                        input = ImGui.InputInt("W", ref CHPEditor.ChpFile.CharFaceAllSize.Size.X) || input;
                        input = ImGui.InputInt("H", ref CHPEditor.ChpFile.CharFaceAllSize.Size.Y) || input;

                        if (input)
                        {
                            CHPEditor.ChpFile.CharFaceAllSize.Origin.X = int.Clamp(CHPEditor.ChpFile.CharFaceAllSize.Origin.X, 0, int.MaxValue);
                            CHPEditor.ChpFile.CharFaceAllSize.Origin.Y = int.Clamp(CHPEditor.ChpFile.CharFaceAllSize.Origin.Y, 0, int.MaxValue);
                            CHPEditor.ChpFile.CharFaceAllSize.Size.X = int.Clamp(CHPEditor.ChpFile.CharFaceAllSize.Size.X, 0, int.MaxValue);
                            CHPEditor.ChpFile.CharFaceAllSize.Size.Y = int.Clamp(CHPEditor.ChpFile.CharFaceAllSize.Size.Y, 0, int.MaxValue);
                        }

                        ImGui.Text("CharFaceUpperSize");
                        input = ImGui.InputInt("X", ref CHPEditor.ChpFile.CharFaceUpperSize.Origin.X);
                        input = ImGui.InputInt("Y", ref CHPEditor.ChpFile.CharFaceUpperSize.Origin.Y) || input;
                        input = ImGui.InputInt("W", ref CHPEditor.ChpFile.CharFaceUpperSize.Size.X) || input;
                        input = ImGui.InputInt("H", ref CHPEditor.ChpFile.CharFaceUpperSize.Size.Y) || input;

                        if (input)
                        {
                            CHPEditor.ChpFile.CharFaceUpperSize.Origin.X = int.Clamp(CHPEditor.ChpFile.CharFaceUpperSize.Origin.X, 0, int.MaxValue);
                            CHPEditor.ChpFile.CharFaceUpperSize.Origin.Y = int.Clamp(CHPEditor.ChpFile.CharFaceUpperSize.Origin.Y, 0, int.MaxValue);
                            CHPEditor.ChpFile.CharFaceUpperSize.Size.X = int.Clamp(CHPEditor.ChpFile.CharFaceUpperSize.Size.X, 0, int.MaxValue);
                            CHPEditor.ChpFile.CharFaceUpperSize.Size.Y = int.Clamp(CHPEditor.ChpFile.CharFaceUpperSize.Size.Y, 0, int.MaxValue);
                        }
                    }
                    ImGui.EndTabItem();
                }
                #endregion
                #region Animation
                if (ImGui.BeginTabItem("Animation"))
                {
                    if (ImGui.BeginCombo(CHPEditor.Lang.GetValue("TAB_ANIMATIONS"), StateNames[CHPEditor.anishow - 1]))
                    {
                        AnimationSelectables();
                        ImGui.EndCombo();
                    }

                    ImGui.Separator();

                    if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount > 0)
                    {
                        int maxframe = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount - 1;
                        int minframe = Timeline.UseLoop ? Timeline.CurrentLoop : 0;

                        /*if (!CHPEditor.pause)*/ CurrentFrame = Timeline.CurrentFrame;
                        ImGui.BeginDisabled(!CHPEditor.pause);
                        if (ImGui.SliderInt("Frame", ref CurrentFrame, minframe, maxframe, "Frame %d/" + maxframe))
                        {
                            int framerate = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Frame > 0 ? CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Frame : CHPEditor.ChpFile.Anime;
                            
                            CHPEditor.tick = (CurrentFrame - minframe) * framerate;
                        }
                        ImGui.EndDisabled();

                        if (CHPEditor.pause)
                            { if (ImGui.Button("Play Animation")) CHPEditor.pause = false; }
                        else
                            { if (ImGui.Button(CHPEditor.Lang.GetValue("ANIMATIONS_PAUSE_PROMPT"))) CHPEditor.pause = true; }

                        if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern.Count > 0)
                        {
                            if (ImGui.BeginCombo("Patterns", "#" + (SelectedPattern+1)))
                            {
                                for (int i = 0; i < CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern.Count; i++)
                                    if (ImGui.Selectable("#" + (i+1))) SelectedPattern = i;
                                ImGui.EndCombo();
                            }
                        }
                        if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture.Count > 0)
                        {
                            if (ImGui.BeginCombo("Textures", "#" + (SelectedTexture+1)))
                            {
                                for (int i = 0; i < CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture.Count; i++)
                                    if (ImGui.Selectable("#" + (i+1))) SelectedTexture = i;
                                ImGui.EndCombo();
                            }
                        }
                        if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer.Count > 0)
                        {
                            if (ImGui.BeginCombo("Layers", "#" + (SelectedLayer+1)))
                            {
                                for (int i = 0; i < CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer.Count; i++)
                                    if (ImGui.Selectable("#" + (i+1))) SelectedLayer = i;
                                ImGui.EndCombo();
                            }
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled("No frames available.");
                    }

                    ImGui.EndTabItem();
                }
                #endregion

                ImGui.EndTabBar();
            }
            else
            {
                ImGui.TextDisabled("Character is not loaded.");
            }

            ImGui.End();
            #endregion

            if (!ImGui.IsWindowFocused(ImGuiFocusedFlags.AnyWindow) && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                BackgroundOffset += ImGui.GetMouseDragDelta();
                ImGui.ResetMouseDragDelta();
            }
        }
        public static void UpdateCHPStats()
        {
            PatternDisabled = [];
            TextureDisabled = [];
            LayerDisabled = [];
            UsedRects = [];

            if (!CHPEditor.ChpFile.Loaded) { return; }

            PatternDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern.Count];
            TextureDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture.Count];
            LayerDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer.Count];

            UsedRects = new bool[CHPEditor.ChpFile.RectCollection.Length];
            UsedRectsPreview = new string[CHPEditor.ChpFile.RectCollection.Length];
            SelectedRect = 0;

            for (int i = 0; i < UsedRects.Length; i++)
            {
                UpdateUsedRect(i);
            }
        }
        public static void UpdateUsedRect(int index)
        {
            var rect = CHPEditor.ChpFile.RectCollection[index];
            string numbers = " [" + rect.Origin.X + "," + rect.Origin.Y + "," + rect.Size.X + "," + rect.Size.Y + "]";
            string info = !string.IsNullOrEmpty(CHPEditor.ChpFile.RectComments[index]) ? CHPEditor.Lang.GetValue("CHP_CHARA_ITEM_DETAIL", index + 1, CHPEditor.ChpFile.RectComments[index]) : CHPEditor.Lang.GetValue("CHP_CHARA_ITEM", index + 1);

            UsedRects[index] = rect != new Rectangle<int>(0, 0, 0, 0);
            UsedRectsPreview[index] = UsedRects[index] ? info + numbers : info + " [Unused]";
        }

        static void AnimationSelectables()
        {
            for (int i = 1; i <= 18; i++)
            {
                if (ImGui.Selectable(StateNames[i - 1], CHPEditor.anishow == i))
                {
                    Trace.TraceInformation("Previewing " + StateNames[i - 1]);
                    CHPEditor.anishow = i;
                    CHPEditor.tick = 0;
                    Timeline.Clear();
                    Timeline.CurrentLoop = CHPEditor.ChpFile.AnimeCollection[i - 1].Loop;

                    PatternDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[i - 1].Pattern.Count];
                    TextureDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[i - 1].Texture.Count];
                    LayerDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[i - 1].Layer.Count];

                    SelectedPattern = 0;
                    SelectedTexture = 0;
                    SelectedLayer = 0;
                }
            }
        }
        public static void DrawHighlight(Rectangle<int> rect) { if (HighlightRect) Highlight.Draw(rect); }
        static Vector2 RatioFromWindowSize(float ratio_x, float ratio_y)
        {
            return new Vector2(CHPEditor._window.Size.X * ratio_x, (CHPEditor._window.Size.Y - 21) * ratio_y);
        }
    }
}
