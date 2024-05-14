using System;
using System.Diagnostics;
using System.IO;

using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using System.Text;

namespace CHPEditor
{
    class CHPEditor
    {
        public static IWindow _window { get; private set; }
        public static GL _gl { get; private set; }
        public static uint _vao;
        public static uint _vbo { get; private set; }
        public static uint _ebo { get; private set; }
        public static uint _program { get; private set; }
        public static ImGuiIOPtr _io;
        public static LangManager lang;
        public static ConfigManager config;

        // GL locations
        public static int tex_loc { get; private set; }
        public static int alpha_loc { get; private set; }
        public static int key_loc { get; private set; }

        public static ImGuiController _controller { get; private set; }
        public static ImageManager? _imguiFontAtlas;

        private static int bmpstate = 1;
        private static int bmpshow = 1;
        private static int anistate = 1;
        private static int anishow = 1;

        // time-keeping stuff
        private static double tick = 0;
        private static bool pause = false;
        private static int currentframe = 0;
        private static int currenttime = 0;

        // CHP stuff
        private static CHPFile chpFile;
        private static bool anitoggle = false;
        private static bool use2P = false;

        // Misc. stuff
        private static bool useLoop = false;
        private static bool hideBg = false;
        private static bool hidePat = false;
        private static int hideTexCount = 0;
        private static int hideLayCount = 0;

#if DEBUG
        private static bool showDebug = false;
#endif
        // private static UserConfig _userconfig;

        private static readonly string window_title = "CHPEditor INDEV " + System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            #if DEBUG
            + " (DEBUG)"
            #endif
            ;
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            config = new ConfigManager("Config.ini");
            lang = new LangManager(config.Lang);
            InitLog(config.LogFileIsTimestamped);

            WindowOptions options = WindowOptions.Default;
            options.Size = config.WindowSize;
            options.Position = config.WindowPos;
            options.Title = window_title;

            _window = Window.Create(options);

            _window.Load += Load;
            _window.Update += Update;
            _window.Render += Render;
            _window.FramebufferResize += Resize;
            _window.Closing += Closing;

            _window.Run();
        }

        private static void Closing()
        {
            config.WindowSize = _window.Size;
            config.WindowPos = _window.Position;
            config.SaveConfig("Config.ini");
        }

        static void InitLog(bool timestamp)
        {
            string listenerpath;
            if (timestamp)
                listenerpath = "Log" + Path.DirectorySeparatorChar +
                DateTime.Now.Year.ToString() + "-" +
                DateTime.Now.Month.ToString("D2") + "-" +
                DateTime.Now.Day.ToString("D2") + "_" +
                DateTime.Now.Hour.ToString("D2") + "-" +
                DateTime.Now.Minute.ToString("D2") + "-" +
                DateTime.Now.Second.ToString("D2") +
                ".log";
            else
                listenerpath = "Log" + Path.DirectorySeparatorChar + "Output.log";

            Directory.CreateDirectory("Log");
            StreamWriter writer = new StreamWriter(listenerpath, false);
            Trace.Listeners.Add(new TextWriterTraceListener(writer));
            Trace.AutoFlush = true;
        }
        // Window Events
        static unsafe void Load()
        {
            #region Window & ImGUI Setup
            _gl = _window.CreateOpenGL();
            _gl.ClearColor(0.78f, 0.78f, 1f, 1f);

            IInputContext input = _window.CreateInput();
            for (int i = 0; i < input.Keyboards.Count; i++)
                input.Keyboards[i].KeyDown += KeyDown;

            _controller = new ImGuiController(_gl, _window, input);
            _io = ImGui.GetIO();
            _io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;

            #region ImGUI Font Setup
            lang.UseFont();
            #endregion

            #endregion

            #region Vertex/Vertices/Indices
            _vao = _gl.GenVertexArray();
            _gl.BindVertexArray(_vao);

            float[] vertices =
            {
                 0.5f,  0.5f, 1.0f, 1.0f, //tr
                 0.5f, -0.5f, 1.0f, 0.0f, //br
                -0.5f, -0.5f, 0.0f, 0.0f, //bl
                -0.5f,  0.5f, 0.0f, 1.0f  //tl
            };

            _vbo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            fixed (float* buffer = vertices)
                _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), buffer, BufferUsageARB.DynamicDraw);

            uint[] indices =
            {
                0u, 1u, 3u,
                1u, 2u, 3u
            };

            _ebo = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);

            fixed (uint* buffer = indices)
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), buffer, BufferUsageARB.DynamicDraw);
            #endregion

            #region Vertex/Fragment
            const string vertexCode = @"
#version 330 core

layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTextureCoord;

out vec2 frag_texCoords;

