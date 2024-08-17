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
        public static LangManager Lang;
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
        public static int currentframe = 0;
        public static int currenttime = 0;

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

            Config = new ConfigManager("Config.ini");
            Lang = new LangManager(Config.Lang);
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
            for (int i = 0; i < input.Keyboards.Count; i++)
                input.Keyboards[i].KeyDown += KeyDown;

            _controller = new ImGuiController(_gl, _window, input);
            IO = ImGui.GetIO();
            IO.ConfigFlags |= ImGuiConfigFlags.DockingEnable;

            #region ImGUI Font Setup
            Lang.UseFont();
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
                RenderAnimation(ref ChpFile);
            }

            ImGuiManager.Draw();

            _controller.Render();
        }
        static void Resize(Vector2D<int> size)
        {
            _gl.Viewport(size);
        }
        static void KeyDown(IKeyboard keyboard, Key key, int value)
        {
            
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
                    if (ChpFile.AnimeCollection[state].Frame > 0)
                    {
                        if (ChpFile.AnimeCollection[state].Loop > 0 && useLoop)
                        {
                            int loop = Math.Clamp(ChpFile.AnimeCollection[state].Loop, 0, ChpFile.AnimeCollection[state].FrameCount - 1);
                            currentframe = (((int)tick / ChpFile.AnimeCollection[state].Frame) % (ChpFile.AnimeCollection[state].FrameCount - loop)) + ChpFile.AnimeCollection[state].Loop;
                            currenttime = ((int)tick % (ChpFile.AnimeCollection[state].Frame * (ChpFile.AnimeCollection[state].FrameCount - loop)) + (ChpFile.AnimeCollection[state].Frame * loop));
                        }
                        else
                        {
                            currentframe = ((int)tick / ChpFile.AnimeCollection[state].Frame) % ChpFile.AnimeCollection[state].FrameCount;
                            currenttime = ((int)tick % (ChpFile.AnimeCollection[state].Frame * ChpFile.AnimeCollection[state].FrameCount));
                        }
                    }
                    else
                    {
                        if (ChpFile.AnimeCollection[state].Loop > 0 && useLoop)
                        {
                            currentframe = ((int)tick / ChpFile.Anime) % (ChpFile.AnimeCollection[state].FrameCount - ChpFile.AnimeCollection[state].Loop) + ChpFile.AnimeCollection[state].Loop;
                            currenttime = ((int)tick % (ChpFile.Anime * (ChpFile.AnimeCollection[state].FrameCount - ChpFile.AnimeCollection[state].Loop)) + (ChpFile.Anime * ChpFile.AnimeCollection[state].Loop));
                        }
                        else
                        {
                            currentframe = ((int)tick / ChpFile.Anime) % ChpFile.AnimeCollection[state].FrameCount;
                            currenttime = ((int)tick % (ChpFile.Anime * ChpFile.AnimeCollection[state].FrameCount));
                        }
                    }
                    #endregion

                    int anchor_x = (_window.FramebufferSize.X / 2) - (chpfile.Size[0] / 2);
                    int anchor_y = (_window.FramebufferSize.Y / 2) - (chpfile.Size[1] / 2);

                    Rectangle<int> dst = new Rectangle<int> { Origin = new Vector2D<int>(anchor_x, anchor_y), Size = new Vector2D<int>(chpfile.Size[0], chpfile.Size[1]) };

                    Rectangle<int> namedst = new Rectangle<int> { Origin = new Vector2D<int>(anchor_x + ((chpfile.RectCollection[1].Size.X - chpfile.RectCollection[0].Size.X) / 2), anchor_y - chpfile.RectCollection[0].Size.Y), Size = new Vector2D<int>(chpfile.RectCollection[0].Size.X, chpfile.RectCollection[0].Size.Y) };

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
            if (ChpFile.AutoColorSet && bitmap_data.ColorKeyType == CHPFile.ColorKeyType.Auto ||
                bitmap_data.ColorKeyType == CHPFile.ColorKeyType.Manual)
                _gl.Uniform4(key_loc, (float)bitmap_data.ColorKey.R / 255.0f, (float)bitmap_data.ColorKey.G / 255.0f, (float)bitmap_data.ColorKey.B / 255.0f, (float)bitmap_data.ColorKey.A / 255.0f);
            else
                _gl.Uniform4(key_loc, 0.0, 0.0, 0.0, 0.0);

            _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);

            _gl.Uniform1(alpha_loc, 1.0f);
        }
    }
}


