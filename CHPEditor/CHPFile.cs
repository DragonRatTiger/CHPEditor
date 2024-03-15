using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using StbImageSharp;

using static CHPEditor.Program;

namespace CHPEditor
{
    internal class CHPFile : IDisposable
    {
        public string FileName;
        public Encoding FileEncoding { get; private set; }
        public struct AnimeData
        {
            public bool Loaded;
            public int Frame;
            public int FrameCount;
            public int Loop;
            public int[] Pattern;
            public List<int[][]> Layer;
            public List<int[][]> Texture;
        }
        public struct InterpolateData // type -> key -> interarray: start, length, startpos, endpos
        {
            public List<int[][][]> Layer;
            public List<int[][][]> Texture;
        }
        public struct BitmapData
        {
            public ImageResult Image;
            public bool UseColorKey;
            public Color ColorKey;
            public Vector2D<int> Bounds
            { 
                get
                {
                    if (Image != null)
                        return new Vector2D<int>(Image.Width, Image.Height);
                    else
                        return new Vector2D<int>(1, 1);
                }
            }
            public string Path
            {
                get
                {
                    return _path;
                } 
                set
                {
                    _path = value;
                    if (File.Exists(_path))
                        Image = ImageResult.FromMemory(File.ReadAllBytes(Path), ColorComponents.RedGreenBlueAlpha);
                } 
            }
            private string _path;
            public uint Pointer;
        }

        public string CharName,
            Artist;
        public string CharFile { get; protected set; }
        public BitmapData CharBMP,
            CharBMP2P,
            CharFace,
            CharFace2P,
            SelectCG,
            SelectCG2P,
            CharTex,
            CharTex2P;

        public int Anime = 83;
        public int[] Size = new int[2] { 167, 271 };
        public int Wait = 1;
        public int Data = 16; // Required for hexadecimal conversion

        public Rectangle<int>[] RectCollection;

