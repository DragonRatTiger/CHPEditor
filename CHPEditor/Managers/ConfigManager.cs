using Silk.NET.Maths;
using System.Drawing;
using System.IO;
using System.Text;

namespace CHPEditor
{
    internal class ConfigManager
    {
        public string Lang;
        public string Path;
        public Vector2D<int> WindowSize;
        public Vector2D<int> WindowPos;
        public Size NameSize;
        public Size BackgroundSize;
        public bool UseDataSizeForName;
        public bool UseCharaSizeForBackground;

        public bool LogFileIsTimestamped;

        private Encoding _encoding;

        public ConfigManager(string path)
        {
            Lang = "en";
            Path = "chara" + System.IO.Path.DirectorySeparatorChar + "chara.chp";
            WindowSize = new Vector2D<int>(800, 640);
            WindowPos = new Vector2D<int>(50, 50);
            NameSize = new Size(131, 29);
            BackgroundSize = new Size(167, 271);
            UseDataSizeForName = false;
            UseCharaSizeForBackground = false;
            
            LogFileIsTimestamped = false;

            if (File.Exists(path))
            {
                _encoding = HEncodingDetector.DetectEncoding(path, Encoding.ASCII);
                string[] lines = File.ReadAllLines(path, _encoding);
                foreach (string line in lines)
                {
                    string[] split = line.Split("=");
                    switch (split[0])
                    {
                        case "Lang":
                            Lang = split[1];
                            break;
                        case "Path":
                            Path = split[1];
                            break;
                        case "WindowSize":
                            string[] sizes = split[1].Split(',');
                            if (sizes.Length == 2)
                                if (int.TryParse(sizes[0], out int x) && int.TryParse(sizes[1], out int y))
                                    WindowSize = new Vector2D<int>(x, y);
                            break;
                        case "WindowPos":
                            string[] pos = split[1].Split(',');
                            if (pos.Length == 2)
                                if (int.TryParse(pos[0], out int x) && int.TryParse(pos[1], out int y))
                                    WindowPos = new Vector2D<int>(x, y);
                            break;
                        case "NameSize":
                            string[] namesize = split[1].Split(',');
                            if (namesize.Length == 2)
                                if (int.TryParse(namesize[0], out int x) && int.TryParse(namesize[1], out int y))
                                    NameSize = new Size(x, y);
                            break;
                        case "BackgroundSize":
                            string[] backgroundsize = split[1].Split(',');
                            if (backgroundsize.Length == 2)
                                if (int.TryParse(backgroundsize[0], out int x) && int.TryParse(backgroundsize[1], out int y))
                                    BackgroundSize = new Size(x, y);
                            break;
                        case "UseDataSizeForName":
                            if (int.TryParse(split[1], out int usenamesize))
                                UseDataSizeForName = IntToBool(usenamesize);
                            break;
                        case "UseCharaSizeForBackground":
                            if (int.TryParse(split[1], out int usecharasize))
                                UseCharaSizeForBackground = IntToBool(usecharasize);
                            break;
                        case "LogFileIsTimestamped":
                            if (int.TryParse(split[1], out int result))
                                LogFileIsTimestamped = IntToBool(result);
                            break;
                    }
                }
            }
            else
            {
                _encoding = Encoding.ASCII;
                SaveConfig(path);
            }
        }
        public void SaveConfig(string path)
        {
            using (StreamWriter writer = new StreamWriter(path, false, _encoding))
            {
                writer.WriteLine("Lang=" + Lang);
                writer.WriteLine("Path=" + Path);
                writer.WriteLine("WindowSize=" + WindowSize.X + "," + WindowSize.Y);
                writer.WriteLine("WindowPos=" + WindowPos.X + "," + WindowPos.Y);
                writer.WriteLine("NameSize=" + NameSize.Width + "," + NameSize.Height);
                writer.WriteLine("BackgroundSize=" + BackgroundSize.Width + "," + BackgroundSize.Height);
                writer.WriteLine("UseDataSizeForName=" + BoolToInt(UseDataSizeForName));
                writer.WriteLine("UseCharaSizeForBackground=" + BoolToInt(UseCharaSizeForBackground));
                writer.WriteLine("LogFileIsTimestamped=" + BoolToInt(LogFileIsTimestamped));
                writer.Close();
            }

        }
        private bool IntToBool(int value) { return value != 0; }
        private int BoolToInt(bool value) { return value ? 1 : 0; }
    }
}
