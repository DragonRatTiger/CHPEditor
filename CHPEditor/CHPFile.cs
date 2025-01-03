﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Silk.NET.Maths;

namespace CHPEditor
{
    public class CHPFile : IDisposable
    {
        public enum ColorKeyType
        {
            None = 0,
            Manual = 1,
            Auto = 2
        }
        public bool Loaded { get; private set; }
        public string Error { get; private set; }

        public FileInfo? FileInformation { get; private set; }
        public string FileName => FileInformation?.Name ?? "";
        public string FilePath { get { try { return FileInformation?.FullName ?? ""; } catch { return ""; } } }
        public string FolderPath { get { try { return Path.GetDirectoryName(FilePath) ?? ""; } catch { return ""; } } }
        public Encoding FileEncoding { get; private set; } = Encoding.GetEncoding(932);

        public int Anime = 83; // Took a guess for this one, since some charas don't have #Anime defined. Need to look a bit more into this. (It's 12fps rounded down)
        public Size Size = new Size(121, 271); // Begin with 121,271 for legacy support. Modern pomyus are typically 167,271.
        public int Wait = 1; // # of times to repeat idle animation before playing other animations
        public int Data = 10; // Required for hexadecimal conversion. Defaulted to 10 for legacy pomyu support.
        public bool AutoColorSet
        {
            get { return IsLegacy ? true : _autoColorSet; }
            set { _autoColorSet = value; }
        }
        private bool _autoColorSet = false;
        public Rectangle<int> CharFaceUpperSize = new Rectangle<int>(0, 0, 256, 256);
        public Rectangle<int> CharFaceAllSize = new Rectangle<int>(320, 0, 320, 480);

        /// <summary>
        /// Legacy characters have slightly different behavior from modern characters.<br/>
        /// Data-wise, legacy pomyus only use up to 100 rects, and always use color keying regardless if <c>#AutoColorSet</c> is found.<br/>
        /// This is set to false when <c>#Data</c> is found in a CHP file during parsing.
        /// </summary>
        public bool IsLegacy { get; private set; } = true;

        public struct AnimeData
        {
            public struct PatternData
            {
                public PatternData() { Sprite = []; Offset = []; Comment = ""; }
                /// <summary>
                /// The index pointing to which sprite to display.
                /// </summary>
                public int[] Sprite;
                /// <summary>
                /// The index pointing to which offset/scale to use for the sprite.
                /// </summary>
                public int[] Offset;
                /// <summary>
                /// If available, this variable will contain the comment left after the data.
                /// </summary>
                public string Comment;
            }
            public struct TextureData
            {
                public TextureData() { Sprite = []; Offset = []; Alpha = []; Rotation = []; Comment = ""; }
                /// <summary>
                /// The index pointing to which sprite to display.
                /// </summary>
                public int[] Sprite;
                /// <summary>
                /// The index pointing to which offset/scale to use for the sprite.
                /// </summary>
                public int[] Offset;
                /// <summary>
                /// The transparency of the sprite being displayed.
                /// </summary>
                public int[] Alpha;
                /// <summary>
                /// The angle of the sprite being displayed, written in byte format, and then calculated to get the closest matching angle in degrees.
                /// </summary>
                public int[] Rotation;
                /// <summary>
                /// If available, this variable will contain the comment left after the data.
                /// </summary>
                public string Comment;
            }

            public bool Loaded;
            public int Frame;
            public int FrameCount;
            public int Loop;
            public List<PatternData> Pattern;
            public List<TextureData> Texture;
            public List<PatternData> Layer;

            public AnimeData()
            {
                Pattern = [];
                Texture = [];
                Layer = [];
            }
        }
        public struct TweenData
        {
            public struct TweenKey
            {
                public int Start;
                public int Length;
                public int End { get { return Start + Length; } }
                public int StartIndex;
                public int EndIndex;

                public bool IsWithinTimeframe(int time) { return time >= Start && time <= End; }
            }
            public struct PatternData
            {
                public PatternData() { Sprite = []; Offset = []; }

                public TweenKey[] Sprite;
                public TweenKey[] Offset;
            }
            public struct TextureData
            {
                public TextureData() { Sprite = []; Offset = []; Alpha = []; Rotation = []; }

