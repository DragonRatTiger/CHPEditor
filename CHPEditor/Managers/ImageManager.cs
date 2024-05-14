using System;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace CHPEditor
{
    internal class ImageManager : IDisposable
    {
        public bool Loaded = false;

        public ImageResult Image { get; private set; }
        public uint Pointer { get; private set; }

        public ImageManager()
        {
            Loaded = false;
            Image = new ImageResult();
            Pointer = 0;
        }
        public unsafe ImageManager(byte[] pixels, int width, int height, ColorComponents comp = ColorComponents.RedGreenBlueAlpha, ColorComponents source_comp = ColorComponents.RedGreenBlueAlpha, bool loadOnInit = true) : base()
        {
            Image = new ImageResult() { Data = pixels, Width = width, Height = height, Comp = comp, SourceComp = source_comp };

            if (loadOnInit)
                LoadImage();
        }
        public ImageManager(ImageResult image, bool loadOnInit = true) : base()
        {
            Image = image;

            if (loadOnInit)
                LoadImage();
        }
        public void LoadImage(ImageResult image)
        {
            Image = image;
            LoadImage();
        }
        public unsafe void LoadImage()
        {
            if (Pointer == 0)
                Pointer = CHPEditor._gl.GenTexture();

            CHPEditor._gl.ActiveTexture(TextureUnit.Texture0);
            CHPEditor._gl.BindTexture(TextureTarget.Texture2D, Pointer);

            CHPEditor._gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapS, (int)TextureWrapMode.Repeat);
            CHPEditor._gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureWrapT, (int)TextureWrapMode.Repeat);
            CHPEditor._gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)TextureMinFilter.Nearest);
            CHPEditor._gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)TextureMagFilter.Nearest);
            fixed (byte* ptr = Image.Data)
                CHPEditor._gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)Image.Width, (uint)Image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            CHPEditor._gl.BindTexture(TextureTarget.Texture2D, 0);

            Loaded = true;
        }

        protected bool _isDisposed;
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    Image.Data = null;
                    Image.Height = 0;
                    Image.Width = 0;
                    Image.Comp = 0;
                    Image.SourceComp = 0;
                    Loaded = false;
                }

                if (Pointer != 0)
                {
                    CHPEditor._gl.DeleteTexture(Pointer);
                    Pointer = 0;
                }

                _isDisposed = true;
            }
        }
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