void main()
{
    gl_Position = vec4(aPosition, 0.0, 1.0);
    frag_texCoords = aTextureCoord;
}";
            const string fragmentCode = @"
#version 330 core

uniform sampler2D uTexture;
uniform float fragAlpha;
uniform vec4 fragColorKey;
in vec2 frag_texCoords;

vec4 compare;

out vec4 out_color;

void main()
{
    compare = texture(uTexture, frag_texCoords);

    if ((compare-fragColorKey)==0.0)
        out_color = vec4(1.0, 1.0, 1.0, 0.0);
    else
        out_color = vec4(1.0, 1.0, 1.0, fragAlpha) * texture(uTexture, frag_texCoords);
}";

            uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
            _gl.ShaderSource(vertexShader, vertexCode);
            _gl.CompileShader(vertexShader);

            _gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True)
                throw new Exception("The vertex shader failed to compile: " + _gl.GetShaderInfoLog(vertexShader));

            uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
            _gl.ShaderSource(fragmentShader, fragmentCode);
            _gl.CompileShader(fragmentShader);

            _gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out int fStatus);
            if (fStatus != (int)GLEnum.True)
                throw new Exception("The fragment shader failed to compile: " + _gl.GetShaderInfoLog(fragmentShader));
            #endregion

            #region Program Setup
            _program = _gl.CreateProgram();

            _gl.AttachShader(_program, vertexShader);
            _gl.AttachShader(_program, fragmentShader);

            _gl.LinkProgram(_program);

            _gl.GetProgram(_program, ProgramPropertyARB.LinkStatus, out int lStatus);
            if (lStatus != (int)GLEnum.True)
                throw new Exception("The program failed to link: " + _gl.GetProgramInfoLog(_program));

            // Initialization complete, remove test shaders
            _gl.DetachShader(_program, vertexShader);
            _gl.DetachShader(_program, fragmentShader);
            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);

            // Position info, size 2 for x/y
            const uint positionLoc = 0;
            _gl.EnableVertexAttribArray(positionLoc);
            _gl.VertexAttribPointer(positionLoc, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)0);

            // texCoord info, size 2 for u/v
            const uint texCoordLoc = 1;
            _gl.EnableVertexAttribArray(texCoordLoc);
            _gl.VertexAttribPointer(texCoordLoc, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), (void*)(2 * sizeof(float))); // pointer offset
            #endregion

            _gl.BindVertexArray(0);
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
            _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);

            #region Texture Setup
            //StbImageSharp.StbImage.stbi_set_flip_vertically_on_load(1);

            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            #endregion
            tex_loc = _gl.GetUniformLocation(_program, "uTexture");
            alpha_loc = _gl.GetUniformLocation(_program, "fragAlpha");
            key_loc = _gl.GetUniformLocation(_program, "fragColorKey");

            chpFile = new CHPFile(config.Path);
        }
        static void Update(double deltaTime)
        {
            if (!pause) tick += deltaTime * 1000;
            _controller.Update((float)deltaTime);
        }
        static void Render(double deltaTime)
        {
            _gl.Clear(ClearBufferMask.ColorBufferBit);

            if (!anitoggle)
                switch (bmpshow)
                {
                    case 1:
                        RenderTex(chpFile.CharBMP);
                        break;
                    case 2:
                        RenderTex(chpFile.CharBMP2P);
                        break;
                    case 3:
                        RenderTex(chpFile.CharFace);
                        break;
                    case 4:
                        RenderTex(chpFile.CharFace2P);
                        break;
                    case 5:
                        RenderTex(chpFile.SelectCG);
                        break;
                    case 6:
                        RenderTex(chpFile.SelectCG2P);
                        break;
                    case 7:
                        RenderTex(chpFile.CharTex);
                        break;
                    case 8:
                        RenderTex(chpFile.CharTex2P);
                        break;
                }
            else
            {
                RenderAnimation(ref chpFile);
            }

            ImGui_StateSetup();

            _controller.Render();
        }
        static void Resize(Vector2D<int> size)
        {
            _gl.Viewport(size);
        }
        static void KeyDown(IKeyboard keyboard, Key key, int value)
        {
            
        }
        // ImGui Window(s)
        static void ImGui_StateSetup()
        {
            #region CHP Selector
#if DEBUG
            if (showDebug)
                ImGui.ShowDemoWindow();
#endif
            ImGui.SetNextWindowDockID(1, ImGuiCond.FirstUseEver);
            ImGui.Begin(lang.GetValue("WINDOW_PREVIEW_SELECTOR_TITLE") + "###SELECT");
            ImGui.SetWindowPos(new System.Numerics.Vector2(0,0), ImGuiCond.FirstUseEver);
            ImGui.SetWindowSize(new System.Numerics.Vector2(300, 300), ImGuiCond.FirstUseEver);

            if (ImGui.BeginTabBar("DisplayMode"))
            {
                if (ImGui.BeginTabItem(lang.GetValue("TAB_BITMAPS")))
                {
                    anitoggle = false;
                    string[] bmpnames = ["CharBMP", "CharBMP2P", "CharFace", "CharFace2P", "SelectCG", "SelectCG2P", "CharTex", "CharTex2P"];
                    for (int i = 1; i <= 8; i++)
                    {
                        if (ImGui.Selectable(bmpnames[i-1], bmpshow == i))
                        {
                            Trace.TraceInformation("Displaying " + bmpnames[i-1]);
                            bmpshow = i;
                        }
                    }
                    ImGui.EndTabItem();
                }                
                if (ImGui.BeginTabItem(lang.GetValue("TAB_ANIMATIONS")))
                {
                    anitoggle = true;

                    ImGui.Checkbox(lang.GetValue("ANIMATIONS_PAUSE_PROMPT"), ref pause);
                    if (chpFile.CharBMP2P.Loaded)
                        ImGui.Checkbox(lang.GetValue("ANIMATIONS_USE2P_PROMPT"), ref use2P);

                    ImGui.Separator();
                    for (int i = 1; i <= 18; i++)
                    {
                        string text = lang.GetValue("STATE_FULL_INDEXED", i, lang.GetValue(string.Format("STATE{0}_TITLE", i)));
                        if (ImGui.Selectable(text, anishow == i))
                        {
                            Trace.TraceInformation("Previewing " + text);
                            anishow = i;
                            tick = 0;
                        }
                    }
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.End();
            #endregion

            #region CHP Info
            ImGui.SetNextWindowDockID(2, ImGuiCond.FirstUseEver);
            ImGui.Begin(lang.GetValue("WINDOW_CHP_INFO_TITLE") + "###INFO");
            ImGui.SetWindowPos(new System.Numerics.Vector2(300, 0), ImGuiCond.FirstUseEver);
            ImGui.SetWindowSize(new System.Numerics.Vector2(300, 300), ImGuiCond.FirstUseEver);

            ImGui.InputTextWithHint(lang.GetValue("CHP_PATH_PROMPT"), Path.Combine("chara", "chara.chp"), ref config.Path, 64);
            if (ImGui.Button(lang.GetValue("CHP_RELOAD_PROMPT")))
            {
                chpFile.Dispose();
                chpFile = null;
                chpFile = new CHPFile(config.Path);
                currentframe = 0;
                currenttime = 0;
            }
#if DEBUG
            ImGui.Checkbox("DEBUG ONLY! Show ImGUI Demo Window", ref showDebug);
#endif
            ImGui.Separator();
            if (chpFile.Loaded)
            {
                ImGui.Text(lang.GetValue("CHP_FILE_INFO", chpFile.FileName, chpFile.FileEncoding.WebName));

                if (!string.IsNullOrEmpty(chpFile.CharName)) ImGui.Text(lang.GetValue("CHP_CHARA_NAME", chpFile.CharName));
                if (!string.IsNullOrEmpty(chpFile.Artist)) ImGui.Text(lang.GetValue("CHP_CHARA_ARTIST", chpFile.Artist));

                ImGui.Separator();

                if (!anitoggle)
                {
                    string bmpname = lang.GetValue("CHP_BMP_PATH_NONE");
                    Vector2D<int> bmpsize = new Vector2D<int>(0, 0);
                    System.Drawing.Color bmpcolor = System.Drawing.Color.Transparent;
                    switch (bmpstate)
                    {
                        case 1:
                            if (chpFile.CharBMP.Loaded)
                            {
                                bmpname = chpFile.CharBMP.Path;
                                bmpsize = chpFile.CharBMP.Bounds;
                                bmpcolor = chpFile.CharBMP.ColorKey;
                            }
                            break;
                        case 2:
                            if (chpFile.CharBMP2P.Loaded)
                            {
                                bmpname = chpFile.CharBMP2P.Path;
                                bmpsize = chpFile.CharBMP2P.Bounds;
                                bmpcolor = chpFile.CharBMP2P.ColorKey;
                            }
                            break;
                        case 3:
                            if (chpFile.CharFace.Loaded)
                            {
                                bmpname = chpFile.CharFace.Path;
                                bmpsize = chpFile.CharFace.Bounds;
                                bmpcolor = chpFile.CharFace.ColorKey;
                            }
                            break;
                        case 4:
                            if (chpFile.CharFace2P.Loaded)
                            {
                                bmpname = chpFile.CharFace2P.Path;
                                bmpsize = chpFile.CharFace2P.Bounds;
                                bmpcolor = chpFile.CharFace2P.ColorKey;
                            }
                            break;
                        case 5:
                            if (chpFile.SelectCG.Loaded)
                            {
                                bmpname = chpFile.SelectCG.Path;
                                bmpsize = chpFile.SelectCG.Bounds;
                                bmpcolor = chpFile.SelectCG.ColorKey;
                            }
                            break;
                        case 6:
                            if (chpFile.SelectCG2P.Loaded)
                            {
                                bmpname = chpFile.SelectCG2P.Path;
                                bmpsize = chpFile.SelectCG2P.Bounds;
                                bmpcolor = chpFile.SelectCG2P.ColorKey;
                            }
                            break;
                        case 7:
                            if (chpFile.CharTex.Loaded)
                            {
                                bmpname = chpFile.CharTex.Path;
                                bmpsize = chpFile.CharTex.Bounds;
                                bmpcolor = chpFile.CharTex.ColorKey;
                            }
                            break;
                        case 8:
                            if (chpFile.CharTex2P.Loaded)
                            {
                                bmpname = chpFile.CharTex2P.Path;
                                bmpsize = chpFile.CharTex2P.Bounds;
                                bmpcolor = chpFile.CharTex2P.ColorKey;
                            }
                            break;
                    }
                    ImGui.Text(lang.GetValue("CHP_BMP_PATH", bmpname));
                    ImGui.Text(lang.GetValue("CHP_BMP_SIZE", bmpsize.X, bmpsize.Y));
                    ImGui.Text(lang.GetValue("CHP_BMP_COLORKEY", bmpcolor.R, bmpcolor.G, bmpcolor.B, bmpcolor.A));
                }
                else
                {
                    ImGui.Text(lang.GetValue("STATE_INDEXED", anistate) + "\n\n");
                    if (chpFile.AnimeCollection[anistate - 1].Loaded)
                    {
                        if (chpFile.AnimeCollection[anistate - 1].Frame != 0)
                        {
                            ImGui.Text(lang.GetValue("CHP_CHARA_TIMELINE", 
                                currentframe,
                                chpFile.AnimeCollection[anistate - 1].FrameCount - 1,
                                Math.Round(currenttime / 1000.0, 2),
                                (chpFile.AnimeCollection[anistate - 1].Frame * chpFile.AnimeCollection[anistate - 1].FrameCount) / 1000.0
                                ));
                            ImGui.Text(lang.GetValue("CHP_CHARA_FPS", 1000.0f / chpFile.AnimeCollection[anistate - 1].Frame, chpFile.AnimeCollection[anistate - 1].Frame));
                        }                        
                        else
                        {
                            ImGui.Text(lang.GetValue("CHP_CHARA_TIMELINE",
                                currentframe,
                                chpFile.AnimeCollection[anistate - 1].FrameCount - 1,
                                Math.Round(currenttime / 1000.0, 2),
                                (chpFile.Anime * chpFile.AnimeCollection[anistate - 1].FrameCount) / 1000.0
                                ));
                            ImGui.Text(lang.GetValue("CHP_CHARA_FPS", 1000.0f / chpFile.Anime, chpFile.Anime));
                        }

                        if (chpFile.AnimeCollection[anistate - 1].Loop > 0)
                        {
                            ImGui.Text(lang.GetValue("CHP_CHARA_LOOP", chpFile.AnimeCollection[anistate - 1].Loop));
                            ImGui.Checkbox(lang.GetValue("CHP_CHARA_LOOP_PROMPT"), ref useLoop);
                        }

                        ImGui.Separator();

                        if (anishow != 14)
                            ImGui.Checkbox(lang.GetValue("CHP_CHARA_HIDE_BG_PROMPT"), ref hideBg);
                        if (chpFile.AnimeCollection[anistate - 1].Pattern != null)
                        {
                            ImGui.Text(lang.GetValue("CHP_CHARA_PATTERN_ACTIVE"));
                            ImGui.Checkbox(lang.GetValue("CHP_CHARA_HIDE_PATTERN_PROMPT"), ref hidePat);
                        }
                        if (chpFile.AnimeCollection[anistate - 1].Texture != null)
                        {
                            ImGui.Text(lang.GetValue("CHP_CHARA_TEXTURE_ACTIVE", chpFile.AnimeCollection[anistate - 1].Texture.Count));
                            ImGui.SliderInt(lang.GetValue("CHP_CHARA_HIDE_TEXTURE_PROMPT"), ref hideTexCount, 0, chpFile.AnimeCollection[anistate - 1].Texture.Count);
                        }
                        if (chpFile.AnimeCollection[anistate - 1].Layer != null)
                        {
                            ImGui.Text(lang.GetValue("CHP_CHARA_LAYER_ACTIVE", chpFile.AnimeCollection[anistate - 1].Layer.Count));
                            ImGui.SliderInt(lang.GetValue("CHP_CHARA_HIDE_LAYER_PROMPT"), ref hideLayCount, 0, chpFile.AnimeCollection[anistate - 1].Layer.Count);
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled(lang.GetValue("CHP_CHARA_ANIMATION_NONE"));
                    }
                }

            }
            else
            {
                ImGui.TextColored(new System.Numerics.Vector4(1.0f, 0.5f, 0.5f, 1.0f), lang.GetValue("CHP_FILE_LOAD_FAIL"));
                ImGui.TextWrapped(chpFile.Error);
            }
            ImGui.End();
            #endregion
        }
        // Texture Drawing
        static void RenderTex(CHPFile.BitmapData data)
        {
            if (chpFile.Loaded)
            {
                if (bmpshow != bmpstate)
                {
                    if (!data.Loaded)
                    {
                        Trace.TraceInformation("The texture selected is not loaded. Nothing will be displayed.");
                        bmpstate = bmpshow;
                        return;
                    }
                    bmpstate = bmpshow;
                }
                if (data.Loaded)
                {
                    Draw(
                        ref data, 
                        new Rectangle<int>(0,0,data.Bounds), 
                        new Rectangle<int>(0,0,data.Bounds)
                        );
                }
            }
        }
        static void RenderAnimation(ref CHPFile chpfile)
        {
            if (chpfile.Loaded)
                if (chpfile.AnimeCollection[anishow - 1].Loaded)
                {
                    int state = anishow - 1;
                    anistate = anishow;

                    #region Update Frame
                    // Get the current frame
                    if (chpFile.AnimeCollection[state].Frame > 0)
                    {
                        if (chpFile.AnimeCollection[state].Loop > 0 && useLoop)
                        {
                            currentframe = (((int)tick / chpFile.AnimeCollection[state].Frame) % (chpFile.AnimeCollection[state].FrameCount - chpFile.AnimeCollection[state].Loop)) + chpFile.AnimeCollection[state].Loop;
                            currenttime = ((int)tick % (chpFile.AnimeCollection[state].Frame * (chpFile.AnimeCollection[state].FrameCount - chpFile.AnimeCollection[state].Loop)) + (chpFile.AnimeCollection[state].Frame * chpFile.AnimeCollection[state].Loop));
                        }
                        else
                        {
                            currentframe = ((int)tick / chpFile.AnimeCollection[state].Frame) % chpFile.AnimeCollection[state].FrameCount;
                            currenttime = ((int)tick % (chpFile.AnimeCollection[state].Frame * chpFile.AnimeCollection[state].FrameCount));
                        }
                    }
                    else
                    {
                        if (chpFile.AnimeCollection[state].Loop > 0 && useLoop)
                        {
                            currentframe = ((int)tick / chpFile.Anime) % (chpFile.AnimeCollection[state].FrameCount - chpFile.AnimeCollection[state].Loop) + chpFile.AnimeCollection[state].Loop;
                            currenttime = ((int)tick % (chpFile.Anime * (chpFile.AnimeCollection[state].FrameCount - chpFile.AnimeCollection[state].Loop)) + (chpFile.Anime * chpFile.AnimeCollection[state].Loop));
                        }
                        else
                        {
                            currentframe = ((int)tick / chpFile.Anime) % chpFile.AnimeCollection[state].FrameCount;
                            currenttime = ((int)tick % (chpFile.Anime * chpFile.AnimeCollection[state].FrameCount));
                        }
                    }
                    #endregion

                    int anchor_x = (_window.FramebufferSize.X / 2) - (chpfile.Size[0] / 2);
                    int anchor_y = (_window.FramebufferSize.Y / 2) - (chpfile.Size[1] / 2);

                    Rectangle<int> dst = new Rectangle<int> { Origin = new Vector2D<int>(anchor_x, anchor_y), Size = new Vector2D<int>(chpfile.Size[0], chpfile.Size[1]) };

                    Rectangle<int> namedst = new Rectangle<int> { Origin = new Vector2D<int>(anchor_x, anchor_y - chpfile.RectCollection[0].Size.Y), Size = new Vector2D<int>(chpfile.RectCollection[0].Size.X, chpfile.RectCollection[0].Size.Y) };

                    // Name logo & background
                    if (state != 13 && !hideBg) // Don't display during Dance
                    {
                        if (use2P && chpfile.CharBMP2P.Loaded)
                        {
                            Draw(
                                ref chpfile.CharBMP2P,
                                chpfile.RectCollection[1],
                                dst);
                            Draw(
                                ref chpfile.CharBMP2P,
                                chpfile.RectCollection[0],
                                namedst);
                        }
                        else
                        {
                            Draw(
                                ref chpfile.CharBMP,
                                chpfile.RectCollection[1],
                                dst);
                            Draw(
                                ref chpfile.CharBMP,
                                chpfile.RectCollection[0],
                                namedst);
                        }
                    }

                    if (chpfile.AnimeCollection[state].Pattern != null && !hidePat)
                    {
                        int framecap = Math.Clamp(currentframe, 0, chpfile.AnimeCollection[state].Pattern.Length - 1);
                        int data = chpfile.AnimeCollection[state].Pattern[framecap];
                        Rectangle<int> patdst = dst;
                        patdst.Size.X = Math.Min(dst.Size.X, chpfile.RectCollection[data].Size.X);
                        patdst.Size.Y = Math.Min(dst.Size.Y, chpfile.RectCollection[data].Size.Y);

                        if (use2P && chpfile.CharBMP2P.Loaded)
                            Draw(
                                ref chpfile.CharBMP2P,
                                chpfile.RectCollection[data],
                                patdst);
                        else
                            Draw(
                                ref chpfile.CharBMP,
                                chpfile.RectCollection[data],
                                patdst);
                    }
                    if (chpfile.AnimeCollection[state].Texture != null)
                    {
                        for (int i = 0; i < chpfile.AnimeCollection[state].Texture.Count - hideTexCount; i++)
                        {
                            int[][] texture = chpfile.AnimeCollection[state].Texture[i];
                            int[][][] inter = chpfile.InterpolateCollection[state].Texture[i];
                            int framecap = Math.Clamp(currentframe, 0, texture.Length - 1);

                            int srcdata = texture[framecap][0];
                            byte alpha = 0xFF;
                            double rot = 0;
                            Rectangle<int> texdst = new Rectangle<int>();

                            bool[] isInterpole = new bool[4];

                            for (int j = 0; j < inter.Length; j++)
                            {
                                if (inter[j] != null)
                                {
                                    for (int k = 0; k < inter[j].Length; k++)
                                    {
                                        if (currenttime >= inter[j][k][0] && currenttime <= (inter[j][k][1] + inter[j][k][0]))
                                        {
                                            double progress = (double)(currenttime - inter[j][k][0]) / (double)inter[j][k][1];
                                            switch (j)
                                            {
                                                case 1:
                                                    texdst = new Rectangle<int>
                                                    (
                                                        (int)(chpfile.RectCollection[inter[j][k][2]].Origin.X + anchor_x + ((chpfile.RectCollection[inter[j][k][3]].Origin.X - chpfile.RectCollection[inter[j][k][2]].Origin.X) * progress)),
                                                        (int)(chpfile.RectCollection[inter[j][k][2]].Origin.Y + anchor_y + ((chpfile.RectCollection[inter[j][k][3]].Origin.Y - chpfile.RectCollection[inter[j][k][2]].Origin.Y) * progress)),
                                                        (int)(chpfile.RectCollection[inter[j][k][2]].Size.X + ((chpfile.RectCollection[inter[j][k][3]].Size.X - chpfile.RectCollection[inter[j][k][2]].Size.X) * progress)),
                                                        (int)(chpfile.RectCollection[inter[j][k][2]].Size.Y + ((chpfile.RectCollection[inter[j][k][3]].Size.Y - chpfile.RectCollection[inter[j][k][2]].Size.Y) * progress))
                                                    );
                                                    isInterpole[1] = true;
                                                    break;
                                                case 2:
                                                    //alpha = (byte)(inter[2][k][2] + ((inter[2][k][3] - inter[2][k][2]) * progress * (Math.Pow(chpfile.Data, 2) / 256 /* In case a Data value of 16 isn't used */)));
                                                    alpha = (byte)(inter[2][k][2] + ((inter[2][k][3] - inter[2][k][2]) * progress));
                                                    isInterpole[2] = true;
                                                    break;
                                                case 3:
                                                    //rot = ((inter[3][k][2] + ((inter[3][k][3] - inter[3][k][2]) * progress)) / Math.Pow(chpfile.Data, 2)) * 360d;
                                                    rot = ((inter[3][k][2] + ((inter[3][k][3] - inter[3][k][2]) * progress)) / 256) * 360d;
                                                    isInterpole[3] = true;
                                                    break;
                                            }
                                        }
                                    }
                                }
                            }

                            if (!isInterpole[1])
                            {
                                int dstdata = texture[framecap][1];
                                texdst = new Rectangle<int>(
                                    chpfile.RectCollection[dstdata].Origin.X + anchor_x,
                                    chpfile.RectCollection[dstdata].Origin.Y + anchor_y,
                                    chpfile.RectCollection[dstdata].Size.X,
                                    chpfile.RectCollection[dstdata].Size.Y );
                            }
                            if (!isInterpole[2])
                            {
                                alpha = (byte)texture[framecap][2];
                            }
                            if (!isInterpole[3])
                            {
                                rot = (texture[framecap][3] / 256.0) * 360.0;
                            }
                            if (chpfile.RectCollection[srcdata].Size.X <= 0 || chpfile.RectCollection[srcdata].Size.X <= 0)
                                texdst.Size = new Vector2D<int>(0, 0);

                            if (use2P && chpfile.CharTex2P.Loaded)
                            {
                                Draw(
                                    ref chpfile.CharTex2P,
                                    chpfile.RectCollection[srcdata],
                                    texdst,
                                    rot,
                                    (float)alpha / 255.0f);
                            }
                            else
                            {
                                Draw(
                                    ref chpfile.CharTex,
                                    chpfile.RectCollection[srcdata],
                                    texdst,
                                    rot,
                                    (float)alpha / 255.0f);
                            }
                        }
                    }
                    if (chpfile.AnimeCollection[state].Layer != null)
                    {
                        for (int i = 0; i < chpfile.AnimeCollection[state].Layer.Count - hideLayCount; i++)
                        {
                            int[][] layer = chpfile.AnimeCollection[state].Layer[i];
                            int[][][] inter = chpfile.InterpolateCollection[state].Layer[i];
                            int framecap = Math.Clamp(currentframe, 0, layer.Length - 1);
                            Rectangle<int> laydst = new Rectangle<int>();

                            bool isInterpole = false;

                            if (inter[1] != null)
                            {
                                for (int j = 0; j < inter[1].Length; j++)
                                {
                                    if (inter[1][j] != null)
                                    {
                                        if (currenttime >= inter[1][j][0] && currenttime <= (inter[1][j][1] + inter[1][j][0]))
                                        {
                                            double progress = (double)(currenttime - inter[1][j][0]) / (double)inter[1][j][1];
                                            laydst = new Rectangle<int>
                                            (
                                                (int)(chpfile.RectCollection[inter[1][j][2]].Origin.X + anchor_x + ((chpfile.RectCollection[inter[1][j][3]].Origin.X - chpfile.RectCollection[inter[1][j][2]].Origin.X) * progress)),
                                                (int)(chpfile.RectCollection[inter[1][j][2]].Origin.Y + anchor_y + ((chpfile.RectCollection[inter[1][j][3]].Origin.Y - chpfile.RectCollection[inter[1][j][2]].Origin.Y) * progress)),
                                                (int)(chpfile.RectCollection[inter[1][j][2]].Size.X + ((chpfile.RectCollection[inter[1][j][3]].Size.X - chpfile.RectCollection[inter[1][j][2]].Size.X) * progress)),
                                                (int)(chpfile.RectCollection[inter[1][j][2]].Size.Y + ((chpfile.RectCollection[inter[1][j][3]].Size.Y - chpfile.RectCollection[inter[1][j][2]].Size.Y) * progress))
                                            );
                                            isInterpole = true;
                                        }
                                    }
                                }
                            }

                            if (!isInterpole && layer[framecap][1] >= 0)
                            {
                                int dstdata = layer[framecap][1];
                                laydst = new Rectangle<int> ( chpfile.RectCollection[dstdata].Origin.X + anchor_x, chpfile.RectCollection[dstdata].Origin.Y + anchor_y, chpfile.RectCollection[dstdata].Size.X, chpfile.RectCollection[dstdata].Size.Y );
                            }

                            int srcdata = layer[framecap][0];

                            Rectangle<int> crop_amount = new Rectangle<int>()
                            {
                                Origin = new Vector2D<int>()
                                {
                                    X = laydst.Origin.X - Math.Clamp(laydst.Origin.X, dst.Origin.X, dst.Max.X),
                                    Y = laydst.Origin.Y - Math.Clamp(laydst.Origin.Y, dst.Origin.Y, dst.Max.Y)
                                },
                                Size = new Vector2D<int>()
                                {
                                    X = laydst.Max.X - Math.Clamp(laydst.Max.X, dst.Origin.X, dst.Max.X),
                                    Y = laydst.Max.Y - Math.Clamp(laydst.Max.Y, dst.Origin.Y, dst.Max.Y),
                                }
                            };

                            Rectangle<int> crop_rect = chpfile.RectCollection[srcdata];
                            Rectangle<int> crop_dst = laydst;

                            // Layer can not cross its size boundaries, so anything extra must be cropped out
                            crop_rect.Origin.X -= crop_amount.Origin.X;
                            crop_rect.Origin.Y -= crop_amount.Origin.Y;
                            crop_rect.Size.X += crop_amount.Origin.X - crop_amount.Size.X;
                            crop_rect.Size.Y += crop_amount.Origin.Y - crop_amount.Size.Y;

                            crop_dst.Origin.X -= crop_amount.Origin.X;
                            crop_dst.Origin.Y -= crop_amount.Origin.Y;
                            crop_dst.Size.X += crop_amount.Origin.X - crop_amount.Size.X;
                            crop_dst.Size.Y += crop_amount.Origin.Y - crop_amount.Size.Y;

                            // Quick fix
                            if (crop_rect.Size.X <= 0 || crop_rect.Size.Y <= 0)
                                crop_dst.Size = new Vector2D<int>(0, 0);

                            if (use2P && chpfile.CharBMP2P.Loaded)
                                Draw(
                                ref chpfile.CharBMP2P,
                                crop_rect,
                                crop_dst);
                            else
                                Draw(
                                ref chpfile.CharBMP,
                                crop_rect,
                                crop_dst);
                        }
                    }
                }
                else if (anishow != anistate)
                {
                    Trace.TraceWarning("State #" + anishow + " is not loaded. Nothing will be displayed.");
                    anistate = anishow;
                }
        }
        static unsafe void Draw(ref CHPFile.BitmapData bitmap_data, Rectangle<int> rect, Rectangle<int> offset)
        {
            Draw(ref bitmap_data, rect, offset, 0.0, 1f);
        }
        static unsafe void Draw(ref CHPFile.BitmapData bitmap_data, Rectangle<int> rect, Rectangle<int> offset, double rot, float alpha)
        {
            float RectX = (float)rect.Origin.X / bitmap_data.Bounds.X;
            float RectY = (float)rect.Origin.Y / bitmap_data.Bounds.Y;
            float RectW = (float)rect.Size.X / bitmap_data.Bounds.X;
            float RectH = (float)rect.Size.Y / bitmap_data.Bounds.Y;
            float OffX = (float)offset.Origin.X / 100.0f;
            float OffY = (float)offset.Origin.Y / 100.0f;
            float OffW = (float)offset.Size.X / 100.0f;
            float OffH = (float)offset.Size.Y / 100.0f;

            // Fix non-uniform viewports creating warped rotations
            float viewportX = 100.0f / _window.FramebufferSize.X;
            float viewportY = 100.0f / _window.FramebufferSize.Y;

            OffX *= 2;
            OffY *= 2;
            OffW *= 2;
            OffH *= 2;

            _gl.BindVertexArray(_vao);
            _gl.UseProgram(_program);

            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, bitmap_data.ImageFile.Pointer);

            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

            float[] vertices =
            {    /* X */     /* Y */       /* U */        /* V */
                 OffW + OffX,        -OffY, RectW + RectX,         RectY, //top-right
                 OffW + OffX, -OffH - OffY, RectW + RectX, RectH + RectY, //bottom-right
                      + OffX, -OffH - OffY,  0.0f + RectX, RectH + RectY, //bottom-left
                      + OffX,        -OffY,  0.0f + RectX,         RectY  //top-left
            };

            if (rot != 0.0f)
            {
                double[] center = new double[2] { (vertices[12] + vertices[4]) / 2.0f, (vertices[13] + vertices[5]) / 2.0f };
                rot = rot * Math.PI / 180.0;
                for (int i = 0; i < vertices.Length; i += 4)
                {
                    double sin = Math.Sin(rot);
                    double cos = Math.Cos(rot);

                    vertices[i] -= (float)center[0];
                    vertices[i+1] -= (float)center[1];

                    float x = (float)((vertices[i] * cos) - (vertices[i + 1] * sin));
                    float y = (float)((vertices[i] * sin) + (vertices[i + 1] * cos));

                    vertices[i] = x + (float)center[0];
                    vertices[i+1] = y + (float)center[1];
                }
            }

            // Fix non-uniform viewports causing warped rotations
            for (int i = 0; i < vertices.Length; i += 4)
            {
                vertices[i] = (vertices[i] * viewportX) - 1.0f;
                vertices[i+1] = (vertices[i+1] * viewportY) + 1.0f;
            }

            fixed (float* buffer = vertices)
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)vertices.Length * sizeof(float), buffer);

            _gl.Uniform1(tex_loc, 0);
            _gl.Uniform1(alpha_loc, alpha);
            if (chpFile.AutoColorSet && bitmap_data.ColorKeyType == CHPFile.ColorKeyType.Auto ||
                bitmap_data.ColorKeyType == CHPFile.ColorKeyType.Manual)
                _gl.Uniform4(key_loc, (float)bitmap_data.ColorKey.R / 255.0f, (float)bitmap_data.ColorKey.G / 255.0f, (float)bitmap_data.ColorKey.B / 255.0f, (float)bitmap_data.ColorKey.A / 255.0f);
            else
                _gl.Uniform4(key_loc, 0.0, 0.0, 0.0, 0.0);

            _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);

            _gl.Uniform1(alpha_loc, 1.0f);
        }
    }
}


