using ImGuiNET;
using Silk.NET.Maths;
using System;
using System.Collections.Generic;
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
        private static string[] PatternNames;
        private static string[] TextureNames;
        private static string[] LayerNames;

        private static int SelectedPattern = 0;
        private static int SelectedTexture = 0;
        private static int SelectedLayer = 0;

        private static int CurrentFrame = 0;

        private static ImGuiWindowFlags flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking;

        public static void Initialize()
        {
            for (int i = 1; i <= 18; i++) { StateNames[i-1] = CHPEditor.Lang.GetValue("STATE_FULL_INDEXED", i, CHPEditor.Lang.GetValue(string.Format("STATE{0}_TITLE", i))); }
            if (CHPEditor.ChpFile.Loaded) { UpdateObjectNames(ref CHPEditor.ChpFile.AnimeCollection[0]); }
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
                    ImGui.MenuItem("New###MENU_FILE_NEW", "CTRL+N", false, false);
                    ImGui.MenuItem("Open###MENU_FILE_OPEN", "CTRL+O", false, false);
                    ImGui.MenuItem("Save###MENU_FILE_SAVE", "CTRL+S", false, false);
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
                CHPEditor.ChpFile = new CHPFile(CHPEditor.Config.Path);
                Timeline.Clear();

                UpdateObjectNames(ref CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1]);
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

                        if (ImGui.BeginCombo("Rect###ACTIVE RECT", UsedRectsPreview[SelectedRect]))
                        {
                            for (int i = 0; i < UsedRectsPreview.Length; i++)
                            {
                                if (ImGui.Selectable(UsedRectsPreview[i]))
                                    SelectedRect = i;

                                if (CHPEditor.bmpshow == 1 || CHPEditor.bmpshow == 2) // CharBMP
                                    BMPTooltip(i, CHPEditor.bmpshow == 2);
                                else if (CHPEditor.bmpshow == 7 || CHPEditor.bmpshow == 8) //CharTex
                                    BMPTooltip(i, CHPEditor.bmpshow == 8, true);
                            }
                            ImGui.EndCombo();
                        }

                        if (CHPEditor.bmpshow == 1 || CHPEditor.bmpshow == 2) // CharBMP
                            BMPTooltip(SelectedRect, CHPEditor.bmpshow == 2);
                        else if (CHPEditor.bmpshow == 7 || CHPEditor.bmpshow == 8) //CharTex
                            BMPTooltip(SelectedRect, CHPEditor.bmpshow == 8, true);

                        if (ImGui.TreeNodeEx("Highlight###HIGHLIGHT", ImGuiTreeNodeFlags.Framed))
                        {
                            ImGui.Checkbox("Highlight Rect", ref HighlightRect);

                            if (ImGui.ColorPicker4("Highlight Color", ref Color, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.DisplayRGB | ImGuiColorEditFlags.DisplayHex))
                                Highlight.UpdateColor((byte)(Color.X * 255), (byte)(Color.Y * 255), (byte)(Color.Z * 255), (byte)(Color.W * 255));
                            
                            ImGui.TreePop();
                        }

                        if (UsedRects.Length > 0)
                        {
                            ImGui.SeparatorText("Rect");

                            RectCombo(SelectedRect);
                        }
                    }
                    else if (CHPEditor.bmpshow == 3 || CHPEditor.bmpshow == 4) // CharFace
                    {
                        ImGui.Checkbox("Highlight Rect", ref HighlightRect);
                        if (HighlightRect)
                            if (ImGui.ColorPicker4("Highlight Color", ref Color, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.DisplayRGB | ImGuiColorEditFlags.DisplayHex))
                                Highlight.UpdateColor((byte)(Color.X * 255), (byte)(Color.Y * 255), (byte)(Color.Z * 255), (byte)(Color.W * 255));

                        ImGui.Text("CharFaceAllSize");
                        CharFaceTooltip(CHPEditor.ChpFile.CharFaceAllSize, CHPEditor.bmpshow == 4);

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
                        CharFaceTooltip(CHPEditor.ChpFile.CharFaceUpperSize, CHPEditor.bmpshow == 4);

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
                    if (ImGui.IsItemClicked())
                    {
                        SelectedPattern = 0;
                        SelectedTexture = 0;
                        SelectedLayer = 0;
                    }

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

                        #region Pattern
                        if (PatternNames.Length > 0)
                        {
                            ImGui.SeparatorText("Patterns");

                            if (ImGui.BeginCombo($"Patterns ({PatternNames.Length})", PatternNames[SelectedPattern]))
                            {
                                for (int i = 0; i < PatternNames.Length; i++)
                                {
                                    if (ImGui.Selectable(PatternNames[i], i == SelectedPattern)) SelectedPattern = i;
                                    if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[i].Sprite[CurrentFrame] > -1)
                                    {
                                        BMPTooltip(CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[i].Sprite[CurrentFrame], CHPEditor.use2P);
                                    }
                                }
                                ImGui.EndCombo();
                            }
                            if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern].Sprite[CurrentFrame] > -1)
                            {
                                BMPTooltip(CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern].Sprite[CurrentFrame], CHPEditor.use2P);
                            }

                            bool isinterpolating = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern].Sprite.Any(i => i.IsWithinTimeframe(Timeline.CurrentTime));
                            int currentrect = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern].Sprite[CurrentFrame];

                            #region Sprite
                            ImGui.BeginDisabled(!CHPEditor.pause || isinterpolating);

                            var currentref = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern];
                            if (ImGui.InputText("Label", ref currentref.Comment, 128))
                            {
                                CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern] = currentref;
                                UpdatePatternNames(ref CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern);
                            }

                            if (isinterpolating)
                            {
                                int fake = 0;
                                ImGui.Combo("Sprite", ref fake, ["(Interpolated animation)"], 1);
                                if (ImGui.TreeNodeEx("Edit Offset Rect###ANIMATION_PATTERN_SPRITE_RECT"))
                                {
                                    InterpolateRectCombo();
                                    ImGui.TreePop();
                                }
                            }
                            else
                            {
                                SpriteCombo("Sprite###ANIMATION_PATTERN_SPRITE", ref CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern].Sprite[CurrentFrame]);
                                if (ImGui.TreeNodeEx("Edit Sprite Rect###ANIMATION_PATTERN_SPRITE_RECT"))
                                {
                                    RectCombo(currentrect);
                                    ImGui.TreePop();
                                }
                            }
                            ImGui.EndDisabled();
                            #endregion
                            #region Offset
                            isinterpolating = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern].Offset.Any(i => i.IsWithinTimeframe(Timeline.CurrentTime));
                            bool isused = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern].Offset.Length > 0;
                            int currentrect_offset = isused ? CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern].Offset[CurrentFrame] : 0;

                            ImGui.BeginDisabled(!CHPEditor.pause || isinterpolating);
                            if (isinterpolating)
                            {
                                int fake = 0;
                                ImGui.Combo("Offset", ref fake, ["(Interpolated animation)"], 1);
                                if (ImGui.TreeNodeEx("Edit Offset Rect###ANIMATION_PATTERN_OFFSET_RECT"))
                                {
                                    InterpolateRectCombo();
                                    ImGui.TreePop();
                                }

                                if (ImGui.Button("Remove Offset Frames"))
                                {
                                    var item = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern];
                                    var inter = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern];
                                    item.Offset = [];
                                    inter.Offset = [];
                                    CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern] = item;
                                    CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern] = inter;
                                }
                            }
                            else if (!isused)
                            {
                                ImGui.TextDisabled("No offset frames created.");

                                if (ImGui.Button("Create Offset Frames"))
                                {
                                    var item = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern];
                                    var inter = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern];
                                    item.Offset = new int[CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount];
                                    inter.Offset = [];
                                    CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern] = item;
                                    CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern] = inter;
                                }
                            }
                            else
                            {
                                OffsetCombo("Offset###ANIMATION_PATTERN_OFFSET",
                                    ref CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern].Sprite[CurrentFrame],
                                    ref CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern].Offset[CurrentFrame]);
                                if (ImGui.TreeNodeEx("Edit Offset Rect###ANIMATION_PATTERN_OFFSET_RECT"))
                                {
                                    RectCombo(currentrect_offset);
                                    ImGui.TreePop();
                                }

                                if (ImGui.Button("Remove Offset Frames"))
                                {
                                    var item = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern];
                                    item.Offset = [];
                                    CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[SelectedPattern] = item;
                                }
                            }
                            ImGui.EndDisabled();
                            #endregion
                        }
                        #endregion
                        #region Texture
                        if (TextureNames.Length > 0)
                        {
                            string inter_placeholder_text = "(Interpolated animation)";
                            ImGui.SeparatorText("Textures");

                            if (ImGui.BeginCombo($"Textures ({TextureNames.Length})", TextureNames[SelectedTexture]))
                            {
                                for (int i = 0; i < TextureNames.Length; i++)
                                {
                                    if (ImGui.Selectable(TextureNames[i], i == SelectedTexture)) SelectedTexture = i;
                                    if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[i].Sprite[CurrentFrame] > -1)
                                    {
                                        BMPTooltip(CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[i].Sprite[CurrentFrame], CHPEditor.use2P, true);
                                    }
                                }
                                ImGui.EndCombo();
                            }
                            if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Sprite[CurrentFrame] > -1)
                            {
                                BMPTooltip(CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Sprite[CurrentFrame], CHPEditor.use2P, true);
                            }

                            int currentrect = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Sprite[CurrentFrame];
                            bool isinterpolating = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Sprite.Any(i => i.IsWithinTimeframe(Timeline.CurrentTime));
                            bool offset_isused = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Offset.Length > 0;
                            bool alpha_isused = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Alpha.Length > 0;
                            bool rotation_isused = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Rotation.Length > 0;

                            #region Sprite
                            ImGui.BeginDisabled(!CHPEditor.pause || isinterpolating);

                            var currentref = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture];
                            if (ImGui.InputText("Label", ref currentref.Comment, 128))
                            {
                                CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture] = currentref;
                                UpdateTextureNames(ref CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture);
                            }

                            if (isinterpolating)
                            {
                                int fake = 0;
                                ImGui.Combo("Sprite", ref fake, ["(Interpolated animation)"], 1);
                                if (ImGui.TreeNodeEx("Edit Sprite Rect###ANIMATION_TEXTURE_SPRITE_RECT"))
                                {
                                    InterpolateRectCombo();
                                    ImGui.TreePop();
                                }
                            }
                            else
                            {
                                SpriteCombo("Sprite###ANIMATION_TEXTURE_SPRITE",
                                    ref CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Sprite[CurrentFrame],
                                    true);
                                if (ImGui.TreeNodeEx("Edit Sprite Rect###ANIMATION_TEXTURE_SPRITE_RECT"))
                                {
                                    RectCombo(currentrect);
                                    ImGui.TreePop();
                                }
                            }
                            ImGui.EndDisabled();
                            #endregion
                            #region Offset
                            isinterpolating = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Offset.Any(i => i.IsWithinTimeframe(Timeline.CurrentTime));
                            int currentrect_offset = offset_isused ? CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Offset[CurrentFrame] : 0;

                            ImGui.BeginDisabled(!CHPEditor.pause || isinterpolating);
                            if (isinterpolating)
                            {
                                int fake = 0;
                                ImGui.Combo("Offset", ref fake, ["(Interpolated animation)"], 1);
                                if (ImGui.TreeNodeEx("Edit Offset Rect###ANIMATION_TEXTURE_OFFSET_RECT"))
                                {
                                    InterpolateRectCombo();
                                    ImGui.TreePop();
                                }
                            }
                            else if (!offset_isused)
                            {
                                ImGui.TextDisabled("No offset frames created.");
                            }
                            else
                            {
                                OffsetCombo("Offset###ANIMATION_TEXTURE_OFFSET",
                                    ref CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Sprite[CurrentFrame],
                                    ref CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Offset[CurrentFrame],
                                    true);
                                if (ImGui.TreeNodeEx("Edit Offset Rect###ANIMATION_TEXTURE_OFFSET_RECT"))
                                {
                                    RectCombo(currentrect_offset);
                                    ImGui.TreePop();
                                }
                            }
                            ImGui.EndDisabled();
                            #endregion
                            #region Alpha
                            isinterpolating = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Alpha.Any(i => i.IsWithinTimeframe(Timeline.CurrentTime));
                            currentrect = alpha_isused ? CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Alpha[CurrentFrame] : 0;

                            ImGui.BeginDisabled(!CHPEditor.pause || isinterpolating);
                            if (isinterpolating)
                            {
                                ImGui.InputText("Alpha", ref inter_placeholder_text, 1);
                            }
                            else if (!alpha_isused)
                            {
                                ImGui.TextDisabled("No alpha frames created.");
                            }
                            else
                            {
                                if (ImGui.InputInt("Alpha###ANIMATION_TEXTURE_ALPHA", ref currentrect))
                                {
                                    currentrect = int.Clamp(currentrect, 0, 255);
                                    CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Alpha[CurrentFrame] = currentrect;
                                }
                            }
                            ImGui.EndDisabled();
                            #endregion
                            #region Rotation
                            isinterpolating = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Rotation.Any(i => i.IsWithinTimeframe(Timeline.CurrentTime));
                            currentrect = rotation_isused ? CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Rotation[CurrentFrame] : 0;

                            ImGui.BeginDisabled(!CHPEditor.pause || isinterpolating);
                            if (isinterpolating)
                            {
                                ImGui.InputText("Rotation", ref inter_placeholder_text, 1);
                            }
                            else if (!rotation_isused)
                            {
                                ImGui.TextDisabled("No rotation frames created.");
                            }
                            else
                            {
                                if (ImGui.InputInt("Rotation###ANIMATION_TEXTURE_ROTATION", ref currentrect))
                                {
                                    currentrect = int.Clamp(currentrect, 0, 255);
                                    CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture].Rotation[CurrentFrame] = currentrect;
                                }
                            }
                            ImGui.EndDisabled();
                            #endregion

                            #region Buttons

                            #region Offset
                            if (!alpha_isused && offset_isused)
                            {
                                if (ImGui.Button("Remove Offset Frames"))
                                {
                                    var item = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture];
                                    var inter = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture];
                                    item.Offset = [];
                                    inter.Offset = [];
                                    CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture] = item;
                                    CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture] = inter;
                                }
                                ImGui.SameLine();
                            }
                            else if (!offset_isused)
                            {
                                if (ImGui.Button("Create Offset Frames"))
                                {
                                    var item = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture];
                                    var inter = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture];
                                    item.Offset = new int[CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount];
                                    inter.Offset = [];
                                    CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture] = item;
                                    CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture] = inter;
                                }
                            }
                            #endregion
                            #region Alpha
                            if (!rotation_isused && alpha_isused)
                            {
                                if (ImGui.Button("Remove Alpha Frames"))
                                {
                                    var item = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture];
                                    var inter = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture];
                                    item.Alpha = [];
                                    inter.Alpha = [];
                                    CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture] = item;
                                    CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture] = inter;
                                }
                                ImGui.SameLine();
                            }
                            else if (!rotation_isused && offset_isused)
                            {
                                if (ImGui.Button("Create Alpha Frames"))
                                {
                                    var item = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture];
                                    var inter = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture];
                                    item.Alpha = new int[CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount];
                                    for (int i = 0; i < item.Alpha.Length; i++) { item.Alpha[i] = 255; }
                                    inter.Alpha = [];
                                    CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture] = item;
                                    CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture] = inter;
                                }
                            }

                            #endregion
                            #region Rotation
                            if (rotation_isused)
                            {
                                if (ImGui.Button("Remove Rotation Frames"))
                                {
                                    var item = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture];
                                    var inter = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture];
                                    item.Rotation = [];
                                    inter.Rotation = [];
                                    CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture] = item;
                                    CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture] = inter;
                                }
                            }
                            else if (!rotation_isused && alpha_isused)
                            {
                                if (ImGui.Button("Create Rotation Frames"))
                                {
                                    var item = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture];
                                    var inter = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture];
                                    item.Rotation = new int[CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount];
                                    for (int i = 0; i < item.Rotation.Length; i++) { item.Rotation[i] = 0; }
                                    inter.Rotation = [];
                                    CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[SelectedTexture] = item;
                                    CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Texture[SelectedTexture] = inter;
                                }
                            }
                            #endregion
                            
                            #endregion
                        }
                        #endregion
                        #region Layer
                        if (LayerNames.Length > 0)
                        {
                            ImGui.SeparatorText("Layers");

                            if (ImGui.BeginCombo($"Layers ({LayerNames.Length})", LayerNames[SelectedLayer]))
                            {
                                for (int i = 0; i < LayerNames.Length; i++)
                                {
                                    if (ImGui.Selectable(LayerNames[i], i == SelectedLayer)) SelectedLayer = i;
                                    if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[i].Sprite[CurrentFrame] > -1)
                                    {
                                        BMPTooltip(CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[i].Sprite[CurrentFrame], CHPEditor.use2P);
                                    }
                                }
                                ImGui.EndCombo();
                            }
                            if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer].Sprite[CurrentFrame] > -1)
                            {
                                BMPTooltip(CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer].Sprite[CurrentFrame], CHPEditor.use2P);
                            }

                            bool isinterpolating = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Layer[SelectedLayer].Sprite.Any(i => i.IsWithinTimeframe(Timeline.CurrentTime));
                            int currentrect = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer].Sprite[CurrentFrame];

                            #region Sprite
                            ImGui.BeginDisabled(!CHPEditor.pause || isinterpolating);

                            var currentref = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer];
                            if (ImGui.InputText("Label", ref currentref.Comment, 128))
                            {
                                CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer] = currentref;
                                UpdateLayerNames(ref CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer);
                            }

                            if (isinterpolating)
                            {
                                int fake = 0;
                                ImGui.Combo("Sprite###ANIMATION_LAYER_SPRITE", ref fake, ["(Interpolated animation)"], 1);
                                if (ImGui.TreeNodeEx("Edit Sprite Rect###ANIMATION_LAYER_SPRITE_RECT"))
                                {
                                    InterpolateRectCombo();
                                    ImGui.TreePop();
                                }
                            }
                            else
                            {
                                SpriteCombo("Sprite###ANIMATION_LAYER_SPRITE", ref CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer].Sprite[CurrentFrame]);
                                if (ImGui.TreeNodeEx("Edit Sprite Rect###ANIMATION_LAYER_SPRITE_RECT"))
                                {
                                    RectCombo(currentrect);
                                    ImGui.TreePop();
                                }
                            }
                            ImGui.EndDisabled();
                            #endregion
                            #region Offset
                            isinterpolating = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Layer[SelectedLayer].Offset.Any(i => i.IsWithinTimeframe(Timeline.CurrentTime));
                            bool isused = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer].Offset.Length > 0;
                            int currentrect_offset = isused ? CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer].Offset[CurrentFrame] : 0;

                            ImGui.BeginDisabled(!CHPEditor.pause || isinterpolating);
                            if (isinterpolating)
                            {
                                int fake = 0;
                                ImGui.Combo("Offset", ref fake, ["(Interpolated animation)"], 1);
                                if (ImGui.TreeNodeEx("Edit Offset Rect###ANIMATION_LAYER_OFFSET_RECT"))
                                {
                                    InterpolateRectCombo();
                                    ImGui.TreePop();
                                }

                                if (ImGui.Button("Remove Offset Frames"))
                                {
                                    var item = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer];
                                    var inter = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Layer[SelectedLayer];
                                    item.Offset = [];
                                    inter.Offset = [];
                                    CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer] = item;
                                    CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Layer[SelectedLayer] = inter;
                                }
                            }
                            else if (!isused)
                            {
                                ImGui.TextDisabled("No offset frames created.");

                                if (ImGui.Button("Create Offset Frames"))
                                {
                                    var item = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer];
                                    var inter = CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Layer[SelectedLayer];
                                    item.Offset = new int[CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount];
                                    inter.Offset = [];
                                    CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer] = item;
                                    CHPEditor.ChpFile.InterpolateCollection[CHPEditor.anishow - 1].Layer[SelectedLayer] = inter;
                                }
                            }
                            else
                            {
                                OffsetCombo("Offset###ANIMATION_LAYER_OFFSET",
                                    ref CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer].Sprite[CurrentFrame],
                                    ref CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer].Offset[CurrentFrame]);
                                if (ImGui.TreeNodeEx("Edit Offset Rect###ANIMATION_LAYER_OFFSET_RECT"))
                                {
                                    RectCombo(currentrect_offset);
                                    ImGui.TreePop();
                                }

                                if (ImGui.Button("Remove Offset Frames"))
                                {
                                    var item = CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer];
                                    item.Offset = [];
                                    CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[SelectedLayer] = item;
                                }
                            }
                            ImGui.EndDisabled();
                            #endregion
                        }
                        #endregion
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

            UsedRects[index] = (rect != new Rectangle<int>(0, 0, 0, 0)) || !string.IsNullOrEmpty(CHPEditor.ChpFile.RectComments[index]);
            UsedRectsPreview[index] = UsedRects[index] ? info + numbers : info + " [Unused]";
        }
        public static void UpdateObjectNames(ref CHPFile.AnimeData data)
        {
            if (!data.Loaded)
            {
                PatternNames = [];
                TextureNames = [];
                LayerNames = [];
                return;
            }

            UpdatePatternNames(ref data.Pattern);
            UpdateTextureNames(ref data.Texture);
            UpdateLayerNames(ref data.Layer);
        }
        public static void UpdatePatternNames(ref List<CHPFile.AnimeData.PatternData> data)
        {
            PatternNames = new string[data.Count];

            for (int i = 0; i < data.Count; i++)
            {
                PatternNames[i] = !string.IsNullOrEmpty(data[i].Comment) ? ($"#{i + 1} ({data[i].Comment})") : $"#{i + 1}";
            }
        }
        public static void UpdateTextureNames(ref List<CHPFile.AnimeData.TextureData> data)
        {
            TextureNames = new string[data.Count];

            for (int i = 0; i < data.Count; i++)
            {
                TextureNames[i] = !string.IsNullOrEmpty(data[i].Comment) ? ($"#{i + 1} ({data[i].Comment})") : $"#{i + 1}";
            }
        }
        public static void UpdateLayerNames(ref List<CHPFile.AnimeData.PatternData> data)
        {
            LayerNames = new string[data.Count];

            for (int i = 0; i < data.Count; i++)
            {
                LayerNames[i] = !string.IsNullOrEmpty(data[i].Comment) ? ($"#{i + 1} ({data[i].Comment})") : $"#{i + 1}";
            }
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
                    UpdateObjectNames(ref CHPEditor.ChpFile.AnimeCollection[i - 1]);

                    SelectedPattern = 0;
                    SelectedTexture = 0;
                    SelectedLayer = 0;
                }
            }
        }
        
        static void BMPTooltip(int sprite_index, bool use2P, bool use_tex = false) { BMPTooltip(sprite_index, sprite_index, use2P, false, use_tex); }
        static void BMPTooltip(int sprite_index, int offset_index, bool use2P, bool show_offset = false, bool use_tex = false)
        {
            if (sprite_index < 0 || offset_index < 0) return;

            var rect = CHPEditor.ChpFile.RectCollection[sprite_index];
            var offset = CHPEditor.ChpFile.RectCollection[offset_index];

            bool not_drawable = (rect.Size.X <= 0 || rect.Size.Y <= 0 || offset.Size.X <= 0 || offset.Size.Y <= 0);

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();

                bool bounds_crossed = false; // Behavior related to coordinates going outside of a given image hasn't been explored,
                                             // but it's safe to assume that we should warn the user not to do this.
                if (use_tex)
                {
                    if (use2P && CHPEditor.ChpFile.CharTex2P.Loaded)
                    {
                        if (!not_drawable)
                            CHPEditor.ChpFile.CharTex2P.ImageFile.DrawForImGui(rect, offset.Size);
                        bounds_crossed = CHPEditor.ChpFile.CharTex2P.ImageFile.Image.Width < rect.Max.X || CHPEditor.ChpFile.CharTex2P.ImageFile.Image.Height < rect.Max.Y;
                    }
                    else if (CHPEditor.ChpFile.CharTex.Loaded)
                    {
                        if (!not_drawable)
                            CHPEditor.ChpFile.CharTex.ImageFile.DrawForImGui(rect, offset.Size);
                        bounds_crossed = CHPEditor.ChpFile.CharTex.ImageFile.Image.Width < rect.Max.X || CHPEditor.ChpFile.CharTex.ImageFile.Image.Height < rect.Max.Y;
                    }
                }
                else
                {
                    if (use2P && CHPEditor.ChpFile.CharBMP2P.Loaded)
                    {
                        if (!not_drawable)
                            CHPEditor.ChpFile.CharBMP2P.ImageFile.DrawForImGui(rect, offset.Size);
                        bounds_crossed = CHPEditor.ChpFile.CharBMP2P.ImageFile.Image.Width < rect.Max.X || CHPEditor.ChpFile.CharBMP2P.ImageFile.Image.Height < rect.Max.Y;
                    }
                    else if (CHPEditor.ChpFile.CharBMP.Loaded)
                    {
                        if (!not_drawable)
                            CHPEditor.ChpFile.CharBMP.ImageFile.DrawForImGui(rect, offset.Size);
                        bounds_crossed = CHPEditor.ChpFile.CharBMP.ImageFile.Image.Width < rect.Max.X || CHPEditor.ChpFile.CharBMP.ImageFile.Image.Height < rect.Max.Y;
                    }
                }

                string comment = show_offset ? CHPEditor.ChpFile.RectComments[offset_index] : CHPEditor.ChpFile.RectComments[sprite_index];
                string rect_text = $"{(show_offset ? "Sprite " : "")}Rect: {rect.Origin.X},{rect.Origin.Y},{rect.Size.X},{rect.Size.Y}";
                string offset_text = $"Offset Rect: {offset.Origin.X},{offset.Origin.Y},{offset.Size.X},{offset.Size.Y}";

                if (!string.IsNullOrEmpty(comment))
                    ImGui.Text($"Label: {comment}");

                if (bounds_crossed)
                    ImGui.TextColored(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), rect_text + " (OUT OF BOUNDS)");
                else
                    ImGui.Text(rect_text);

                if (show_offset)
                    ImGui.Text(offset_text);

                ImGui.EndTooltip();
            }
        }
        static void CharFaceTooltip(Rectangle<int> rect, bool use2P)
        {
            if (rect.Size.X <= 0 || rect.Size.Y <= 0) return;
            if (!CHPEditor.ChpFile.CharFace.Loaded && !CHPEditor.ChpFile.CharFace2P.Loaded) return;

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                if (use2P && CHPEditor.ChpFile.CharFace2P.Loaded)
                    CHPEditor.ChpFile.CharFace2P.ImageFile?.DrawForImGui(rect);
                else
                    CHPEditor.ChpFile.CharFace.ImageFile?.DrawForImGui(rect);
                ImGui.EndTooltip();
            }
        }

        static void SpriteCombo(string label, ref int currentrect, bool use_tex = false)
        {
            if (ImGui.BeginCombo(label, currentrect > -1 ? UsedRectsPreview[currentrect] : "???"))
            {
                for (int i = 0; i < UsedRectsPreview.Length; i++)
                {
                    if (ImGui.Selectable(UsedRectsPreview[i], i == currentrect))
                        currentrect = i;

                    BMPTooltip(i, CHPEditor.use2P, use_tex);

                    if (!CHPEditor.anitoggle && ImGui.IsItemHovered())
                        DrawHighlight(CHPEditor.ChpFile.RectCollection[i]);
                }
                ImGui.EndCombo();
            }

            if (currentrect > -1)
            {
                BMPTooltip(currentrect, CHPEditor.use2P, use_tex);
                if (!CHPEditor.anitoggle && ImGui.IsItemHovered())
                    DrawHighlight(CHPEditor.ChpFile.RectCollection[currentrect]);
            }
        }
        static void OffsetCombo(string label, ref int currentrect, ref int currentrect_offset, bool use_tex = false)
        {
            if (ImGui.BeginCombo(label, currentrect_offset > -1 ? UsedRectsPreview[currentrect_offset] : "???"))
            {
                for (int i = 0; i < UsedRectsPreview.Length; i++)
                {
                    if (ImGui.Selectable(UsedRectsPreview[i], i == currentrect_offset))
                        currentrect_offset = i;
                    if (currentrect > -1)
                    {
                        BMPTooltip(currentrect, i, CHPEditor.use2P, true, use_tex);
                        DrawAnimationHighlight(CHPEditor.ChpFile.RectCollection[i]);
                    }
                }
                ImGui.EndCombo();
            }

            if (currentrect > -1 && currentrect_offset > -1)
            {
                BMPTooltip(currentrect, currentrect_offset, CHPEditor.use2P, true, use_tex);
                DrawAnimationHighlight(CHPEditor.ChpFile.RectCollection[currentrect_offset]);
            }
        }

        static void RectCombo(int selected)
        {
            // Weird setup to ensure any inputs after the currently modifying input don't vanish
            bool input = ImGui.InputText("Label", ref CHPEditor.ChpFile.RectComments[selected], 1024);
            input = ImGui.InputInt("X", ref CHPEditor.ChpFile.RectCollection[selected].Origin.X, 1, 10) || input;
            input = ImGui.InputInt("Y", ref CHPEditor.ChpFile.RectCollection[selected].Origin.Y, 1, 10) || input;
            input = ImGui.InputInt("W", ref CHPEditor.ChpFile.RectCollection[selected].Size.X, 1, 10) || input;
            input = ImGui.InputInt("H", ref CHPEditor.ChpFile.RectCollection[selected].Size.Y, 1, 10) || input;

            if (input)
            {
                CHPEditor.ChpFile.RectCollection[selected].Size.X = int.Clamp(CHPEditor.ChpFile.RectCollection[selected].Size.X, 0, int.MaxValue);
                CHPEditor.ChpFile.RectCollection[selected].Size.Y = int.Clamp(CHPEditor.ChpFile.RectCollection[selected].Size.Y, 0, int.MaxValue);
                UpdateUsedRect(selected);
            }
        }
        static void InterpolateRectCombo()
        {
            string fake = "";
            ImGui.InputTextWithHint("Label", "(Interpolated animation)", ref fake, 1);
            ImGui.InputTextWithHint("X", "(Interpolated animation)", ref fake, 1);
            ImGui.InputTextWithHint("Y", "(Interpolated animation)", ref fake, 1);
            ImGui.InputTextWithHint("W", "(Interpolated animation)", ref fake, 1);
            ImGui.InputTextWithHint("H", "(Interpolated animation)", ref fake, 1);
        }

        public static void DrawHighlight(Rectangle<int> rect) { if (HighlightRect) Highlight.Draw(rect); }
        static void DrawAnimationHighlight(Rectangle<int> rect)
        {
            if (!CHPEditor.anitoggle) return;
            if (ImGui.IsItemHovered())
            {
                int anchor_x = (CHPEditor._window.FramebufferSize.X / 2) - (CHPEditor.ChpFile.Size.Width / 2);
                int anchor_y = (CHPEditor._window.FramebufferSize.Y / 2) - (CHPEditor.ChpFile.Size.Height / 2);
                var offset = new Rectangle<int>(anchor_x, anchor_y, 0, 0);
                offset = offset.Add(rect);
                DrawHighlight(offset);
            }
        }

        static Vector2 RatioFromWindowSize(float ratio_x, float ratio_y)
        {
            return new Vector2(CHPEditor._window.Size.X * ratio_x, (CHPEditor._window.Size.Y - 21) * ratio_y);
        }
    }
}
