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
using System.Reflection;

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
        public static ImGuiIOPtr IO;
        public static ConfigManager Config;

        // GL locations
        public static int tex_loc = 0;
        public static int alpha_loc = 0;
        public static int key_loc = 0;

        public static ImGuiController _controller { get; private set; }
        public static ImageManager? _imguiFontAtlas;

        public static int bmpstate = 1;
        public static int bmpshow = 1;
        public static int anistate = 1;
        public static int anishow = 1;

        // time-keeping stuff
        public static double tick = 0;
        public static bool pause = false;
        //public static int currentframe = 0;
        //public static int currenttime = 0;

        // CHP stuff
        public static CHPFile ChpFile;
        public static bool anitoggle = false;
        public static bool use2P = false;

        // Misc. stuff
        public static bool useLoop = false;
        public static bool hideBg = false;
        public static bool hidePat = false;
        public static int hideTexCount = 0;
        public static int hideLayCount = 0;

#if DEBUG
        public static bool showDebug = false;
#endif
        // private static UserConfig _userconfig;

        private static Assembly _assembly = Assembly.GetExecutingAssembly();

        private static readonly string window_title = "CHPEditor INDEV " + Assembly.GetExecutingAssembly().GetName().Version
            #if DEBUG
            + " (DEBUG)"
            #endif
            ;
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            HEncodingDetector.InitializeEncodings();

            Config = new ConfigManager("Config.ini");
            LangManager.Initalize(Config.Lang);
            InitLog(Config.LogFileIsTimestamped);

            Trace.TraceInformation("Booting " + _assembly.GetName().Name + " Version " + _assembly.GetName().Version
                #if DEBUG
                 + " (DEBUG)"
                #endif
                );

            WindowOptions options = WindowOptions.Default;
            options.Size = Config.WindowSize;
            options.Position = Config.WindowPos;
            options.Title = window_title;

            _window = Window.Create(options);

            _window.Load += Load;
            _window.Update += Update;
            _window.Render += Render;
            _window.FramebufferResize += Resize;
            _window.Closing += Closing;
            _window.FileDrop += FileDrop;

#if !DEBUG
            try
            {
#endif
                _window.Run();
                Trace.WriteLine("");
                Trace.WriteLine("Exiting CHPEditor. Thank you for your support! ♪(^∇^*)");
#if !DEBUG
            }
            catch (Exception ex)
            {
                Trace.WriteLine("");
                Trace.WriteLine("An unhandled exception has forced CHPEditor to shut down.");
                Trace.WriteLine("More details:");
                Trace.WriteLine(ex);
            }
