using ImGuiNET;
using Silk.NET.Maths;
using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;

namespace CHPEditor
{
    static class ImGuiManager
    {
        public static bool[] PatternDisabled = [];
        public static bool[] TextureDisabled = [];
        public static bool[] LayerDisabled = [];
        public static void Draw()
        {
            //if (ImGui.BeginMainMenuBar())
            //{
            //    if (ImGui.BeginMenu("File###MENU_FILE"))
            //    {
            //        ImGui.MenuItem("New###MENU_FILE_NEW", "CTRL+N");
            //        ImGui.EndMenu();
            //    }

            //    ImGui.EndMainMenuBar();
            //}
            #region CHP Selector
#if DEBUG
            if (CHPEditor.showDebug)
                ImGui.ShowDemoWindow();
#endif
            ImGui.Begin(CHPEditor.Lang.GetValue("WINDOW_PREVIEW_SELECTOR_TITLE") + "###SELECT");
            ImGui.SetWindowPos(new System.Numerics.Vector2(0, 0), ImGuiCond.FirstUseEver);
            ImGui.SetWindowSize(new System.Numerics.Vector2(300, 300), ImGuiCond.FirstUseEver);

            if (ImGui.BeginTabBar("DisplayMode"))
            {
                if (ImGui.BeginTabItem(CHPEditor.Lang.GetValue("TAB_BITMAPS")))
                {
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
                    CHPEditor.anitoggle = true;

                    for (int i = 1; i <= 18; i++)
                    {
                        string text = CHPEditor.Lang.GetValue("STATE_FULL_INDEXED", i, CHPEditor.Lang.GetValue(string.Format("STATE{0}_TITLE", i)));
                        if (ImGui.Selectable(text, CHPEditor.anishow == i))
                        {
                            Trace.TraceInformation("Previewing " + text);
                            CHPEditor.anishow = i;
                            CHPEditor.tick = 0;

                            PatternDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[i - 1].Pattern.Count];
                            TextureDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[i - 1].Texture.Count];
                            LayerDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[i - 1].Layer.Count];
                        }
                    }
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.End();
            #endregion

            #region CHP Info
            ImGui.Begin(CHPEditor.Lang.GetValue("WINDOW_CHP_INFO_TITLE") + "###INFO");
            ImGui.SetWindowPos(new System.Numerics.Vector2(300, 0), ImGuiCond.FirstUseEver);
            ImGui.SetWindowSize(new System.Numerics.Vector2(300, 300), ImGuiCond.FirstUseEver);

            ImGui.InputTextWithHint(CHPEditor.Lang.GetValue("CHP_PATH_PROMPT"), Path.Combine("chara", "chara.chp"), ref CHPEditor.Config.Path, 1024);
            if (ImGui.Button(CHPEditor.Lang.GetValue("CHP_RELOAD_PROMPT")))
            {
                CHPEditor.ChpFile.Dispose();
                CHPEditor.ChpFile = null;
                CHPEditor.ChpFile = new CHPFile(CHPEditor.Config.Path);
                CHPEditor.currentframe = 0;
                CHPEditor.currenttime = 0;

                PatternDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern.Count];
                TextureDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture.Count];
                LayerDisabled = new bool[CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer.Count];
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
                            CHPEditor.currentframe,
                            CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount - 1,
                            Math.Round(CHPEditor.currenttime / 1000.0, 2),
                            (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Frame * CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount) / 1000.0
                            ));
                            ImGui.Text(CHPEditor.Lang.GetValue("CHP_CHARA_FPS", 1000.0f / CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Frame, CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Frame));
                        }
                        else
                        {
                            ImGui.Text(CHPEditor.Lang.GetValue("CHP_CHARA_TIMELINE",
                            CHPEditor.currentframe,
                            CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].FrameCount - 1,
                            Math.Round(CHPEditor.currenttime / 1000.0, 2),
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
                            ImGui.Checkbox(CHPEditor.Lang.GetValue("CHP_CHARA_LOOP_PROMPT"), ref CHPEditor.useLoop);
                        }
                        ImGui.Separator();

                        ImGui.Checkbox(CHPEditor.Lang.GetValue("ANIMATIONS_PAUSE_PROMPT"), ref CHPEditor.pause);
                        if (CHPEditor.ChpFile.CharBMP2P.Loaded)
                            ImGui.Checkbox(CHPEditor.Lang.GetValue("ANIMATIONS_USE2P_PROMPT"), ref CHPEditor.use2P);

                        if (CHPEditor.anishow != 14)
                            ImGui.Checkbox(CHPEditor.Lang.GetValue("CHP_CHARA_HIDE_BG_PROMPT"), ref CHPEditor.hideBg);

                        if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern.Count > 0)
                        {
                            ImGui.Text(CHPEditor.Lang.GetValue("CHP_CHARA_PATTERN_ACTIVE", CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern.Count));
                            
                            if (ImGui.BeginListBox("Hide Pattern(s)"))
                            {
                                for (int i = 0; i < PatternDisabled.Length; i++)
                                    if (ImGui.Selectable(CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[i].Comment != "" ?
                                        CHPEditor.Lang.GetValue("CHP_CHARA_ITEM_DETAIL", i + 1, CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Pattern[i].Comment) :
                                        CHPEditor.Lang.GetValue("CHP_CHARA_ITEM", i + 1), !PatternDisabled[i]))
                                        PatternDisabled[i] = !PatternDisabled[i];
                                ImGui.EndListBox();
                            }

                        }
                        if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture.Count > 0)
                        {
                            ImGui.Text(CHPEditor.Lang.GetValue("CHP_CHARA_TEXTURE_ACTIVE", CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture.Count));

                            if (ImGui.BeginListBox("Hide Texture(s)"))
                            {
                                for (int i = 0; i < TextureDisabled.Length; i++)
                                    if (ImGui.Selectable(CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[i].Comment != "" ?
                                        CHPEditor.Lang.GetValue("CHP_CHARA_ITEM_DETAIL", i + 1, CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Texture[i].Comment) :
                                        CHPEditor.Lang.GetValue("CHP_CHARA_ITEM", i + 1), !TextureDisabled[i]))
                                        TextureDisabled[i] = !TextureDisabled[i];
                                ImGui.EndListBox();
                            }
                        }
                        if (CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer.Count > 0)
                        {
                            ImGui.Text(CHPEditor.Lang.GetValue("CHP_CHARA_LAYER_ACTIVE", CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer.Count));

                            if (ImGui.BeginListBox("Hide Layer(s)"))
                            {
                                for (int i = 0; i < LayerDisabled.Length; i++)
                                    if (ImGui.Selectable(CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[i].Comment != "" ?
                                        CHPEditor.Lang.GetValue("CHP_CHARA_ITEM_DETAIL", i + 1, CHPEditor.ChpFile.AnimeCollection[CHPEditor.anishow - 1].Layer[i].Comment) :
                                        CHPEditor.Lang.GetValue("CHP_CHARA_ITEM", i + 1), !LayerDisabled[i]))
                                        LayerDisabled[i] = !LayerDisabled[i];
                                ImGui.EndListBox();
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
        }
    }
}
