using Silk.NET.Maths;
using System;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Text;

namespace CHPEditor
{
    internal class ConfigManager
    {
        public string Lang = "en";
        public string Path = System.IO.Path.Combine("chara","chara.chp");
        public Vector2D<int> WindowSize = new(960, 540);
        public Vector2D<int> WindowPos = new(50,50);
        public Vector4 WindowColor = new(0.28f, 0.4f, 0.52f, 1);
        public string SkinName = "Default";

        // Experimental
        public Size NameSize = new(131, 29);
        public Size BackgroundSize = new(167, 271);
        public Size DanceSize = new(314, 480);
        public bool UseDataSizeForName = true;
        public bool UseCharaSizeForBackground = false;
        public bool IgnoreBitmapAlpha = true;
        public bool DoNotDrawOnePixelSprites = true;

        public bool LogFileIsTimestamped = false;

        public ConfigManager(string path)
        {
            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                foreach (string line in lines)
                {
                    if (line.StartsWith("//")) continue;
                    string[] split = line.Split("=");
                    switch (split[0])
                    {
                        case nameof(Lang):
                            Lang = split[1];
                            break;
                        case nameof(Path):
                            Path = split[1];
                            break;
                        case nameof(WindowSize):
                            string[] sizes = split[1].Split(',');
                            if (sizes.Length == 2)
                                if (int.TryParse(sizes[0], out int x) && int.TryParse(sizes[1], out int y))
                                    WindowSize = new Vector2D<int>(x, y);
                            break;
                        case nameof(WindowPos):
                            string[] pos = split[1].Split(',');
                            if (pos.Length == 2)
                                if (int.TryParse(pos[0], out int x) && int.TryParse(pos[1], out int y))
                                    WindowPos = new Vector2D<int>(x, y);
                            break;
                        case nameof(WindowColor):
                            string[] colors = split[1].Split(',');
                            if (colors.Length >= 3)
                            {
                                WindowColor.X = ByteToFloat((byte)(int.TryParse(colors[0], out int color1) ? (byte)color1 : 0));
                                WindowColor.Y = ByteToFloat((byte)(int.TryParse(colors[1], out int color2) ? (byte)color2 : 0));
                                WindowColor.Z = ByteToFloat((byte)(int.TryParse(colors[2], out int color3) ? (byte)color3 : 0));
                                WindowColor.W = 1;
                            }
                            break;
                        case nameof(NameSize):
                            string[] namesize = split[1].Split(',');
                            if (namesize.Length == 2)
                                if (int.TryParse(namesize[0], out int x) && int.TryParse(namesize[1], out int y))
                                    NameSize = new Size(x, y);
                            break;
                        case nameof(BackgroundSize):
                            string[] backgroundsize = split[1].Split(',');
                            if (backgroundsize.Length == 2)
                                if (int.TryParse(backgroundsize[0], out int x) && int.TryParse(backgroundsize[1], out int y))
                                    BackgroundSize = new Size(x, y);
                            break;
                        case nameof(UseDataSizeForName):
                            if (int.TryParse(split[1], out int usenamesize))
                                UseDataSizeForName = IntToBool(usenamesize);
                            break;
                        case nameof(UseCharaSizeForBackground):
                            if (int.TryParse(split[1], out int usecharasize))
                                UseCharaSizeForBackground = IntToBool(usecharasize);
                            break;
                        case nameof(IgnoreBitmapAlpha):
                            if (int.TryParse(split[1], out int ignorebmpalpha))
                                IgnoreBitmapAlpha = IntToBool(ignorebmpalpha);
                            break;
                        case nameof(DoNotDrawOnePixelSprites):
                            if (int.TryParse(split[1], out int donotdrawonepixelsprites))
                                DoNotDrawOnePixelSprites = IntToBool(donotdrawonepixelsprites);
                            break;
                        case nameof(LogFileIsTimestamped):
                            if (int.TryParse(split[1], out int result))
                                LogFileIsTimestamped = IntToBool(result);
                            break;
                    }
                }
            }
            else
            {
                SaveConfig(path);
            }
        }
        public void SaveConfig(string path)
        {
            using (StreamWriter writer = new StreamWriter(path, false, Encoding.UTF8))
            {
                writer.WriteLine("Lang=" + Lang);
                writer.WriteLine("Path=" + Path);
                writer.WriteLine("WindowSize=" + WindowSize.X + "," + WindowSize.Y);
                writer.WriteLine("WindowPos=" + WindowPos.X + "," + WindowPos.Y);
                writer.WriteLine($"WindowColor={FloatToByte(WindowColor.X)},{FloatToByte(WindowColor.Y)},{FloatToByte(WindowColor.Z)}");
                writer.WriteLine("\n// Experimental Settings\n" +
                    "// Some settings are here to further study how Pomyu Charas are meant to be handled.\n" +
                    "// Some characters may not function as expected between CHPEditor & FeelingPomu2nd,\n" +
                    "// so these are added to try and reduce the differences between the two applications.");
                writer.WriteLine("NameSize=" + NameSize.Width + "," + NameSize.Height);
                writer.WriteLine("BackgroundSize=" + BackgroundSize.Width + "," + BackgroundSize.Height);
                writer.WriteLine("UseDataSizeForName=" + BoolToInt(UseDataSizeForName));
                writer.WriteLine("UseCharaSizeForBackground=" + BoolToInt(UseCharaSizeForBackground));
                writer.WriteLine("IgnoreBitmapAlpha=" + BoolToInt(IgnoreBitmapAlpha));
                writer.WriteLine("DoNotDrawOnePixelSprites=" + BoolToInt(DoNotDrawOnePixelSprites));
                writer.WriteLine("\n// Misc.");
                writer.WriteLine("LogFileIsTimestamped=" + BoolToInt(LogFileIsTimestamped));
                writer.Close();
            }

        }
        private bool IntToBool(int value) { return value != 0; }
        private int BoolToInt(bool value) { return value ? 1 : 0; }
        private byte FloatToByte(float value) { return (byte)(value * 255); }
        private float ByteToFloat(byte value) { return (float)value / 255; }
    }
}
