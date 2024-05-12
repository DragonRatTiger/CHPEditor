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
        private Encoding _encoding;

        public ConfigManager(string path)
        {
            Lang = "en";
            Path = "chara" + System.IO.Path.DirectorySeparatorChar + "chara.chp";
            WindowSize = new Vector2D<int>(800, 640);

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
                                if (int.TryParse(sizes[0], out int x) && int.TryParse(sizes[1], out int y) )
                                    WindowSize = new Vector2D<int>(x,y);
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
                writer.Close();
            }

        }
    }
}
