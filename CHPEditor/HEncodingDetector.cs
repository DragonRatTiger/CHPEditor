using System.Text;

namespace CHPEditor
{
    internal class HEncodingDetector
    {
        public static Encoding[] Encodings { get; private set; } = [];

        public static void InitializeEncodings()
        {
            Encodings = [ 
                Encoding.GetEncoding(932),      // Shift-JIS
                Encoding.GetEncoding(65001),    // UTF-8
                Encoding.GetEncoding(20127),    // US-ASCII
                Encoding.GetEncoding(28591),    // ISO-8859-1
                Encoding.GetEncoding(950),      // Big5
                ];
        }

        // DO NOT REMOVE THESE COMMENTS! This code will be reworked in the future.

        //public static Encoding DetectEncoding(string filename, Encoding fallback_encoding)
        //{
        //    return DetectEncoding(File.OpenRead(filename), fallback_encoding);
        //}
        //public static Encoding DetectEncoding(FileStream stream, Encoding fallback_encoding)
        //{
        //    return FileEncoding;
        //}
    }
}
