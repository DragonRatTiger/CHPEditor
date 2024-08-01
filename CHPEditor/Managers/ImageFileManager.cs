using System;
using System.Diagnostics;
using System.IO;
using StbImageSharp;

namespace CHPEditor
{
    internal class ImageFileManager : ImageManager
    {
        /// <summary>
        /// The path that is requested when first initializing. Actual path may differ if initialized with case-insensitivity.<br/>
        /// Use <see cref="Path"/> to get the actual path that's loaded.
        /// </summary>
        public string RequestedPath { get; private set; }
        /// <summary>
        /// If initialized path is case-insensitive, the name of the loaded file will be shown here.<br/>
        /// Otherwise, this value will return <see cref="RequestedPath"/>.
        /// </summary>
        public string Path
        { 
            get
            {
                if (CaseInsensitive)
                {
                    return _path;
                }
                else
                {
                    return RequestedPath;
                }
            } 
            private set
            {
                _path = value;
            }
        }
        public bool CaseInsensitive { get; private set; }

        public ImageFileManager(string path, bool caseInsensitive = false) : base()
        {
            RequestedPath = path;
            CaseInsensitive = caseInsensitive;
            _path = path;
            try
            {
                if (caseInsensitive)
                {
                    string[] paths = Directory.GetFiles(System.IO.Path.GetDirectoryName(path));
                    foreach (string single_path in paths)
                    {
                        if (path.Equals(single_path, StringComparison.OrdinalIgnoreCase))
                            Path = single_path;
                    }
                    if (!File.Exists(Path))
                        throw new FileNotFoundException("Path does not exist.", Path);
                    LoadImage(ImageResult.FromMemory(File.ReadAllBytes(Path), ColorComponents.RedGreenBlueAlpha));
                }
                else
                {
                    if (!File.Exists(RequestedPath))
                        throw new FileNotFoundException("Path does not exist.", Path);
                    LoadImage(ImageResult.FromMemory(File.ReadAllBytes(RequestedPath), ColorComponents.RedGreenBlueAlpha));
                }
            }
            catch (FileNotFoundException e)
            {
                Trace.TraceError(Path + " could not be found. (Load option: " + (caseInsensitive ? "Case-insensitive" : "Case-sensitive") + ") More details: " + e);
            }
            catch (Exception e)
            {
                Trace.TraceError("Could not load image file located at " + RequestedPath + ". (Load option: " + (caseInsensitive ? "Case-insensitive" : "Case-sensitive") + ") More details: " + e);
            }
        }

        private string _path;

        protected override void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                if (disposing)
                {
                    RequestedPath = String.Empty;
                    _path = String.Empty;
                    CaseInsensitive = false;
                }

                base.Dispose(disposing);
            }
        }
    }
}
