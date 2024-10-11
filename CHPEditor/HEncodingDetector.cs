using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CHPEditor
{
    internal class HEncodingDetector
    {
        public static EncodingInfo[] Encodings = Encoding.GetEncodings();
        //public static Encoding DetectEncoding(string filename, Encoding fallback_encoding)
        //{
        //    return DetectEncoding(File.OpenRead(filename), fallback_encoding);
        //}
        //public static Encoding DetectEncoding(FileStream stream, Encoding fallback_encoding)
        //{
        //    // A replacement will need to be written in the future.
        //
        //    return FileEncoding;
        //}
    }
}
