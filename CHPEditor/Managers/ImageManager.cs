using System;
using System.Drawing;
using System.IO.Compression;
using Silk.NET.Maths;
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

        public unsafe void Draw(Rectangle<int> rect, Rectangle<int> offset, double rot, float alpha, float r = 0.0f, float g = 0.0f, float b = 0.0f, float a = 0.0f)
        {
            float RectX = (float)rect.Origin.X / Image.Width;
            float RectY = (float)rect.Origin.Y / Image.Height;
            float RectW = (float)rect.Size.X / Image.Width;
            float RectH = (float)rect.Size.Y / Image.Height;
            float OffX = (float)offset.Origin.X / 100.0f;
            float OffY = (float)offset.Origin.Y / 100.0f;
            float OffW = (float)offset.Size.X / 100.0f;
            float OffH = (float)offset.Size.Y / 100.0f;

            // Fix non-uniform viewports creating warped rotations
            float viewportX = 100.0f / CHPEditor._window.FramebufferSize.X;
            float viewportY = 100.0f / CHPEditor._window.FramebufferSize.Y;

            OffX *= 2;
            OffY *= 2;
            OffW *= 2;
            OffH *= 2;

            CHPEditor._gl.BindVertexArray(CHPEditor._vao);
            CHPEditor._gl.UseProgram(CHPEditor._program);

            CHPEditor._gl.ActiveTexture(TextureUnit.Texture0);
            CHPEditor._gl.BindTexture(TextureTarget.Texture2D, Pointer);

            CHPEditor._gl.BindBuffer(BufferTargetARB.ArrayBuffer, CHPEditor._vbo);

            float[] vertices =
            {    /* X */     /* Y */       /* U */        /* V */
                 OffW + OffX,        -OffY, RectW + RectX,         RectY, //top-right
                 OffW + OffX, -OffH - OffY, RectW + RectX, RectH + RectY, //bottom-right
                      + OffX, -OffH - OffY,  0.0f + RectX, RectH + RectY, //bottom-left
                      + OffX,        -OffY,  0.0f + RectX,         RectY  //top-left
            };

            if (rot != 0.0f)
            {
                double[] center = new double[2] { (vertices[12] + vertices[4]) / 2.0f, (vertices[13] + vertices[5]) / 2.0f };
                rot = rot * Math.PI / 180.0;
                for (int i = 0; i < vertices.Length; i += 4)
                {
                    double sin = Math.Sin(rot);
                    double cos = Math.Cos(rot);

                    vertices[i] -= (float)center[0];
                    vertices[i + 1] -= (float)center[1];

                    float x = (float)((vertices[i] * cos) - (vertices[i + 1] * sin));
                    float y = (float)((vertices[i] * sin) + (vertices[i + 1] * cos));

                    vertices[i] = x + (float)center[0];
                    vertices[i + 1] = y + (float)center[1];
                }
            }

            // Fix non-uniform viewports causing warped rotations
            for (int i = 0; i < vertices.Length; i += 4)
            {
                vertices[i] = (vertices[i] * viewportX) - 1.0f;
                vertices[i + 1] = (vertices[i + 1] * viewportY) + 1.0f;
            }

            fixed (float* buffer = vertices)
                CHPEditor._gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0, (nuint)vertices.Length * sizeof(float), buffer);

            CHPEditor._gl.Uniform1(CHPEditor.tex_loc, 0);
            CHPEditor._gl.Uniform1(CHPEditor.alpha_loc, alpha);
            CHPEditor._gl.Uniform4(CHPEditor.key_loc, r, g, b, a);

            CHPEditor._gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);

            CHPEditor._gl.Uniform1(CHPEditor.alpha_loc, 1.0f);
        }

        #region Dispose
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
        #endregion
    }
}
