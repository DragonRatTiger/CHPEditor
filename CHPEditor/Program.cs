using CHPEditor;
using SDL2;
using System.Diagnostics;
using ImGuiNET;

using static SDL2.SDL;

string listenerpath;
if (false) // Placeholder, will make toggleable later
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

#region SDL Setup
SDL_GetVersion(out SDL_version ver);
Trace.TraceInformation("Running SDL Version " + ver.major.ToString() + "." + ver.minor.ToString() + "." + ver.patch.ToString());

if (SDL_Init(SDL_INIT_VIDEO) < 0)
{
    Trace.TraceError("Something went wrong while initializing SDL. {0}", SDL_GetError());
    return;
}

var appWindow = SDL_CreateWindow(
    "CHPEditor INDEV " + System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString()
#if DEBUG
    + " (DEBUG)"
#endif
    ,
    SDL_WINDOWPOS_UNDEFINED,
    SDL_WINDOWPOS_UNDEFINED,
    640,
    480,
    SDL_WindowFlags.SDL_WINDOW_RESIZABLE | SDL_WindowFlags.SDL_WINDOW_ALLOW_HIGHDPI);

if (appWindow == IntPtr.Zero)
    Trace.TraceError("Something went wrong while creating the window. {0}", SDL_GetError());

var renderer = SDL_CreateRenderer(appWindow,
    -1,
    SDL_RendererFlags.SDL_RENDERER_ACCELERATED |
    SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);

if (renderer == IntPtr.Zero)
    Trace.TraceError("Something went wrong while creating the renderer. {0}", SDL_GetError());

SDL_ShowWindow(appWindow);
SDL_RaiseWindow(appWindow);
#endregion

//#region ImGui Setup
//Trace.TraceInformation("Running ImGui Version " + ImGui.GetVersion());

//IntPtr imgui_context = ImGui.CreateContext();
//ImGuiIOPtr imgui_io = ImGui.GetIO();
//imgui_io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;

//ImGui.StyleColorsDark();

//SDL_SysWMinfo info = default;
//SDL_VERSION(out info.version);

//if (SDL_GetWindowWMInfo(appWindow, ref info) == SDL_bool.SDL_TRUE)
//{
//    ImGuiViewportPtr viewport = ImGui.GetMainViewport();
//    viewport.PlatformHandleRaw = info.info.win.window;
//}

//#endregion

bool running = true;

bool anitoggle = false;
bool use2P = false;
int bmpshow = 1;
int bmpstate = 0;
int anishow = 1;
int anistate = 0;

SDL_Rect srcrect = new SDL_Rect() { x = 0, y = 0, w = 1, h = 1 };
SDL_Rect dstrect = new SDL_Rect() { x = 0, y = 0, w = 1, h = 1 };

Trace.TraceInformation("Currently in directory " + Environment.CurrentDirectory);
CHPFile chpFile;

chpFile = new CHPFile(renderer, "chara" + Path.DirectorySeparatorChar +"chara.chp");

ulong tick = 0;
bool pause = false;