        public AnimeData[] AnimeCollection { get; protected set; }
        public InterpolateData[] InterpolateCollection { get; protected set; }
        public CHPFile(string filename)
        {
            try
            {
                string filedata = File.ReadAllText(filename);
                // TEST
                using (var fs = File.OpenRead(filename))
                {
                    Ude.CharsetDetector cdet = new Ude.CharsetDetector();
                    cdet.Feed(fs);
                    cdet.DataEnd();
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    if (cdet.Charset != null)
                    {
                        foreach (EncodingInfo encodinginfo in Encoding.GetEncodings())
                        {
                            if (string.Equals(encodinginfo.Name, cdet.Charset, StringComparison.InvariantCultureIgnoreCase))
                                FileEncoding = encodinginfo.GetEncoding();
                        }
                        if (FileEncoding == null)
                            FileEncoding = Encoding.GetEncoding(932);
                    }
                    else
                    {
                        Console.WriteLine("Detection failed.");
                        FileEncoding = Encoding.UTF8;
                    }
                }
                // TEST
                string folderpath = Path.GetDirectoryName(filename) + Path.DirectorySeparatorChar;
                FileName = Path.GetFileName(filename);

                AnimeCollection = new AnimeData[18];
                InterpolateCollection = new InterpolateData[18];

                filedata = filedata.Replace("\r\n", "\n");
                string[] lines = filedata.Split("\n");
                foreach (string line in lines)
                {
                    if (line.StartsWith("//") || string.IsNullOrWhiteSpace(line))
                        continue;
                    else
                    {
                        // parsing time :)
                        string[] split = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                        if (line.IndexOf("\t") < 0)
                            continue;
                        switch (line.Substring(0, line.IndexOf("\t")))
                        {
                            #region Chara name & artist
                            case "#CharName":
                                CharName = split[1];
                                break;

                            case "#Artist":
                                Artist = split[1];
                                break;
                            #endregion
                            #region Bitmaps
                            case "#CharBMP":
                                LoadTexture(ref CharBMP, folderpath + split[1]);
                                //if (CharBMP == IntPtr.Zero)
                                //    Trace.TraceError("Couldn't load CharBMP. " + SDL.SDL_GetError().ToString());
                                break;

                            case "#CharBMP2P":
                                LoadTexture(ref CharBMP2P, folderpath + split[1]);
                                //if (CharBMP2P == IntPtr.Zero)
                                //    Trace.TraceError("Couldn't load CharBMP2P. " + SDL.SDL_GetError().ToString());
                                break;

                            // Regarding CharFace's background, ColorSet is ignored, and always uses Black (0,0,0,255) as the transparency color.
                            // This is my assumption at least, as every single CharFace I've looked at uses a pure black background.
                            case "#CharFace":
                                LoadTexture(ref CharFace, folderpath + split[1], 1, 0, 0, 0);
                                //if (CharFace == IntPtr.Zero)
                                //    Trace.TraceError("Couldn't load CharFace. " + SDL.SDL_GetError().ToString());
                                break;

                            case "#CharFace2P":
                                LoadTexture(ref CharFace2P, folderpath + split[1], 1, 0, 0, 0);
                                //if (CharFace2P == IntPtr.Zero)
                                //    Trace.TraceError("Couldn't load CharFace2P. " + SDL.SDL_GetError().ToString());
                                break;

                            case "#SelectCG":
                                string cgfile = split[1];
                                string cgfile2 = cgfile.Replace("1p", "2p");

                                LoadTexture(ref SelectCG, folderpath + cgfile, 0);
                                //if (SelectCG == IntPtr.Zero)
                                //    Trace.TraceError("Couldn't load SelectCG. " + SDL.SDL_GetError().ToString());

                                // 2P version of SelectCG. For some reason, #SelectCG2P does not exist on any CHP that I've seen, but the 2P icon is still loaded anyways.
                                // This is my assumption of how it's loaded.
                                if (File.Exists(folderpath + cgfile2) && cgfile2.Contains("1p"))
                                {
                                    LoadTexture(ref SelectCG2P, folderpath + cgfile2, 0);
                                    //if (SelectCG2P == IntPtr.Zero)
                                    //    Trace.TraceError("Couldn't load SelectCG's 2P equivalent. " + SDL.SDL_GetError().ToString());
                                }
                                else
                                    Trace.TraceWarning("SelectCG's 2P equivalent couldn't be found. If you don't have a 2P palette, or if you prefer to not use a 2P icon, you can ignore this message. Otherwise, you may want to check that your file is named correctly. (Try replacing \"1p\" with \"2p\")");
                                
                                break;

                            case "#CharTex":
                                LoadTexture(ref CharTex, folderpath + split[1]);
                                //if (CharTex == IntPtr.Zero)
                                //    Trace.TraceError("Couldn't load CharTex. " + SDL.SDL_GetError().ToString());
                                break;

                            case "#CharTex2P":
                                LoadTexture(ref CharTex2P, folderpath + split[1]);
                                //if (CharTex2P == IntPtr.Zero)
                                //    Trace.TraceError("Couldn't load CharTex2P. " + SDL.SDL_GetError().ToString());
                                break;
                            #endregion
                            #region Chara parameters
                            case "#AutoColorSet":
                                // This should only run after #CharBMP has been loaded.
                                //if (CharBMP == IntPtr.Zero)
                                //{
                                //    Trace.TraceError("Tried to get the transparency color, but CharBMP is not loaded. Defaulting to Black (0,0,0,255).");
                                //    break;
                                //}
                                // getting pixels via. SDL is hard
                                // will do this later
                                break;

                            case "#Anime":
                                if (!int.TryParse(split[1], out Anime))
                                    Trace.TraceError("Failed to parse Anime value. Did you write it correctly?");
                                break;

                            case "#Size":
                                if (!int.TryParse(split[1], out Size[0]) || !int.TryParse(split[2], out Size[1]))
                                    Trace.TraceError("Failed to parse Size value. Did you write it correctly?");
                                break;

                            case "#Wait":
                                if (!int.TryParse(split[1], out Wait))
                                    Trace.TraceError("Failed to parse Wait value. Did you write it correctly?");
                                break;

                            case "#Data":
                                if (!int.TryParse(split[1], out Data))
                                    Trace.TraceError("Failed to parse Data value. Did you write it correctly?");
                                break;
                            #endregion
                            #region Animation
                            case "#Loop":
                                int loop = int.Parse(split[1]) - 1;

                                AnimeCollection[loop].Loop = int.Parse(split[2]);
                                break;

                            case "#Flame": // This is the correct command, it's just a misspelling that ended up being final.
                            case "#Frame": // Optionally including this one just in case.
                                int flame = int.Parse(split[1]) - 1;
                                AnimeCollection[flame].Frame = int.Parse(split[2]);
                                break;

                            case "#Patern": // This is also the correct command, but was once again misspelled.
                            case "#Pattern": // Also optionally including this one.
                                int patern = int.Parse(split[1]) - 1;

                                AnimeCollection[patern].Loaded = true;

                                AnimeCollection[patern].Pattern = new int[split[2].Length / 2];
                                for (int i = 0; i < AnimeCollection[patern].Pattern.Length; i++)
                                    if (int.TryParse(split[2].Substring(i * 2, 2), NumberStyles.HexNumber, null, out int result))
                                        AnimeCollection[patern].Pattern[i] = result;
                                    else
                                        AnimeCollection[patern].Pattern[i] = -1; // Indicator of interpoling point

                                AnimeCollection[patern].FrameCount = AnimeCollection[patern].Pattern.Length; // All layers must have an equal frame count.
                                break;

                            case "#Texture":
                                int texture = int.Parse(split[1]) - 1;

                                AnimeCollection[texture].Loaded = true;

                                if (AnimeCollection[texture].Texture == null)
                                {
                                    AnimeCollection[texture].Texture = new List<int[][]>();
                                    InterpolateCollection[texture].Texture = new List<int[][][]>();
                                }
                                AnimeCollection[texture].Texture.Add(new int[split[2].Length / 2][]);
                                InterpolateCollection[texture].Texture.Add(new int[4][][]);

                                for (int i = 0; i < AnimeCollection[texture].Texture.Last().Length; i++)
                                {
                                    AnimeCollection[texture].Texture.Last()[i] = new int[4] { 0, 0, 255, 0 };

                                    for (int j = 0; j < 4; j++)
                                        if (j + 2 < split.Length)
                                        {
                                            if (int.TryParse(split[j+2].Substring(i * 2, 2), NumberStyles.HexNumber, null, out int result))
                                            {
                                                AnimeCollection[texture].Texture.Last()[i][j] = result;
                                                #region Interpolate
                                                if (i + 1 < AnimeCollection[texture].Texture.Last().Length)
                                                {
                                                    if (!int.TryParse(split[j + 2].Substring((i+1) * 2, 2), NumberStyles.HexNumber, null, out int interresult))
                                                    {
                                                        if (InterpolateCollection[texture].Texture.Last()[j] == null)
                                                        {
                                                            InterpolateCollection[texture].Texture.Last()[j] = new int[1][];
                                                            InterpolateCollection[texture].Texture.Last()[j][0] = new int[4] {
                                                                AnimeCollection[texture].Frame != 0 ? AnimeCollection[texture].Frame * i : Anime * i,
                                                                AnimeCollection[texture].Frame != 0 ? AnimeCollection[texture].Frame : Anime,
                                                                result,
                                                                0
                                                            };
                                                        }
                                                        else
                                                        {
                                                            Array.Resize(ref InterpolateCollection[texture].Texture.Last()[j], InterpolateCollection[texture].Texture.Last()[j].Length + 1);
                                                            InterpolateCollection[texture].Texture.Last()[j][InterpolateCollection[texture].Texture.Last()[j].Length - 1] = new int[4] {
                                                                AnimeCollection[texture].Frame != 0 ? AnimeCollection[texture].Frame * i : Anime * i,
                                                                AnimeCollection[texture].Frame != 0 ? AnimeCollection[texture].Frame : Anime,
                                                                result,
                                                                0
                                                            };
                                                        }
                                                    }
                                                }
                                                #endregion
                                            }
                                            else
                                            {
                                                AnimeCollection[texture].Texture.Last()[i][j] = -1; // Indicator of interpoling point
                                                #region Interpolate
                                                InterpolateCollection[texture].Texture.Last()[j].Last()[1] += AnimeCollection[texture].Frame != 0 ? AnimeCollection[texture].Frame : Anime;
                                                if (i + 1 < AnimeCollection[texture].Texture.Last().Length)
                                                {
                                                    if (int.TryParse(split[j + 2].Substring((i + 1) * 2, 2), NumberStyles.HexNumber, null, out int interresult))
                                                    {
                                                        InterpolateCollection[texture].Texture.Last()[j].Last()[3] = interresult;
                                                    }
                                                }
                                                #endregion
                                            }
                                        }

                                }
                                AnimeCollection[texture].FrameCount = AnimeCollection[texture].Texture.Last().Length; // All layers must have an equal frame count.
                                break;

                            case "#Layer":
                                int layer = int.Parse(split[1]) - 1;

                                AnimeCollection[layer].Loaded = true;

                                if (AnimeCollection[layer].Layer == null)
                                {
                                    AnimeCollection[layer].Layer = new List<int[][]>();
                                    InterpolateCollection[layer].Layer = new List<int[][][]>();
                                }
                                AnimeCollection[layer].Layer.Add(new int[split[2].Length / 2][]);
                                InterpolateCollection[layer].Layer.Add(new int[2][][]);

                                for (int i = 0; i < AnimeCollection[layer].Layer.Last().Length; i++)
                                {
                                    AnimeCollection[layer].Layer.Last()[i] = new int[2] { 0, 0 };
                                    for (int j = 0; j < 2; j++)
                                        if (int.TryParse(split[j + 2].Substring(i * 2, 2), NumberStyles.HexNumber, null, out int result))
                                        {
                                            AnimeCollection[layer].Layer.Last()[i][j] = result;
                                            #region Interpolate
                                            if (i + 1 < AnimeCollection[layer].Layer.Last().Length)
                                            {
                                                if (!int.TryParse(split[j + 2].Substring((i + 1) * 2, 2), NumberStyles.HexNumber, null, out int interresult))
                                                {
                                                    if (InterpolateCollection[layer].Layer.Last()[j] == null)
                                                    {
                                                        InterpolateCollection[layer].Layer.Last()[j] = new int[1][];
                                                        InterpolateCollection[layer].Layer.Last()[j][0] = new int[4] {
                                                                AnimeCollection[layer].Frame != 0 ? AnimeCollection[layer].Frame * i : Anime * i,
                                                                AnimeCollection[layer].Frame != 0 ? AnimeCollection[layer].Frame : Anime,
                                                                result,
                                                                0
                                                            };
                                                    }
                                                    else
                                                    {
                                                        Array.Resize(ref InterpolateCollection[layer].Layer.Last()[j], InterpolateCollection[layer].Layer.Last()[j].Length + 1);
                                                        InterpolateCollection[layer].Layer.Last()[j][InterpolateCollection[layer].Layer.Last()[j].Length - 1] = new int[4] {
                                                                AnimeCollection[layer].Frame != 0 ? AnimeCollection[layer].Frame * i : Anime * i,
                                                                AnimeCollection[layer].Frame != 0 ? AnimeCollection[layer].Frame : Anime,
                                                                result,
                                                                0
                                                            };
                                                    }
                                                }
                                            }
                                            #endregion
                                        }

                                        else
                                        { 
                                            AnimeCollection[layer].Layer.Last()[i][j] = -1; // Indicator of interpoling point
                                            #region Interpolate
                                            InterpolateCollection[layer].Layer.Last()[j].Last()[1] += AnimeCollection[layer].Frame != 0 ? AnimeCollection[layer].Frame : Anime;
                                            if (i + 1 < AnimeCollection[layer].Layer.Last().Length)
                                            {
                                                if (int.TryParse(split[j + 2].Substring((i + 1) * 2, 2), NumberStyles.HexNumber, null, out int interresult))
                                                {
                                                    InterpolateCollection[layer].Layer.Last()[j].Last()[3] = interresult;
                                                }
                                            }
                                            #endregion
                                        }
                                }
                                AnimeCollection[layer].FrameCount = AnimeCollection[layer].Layer.Last().Length; // All layers must have an equal frame count.
                                break;
                            #endregion
                            default: // Remember that #00 and #01 are strictly reserved for Name Logo & character background respectively
                                if (RectCollection == null)
                                {
                                    RectCollection = new Rectangle<int>[Data * Data];
                                    for (int i = 0; i < RectCollection.Length; i++)
                                        RectCollection[i] = new Rectangle<int>(0,0,0,0);
                                }
                                if (int.TryParse(split[0].Substring(1, 2), NumberStyles.HexNumber, null, out int number))
                                {
                                    if (split.Length >= 2 && int.TryParse(split[1], out int x))
                                        RectCollection[number].Origin.X = x;
                                    if (split.Length >= 3 && int.TryParse(split[2], out int y))
                                        RectCollection[number].Origin.Y = y;
                                    if (split.Length >= 4 && int.TryParse(split[3], out int w))
                                        RectCollection[number].Size.X = w;
                                    if (split.Length >= 5 && int.TryParse(split[4], out int h))
                                        RectCollection[number].Size.Y = h;
                                }
                                break;
                        }
                    }
                }

            }
            catch (FileNotFoundException e)
            {
                Trace.TraceError("Couldn't find the requested file. More details:" + Environment.NewLine + e);
            }
            catch (Exception e)
            {
                Trace.TraceError("Something went wrong while trying to read the requested CHP file. More details:" + Environment.NewLine + e);
            }
        }
        /// <param name="useColorKey">Write 0 to not use color keying, write 1 to manually set the color key, write 2 to automatically set the color key (uses bottom-right pixel of provided image)</param>
        private static unsafe void LoadTexture(ref BitmapData data, string filepath, int useColorKey = 2, byte r = 0x00, byte g = 0x00, byte b = 0x00, byte a = 0xFF)
        {
            data.Path = filepath;
            data.UseColorKey = useColorKey > 0;

            if (useColorKey == 2 && data.Image != null)
            {
                int offset = ((data.Image.Width * data.Image.Height) - 1) * 4;
                data.ColorKey = Color.FromArgb(data.Image.Data[offset + 3], data.Image.Data[offset], data.Image.Data[offset + 1], data.Image.Data[offset + 2]);
            }
            else if (useColorKey == 1 && data.Image != null)
            {
                data.ColorKey = Color.FromArgb(a,r,g,b);
            }
            else
            {
                data.ColorKey = Color.FromArgb(0x00,0x00,0x00,0x00);
            }

            data.Pointer = _gl.GenTexture();
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, data.Pointer);

            if (data.Image != null)
            {
                fixed (byte* ptr = data.Image.Data)
                    _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)data.Image.Width, (uint)data.Image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);

                _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)TextureWrapMode.Repeat);
                _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)TextureWrapMode.Repeat);
                _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)TextureMinFilter.Nearest);
                _gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)TextureMagFilter.Nearest);

                _gl.BindTexture(TextureTarget.Texture2D, 0);
            }
        }

        #region Dispose
        private bool isDisposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