                public TweenKey[] Sprite;
                public TweenKey[] Offset;
                public TweenKey[] Alpha;
                public TweenKey[] Rotation;
            }

            public List<PatternData> Pattern;
            public List<TextureData> Texture;
            public List<PatternData> Layer;
            
            public TweenData()
            {
                Pattern = [];
                Texture = [];
                Layer = [];
            }
        }
        public struct BitmapData
        {
            public string Path;
            public ImageFileManager ImageFile;
            public ColorKeyType ColorKeyType;
            public Color ColorKey;
            public Vector2D<int> Bounds
            { 
                get
                {
                    if (Loaded)
                        return new Vector2D<int>(ImageFile.Width, ImageFile.Height);
                    else
                        return new Vector2D<int>(0, 0);
                }
            }
            public bool IsBMPFile
            {
                get
                {   
                    return System.IO.Path.GetExtension(Path) == ".bmp";
                }
            }
            public bool Loaded => ImageFile?.Loaded ?? false;
        }

        public string CharName = "";
        public string Artist = "";
        public string CharFile { get; protected set; } = "";
        public BitmapData CharBMP,
            CharBMP2P,
            CharFace,
            CharFace2P,
            SelectCG,
            SelectCG2P,
            CharTex,
            CharTex2P;

        public Rectangle<int>[] RectCollection = [];
        public string[] RectComments = [];

        public AnimeData[] AnimeCollection { get; protected set; } = new AnimeData[(int)AnimationNames.COUNT];
        public TweenData[] TweenCollection { get; protected set; } = new TweenData[(int)AnimationNames.COUNT];