while (running)
{
    if (!pause)
        tick = SDL_GetTicks64();

    while (SDL_PollEvent(out SDL_Event output) == 1)
    {
        switch (output.type)
        {
            case SDL_EventType.SDL_QUIT:
                Trace.TraceInformation("Time to quit!");
                running = false;
                break;
            case SDL_EventType.SDL_KEYDOWN:
                switch (output.key.keysym.sym)
                {
                    #region Toggle between bitmaps & animation
                    case SDL_Keycode.SDLK_LEFTBRACKET:
                        anitoggle = false;
                        Trace.TraceInformation("Switching to Bitmap mode");
                        break;
                    case SDL_Keycode.SDLK_RIGHTBRACKET:
                        anitoggle = true;
                        Trace.TraceInformation("Switching to Animation mode");
                        break;
                    case SDL_Keycode.SDLK_SPACE:
                        use2P = !use2P;
                        Trace.TraceInformation("Switching to " + (use2P ? "2P" : "1P") + " palette for Animation");
                        break;
                    #endregion
                    #region Pause Toggle
                    case SDL_Keycode.SDLK_p:
                        pause = !pause;
                        Trace.TraceInformation("Pause is set to {0}", pause);
                        break;
                    #endregion
                    #region Switch Keys
                    case SDL_Keycode.SDLK_1:
                        if (anitoggle)
                        {
                            anishow = 1;
                            Trace.TraceInformation("State 1 (Neutral)");
                        }
                        else
                        {
                            bmpshow = 1;
                            Trace.TraceInformation("CharBMP");
                        }
                        break;
                    case SDL_Keycode.SDLK_2:
                        if (anitoggle)
                        {
                            anishow = 2;
                            Trace.TraceInformation("State 2 (Second - Legacy)");
                        }
                        else
                        {
                            bmpshow = 2;
                            Trace.TraceInformation("CharBMP2P");
                        }
                        break;
                    case SDL_Keycode.SDLK_3:
                        if (anitoggle)
                        {
                            anishow = 3;
                            Trace.TraceInformation("State 3 (Ojama)");
                        }
                        else
                        {
                            bmpshow = 3;
                            Trace.TraceInformation("CharFace");
                        }
                        break;
                    case SDL_Keycode.SDLK_4:
                        if (anitoggle)
                        {
                            anishow = 4;
                            Trace.TraceInformation("State 4 (Miss)");
                        }
                        else
                        {
                            bmpshow = 4;
                            Trace.TraceInformation("CharFace2P");
                        }
                        break;
                    case SDL_Keycode.SDLK_5:
                        if (anitoggle)
                        {
                            anishow = 5;
                            Trace.TraceInformation("State 5 (Standing)");
                        }
                        else
                        {
                            bmpshow = 5;
                            Trace.TraceInformation("SelectCG");
                        }
                        break;
                    case SDL_Keycode.SDLK_6:
                        if (anitoggle)
                        {
                            anishow = 6;
                            Trace.TraceInformation("State 6 (Fever)");
                        }
                        else
                        {
                            bmpshow = 6;
                            Trace.TraceInformation("SelectCG2P");
                        }
                        break;
                    case SDL_Keycode.SDLK_7:
                        if (anitoggle)
                        {
                            anishow = 7;
                            Trace.TraceInformation("State 7 (Great)");
                        }
                        else
                        {
                            bmpshow = 7;
                            Trace.TraceInformation("CharTex");
                        }
                        break;
                    case SDL_Keycode.SDLK_8:
                        if (anitoggle)
                        {
                            anishow = 8;
                            Trace.TraceInformation("State 8 (Good)");
                        }
                        else
                        {
                            bmpshow = 8;
                            Trace.TraceInformation("CharTex2P");
                        }
                        break;
                    case SDL_Keycode.SDLK_9:
                        if (anitoggle)
                        {
                            Trace.TraceInformation("State 9 (Great - Opponent Miss - Rival)");
                            anishow = 9;
                        }
                        break;
                    case SDL_Keycode.SDLK_q:
                        if (anitoggle)
                        {
                            Trace.TraceInformation("State 10 (Bad - Player hits Fever)");
                            anishow = 10;
                        }
                        break;
                    case SDL_Keycode.SDLK_w:
                        if (anitoggle)
                        {
                            Trace.TraceInformation("State 11 (Bad - Player hits Great)");
                            anishow = 11;
                        }
                        break;
                    case SDL_Keycode.SDLK_e:
                        if (anitoggle)
                        {
                            Trace.TraceInformation("State 12 (Bad - Player hits Good)");
                            anishow = 12;
                        }
                        break;
                    case SDL_Keycode.SDLK_r:
                        if (anitoggle)
                        {
                            Trace.TraceInformation("State 13 (Unknown? - Need Clarification)");
                            anishow = 13;
                        }
                        break;
                    case SDL_Keycode.SDLK_t:
                        if (anitoggle)
                        {
                            Trace.TraceInformation("State 14 (Dance)");
                            anishow = 14;
                        }
                        break;
                    case SDL_Keycode.SDLK_y:
                        if (anitoggle)
                        {
                            Trace.TraceInformation("State 15 (Win)");
                            anishow = 15;
                        }
                        break;
                    case SDL_Keycode.SDLK_u:
                        if (anitoggle)
                        {
                            Trace.TraceInformation("State 16 (Lose)");
                            anishow = 16;
                        }
                        break;
                    case SDL_Keycode.SDLK_i:
                        if (anitoggle)
                        {
                            Trace.TraceInformation("State 17 (Fever Win)");
                            anishow = 17;
                        }
                        break;
                    case SDL_Keycode.SDLK_o:
                        if (anitoggle)
                        {
                            Trace.TraceInformation("State 18 (Disturbed - Attacked by Ojama)");
                            anishow = 18;
                        }
                        break;
                    #endregion
                    default:
                        break;
                }
                break;
            default:
                break;
        }
    }

    if (SDL_SetRenderDrawColor(renderer, 200, 200, 255, 255) < 0)
        Trace.TraceError("Couldn't set the render draw color. {0}", SDL_GetError());

    if (SDL_RenderClear(renderer) < 0)
        Trace.TraceError("Couldn't clear the render. {0}", SDL_GetError());
    if (!anitoggle)
        switch (bmpshow)
        {
            case 1:
                RenderTex(renderer, chpFile.CharBMP);
                break;
            case 2:
                RenderTex(renderer, chpFile.CharBMP2P);
                break;
            case 3:
                RenderTex(renderer, chpFile.CharFace);
                break;
            case 4:
                RenderTex(renderer, chpFile.CharFace2P);
                break;
            case 5:
                RenderTex(renderer, chpFile.SelectCG);
                break;
            case 6:
                RenderTex(renderer, chpFile.SelectCG2P);
                break;
            case 7:
                RenderTex(renderer, chpFile.CharTex);
                break;
            case 8:
                RenderTex(renderer, chpFile.CharTex2P);
                break;
        }
    else
        RenderAnimation(renderer, ref chpFile);

    SDL_RenderPresent(renderer);
    Trace.Flush();
}
void RenderTex(IntPtr renderer, IntPtr tex)
{
    if (bmpshow != bmpstate)
    {
        if (tex == IntPtr.Zero)
        {
            Trace.TraceInformation("The texture selected is not loaded. Nothing will be displayed.");
            bmpstate = bmpshow;
            return;
        }
        int query = SDL_QueryTexture(tex, out uint format, out int access, out int w, out int h);
        if (query != 0)
            Trace.TraceWarning("Could not query the given texture. {0}", SDL_GetError());

        srcrect = new SDL_Rect() { x = 0, y = 0, w = w, h = h };
        dstrect = srcrect;

        bmpstate = bmpshow;
    }
    if (tex != IntPtr.Zero)
        SDL_RenderCopy(renderer, tex, ref srcrect, ref dstrect);
}
void RenderAnimation(IntPtr renderer, ref CHPFile chpfile)
{

    if (chpfile.AnimeCollection[anishow - 1].Loaded)
    {
        int state = anishow - 1;
        anistate = anishow;

        SDL_GetWindowSize(appWindow, out int anchor_x, out int anchor_y);
        anchor_x = (anchor_x / 2) - (chpfile.Size[0] / 2);
        anchor_y = (anchor_y / 2) - (chpfile.Size[1] / 2);

        SDL_Rect dst = new SDL_Rect() { x = anchor_x, y = anchor_y, w = chpfile.Size[0], h = chpfile.Size[1] };

        SDL_Rect namedst = new SDL_Rect() { x = anchor_x, y = anchor_y - chpfile.RectCollection[0].h, w = chpfile.RectCollection[0].w, h = chpfile.RectCollection[0].h };

        // Name logo & background
        if (state != 13) // Don't display during Dance
        {
            if (use2P && chpfile.CharBMP2P != IntPtr.Zero)
            {
                SDL_RenderCopy(
                    renderer,
                    chpfile.CharBMP2P,
                    ref chpfile.RectCollection[1],
                    ref dst);
                SDL_RenderCopy(
                    renderer,
                    chpfile.CharBMP2P,
                    ref chpfile.RectCollection[0],
                    ref namedst);
            }
            else
            {
                SDL_RenderCopy(
                    renderer,
                    chpfile.CharBMP,
                    ref chpfile.RectCollection[1],
                    ref dst);
                SDL_RenderCopy(
                    renderer,
                    chpfile.CharBMP,
                    ref chpfile.RectCollection[0],
                    ref namedst);
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

        // how the hell do i do interpoling


        if (chpfile.AnimeCollection[state].Pattern != null) // Draw pattern layer (layer 1)
        {
            int data = chpfile.AnimeCollection[state].Pattern[currentframe];

            if (use2P && chpfile.CharBMP2P != IntPtr.Zero)
                SDL_RenderCopy(
                    renderer,
                    chpfile.CharBMP2P,
                    ref chpfile.RectCollection[data],
                    ref dst);
            else
                SDL_RenderCopy(
                    renderer,
                    chpfile.CharBMP,
                    ref chpfile.RectCollection[data],
                    ref dst);
        }
        if (chpfile.AnimeCollection[state].Texture != null)
        {
            for (int i = 0; i < chpfile.AnimeCollection[state].Texture.Count; i++)
            {
                int[][] texture = chpfile.AnimeCollection[state].Texture[i];
                int[][][] inter = chpfile.InterpolateCollection[state].Texture[i];

                int srcdata = texture[currentframe][0];
                byte alpha = 0xFF;
                double rot = 0;
                SDL_Rect texdst = new SDL_Rect();

                bool[] isInterpole = new bool[4];

                for (int j = 0; j < inter.Length; j++)
                {
                    if (inter[j] != null )
                    {
                        for (int k = 0; k < inter[j].Length; k++)
                        {
                            if (currenttime >= inter[j][k][0] && currenttime <= (inter[j][k][1] + inter[j][k][0]))
                            {
                                double progress = (double)(currenttime - inter[j][k][0]) / (double)inter[j][k][1];
                                switch (j)
                                {
                                    case 1:
                                        texdst = new SDL_Rect() {
                                            x = (int)(chpfile.RectCollection[inter[j][k][2]].x + anchor_x + ((chpfile.RectCollection[inter[j][k][3]].x - chpfile.RectCollection[inter[j][k][2]].x) * progress)),
                                            y = (int)(chpfile.RectCollection[inter[j][k][2]].y + anchor_y + ((chpfile.RectCollection[inter[j][k][3]].y - chpfile.RectCollection[inter[j][k][2]].y) * progress)),
                                            w = (int)(chpfile.RectCollection[inter[j][k][2]].w + ((chpfile.RectCollection[inter[j][k][3]].w - chpfile.RectCollection[inter[j][k][2]].w) * progress)),
                                            h = (int)(chpfile.RectCollection[inter[j][k][2]].h + ((chpfile.RectCollection[inter[j][k][3]].h - chpfile.RectCollection[inter[j][k][2]].h) * progress)),
                                        };
                                        isInterpole[1] = true;
                                        break;
                                    case 2:
                                        alpha = (byte)(inter[2][k][2] + ((inter[2][k][3] - inter[2][k][2]) * progress * (Math.Pow(chpfile.Data, 2) / 256 /* In case someone doesn't use a Data value of 16 */)));
                                        isInterpole[2] = true;
                                        break;
                                    case 3:
                                        rot = ((inter[3][k][2] + ((inter[3][k][3] - inter[3][k][2]) * progress)) / Math.Pow(chpfile.Data, 2)) * 360d; // Heads up! Rotation is reversed on SDL_RenderCopyEx.
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
                    texdst = new SDL_Rect() { x = chpfile.RectCollection[dstdata].x + anchor_x, y = chpfile.RectCollection[dstdata].y + anchor_y, w = chpfile.RectCollection[srcdata].w, h = chpfile.RectCollection[srcdata].h };
                }
                if (!isInterpole[2])
                {
                    alpha = (byte)texture[currentframe][2];
                }
                if (!isInterpole[3])
                {
                    rot = ((double)texture[currentframe][3] / 256f) * 360f;
                }

                if (use2P && chpfile.CharTex2P != IntPtr.Zero)
                {
                    SDL_Point point = new SDL_Point()
                    {
                        x = texdst.w / 2,
                        y = texdst.h / 2
                    };
                    SDL_SetTextureAlphaMod(chpfile.CharTex2P, alpha);
                    SDL_RenderCopyEx(
                        renderer,
                        chpfile.CharTex2P,
                        ref chpfile.RectCollection[srcdata],
                        ref texdst,
                        -rot,
                        ref point,
                        0);
                    SDL_SetTextureAlphaMod(chpfile.CharTex2P, 0xFF);
                }
                else
                {
                    SDL_Point point = new SDL_Point()
                    {
                        x = texdst.w / 2,
                        y = texdst.h / 2
                    };
                    SDL_SetTextureAlphaMod(chpfile.CharTex, alpha);
                    SDL_RenderCopyEx(
                        renderer,
                        chpfile.CharTex,
                        ref chpfile.RectCollection[srcdata],
                        ref texdst,
                        -rot,
                        ref point,
                        0);
                    SDL_SetTextureAlphaMod(chpfile.CharTex, 0xFF);
                }
            }
        }
        if (chpfile.AnimeCollection[state].Layer != null)
        {
            for (int i = 0; i < chpfile.AnimeCollection[state].Layer.Count; i++)
            {
                int[][] layer = chpfile.AnimeCollection[state].Layer[i];
                int[][][] inter = chpfile.InterpolateCollection[state].Layer[i];
                SDL_Rect laydst = new SDL_Rect();

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
                                laydst = new SDL_Rect()
                                {
                                    x = (int)(chpfile.RectCollection[inter[1][j][2]].x + anchor_x + ((chpfile.RectCollection[inter[1][j][3]].x - chpfile.RectCollection[inter[1][j][2]].x) * progress)),
                                    y = (int)(chpfile.RectCollection[inter[1][j][2]].y + anchor_y + ((chpfile.RectCollection[inter[1][j][3]].y - chpfile.RectCollection[inter[1][j][2]].y) * progress)),
                                    w = (int)(chpfile.RectCollection[inter[1][j][2]].w + ((chpfile.RectCollection[inter[1][j][3]].w - chpfile.RectCollection[inter[1][j][2]].w) * progress)),
                                    h = (int)(chpfile.RectCollection[inter[1][j][2]].h + ((chpfile.RectCollection[inter[1][j][3]].h - chpfile.RectCollection[inter[1][j][2]].h) * progress)),
                                };
                            }
                        }
                    }
                }

                if (!isInterpole && layer[currentframe][1] >= 0)
                {
                    int dstdata = layer[currentframe][1];
                    laydst = new SDL_Rect() { x = chpfile.RectCollection[dstdata].x + anchor_x, y = chpfile.RectCollection[dstdata].y + anchor_y, w = chpfile.RectCollection[dstdata].w, h = chpfile.RectCollection[dstdata].h };
                }

                int srcdata = layer[currentframe][0];
                SDL_RenderSetClipRect(renderer, ref dst); // Layer can not cross its size boundaries, so anything extra must be cropped out
                if (use2P && chpfile.CharBMP2P != IntPtr.Zero)
                    SDL_RenderCopy(
                        renderer,
                        chpfile.CharBMP2P,
                        ref chpfile.RectCollection[srcdata],
                        ref laydst);
                else
                    SDL_RenderCopy(
                        renderer,
                        chpfile.CharBMP,
                        ref chpfile.RectCollection[srcdata],
                        ref laydst);
                SDL_RenderSetClipRect(renderer, IntPtr.Zero);
            }
        }
    }
    else if (anishow != anistate)
    {
        Debug.WriteLine("Animation #{0} is not loaded.", anishow);
        anistate = anishow;
    }
}

chpFile.Dispose();
SDL_DestroyRenderer(renderer);
SDL_DestroyWindow(appWindow);
SDL_Quit();
Trace.Flush();