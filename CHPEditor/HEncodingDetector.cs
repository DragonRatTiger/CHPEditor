using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHPEditor
{
    internal class HEncodingDetector
    {
        public static Encoding DetectEncoding(string filename, Encoding fallback_encoding)
        {
            return DetectEncoding(File.OpenRead(filename), fallback_encoding);
        }
        public static Encoding DetectEncoding(FileStream stream, Encoding fallback_encoding)
        {
            Encoding FileEncoding = fallback_encoding;

            using (stream)
            {
                Ude.CharsetDetector cdet = new Ude.CharsetDetector();
                cdet.Feed(stream);
                cdet.DataEnd();
                if (cdet.Charset != null)
                {
                    EncodingInfo[] encodings = Encoding.GetEncodings();
                    foreach (EncodingInfo encodinginfo in encodings)
                    {
                        if (encodinginfo.DisplayName.Contains(cdet.Charset, StringComparison.InvariantCultureIgnoreCase))
                            FileEncoding = encodinginfo.GetEncoding();
                    }
                }
                else
                {
                    Trace.TraceWarning("File encoding for " + Path.GetFileName(stream.Name) + " could not be found. Defaulting to " + fallback_encoding.EncodingName + ".");
                }
            }

            return FileEncoding;
        }
    }
}