        private int _lineCount = 1;
        public CHPFile(string filename, Encoding? encoding = null)
        {
            Loaded = false;
            Error = "";

            _lineCount = 1;

            try
            {
                if (encoding != null) { FileEncoding = encoding; }
                //FileEncoding = HEncodingDetector.DetectEncoding(filename, Encoding.GetEncoding(932));

                string filedata = File.ReadAllText(filename, FileEncoding);
                FileInformation = new FileInfo(filename);

                filedata = filedata.Replace("\r\n", "\n");
                string[] lines = filedata.Split("\n");
                bool sizeIsSet = false;

                for (int i = 0; i < (int)AnimationNames.COUNT; i++)
                {
                    AnimeCollection[i].Pattern = new List<AnimeData.PatternData>();
                    AnimeCollection[i].Texture = new List<AnimeData.TextureData>();
                    AnimeCollection[i].Layer = new List<AnimeData.PatternData>();
                    TweenCollection[i].Pattern = new List<TweenData.PatternData>();
                    TweenCollection[i].Texture = new List<TweenData.TextureData>();
                    TweenCollection[i].Layer = new List<TweenData.PatternData>();
                }

                foreach (string line in lines)
                {
                    if (!(line.StartsWith('/') || line.StartsWith("//") || string.IsNullOrWhiteSpace(line)))
                    {
                        // parsing time :)
                        bool containsComment = line.IndexOf("//") > -1;
                        string line_trimmed = line.Substring(0, containsComment ? line.IndexOf("//") : line.Length);
                        string[] split = line_trimmed.Split(new char[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (split.Length == 0) continue;

                        switch (split[0].ToLower())
                        {
                            #region Chara name & artist
                            case "#charname":
                                CharName = line_trimmed.Split(new char[] { '\t', ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[1];
                                break;

                            case "#artist":
                                Artist = line_trimmed.Split(new char[] { '\t', ' ' }, 2, StringSplitOptions.RemoveEmptyEntries)[1];
                                break;
                            #endregion
                            #region Bitmaps
                            case "#charbmp":
                                LoadTexture(ref CharBMP, SquashArray(split, 2)[1]);
                                break;

                            case "#charbmp2p":
                                LoadTexture(ref CharBMP2P, SquashArray(split, 2)[1]);
                                break;

                            case "#charface":
                                LoadTexture(ref CharFace, SquashArray(split, 2)[1], ColorKeyType.Manual, 0, 0, 0);
                                break;

                            case "#charface2p":
                                LoadTexture(ref CharFace2P, SquashArray(split, 2)[1], ColorKeyType.Manual, 0, 0, 0);
                                break;

                            case "#selectcg":
                                LoadTexture(ref SelectCG, SquashArray(split, 2)[1], ColorKeyType.None);
                                break;

                            case "#selectcg2p": // Added in beatoraja
                                LoadTexture(ref SelectCG2P, SquashArray(split, 2)[1], ColorKeyType.None);
                                break;

                            case "#chartex":
                                LoadTexture(ref CharTex, SquashArray(split, 2)[1]);
                                break;

                            case "#chartex2p":
                                LoadTexture(ref CharTex2P, SquashArray(split, 2)[1]);
                                break;
                            #endregion
                            #region Chara parameters
                            case "#autocolorset":
                                AutoColorSet = true;
                                break;

                            case "#anime":
                                if (!int.TryParse(split[1], out Anime))
                                    Trace.TraceError($"Failed to parse Anime value. \"{split[1]}\" was not recognized as an integer. Did you write it correctly?");
                                break;

                            case "#size":
                                if (split.Length >= 2)
                                {
                                    if (int.TryParse(split[1], out int size_x)) { Size.Width = size_x; }
                                    else Trace.TraceError($"Failed to parse Size width value. \"{split[1]}\" was not recognized as an integer. Did you write it correctly?");
                                }
                                else { Trace.TraceError("Attempted to find the character's width when reading #Size, but no value was found."); }
                                if (split.Length >= 3)
                                {
                                    if (int.TryParse(split[2], out int size_y)) { Size.Height = size_y; }
                                    else Trace.TraceError($"Failed to parse Size height value. \"{split[2]}\" was not recognized as an integer. Did you write it correctly?");
                                }
                                else { Trace.TraceError("Attempted to find the character's height when reading #Size, but no value was found."); }
                                sizeIsSet = true;
                                break;

                            case "#wait":
                                if (!int.TryParse(split[1], out Wait))
                                    Trace.TraceError($"Failed to parse Wait value. \"{split[1]}\" was not recognized as an integer. Did you write it correctly?");
                                break;

                            case "#data":
                                //if (!sizeIsSet) { Size = new(167, 271); sizeIsSet = true; }
                                if (!int.TryParse(split[1], out Data))
                                    Trace.TraceError($"Failed to parse Data value. \"{split[1]}\" was not recognized as an integer. Did you write it correctly?");
                                RectCollection = new Rectangle<int>[Data * Data];
                                RectComments = new string[Data * Data];

                                for (int i = 0; i < RectCollection.Length; i++)
                                {
                                    RectCollection[i] = new Rectangle<int>(0, 0, 0, 0);
                                    RectComments[i] = "";
                                }
                                IsLegacy = false;
                                break;
                            
                            case "#charfaceallsize": // Added in beatoraja
                                if (split.Length >= 5)
                                {
                                    CharFaceAllSize.Origin.X = int.TryParse(split[1], out int x) ? x : CharFaceAllSize.Origin.X;
                                    CharFaceAllSize.Origin.Y = int.TryParse(split[2], out int y) ? y : CharFaceAllSize.Origin.Y;
                                    CharFaceAllSize.Size.X = int.TryParse(split[3], out int w) ? w : CharFaceAllSize.Size.X;
                                    CharFaceAllSize.Size.Y = int.TryParse(split[4], out int h) ? h : CharFaceAllSize.Size.Y;
                                }
                                else
                                    Trace.TraceWarning($"#CharFaceAllSize could not be properly parsed. Found {split.Length - 1} values instead of 4. Using default values instead.");
                                break;
                            
                            case "#charfaceuppersize": // Added in beatoraja
                                if (split.Length >= 5)
                                {
                                    CharFaceUpperSize.Origin.X = int.TryParse(split[1], out int x) ? x : CharFaceUpperSize.Origin.X;
                                    CharFaceUpperSize.Origin.Y = int.TryParse(split[2], out int y) ? y : CharFaceUpperSize.Origin.Y;
                                    CharFaceUpperSize.Size.X = int.TryParse(split[3], out int w) ? w : CharFaceUpperSize.Size.X;
                                    CharFaceUpperSize.Size.Y = int.TryParse(split[4], out int h) ? h : CharFaceUpperSize.Size.Y;
                                }
                                else
                                    Trace.TraceWarning($"#CharFaceUpperSize could not be properly parsed. Found {split.Length - 1} values instead of 4. Using default values instead.");
                                break;
                            #endregion
                            #region Animation
                            case "#loop":
                                int loop = int.Parse(split[1]) - 1;

                                AnimeCollection[loop].Loop = int.Parse(split[2]);
                                break;

                            case "#flame": // This is the correct command, it's just a misspelling that ended up being final.
                            case "#frame": // Added in beatoraja
                                int flame = int.Parse(split[1]) - 1;
                                AnimeCollection[flame].Frame = int.Parse(split[2]);
                                break;

                            case "#patern": // This is also the correct command, but was once again misspelled.
                            case "#pattern": // Added in beatoraja

                                if (split.Length < 3)
                                {
                                    Trace.TraceError($"{split[0]} was defined on line {_lineCount}, but does not contain any keyframes to parse. Skipping this line.");
                                    break;
                                }

                                int patern = int.Parse(split[1]) - 1;
                                int frame = AnimeCollection[patern].Frame > 0 ? AnimeCollection[patern].Frame : Anime;

                                AnimeCollection[patern].Pattern.Add(new AnimeData.PatternData()
                                {
                                    Sprite = ParseFromHexes(split[2], IsLegacy ? 16 : Data),
                                    Offset = split.Length >= 4 ? ParseFromHexes(split[3], IsLegacy ? 16 : Data) : [],
                                    Comment = containsComment ? line.Substring(line.IndexOf("//") + 2).Trim() : ""
                                } );

                                TweenCollection[patern].Pattern.Add(new TweenData.PatternData() 
                                {
                                    Sprite = ParseFromInts(AnimeCollection[patern].Pattern.Last().Sprite, frame),
                                    Offset = ParseFromInts(AnimeCollection[patern].Pattern.Last().Offset, frame)
                                });

                                AnimeCollection[patern].Loaded = true;
                                break;

                            case "#texture":
                                if (split.Length < 3)
                                {
                                    Trace.TraceError($"{split[0]} was defined on line {_lineCount}, but does not contain any keyframes to parse. Skipping this line.");
                                    break;
                                }

                                int texture = int.Parse(split[1]) - 1;
                                int texframe = AnimeCollection[texture].Frame > 0 ? AnimeCollection[texture].Frame : Anime;

                                AnimeCollection[texture].Texture.Add(new AnimeData.TextureData()
                                {
                                    Sprite = ParseFromHexes(split[2], IsLegacy ? 16 : Data),
                                    Offset = split.Length > 3 ? ParseFromHexes(split[3], IsLegacy ? 16 : Data) : [],
                                    Alpha = split.Length > 4 ? ParseFromHexes(split[4]) : [],
                                    Rotation = split.Length > 5 ? ParseFromHexes(split[5]) : [],
                                    Comment = containsComment ? line.Substring(line.IndexOf("//") + 2).Trim() : ""
                                });

                                TweenCollection[texture].Texture.Add(new TweenData.TextureData()
                                {
                                    Sprite = ParseFromInts(AnimeCollection[texture].Texture.Last().Sprite, texframe),
                                    Offset = ParseFromInts(AnimeCollection[texture].Texture.Last().Offset, texframe),
                                    Alpha = ParseFromInts(AnimeCollection[texture].Texture.Last().Alpha, texframe),
                                    Rotation = ParseFromInts(AnimeCollection[texture].Texture.Last().Rotation, texframe)
                                });

                                AnimeCollection[texture].Loaded = true;
                                break;

                            case "#layer":
                                if (split.Length < 3)
                                {
                                    Trace.TraceError($"{split[0]} was defined on line {_lineCount}, but does not contain any keyframes to parse. Skipping this line.");
                                    break;
                                }

                                int layer = int.Parse(split[1]) - 1;
                                int layframe = AnimeCollection[layer].Frame > 0 ? AnimeCollection[layer].Frame : Anime;

                                AnimeCollection[layer].Layer.Add(new AnimeData.PatternData()
                                {
                                    Sprite = ParseFromHexes(split[2], IsLegacy ? 16 : Data),
                                    Offset = split.Length > 3 ? ParseFromHexes(split[3], IsLegacy ? 16 : Data) : [],
                                    Comment = containsComment ? line.Substring(line.IndexOf("//") + 2).Trim() : ""
                                });

                                TweenCollection[layer].Layer.Add(new TweenData.PatternData()
                                {
                                    Sprite = ParseFromInts(AnimeCollection[layer].Layer.Last().Sprite, layframe),
                                    Offset = ParseFromInts(AnimeCollection[layer].Layer.Last().Offset, layframe)
                                });

                                AnimeCollection[layer].Loaded = true;
                                break;
                            #endregion
                            default: // Remember that #00 and #01 are strictly reserved for Name Logo & character background respectively
                                if (RectCollection.Length == 0)
                                {
                                    RectCollection = new Rectangle<int>[Data * Data];
                                    RectComments = new string[Data * Data];
                                    
                                    for (int i = 0; i < RectCollection.Length; i++)
                                    {
                                        RectCollection[i] = new Rectangle<int>(0,0,0,0);
                                        RectComments[i] = "";
                                    }
                                }

                                if (IsLegacy && TryParseFromHex(split[0].Substring(1, Math.Clamp(split[0].Length - 1, 0, 2)), 10, out int number))
                                {
                                    if (split.Length >= 2)
                                        RectCollection[number].Origin.X = int.TryParse(split[1], out int x) ? x : 0;
                                    if (split.Length >= 3)
                                        RectCollection[number].Origin.Y = int.TryParse(split[2], out int y) ? y : 0;
                                    if (split.Length >= 4)
                                        RectCollection[number].Size.X = int.TryParse(split[3], out int w) ? w : 0;
                                    if (split.Length >= 5)
                                        RectCollection[number].Size.Y = int.TryParse(split[4], out int h) ? h : 0;

                                    if (split.Length < 5)
                                        Trace.TraceWarning($"#{split[0].Substring(1, 2)} at line {_lineCount} does not contain a full rect. Only {split.Length - 1} out of 4 values were found.");

                                    if (containsComment)
                                        RectComments[number] = line.Substring(line.IndexOf("//") + 2).Trim();
                                }
                                else if (TryParseFromHex(split[0].Substring(1, Math.Clamp(split[0].Length - 1, 0, 2)), Data, out int number_from_hex))
                                {
                                    if (split.Length >= 2)
                                        RectCollection[number_from_hex].Origin.X = int.TryParse(split[1], out int x) ? x : 0;
                                    if (split.Length >= 3)
                                        RectCollection[number_from_hex].Origin.Y = int.TryParse(split[2], out int y) ? y : 0;
                                    if (split.Length >= 4)
                                        RectCollection[number_from_hex].Size.X = int.TryParse(split[3], out int w) ? w : 0;
                                    if (split.Length >= 5)
                                        RectCollection[number_from_hex].Size.Y = int.TryParse(split[4], out int h) ? h : 0;

                                    if (split.Length < 5)
                                        Trace.TraceWarning($"#{split[0].Substring(1, 2)} at line {_lineCount} does not contain a full rect. Only {split.Length - 1} out of 4 values were found.");

                                    if (containsComment)
                                        RectComments[number_from_hex] = line.Substring(line.IndexOf("//") + 2).Trim();
                                }
                                break;
                        }
                    }
                    _lineCount++;
                }
                for (int i = 0; i < AnimeCollection.Length; i++)
                {
                    List<int> all_lengths = new List<int>();

                    foreach (AnimeData.PatternData pattern in AnimeCollection[i].Pattern)
                    {
                        all_lengths.Add(pattern.Sprite.Length);
                        if (pattern.Offset.Length > 0) all_lengths.Add(pattern.Offset.Length);
                    }
                    foreach (AnimeData.TextureData tex in AnimeCollection[i].Texture)
                    {
                        all_lengths.Add(tex.Sprite.Length);
                        if (tex.Offset.Length > 0) all_lengths.Add(tex.Offset.Length);
                        if (tex.Alpha.Length > 0) all_lengths.Add(tex.Alpha.Length);
                        if (tex.Offset.Length > 0) all_lengths.Add(tex.Offset.Length);
                    }
                    foreach (AnimeData.PatternData layer in AnimeCollection[i].Layer)
                    {
                        all_lengths.Add(layer.Sprite.Length);
                        if (layer.Offset.Length > 0) all_lengths.Add(layer.Offset.Length);
                    }

                    AnimeCollection[i].FrameCount = all_lengths.Count > 0 ? all_lengths.Min() : 0;

                    if (all_lengths.Distinct().Count() > 1)
                        Trace.TraceWarning("Animation #" + (i+1) + " contains differing amounts of frames.\n" +
                            "This may break applications that try to display Pomyu Charas.\n" +
                            "Only the minimum amount of frames (" + all_lengths.Min() + ") will be displayed here.\n" +
                            "Frame Counts: " + (string.Join(",", all_lengths.Select(len => len.ToString()).ToArray())));
                }
                Loaded = true;
                Trace.TraceInformation("Pomyu Chara loaded! (Legacy: " + IsLegacy + ")");
                Trace.TraceInformation("File Name: " + FileName);
                Trace.TraceInformation("Chara Name: " + CharName);
                Trace.TraceInformation("Artist: " + Artist);
            }
            catch (FileNotFoundException e)
            {
                string err = "Couldn't find the requested file. More details:" + Environment.NewLine + e;
                Trace.TraceError(err);
                Error = err;
            }
            catch (Exception e)
            {
                string err = "Something went wrong while trying to read the requested CHP file at line " + _lineCount + ". More details:" + Environment.NewLine + e;
                Trace.TraceError(err);
                Error = err;
            }
        }

        #region Parsing Methods
        private unsafe void LoadTexture(ref BitmapData data, string filepath, ColorKeyType colorKey = ColorKeyType.Auto, byte r = 0x00, byte g = 0x00, byte b = 0x00, byte a = 0xFF)
        {
            data.Path = filepath;
            data.ImageFile = new ImageFileManager(GetPath(data.Path), true);

            if (data.IsBMPFile && CHPEditor.Config.IgnoreBitmapAlpha && data.Loaded) {
                byte[] pixels = data.ImageFile.Image.Data;
                for (int i = 3; i < pixels.Length; i += 4)
                {
                    pixels[i] = 255;
                }
                data.ImageFile.UpdateImage(pixels);
            }

            data.ColorKeyType = colorKey;

            if (colorKey == ColorKeyType.Auto && data.ImageFile.Loaded)
            {
                int offset = ((data.ImageFile.Image.Width * data.ImageFile.Image.Height) - 1) * 4;
                data.ColorKey = Color.FromArgb(data.ImageFile.Image.Data[offset + 3], data.ImageFile.Image.Data[offset], data.ImageFile.Image.Data[offset + 1], data.ImageFile.Image.Data[offset + 2]);
            }
            else if (colorKey == ColorKeyType.Manual && data.ImageFile.Loaded)
            {
                data.ColorKey = Color.FromArgb(a,r,g,b);
            }
            else
            {
                data.ColorKey = Color.FromArgb(0x00,0x00,0x00,0x00);
            }
        }

        private string GetPath(string path)
        {
            return Path.Combine(FolderPath, path);
        }

        private string[] SquashArray(string[] array, int size, char joiner = ' ')
        {
            for (int i = size; i < array.Length; i++)
            {
                array[size - 1] += joiner + array[i];
            }
            return array;
        }

        private int ParseFromHex(string hex, int baseSize = 16)
        {
            if (baseSize > 36) { throw new ArgumentOutOfRangeException($"CHPEditor does not support a Base size greater than 36 for CHP files. Consider setting #Data to 16 or 36. If you need more than 1296 rects, consider reducing your rect count. (Base size given was {baseSize}.)"); }

            const string base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            //const string base62 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"; // Unsure if supporting this is necessary at all. Leaving this here just in case.

            int value = 0;
            hex = hex.ToUpper();

            for (int i = 0; i < hex.Length; i++)
            {
                char c = hex[(hex.Length - 1) - i];
                int result = base36.IndexOf(c);

                if (result < 0)
                    throw new InvalidOperationException($"Invalid character received while converting hex to integer. (Could not read '{c}'.)");
                if (result >= baseSize)
                    throw new ArgumentOutOfRangeException($"Expected the value of '{c}' to be less than Base range of {baseSize}, but got {result} instead.");

                value += result * (int)Math.Pow(baseSize, i);
            }

            return value;
        }
        private int[] ParseFromHexes(string hex, int baseSize = 16, int length = 2)
        {
            int count = hex.Length / length;
            int[] result = new int[count];

            for (int i = 0; i < count; i++)
            {
                string item = hex.Substring(i * length, length);

                if (item == "--")
                    result[i] = -1;
                else
                    try
                    {
                        result[i] = ParseFromHex(item, baseSize);
                    }
                    catch (Exception e)
                    {
                        if (i == 0) { throw new InvalidOperationException($"The first frame's hex value '{item}' on line {_lineCount} is invalid, and has no previous frame to fallback to."); }
                        Trace.TraceWarning($"Failed to parse frame {i}'s hex '{item}' on line {_lineCount}. Falling back to previous frame's hex '{hex.Substring((i-1) * length, length)}'. Details: {e.Message}");
                        result[i] = result[i - 1];
                    }
            }

            return result;
        }
        private TweenData.TweenKey[] ParseFromInts(int[] array, int frame)
        {
            if (array.Length == 0) return [];

            var keys = new List<TweenData.TweenKey>();
            var key = new TweenData.TweenKey();

            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == -1)
                    key.Length += frame;

                if ((i > 0) && (i + 1 < array.Length)) // Balance out single keyframe between two tweens
                {
                    if (array[i] != -1 && array[i - 1] == -1 && array[i + 1] == -1)
                    {
                        key.Length -= frame;
                    }
                }
                if (i > 0) // Mark end of tween key
                {
                    if (array[i] != -1 && array[i - 1] == -1)
                    {
                        key.Length += frame;
                        key.EndIndex = array[i];
                        keys.Add(key);
                    }
                }
                if (i + 1 < array.Length) // Mark start of tween key
                {
                    if (array[i] != -1 && array[i + 1] == -1)
                    {
                        key = new TweenData.TweenKey()
                        {
                            Start = frame * i,
                            Length = frame,
                            StartIndex = array[i]
                        };
                    }
                }
            }

            return keys.ToArray();
        }
        private bool TryParseFromHex(string hex, int baseSize, out int result)
        {
            try { result = ParseFromHex(hex, baseSize); return true; }
            catch { result = 0; return false; }
        }
        #endregion
        #region Draw Methods
        public unsafe void Draw(ref BitmapData bitmap)
        {
            if (!bitmap.Loaded) return;
            var rect = new Rectangle<int>(0, 0, bitmap.ImageFile.Image.Width, bitmap.ImageFile.Image.Height);
            Draw(ref bitmap, rect, rect);
        }
        public unsafe void Draw(ref BitmapData bitmap, Rectangle<int> rect, Rectangle<int> offset)
        {
            Draw(ref bitmap, rect, offset, 0.0, 1.0f);
        }
        public unsafe void Draw(ref BitmapData bitmap, Rectangle<int> rect, Rectangle<int> offset, double rot, float alpha)
        {
            if (!bitmap.Loaded) return;
            if ((AutoColorSet && bitmap.ColorKeyType == ColorKeyType.Auto) ||
                bitmap.ColorKeyType == ColorKeyType.Manual)
                bitmap.ImageFile.Draw(rect, offset, rot, alpha, bitmap.ColorKey.R / 255.0f, bitmap.ColorKey.G / 255.0f, bitmap.ColorKey.B / 255.0f, bitmap.ColorKey.A / 255.0f);
            else if (!AutoColorSet && bitmap.ColorKeyType == ColorKeyType.Auto) // If a typically color keyed bitmap is not automatically color keyed, assume its color key is pure black.
                bitmap.ImageFile.Draw(rect, offset, rot, alpha, 0, 0, 0, 1);
            else
                bitmap.ImageFile.Draw(rect, offset, rot, alpha);
        }
        #endregion

        public static double GetRotation(int value) { return ((double)value / 256) * 360.0; }

        #region Dispose
        private bool isDisposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    Loaded = false;
                    Error = "";

                    CharName = "";
                    Artist = "";
                    CharFile = "";
                    RectCollection = [];
                    RectComments = [];
                    AnimeCollection = [];
                    TweenCollection = [];
                }

                CharBMP.ImageFile?.Dispose();
                CharBMP2P.ImageFile?.Dispose();
                CharFace.ImageFile?.Dispose();
                CharFace2P.ImageFile?.Dispose();
                SelectCG.ImageFile?.Dispose();
                SelectCG2P.ImageFile?.Dispose();
                CharTex.ImageFile?.Dispose();
                CharTex2P.ImageFile?.Dispose();

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
