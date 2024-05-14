using Silk.NET.Maths;
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
        public bool LogFileIsTimestamped;

        private Encoding _encoding;

        public ConfigManager(string path)
        {
            Lang = "en";
            Path = "chara" + System.IO.Path.DirectorySeparatorChar + "chara.chp";
            WindowSize = new Vector2D<int>(800, 640);
            WindowPos = new Vector2D<int>(50, 50);
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
                        case "LogFileIsTimestamped":
                            if (bool.TryParse(split[1], out bool result))
                                LogFileIsTimestamped = result;
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
                writer.WriteLine("LogFileIsTimestamped=" + LogFileIsTimestamped.ToString());
                writer.Close();
            }

        }
    }
}