#endif
        }

        private static void FileDrop(string[] files)
        {
            if (files.Length > 0)
            {
                FileInfo file = new FileInfo(files[0]);
                if (file.Extension.ToLower() == ".chp")
                {
                    Config.Path = file.FullName;
                    ImGuiManager.ReloadFile(file.FullName);
                }
            }
        }

        private static void Closing()
        {
            Config.WindowSize = _window.Size;
            Config.WindowPos = _window.Position;
            Config.SaveConfig("Config.ini");
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

            for (int i = 0; i < input.Mice.Count; i++) { input.Mice[i].Scroll += MouseScroll; }

            _controller = new ImGuiController(_gl, _window, input);
            IO = ImGui.GetIO();
            IO.ConfigFlags |= ImGuiConfigFlags.DockingEnable;

            #region ImGUI Font Setup
            LangManager.UseFont();
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
# version 330 core

layout (location = 0) in vec2 aPosition;
layout (location = 1) in vec2 aTextureCoord;

out vec2 frag_texCoords;

void main()
{
    gl_Position = vec4(aPosition, 0.0, 1.0);
    frag_texCoords = aTextureCoord;
}";
            const string fragmentCode = @"
# version 330 core

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

            ChpFile = new CHPFile(Config.Path);
            ImGuiManager.Initialize();
            ImGuiManager.UpdateCHPStats();
        }

        static void Update(double deltaTime)
        {
            if (!pause)
                tick += deltaTime * 1000;
            if (ChpFile.Loaded)
                Timeline.Update(ref ChpFile.AnimeCollection[anishow-1], ChpFile.Anime, tick);

            _controller.Update((float)deltaTime);
        }
        static void Render(double deltaTime)
        {
            _gl.Clear(ClearBufferMask.ColorBufferBit);

            if (!anitoggle)
                switch (bmpshow)
                {
                    case 1:
                        RenderTex(ChpFile.CharBMP);
                        break;
                    case 2:
                        RenderTex(ChpFile.CharBMP2P);
                        break;
                    case 3:
                        RenderTex(ChpFile.CharFace);
                        break;
                    case 4:
                        RenderTex(ChpFile.CharFace2P);
                        break;
                    case 5:
                        RenderTex(ChpFile.SelectCG);
                        break;
                    case 6:
                        RenderTex(ChpFile.SelectCG2P);
                        break;
                    case 7:
                        RenderTex(ChpFile.CharTex);
                        break;
                    case 8:
                        RenderTex(ChpFile.CharTex2P);
                        break;
                }
            else
            {
                RenderAnimation();
            }

            ImGuiManager.Draw();

            _controller.Render();
        }
        static void Resize(Vector2D<int> size)
        {
            _gl.Viewport(size);
        }
        // Inputs
        private static void MouseScroll(IMouse mouse, ScrollWheel scroll)
        {
            if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow)) 
            {
                float value = scroll.Y / 10.0f;
                ImGuiManager.BackgroundZoom += value;
                if (ImGuiManager.BackgroundZoom + value >= 0.1f)
                    ImGuiManager.BackgroundOffset += new System.Numerics.Vector2(
                        (_window.Size.X / 2) * -value,
                        (_window.Size.Y / 2) * -value
                        );
            }
        }
        // Texture Drawing
        static void RenderTex(CHPFile.BitmapData data)
        {
            if (ChpFile.Loaded)
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
                    ChpFile.Draw(ref data);
                }

                if (ChpFile.RectCollection.Length > 0 && (bmpshow == 1 || bmpshow == 2 || bmpshow == 7 || bmpshow == 8))
                    ImGuiManager.DrawHighlight(ChpFile.RectCollection[ImGuiManager.SelectedRect]);
                else if (bmpshow == 3 || bmpshow == 4)
                { 
                    ImGuiManager.DrawHighlight(ChpFile.CharFaceAllSize);
                    ImGuiManager.DrawHighlight(ChpFile.CharFaceUpperSize);
                }
            }
        }
        static void RenderAnimation()
        {
            if (ChpFile.Loaded)
                if (ChpFile.AnimeCollection[anishow - 1].Loaded)
                {
                    int state = anishow - 1;
                    anistate = anishow;

                    #region Update Frame
                    // Get the current frame
                    //if (ChpFile.AnimeCollection[state].Frame > 0)
                    //{
                    //    if (ChpFile.AnimeCollection[state].Loop > 0 && useLoop)
                    //    {
                    //        int loop = Math.Clamp(ChpFile.AnimeCollection[state].Loop, 0, ChpFile.AnimeCollection[state].FrameCount - 1);
                    //        currentframe = (((int)tick / ChpFile.AnimeCollection[state].Frame) % (ChpFile.AnimeCollection[state].FrameCount - loop)) + ChpFile.AnimeCollection[state].Loop;
                    //        currenttime = ((int)tick % (ChpFile.AnimeCollection[state].Frame * (ChpFile.AnimeCollection[state].FrameCount - loop)) + (ChpFile.AnimeCollection[state].Frame * loop));
                    //    }
                    //    else
                    //    {
                    //        currentframe = ((int)tick / ChpFile.AnimeCollection[state].Frame) % ChpFile.AnimeCollection[state].FrameCount;
                    //        currenttime = ((int)tick % (ChpFile.AnimeCollection[state].Frame * ChpFile.AnimeCollection[state].FrameCount));
                    //    }
                    //}
                    //else
                    //{
                    //    if (ChpFile.AnimeCollection[state].Loop > 0 && useLoop)
                    //    {
                    //        currentframe = ((int)tick / ChpFile.Anime) % (ChpFile.AnimeCollection[state].FrameCount - ChpFile.AnimeCollection[state].Loop) + ChpFile.AnimeCollection[state].Loop;
                    //        currenttime = ((int)tick % (ChpFile.Anime * (ChpFile.AnimeCollection[state].FrameCount - ChpFile.AnimeCollection[state].Loop)) + (ChpFile.Anime * ChpFile.AnimeCollection[state].Loop));
                    //    }
                    //    else
                    //    {
                    //        currentframe = ((int)tick / ChpFile.Anime) % ChpFile.AnimeCollection[state].FrameCount;
                    //        currenttime = ((int)tick % (ChpFile.Anime * ChpFile.AnimeCollection[state].FrameCount));
                    //    }
                    //}
                    int currentframe = Timeline.CurrentFrame;
                    int currenttime = Timeline.CurrentTime;
                    #endregion

                    int anchor_x = (_window.FramebufferSize.X / 2) - (ChpFile.Size.Width / 2);
                    int anchor_y = (_window.FramebufferSize.Y / 2) - (ChpFile.Size.Height / 2);
                    int bg_x = (Config.BackgroundSize.Width / 2) - (ChpFile.Size.Width / 2);
                    int bg_y = (Config.BackgroundSize.Height / 2) - (ChpFile.Size.Height / 2);

                    Rectangle<int> dst = new Rectangle<int> { Origin = new Vector2D<int>(anchor_x, anchor_y), Size = new Vector2D<int>(ChpFile.Size.Width, ChpFile.Size.Height) };
                    if (!Config.UseCharaSizeForBackground) { dst.Size = new Vector2D<int>(Config.BackgroundSize.Width, Config.BackgroundSize.Height); }
                    Rectangle<int> bgdst = new Rectangle<int> { Origin = new Vector2D<int>(anchor_x, anchor_y), Size = Config.UseCharaSizeForBackground ? new Vector2D<int>(ChpFile.Size.Width, ChpFile.Size.Height) : new Vector2D<int>(Config.BackgroundSize.Width, Config.BackgroundSize.Height) };
                    Rectangle<int> namedst = new Rectangle<int> { Origin = new Vector2D<int>(anchor_x + ((bgdst.Size.X - Config.NameSize.Width) / 2), anchor_y - Config.NameSize.Height), Size = Config.UseDataSizeForName ? new Vector2D<int>(ChpFile.RectCollection[0].Size.X, ChpFile.RectCollection[0].Size.Y) : new Vector2D<int>(Config.NameSize.Width, Config.NameSize.Height) };

                    // Name logo & background
                    if (state != 13 && !hideBg) // Don't display during Dance
                    {
                        if (use2P && ChpFile.CharBMP2P.Loaded)
                        {
                            ChpFile.Draw(ref ChpFile.CharBMP2P, ChpFile.RectCollection[1], bgdst);
                            ChpFile.Draw(ref ChpFile.CharBMP2P, ChpFile.RectCollection[0], namedst);
                        }
                        else
                        {
                            ChpFile.Draw(ref ChpFile.CharBMP, ChpFile.RectCollection[1], bgdst);
                            ChpFile.Draw(ref ChpFile.CharBMP, ChpFile.RectCollection[0], namedst);
                        }
                    }

                    // Pattern
                    for (int i = 0; i < ChpFile.AnimeCollection[state].Pattern.Count; i++)
                    {
                        // Skip this interval if requested to hide
                        if (i < ImGuiManager.PatternDisabled.Length)
                            if (ImGuiManager.PatternDisabled[i])
                                continue;

                        var pattern = ChpFile.AnimeCollection[state].Pattern[i];
                        var tween = ChpFile.TweenCollection[state].Pattern[i];
                        int framecap = Math.Clamp(currentframe, 0, pattern.Sprite.Length - 1);

                        int spriteindex = pattern.Sprite[framecap];
                        int offsetindex = framecap < pattern.Offset.Length ? pattern.Offset[framecap] : -1;
                        bool[] isTween = [false, false];

                        #region Set Rects
                        Rectangle<int> sprite = new Rectangle<int>(0, 0, 0, 0);
                        Rectangle<int> offset = new Rectangle<int>(0, 0, 0, 0);

                        for (int j = 0; j < tween.Sprite.Length; j++)
                        {
                            if (tween.Sprite[j].Start <= currenttime && tween.Sprite[j].End >= currenttime)
                            {
                                double progress = (double)(currenttime - tween.Sprite[j].Start) / (double)tween.Sprite[j].Length;
                                var diff = ChpFile.RectCollection[tween.Sprite[j].EndIndex].Subtract(ChpFile.RectCollection[tween.Sprite[j].StartIndex]);
                                sprite = ChpFile.RectCollection[tween.Sprite[j].StartIndex].Add(diff.Multiply(progress));
                                isTween[0] = true;
                            }
                        }

                        for (int j = 0; j < tween.Offset.Length; j++)
                        {
                            if (tween.Offset[j].Start <= currenttime && tween.Offset[j].End >= currenttime)
                            {
                                double progress = (double)(currenttime - tween.Offset[j].Start) / (double)tween.Offset[j].Length;
                                var diff = ChpFile.RectCollection[tween.Offset[j].EndIndex].Subtract(ChpFile.RectCollection[tween.Offset[j].StartIndex]);
                                offset = ChpFile.RectCollection[tween.Offset[j].StartIndex].Add(diff.Multiply(progress));
                                isTween[1] = true;
                            }
                        }

                        if (!isTween[0] && spriteindex != -1)
                            sprite = ChpFile.RectCollection[spriteindex];

                        if (!isTween[1] && offsetindex != -1)
                            offset = ChpFile.RectCollection[offsetindex];
                        else if (!isTween[1] && offsetindex == -1)
                            offset.Size = sprite.Size;

                        offset.Origin.X += anchor_x;
                        offset.Origin.Y += anchor_y;
                        if (!Config.UseCharaSizeForBackground)
                        {
                            offset.Origin.X += bg_x;
                            offset.Origin.Y += bg_y;
                        }
                        #endregion

                        #region Crop
                        Rectangle<int> crop_amount = new Rectangle<int>()
                        {
                            Origin = new Vector2D<int>()
                            {
                                X = offset.Origin.X - Math.Clamp(offset.Origin.X, dst.Origin.X, dst.Max.X),
                                Y = offset.Origin.Y - Math.Clamp(offset.Origin.Y, dst.Origin.Y, dst.Max.Y)
                            },
                            Size = new Vector2D<int>()
                            {
                                X = offset.Max.X - Math.Clamp(offset.Max.X, dst.Origin.X, dst.Max.X),
                                Y = offset.Max.Y - Math.Clamp(offset.Max.Y, dst.Origin.Y, dst.Max.Y)
                            }
                        };

                        Rectangle<int> crop_rect = sprite;
                        Rectangle<int> crop_dst = offset;

                        // Layer can not cross its size boundaries, so anything extra must be cropped out
                        crop_rect.Origin.X -= crop_amount.Origin.X;
                        crop_rect.Origin.Y -= crop_amount.Origin.Y;
                        crop_rect.Size.X += crop_amount.Origin.X - crop_amount.Size.X;
                        crop_rect.Size.Y += crop_amount.Origin.Y - crop_amount.Size.Y;

                        crop_dst.Origin.X -= crop_amount.Origin.X;
                        crop_dst.Origin.Y -= crop_amount.Origin.Y;
                        crop_dst.Size.X += crop_amount.Origin.X - crop_amount.Size.X;
                        crop_dst.Size.Y += crop_amount.Origin.Y - crop_amount.Size.Y;

                        // Skip drawing if any rects have zero width or zero height
                        if (crop_rect.Size.X == 0 || crop_rect.Size.Y == 0 || crop_dst.Size.X <= 0 || crop_dst.Size.Y <= 0) continue;
                        #endregion

                        if (use2P && ChpFile.CharBMP2P.Loaded)
                            ChpFile.Draw(ref ChpFile.CharBMP2P, crop_rect, crop_dst);
                        else
                            ChpFile.Draw(ref ChpFile.CharBMP, crop_rect, crop_dst);
                    }
                    // Texture
                    for (int i = 0; i < ChpFile.AnimeCollection[state].Texture.Count; i++)
                    {
                        // Skip this interval if requested to hide
                        if (i < ImGuiManager.TextureDisabled.Length)
                            if (ImGuiManager.TextureDisabled[i])
                                continue;

                        var texture = ChpFile.AnimeCollection[state].Texture[i];
                        var tween = ChpFile.TweenCollection[state].Texture[i];
                        int framecap = Math.Clamp(currentframe, 0, texture.Sprite.Length - 1);

                        int spriteindex = framecap < texture.Sprite.Length ? texture.Sprite[framecap] : -1;
                        int offsetindex = framecap < texture.Offset.Length ? texture.Offset[framecap] : -1;
                        int alphaindex = framecap < texture.Alpha.Length ? texture.Alpha[framecap] : -1;
                        int rotationindex = framecap < texture.Rotation.Length ? texture.Rotation[framecap] : -1;

                        bool[] isTween = [false, false, false, false];

                        #region Set Rects/Ints
                        Rectangle<int> sprite = new Rectangle<int>(0, 0, 0, 0);
                        Rectangle<int> offset = new Rectangle<int>(0, 0, 0, 0);
                        float alpha = 1.0f;
                        double rotation = 0.0;

                        for (int j = 0; j < tween.Sprite.Length; j++)
                        {
                            if (tween.Sprite[j].Start <= currenttime && tween.Sprite[j].End >= currenttime)
                            {
                                double progress = (double)(currenttime - tween.Sprite[j].Start) / (double)tween.Sprite[j].Length;
                                var diff = ChpFile.RectCollection[tween.Sprite[j].EndIndex].Subtract(ChpFile.RectCollection[tween.Sprite[j].StartIndex]);
                                sprite = ChpFile.RectCollection[tween.Sprite[j].StartIndex].Add(diff.Multiply(progress));
                                isTween[0] = true;
                            }
                        }

                        for (int j = 0; j < tween.Offset.Length; j++)
                        {
                            if (tween.Offset[j].Start <= currenttime && tween.Offset[j].End >= currenttime)
                            {
                                double progress = (double)(currenttime - tween.Offset[j].Start) / (double)tween.Offset[j].Length;
                                var diff = ChpFile.RectCollection[tween.Offset[j].EndIndex].Subtract(ChpFile.RectCollection[tween.Offset[j].StartIndex]);
                                offset = ChpFile.RectCollection[tween.Offset[j].StartIndex].Add(diff.Multiply(progress));
                                isTween[1] = true;
                            }
                        }

                        for (int j = 0; j < tween.Alpha.Length; j++)
                        {
                            if (tween.Alpha[j].Start <= currenttime && tween.Alpha[j].End >= currenttime)
                            {
                                double progress = (double)(currenttime - tween.Alpha[j].Start) / (double)tween.Alpha[j].Length;
                                var diff = tween.Alpha[j].StartIndex + (int)((tween.Alpha[j].EndIndex - tween.Alpha[j].StartIndex) * progress);
                                alpha = diff / 255.0f;
                                isTween[2] = true;
                            }
                        }

                        for (int j = 0; j < tween.Rotation.Length; j++)
                        {
                            if (tween.Rotation[j].Start <= currenttime && tween.Rotation[j].End >= currenttime)
                            {
                                double progress = (double)(currenttime - tween.Rotation[j].Start) / (double)tween.Rotation[j].Length;
                                var diff = tween.Rotation[j].StartIndex + (int)((tween.Rotation[j].EndIndex - tween.Rotation[j].StartIndex) * progress);
                                rotation = (diff / 255.0) * 360.0;
                                isTween[3] = true;
                            }
                        }

                        if (!isTween[0] && spriteindex != -1)
                            sprite = ChpFile.RectCollection[spriteindex];

                        if (!isTween[1] && offsetindex != -1)
                            offset = ChpFile.RectCollection[offsetindex];
                        else if (!isTween[1] && offsetindex == -1)
                            offset.Size = sprite.Size;

                        if (!isTween[2] && alphaindex != -1)
                            alpha = alphaindex / 255.0f;
                        if (!isTween[3] && rotationindex != -1)
                            rotation = (rotationindex / 255.0) * 360.0;

                        offset.Origin.X += anchor_x;
                        offset.Origin.Y += anchor_y;
                        #endregion

                        // Skip drawing if any rects have zero width or zero height
                        if (sprite.Size.X == 0 || sprite.Size.Y == 0 || offset.Size.X <= 0 || offset.Size.Y <= 0) continue;

                        if (use2P && ChpFile.CharTex2P.Loaded)
                            ChpFile.Draw(ref ChpFile.CharTex2P, sprite, offset, rotation, alpha);
                        else
                            ChpFile.Draw(ref ChpFile.CharTex, sprite, offset, rotation, alpha);
                    }
                    // Layer
                    for (int i = 0; i < ChpFile.AnimeCollection[state].Layer.Count; i++)
                    {
                        // Skip this interval if requested to hide
                        if (i < ImGuiManager.LayerDisabled.Length)
                            if (ImGuiManager.LayerDisabled[i])
                                continue;

                        var layer = ChpFile.AnimeCollection[state].Layer[i];
                        var tween = ChpFile.TweenCollection[state].Layer[i];
                        int framecap = Math.Clamp(currentframe, 0, layer.Sprite.Length - 1);

                        int spriteindex = layer.Sprite[framecap];
                        int offsetindex = framecap < layer.Offset.Length ? layer.Offset[framecap] : -1;
                        bool[] isTween = [false, false];

                        #region Set Rects
                        Rectangle<int> sprite = new Rectangle<int>(0, 0, 0, 0);
                        Rectangle<int> offset = new Rectangle<int>(0, 0, 0, 0);

                        for (int j = 0; j < tween.Sprite.Length; j++)
                        {
                            if (tween.Sprite[j].Start <= currenttime && tween.Sprite[j].End >= currenttime)
                            {
                                double progress = (double)(currenttime - tween.Sprite[j].Start) / (double)tween.Sprite[j].Length;
                                var diff = ChpFile.RectCollection[tween.Sprite[j].EndIndex].Subtract(ChpFile.RectCollection[tween.Sprite[j].StartIndex]);
                                sprite = ChpFile.RectCollection[tween.Sprite[j].StartIndex].Add(diff.Multiply(progress));
                                isTween[0] = true;
                            }
                        }

                        for (int j = 0; j < tween.Offset.Length; j++)
                        {
                            if (tween.Offset[j].Start <= currenttime && tween.Offset[j].End >= currenttime)
                            {
                                double progress = (double)(currenttime - tween.Offset[j].Start) / (double)tween.Offset[j].Length;
                                var diff = ChpFile.RectCollection[tween.Offset[j].EndIndex].Subtract(ChpFile.RectCollection[tween.Offset[j].StartIndex]);
                                offset = ChpFile.RectCollection[tween.Offset[j].StartIndex].Add(diff.Multiply(progress));
                                isTween[1] = true;
                            }
                        }

                        if (!isTween[0] && spriteindex != -1)
                            sprite = ChpFile.RectCollection[spriteindex];

                        if (!isTween[1] && offsetindex != -1)
                            offset = ChpFile.RectCollection[offsetindex];
                        else if (!isTween[1] && offsetindex == -1)
                            offset.Size = sprite.Size;

                        offset.Origin.X += anchor_x;
                        offset.Origin.Y += anchor_y;
                        if (!Config.UseCharaSizeForBackground)
                        {
                            offset.Origin.X += bg_x;
                            offset.Origin.Y += bg_y;
                        }
                        #endregion

                        #region Crop
                        Rectangle<int> crop_amount = new Rectangle<int>()
                        {
                            Origin = new Vector2D<int>()
                            {
                                X = offset.Origin.X - Math.Clamp(offset.Origin.X, dst.Origin.X, dst.Max.X),
                                Y = offset.Origin.Y - Math.Clamp(offset.Origin.Y, dst.Origin.Y, dst.Max.Y)
                            },
                            Size = new Vector2D<int>()
                            {
                                X = offset.Max.X - Math.Clamp(offset.Max.X, dst.Origin.X, dst.Max.X),
                                Y = offset.Max.Y - Math.Clamp(offset.Max.Y, dst.Origin.Y, dst.Max.Y)
                            }
                        };

                        Rectangle<int> crop_rect = sprite;
                        Rectangle<int> crop_dst = offset;

                        // Layer can not cross its size boundaries, so anything extra must be cropped out
                        crop_rect.Origin.X -= crop_amount.Origin.X;
                        crop_rect.Origin.Y -= crop_amount.Origin.Y;
                        crop_rect.Size.X += crop_amount.Origin.X - crop_amount.Size.X;
                        crop_rect.Size.Y += crop_amount.Origin.Y - crop_amount.Size.Y;

                        crop_dst.Origin.X -= crop_amount.Origin.X;
                        crop_dst.Origin.Y -= crop_amount.Origin.Y;
                        crop_dst.Size.X += crop_amount.Origin.X - crop_amount.Size.X;
                        crop_dst.Size.Y += crop_amount.Origin.Y - crop_amount.Size.Y;
                        
                        // Skip drawing if any rects have zero width or zero height
                        if (crop_rect.Size.X == 0 || crop_rect.Size.Y == 0 || crop_dst.Size.X <= 0 || crop_dst.Size.Y <= 0) continue;
                        #endregion

                        if (use2P && ChpFile.CharBMP2P.Loaded)
                            ChpFile.Draw(ref ChpFile.CharBMP2P, crop_rect, crop_dst);
                        else
                            ChpFile.Draw(ref ChpFile.CharBMP, crop_rect, crop_dst);
                    }

                    if (ChpFile.RectCollection.Length > 0)
                    {
                        var highlight_rect = ChpFile.RectCollection[ImGuiManager.SelectedRect];
                        highlight_rect.Origin.X += anchor_x;
                        highlight_rect.Origin.Y += anchor_y;
                            ImGuiManager.DrawHighlight(highlight_rect);
                    }
                }
                else if (anishow != anistate)
                {
                    Trace.TraceWarning("State #" + anishow + " is not loaded. Nothing will be displayed.");
                    anistate = anishow;
                }            
        }
    }
}


