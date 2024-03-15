using System;
using System.Diagnostics;
using System.IO;

using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace CHPEditor
{
    class Program
    {
        public static IWindow _window { get; private set; }
        public static GL _gl { get; private set; }
        public static uint _vao;
        public static uint _vbo { get; private set; }
        public static uint _ebo { get; private set; }
        public static uint _program { get; private set; }
        private static ImGuiIOPtr _io;
        //private static IntPtr? _fontTextureID;

        // GL locations
        public static int tex_loc { get; private set; }
        public static int alpha_loc { get; private set; }
        public static int key_loc { get; private set; }

        public static ImGuiController _controller { get; private set; }

        private static int bmpstate = 1;
        private static int bmpshow = 1;
        private static int anistate = 1;
        private static int anishow = 1;

        // time-keeping stuff
        private static double tick = 0;
        private static bool pause = false;

        // CHP stuff
        private static CHPFile chpFile;
        private static bool anitoggle = false;
        private static bool use2P = false;

        // Misc. stuff
        private static bool hidePat = false;
        private static int hideTexCount = 0;
        private static int hideLayCount = 0;
        // private static UserConfig _userconfig;

        private static readonly string window_title = "CHPEditor INDEV " + System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            #if DEBUG
            + " (DEBUG)"
            #endif
            ;
        static void Main(string[] args)
        {
            InitLog(false);

            WindowOptions options = WindowOptions.Default;
            options.Size = new Vector2D<int>(800, 800);
            options.Title = window_title;

            _window = Window.Create(options);

            _window.Load += Load;
            _window.Update += Update;
            _window.Render += Render;
            _window.FramebufferResize += Resize;

            _window.Run();
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

            chpFile = new CHPFile("chara" + Path.DirectorySeparatorChar + "chara.chp");
        }
        static void Update(double deltaTime)
        {
            if (!pause) tick += deltaTime * 1000;
            _controller.Update((float)deltaTime);
            ImGui_StateSetup();
            //while (SDL_PollEvent(out SDL_Event output) == 1)
            //{
            //    switch (output.type)
            //    {
            //        case SDL_EventType.SDL_QUIT:
            //            Trace.TraceInformation("Time to quit!");
            //            running = false;
            //            break;
            //        case SDL_EventType.SDL_KEYDOWN:
            //            switch (output.key.keysym.sym)
            //            {
            //                #region Toggle between bitmaps & animation
            //                case SDL_Keycode.SDLK_LEFTBRACKET:
            //                    anitoggle = false;
            //                    Trace.TraceInformation("Switching to Bitmap mode");
            //                    break;
            //                case SDL_Keycode.SDLK_RIGHTBRACKET:
            //                    anitoggle = true;
            //                    Trace.TraceInformation("Switching to Animation mode");
            //                    break;
            //                case SDL_Keycode.SDLK_SPACE:
            //                    use2P = !use2P;
            //                    Trace.TraceInformation("Switching to " + (use2P ? "2P" : "1P") + " palette for Animation");
            //                    break;
            //                #endregion
            //                #region Pause Toggle
            //                case SDL_Keycode.SDLK_p:
            //                    pause = !pause;
            //                    Trace.TraceInformation("Pause is set to {0}", pause);
            //                    break;
            //                #endregion
            //                #region Switch Keys
            //                case SDL_Keycode.SDLK_1:
            //                    if (anitoggle)
            //                    {
            //                        anishow = 1;
            //                        Trace.TraceInformation("State 1 (Neutral)");
            //                    }
            //                    else
            //                    {
            //                        bmpshow = 1;
            //                        Trace.TraceInformation("CharBMP");
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_2:
            //                    if (anitoggle)
            //                    {
            //                        anishow = 2;
            //                        Trace.TraceInformation("State 2 (Second - Legacy)");
            //                    }
            //                    else
            //                    {
            //                        bmpshow = 2;
            //                        Trace.TraceInformation("CharBMP2P");
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_3:
            //                    if (anitoggle)
            //                    {
            //                        anishow = 3;
            //                        Trace.TraceInformation("State 3 (Ojama)");
            //                    }
            //                    else
            //                    {
            //                        bmpshow = 3;
            //                        Trace.TraceInformation("CharFace");
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_4:
            //                    if (anitoggle)
            //                    {
            //                        anishow = 4;
            //                        Trace.TraceInformation("State 4 (Miss)");
            //                    }
            //                    else
            //                    {
            //                        bmpshow = 4;
            //                        Trace.TraceInformation("CharFace2P");
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_5:
            //                    if (anitoggle)
            //                    {
            //                        anishow = 5;
            //                        Trace.TraceInformation("State 5 (Standing)");
            //                    }
            //                    else
            //                    {
            //                        bmpshow = 5;
            //                        Trace.TraceInformation("SelectCG");
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_6:
            //                    if (anitoggle)
            //                    {
            //                        anishow = 6;
            //                        Trace.TraceInformation("State 6 (Fever)");
            //                    }
            //                    else
            //                    {
            //                        bmpshow = 6;
            //                        Trace.TraceInformation("SelectCG2P");
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_7:
            //                    if (anitoggle)
            //                    {
            //                        anishow = 7;
            //                        Trace.TraceInformation("State 7 (Great)");
            //                    }
            //                    else
            //                    {
            //                        bmpshow = 7;
            //                        Trace.TraceInformation("CharTex");
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_8:
            //                    if (anitoggle)
            //                    {
            //                        anishow = 8;
            //                        Trace.TraceInformation("State 8 (Good)");
            //                    }
            //                    else
            //                    {
            //                        bmpshow = 8;
            //                        Trace.TraceInformation("CharTex2P");
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_9:
            //                    if (anitoggle)
            //                    {
            //                        Trace.TraceInformation("State 9 (Great - Opponent Miss - Rival)");
            //                        anishow = 9;
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_q:
            //                    if (anitoggle)
            //                    {
            //                        Trace.TraceInformation("State 10 (Bad - Player hits Fever)");
            //                        anishow = 10;
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_w:
            //                    if (anitoggle)
            //                    {
            //                        Trace.TraceInformation("State 11 (Bad - Player hits Great)");
            //                        anishow = 11;
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_e:
            //                    if (anitoggle)
            //                    {
            //                        Trace.TraceInformation("State 12 (Bad - Player hits Good)");
            //                        anishow = 12;
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_r:
            //                    if (anitoggle)
            //                    {
            //                        Trace.TraceInformation("State 13 (Unknown? - Need Clarification)");
            //                        anishow = 13;
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_t:
            //                    if (anitoggle)
            //                    {
            //                        Trace.TraceInformation("State 14 (Dance)");
            //                        anishow = 14;
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_y:
            //                    if (anitoggle)
            //                    {
            //                        Trace.TraceInformation("State 15 (Win)");
            //                        anishow = 15;
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_u:
            //                    if (anitoggle)
            //                    {
            //                        Trace.TraceInformation("State 16 (Lose)");
            //                        anishow = 16;
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_i:
            //                    if (anitoggle)
            //                    {
            //                        Trace.TraceInformation("State 17 (Fever Win)");
            //                        anishow = 17;
            //                    }
            //                    break;
            //                case SDL_Keycode.SDLK_o:
            //                    if (anitoggle)
            //                    {
            //                        Trace.TraceInformation("State 18 (Disturbed - Attacked by Ojama)");
            //                        anishow = 18;
            //                    }
            //                    break;
            //                #endregion
            //                default:
            //                    break;
            //            }
            //            break;
            //        default:
            //            break;
            //    }
            //}

            //if (SDL_SetRenderDrawColor(renderer, 200, 200, 255, 255) < 0)
            //    Trace.TraceError("Couldn't set the render draw color. {0}", SDL_GetError());

            //if (SDL_RenderClear(renderer) < 0)
            //    Trace.TraceError("Couldn't clear the render. {0}", SDL_GetError());
            //if (!anitoggle)
            //    switch (bmpshow)
            //    {
            //        case 1:
            //            //RenderTex(renderer, chpFile.CharBMP);
            //            break;
            //        case 2:
            //            //RenderTex(renderer, chpFile.CharBMP2P);
            //            break;
            //        case 3:
            //            //RenderTex(renderer, chpFile.CharFace);
            //            break;
            //        case 4:
            //            //RenderTex(renderer, chpFile.CharFace2P);
            //            break;
            //        case 5:
            //            //RenderTex(renderer, chpFile.SelectCG);
            //            break;
            //        case 6:
            //            //RenderTex(renderer, chpFile.SelectCG2P);
            //            break;
            //        case 7:
            //            //RenderTex(renderer, chpFile.CharTex);
            //            break;
            //        case 8:
            //            //RenderTex(renderer, chpFile.CharTex2P);
            //            break;
            //    }
            //else
            //{
            //    //RenderAnimation(renderer, ref chpFile);
            //}

            //SDL_RenderPresent(renderer);
            //Trace.Flush();
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
            ImGui.Begin("Selector");
            if (ImGui.BeginTabBar("DisplayMode"))
            {
                if (ImGui.BeginTabItem("Bitmaps"))
                {
                    anitoggle = false;

                    if (ImGui.Selectable("CharBMP"))
                    {
                        Trace.TraceInformation("Displaying CharBMP");
                        bmpshow = 1;
                    }
                    if (ImGui.Selectable("CharBMP2P"))
                    {
                        Trace.TraceInformation("Displaying CharBMP2P");
                        bmpshow = 2;
                    }
                    if (ImGui.Selectable("CharFace"))
                    {
                        Trace.TraceInformation("Displaying CharFace");
                        bmpshow = 3;
                    }
                    if (ImGui.Selectable("CharFace2P"))
                    {
                        Trace.TraceInformation("Displaying CharFace2P");
                        bmpshow = 4;
                    }
                    if (ImGui.Selectable("SelectCG"))
                    {
                        Trace.TraceInformation("Displaying SelectCG");
                        bmpshow = 5;
                    }
                    if (ImGui.Selectable("SelectCG2P"))
                    {
                        Trace.TraceInformation("Displaying SelectCG2P");
                        bmpshow = 6;
                    }
                    if (ImGui.Selectable("CharTex"))
                    {
                        Trace.TraceInformation("Displaying CharTex");
                        bmpshow = 7;
                    }
                    if (ImGui.Selectable("CharTex2P"))
                    {
                        Trace.TraceInformation("Displaying CharTex2P");
                        bmpshow = 8;
                    }

                    ImGui.EndTabItem();
                }                
                if (ImGui.BeginTabItem("Animations"))
                {
                    anitoggle = true;

                    ImGui.Checkbox("Pause Animation", ref pause);
                    ImGui.Checkbox("Use 2P Palette", ref use2P);

                    ImGui.Separator();

                    if (ImGui.Selectable("State 1 (Neutral)"))
                    {
                        Trace.TraceInformation("Previewing State 1 (Neutral)");
                        anishow = 1;                    
                    }
                    if (ImGui.Selectable("State 2 (Second - Legacy)"))
                    {
                        Trace.TraceInformation("Previewing State 2 (Second - Legacy)");
                        anishow = 2;
                    }
                    if (ImGui.Selectable("State 3 (Ojama)"))
                    {
                        Trace.TraceInformation("Previewing State 3 (Ojama)");
                        anishow = 3;
                    }
                    if (ImGui.Selectable("State 4 (Miss)"))
                    {
                        Trace.TraceInformation("Previewing State 4 (Miss)");
                        anishow = 4;
                    }
                    if (ImGui.Selectable("State 5 (Standing)"))
                    {
                        Trace.TraceInformation("Previewing State 5 (Standing)");
                        anishow = 5;
                    }
                    if (ImGui.Selectable("State 6 (Fever)"))
                    {
                        Trace.TraceInformation("Previewing State 6 (Fever)");
                        anishow = 6;
                    }
                    if (ImGui.Selectable("State 7 (Great)"))
                    {
                        Trace.TraceInformation("Previewing State 7 (Great)");
                        anishow = 7;
                    }
                    if (ImGui.Selectable("State 8 (Good)"))
                    {
                        Trace.TraceInformation("Previewing State 8 (Good)");
                        anishow = 8;
                    }
                    if (ImGui.Selectable("State 9 (Great - Opponent Miss - Rival)"))
                    {
                        Trace.TraceInformation("Previewing State 9 (Great - Opponent Miss - Rival)");
                        anishow = 9;
                    }
                    if (ImGui.Selectable("State 10 (Bad - Player hits Fever)"))
                    {
                        Trace.TraceInformation("Previewing State 10 (Bad - Player hits Fever)");
                        anishow = 10;
                    }
                    if (ImGui.Selectable("State 11 (Bad - Player hits Great)"))
                    {
                        Trace.TraceInformation("Previewing State 11 (Bad - Player hits Great)");
                        anishow = 11;
                    }
                    if (ImGui.Selectable("State 12 (Bad - Player hits Good)"))
                    {
                        Trace.TraceInformation("Previewing State 12 (Bad - Player hits Good)");
                        anishow = 12;
                    }
                    if (ImGui.Selectable("State 13 (Unknown? - Need Clarification)"))
                    {
                        Trace.TraceInformation("Previewing State 13 (Unknown? - Need Clarification)");
                        anishow = 13;
                    }
                    if (ImGui.Selectable("State 14 (Dance)"))
                    {
                        Trace.TraceInformation("Previewing State 14 (Dance)");
                        anishow = 14;
                    }
                    if (ImGui.Selectable("State 15 (Win)"))
                    {
                        Trace.TraceInformation("Previewing State 15 (Win)");
                        anishow = 15;
                    }
                    if (ImGui.Selectable("State 16 (Lose)"))
                    {
                        Trace.TraceInformation("Previewing State 16 (Lose)");
                        anishow = 16;
                    }
                    if (ImGui.Selectable("State 17 (Fever Win)"))
                    {
                        Trace.TraceInformation("Previewing State 17 (Fever Win)");
                        anishow = 17;
                    }
                    if (ImGui.Selectable("State 18 (Disturbed - Attacked by Ojama)"))
                    {
                        Trace.TraceInformation("Previewing State 18 (Disturbed - Attacked by Ojama)");
                        anishow = 18;
                    }

                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
            ImGui.End();

            ImGui.Begin("CHP Information");
            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 0.5f, 1.0f, 1.0f), "Notice: Japanese text can not display at this time.");
            ImGui.Separator();
            ImGui.Text(chpFile.FileName);
            // test stuff don't look
            //string encodedstring = Encoding.GetEncoding(932).GetString(Encoding.GetEncoding(932).GetBytes(chpFile.CharName)); //Shift JIS
            if (!string.IsNullOrEmpty(chpFile.CharName)) ImGui.Text("Name: " + chpFile.CharName);
            if (!string.IsNullOrEmpty(chpFile.Artist)) ImGui.Text("Artist: " + chpFile.Artist + "\n\n");

            if (!anitoggle)
            {
                string bmpname = "None";
                Vector2D<int> bmpsize = new Vector2D<int>(0, 0);
                System.Drawing.Color bmpcolor = System.Drawing.Color.Transparent;
                switch (bmpstate)
                {
                    case 1:
                        if (chpFile.CharBMP.Image != null)
                        {
                            bmpname = chpFile.CharBMP.Path;
                            bmpsize = chpFile.CharBMP.Bounds;
                            bmpcolor = chpFile.CharBMP.ColorKey;
                        }
                        break;
                    case 2:
                        if (chpFile.CharBMP2P.Image != null)
                        {
                            bmpname = chpFile.CharBMP2P.Path;
                            bmpsize = chpFile.CharBMP2P.Bounds;
                            bmpcolor = chpFile.CharBMP2P.ColorKey;
                        }
                        break;
                    case 3:
                        if (chpFile.CharFace.Image != null)
                        {
                            bmpname = chpFile.CharFace.Path;
                            bmpsize = chpFile.CharFace.Bounds;
                            bmpcolor = chpFile.CharFace.ColorKey;
                        }
                        break;
                    case 4:
                        if (chpFile.CharFace2P.Image != null)
                        {
                            bmpname = chpFile.CharFace2P.Path;
                            bmpsize = chpFile.CharFace2P.Bounds;
                            bmpcolor = chpFile.CharFace2P.ColorKey;
                        }
                        break;
                    case 5:
                        if (chpFile.SelectCG.Image != null)
                        {
                            bmpname = chpFile.SelectCG.Path;
                            bmpsize = chpFile.SelectCG.Bounds;
                            bmpcolor = chpFile.SelectCG.ColorKey;
                        }
                        break;
                    case 6:
                        if (chpFile.SelectCG2P.Image != null)
                        {
                            bmpname = chpFile.SelectCG2P.Path;
                            bmpsize = chpFile.SelectCG2P.Bounds;
                            bmpcolor = chpFile.SelectCG2P.ColorKey;
                        }
                        break;
                    case 7:
                        if (chpFile.CharTex.Image != null)
                        {
                            bmpname = chpFile.CharTex.Path;
                            bmpsize = chpFile.CharTex.Bounds;
                            bmpcolor = chpFile.CharTex.ColorKey;
                        }
                        break;
                    case 8:
                        if (chpFile.CharTex2P.Image != null)
                        {
                            bmpname = chpFile.CharTex2P.Path;
                            bmpsize = chpFile.CharTex2P.Bounds;
                            bmpcolor = chpFile.CharTex2P.ColorKey;
                        }
                        break;
                }
                ImGui.Text("Bitmap: " + bmpname);
                ImGui.Text("Size: " + bmpsize.X + "," + bmpsize.Y);
                ImGui.Text("ColorSet: " + bmpcolor.R + "," + bmpcolor.G + "," + bmpcolor.B + "," + bmpcolor.A);
            }
            else
            {
                ImGui.Text("State #" + anistate + "\n\n");
                if (chpFile.AnimeCollection[anistate - 1].Loaded)
                {
                    if (chpFile.AnimeCollection[anistate - 1].Frame != 0)
                    {
                        ImGui.Text("Timeline: " +
                            (int)(tick / chpFile.AnimeCollection[anistate - 1].Frame) % chpFile.AnimeCollection[anistate - 1].FrameCount +
                            "/" +
                            chpFile.AnimeCollection[anistate - 1].FrameCount +
                            " (" +
                            Math.Round((tick % (chpFile.AnimeCollection[anistate - 1].Frame * chpFile.AnimeCollection[anistate - 1].FrameCount)) / 1000.0, 2) +
                            "s/" +
                            (chpFile.AnimeCollection[anistate - 1].Frame * chpFile.AnimeCollection[anistate - 1].FrameCount) / 1000.0 +
                            "s)");
                        ImGui.Text("FPS: " + 1000.0f / chpFile.AnimeCollection[anistate - 1].Frame + " (" + chpFile.AnimeCollection[anistate - 1].Frame + "ms/frame)");
                    }                        
                    else
                    {
                        ImGui.Text("Timeline: " +
                            (int)(tick / chpFile.Anime) % chpFile.AnimeCollection[anistate - 1].FrameCount +
                            "/" +
                            chpFile.AnimeCollection[anistate - 1].FrameCount +
                            " (" +
                            Math.Round((tick % (chpFile.Anime * chpFile.AnimeCollection[anistate - 1].FrameCount)) / 1000.0,2) +
                            "s/" +
                            (chpFile.Anime * chpFile.AnimeCollection[anistate - 1].FrameCount) / 1000.0 +
                            "s)");
                        ImGui.Text("FPS: " + 1000.0f / chpFile.Anime + " (" + chpFile.Anime + "ms/frame)");
                    }
                    ImGui.Text("Frame Count: " + chpFile.AnimeCollection[anistate - 1].FrameCount + "\n\n");

                    if (chpFile.AnimeCollection[anistate - 1].Pattern != null)
                    {
                        ImGui.Text("Pattern is Active");
                        ImGui.Checkbox("Hide Pattern", ref hidePat);
                    }
                    if (chpFile.AnimeCollection[anistate - 1].Texture != null)
                    {
                        ImGui.Text("Total Textures Used: " + chpFile.AnimeCollection[anistate - 1].Texture.Count);
                        ImGui.SliderInt("Hide Texture Count", ref hideTexCount, 0, chpFile.AnimeCollection[anistate - 1].Texture.Count);
                    }
                    if (chpFile.AnimeCollection[anistate - 1].Layer != null)
                    {
                        ImGui.Text("Total Layers Used: " + chpFile.AnimeCollection[anistate - 1].Layer.Count);
                        ImGui.SliderInt("Hide Layer Count", ref hideLayCount, 0, chpFile.AnimeCollection[anistate - 1].Layer.Count);
                    }
                }
                else
                {
                    ImGui.Text("No Animation Available");
                }
            }

            ImGui.End();
        }
        // Texture Drawing
        static void RenderTex(CHPFile.BitmapData data)
        {
            if (bmpshow != bmpstate)
            {
                if (data.Image == null)
                {
                    Trace.TraceInformation("The texture selected is not loaded. Nothing will be displayed.");
                    bmpstate = bmpshow;
                    return;
                }
                bmpstate = bmpshow;
            }
            if (data.Image != null)
            {
                Draw(
                    ref data, 
                    new Rectangle<int>(0,0,data.Bounds), 
                    new Rectangle<int>(0,0,data.Bounds)
                    );
            }
        }
        static void RenderAnimation(ref CHPFile chpfile)
        {

            if (chpfile.AnimeCollection[anishow - 1].Loaded)
            {
                int state = anishow - 1;
                anistate = anishow;

                int anchor_x = (_window.FramebufferSize.X / 2) - (chpfile.Size[0] / 2);
                int anchor_y = (_window.FramebufferSize.Y / 2) - (chpfile.Size[1] / 2);

                Rectangle<int> dst = new Rectangle<int> { Origin = new Vector2D<int>(anchor_x, anchor_y), Size = new Vector2D<int>(chpfile.Size[0], chpfile.Size[1]) };

                Rectangle<int> namedst = new Rectangle<int> { Origin = new Vector2D<int>(anchor_x, anchor_y - chpfile.RectCollection[0].Size.Y), Size = new Vector2D<int>(chpfile.RectCollection[0].Size.X, chpfile.RectCollection[0].Size.Y) };

                // Name logo & background
                if (state != 13) // Don't display during Dance
                {
                    if (use2P && chpfile.CharBMP2P.Image != null)
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

                // Get the current frame
                int currentframe;
                int currenttime;
                if (chpfile.AnimeCollection[state].Frame > 0)
                {
                    currentframe = ((int)tick / chpfile.AnimeCollection[state].Frame) % chpfile.AnimeCollection[state].FrameCount;
                    currenttime = ((int)tick % (chpfile.AnimeCollection[state].Frame * chpfile.AnimeCollection[state].FrameCount));
                }
                else
                {
                    currentframe = ((int)tick / chpfile.Anime) % chpfile.AnimeCollection[state].FrameCount;
                    currenttime = ((int)tick % (chpfile.Anime * chpfile.AnimeCollection[state].FrameCount));
                }

                if (chpfile.AnimeCollection[state].Pattern != null && !hidePat)
                {
                    int data = chpfile.AnimeCollection[state].Pattern[currentframe];
                    Rectangle<int> patdst = dst;
                    patdst.Size.X = Math.Min(dst.Size.X, chpfile.RectCollection[data].Size.X);
                    patdst.Size.Y = Math.Min(dst.Size.Y, chpfile.RectCollection[data].Size.Y);

                    if (use2P && chpfile.CharBMP2P.Image != null)
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

                        int srcdata = texture[currentframe][0];
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
                                                alpha = (byte)(inter[2][k][2] + ((inter[2][k][3] - inter[2][k][2]) * progress * (Math.Pow(chpfile.Data, 2) / 256 /* In case someone doesn't use a Data value of 16 */)));
                                                isInterpole[2] = true;
                                                break;
                                            case 3:
                                                rot = ((inter[3][k][2] + ((inter[3][k][3] - inter[3][k][2]) * progress)) / Math.Pow(chpfile.Data, 2)) * 360d;
                                                isInterpole[3] = true;
                                                break;
                                        }
                                    }
                                }
                            }
                        }

                        if (!isInterpole[1])
                        {
                            int dstdata = texture[currentframe][1];
                            texdst = new Rectangle<int>(
                                chpfile.RectCollection[dstdata].Origin.X + anchor_x,
                                chpfile.RectCollection[dstdata].Origin.Y + anchor_y,
                                chpfile.RectCollection[srcdata].Size.X,
                                chpfile.RectCollection[srcdata].Size.Y );
                        }
                        if (!isInterpole[2])
                        {
                            alpha = (byte)texture[currentframe][2];
                        }
                        if (!isInterpole[3])
                        {
                            rot = (texture[currentframe][3] / 256.0) * 360.0;
                        }

                        if (use2P && chpfile.CharTex2P.Image != null)
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
                                    }
                                }
                            }
                        }

                        if (!isInterpole && layer[currentframe][1] >= 0)
                        {
                            int dstdata = layer[currentframe][1];
                            laydst = new Rectangle<int> ( chpfile.RectCollection[dstdata].Origin.X + anchor_x, chpfile.RectCollection[dstdata].Origin.Y + anchor_y, chpfile.RectCollection[dstdata].Size.X, chpfile.RectCollection[dstdata].Size.Y );
                        }

                        int srcdata = layer[currentframe][0];

                        Rectangle<int> crop_rect = chpfile.RectCollection[srcdata];
                        Rectangle<int> crop_dst = laydst;
                        // Layer can not cross its size boundaries, so anything extra must be cropped out
                        if (laydst.Origin.X < dst.Origin.X)
                        {
                            crop_dst.Origin.X -= (laydst.Origin.X - dst.Origin.X);
                            crop_rect.Origin.X -= (laydst.Origin.X - dst.Origin.X);
                            crop_dst.Size.X += (laydst.Origin.X - dst.Origin.X);
                            crop_rect.Size.X += (laydst.Origin.X - dst.Origin.X);
                        }
                        if (laydst.Origin.Y < dst.Origin.Y)
                        {
                            crop_dst.Origin.Y -= (laydst.Origin.Y - dst.Origin.Y);
                            crop_rect.Origin.Y -= (laydst.Origin.Y - dst.Origin.Y);
                            crop_dst.Size.Y += (laydst.Origin.Y - dst.Origin.Y);
                            crop_rect.Size.Y += (laydst.Origin.Y - dst.Origin.Y);
                        }
                        if (laydst.Max.X > dst.Max.X)
                        {
                            crop_dst.Size.X -= (laydst.Max.X - dst.Max.X);
                            crop_rect.Size.X -= (laydst.Max.X - dst.Max.X);
                        }
                        if (laydst.Max.Y > dst.Max.Y)
                        {
                            crop_dst.Size.Y -= (laydst.Max.Y - dst.Max.Y);
                            crop_rect.Size.Y -= (laydst.Max.Y - dst.Max.Y);
                        }
                        crop_rect.Size.X = Math.Max(crop_rect.Size.X, 0);
                        crop_rect.Size.Y = Math.Max(crop_rect.Size.Y, 0);
                        crop_dst.Size.X = Math.Clamp(crop_dst.Size.X, 0, crop_rect.Size.X);
                        crop_dst.Size.Y = Math.Clamp(crop_dst.Size.Y, 0, crop_rect.Size.Y);

                        if (use2P && chpfile.CharBMP2P.Image != null)
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
                Debug.WriteLine("State #{0} is not loaded.", anishow);
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
            _gl.BindTexture(TextureTarget.Texture2D, bitmap_data.Pointer);

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
            _gl.Uniform4(key_loc, (float)bitmap_data.ColorKey.R / 255.0f, (float)bitmap_data.ColorKey.G / 255.0f, (float)bitmap_data.ColorKey.B / 255.0f, (float)bitmap_data.ColorKey.A / 255.0f);

            _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);

            _gl.Uniform1(alpha_loc, 1.0f);
        }
    }
}


