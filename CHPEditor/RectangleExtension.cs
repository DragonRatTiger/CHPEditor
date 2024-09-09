using Silk.NET.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHPEditor
{
    public static class RectangleExtension
    {
        public static Rectangle<int> Add(this Rectangle<int> a, Rectangle<int> b)
        {
            a.Origin.X += b.Origin.X;
            a.Origin.Y += b.Origin.Y;
            a.Size.X += b.Size.X;
            a.Size.Y += b.Size.Y;
            return a;
        }
        public static Rectangle<int> Subtract(this Rectangle<int> a, Rectangle<int> b)
        {
            a.Origin.X -= b.Origin.X;
            a.Origin.Y -= b.Origin.Y;
            a.Size.X -= b.Size.X;
            a.Size.Y -= b.Size.Y;
            return a;
        }
        public static Rectangle<int> Multiply(this Rectangle<int> a, double b)
        {
            a.Origin.X = (int)(a.Origin.X * b);
            a.Origin.Y = (int)(a.Origin.Y * b);
            a.Size.X = (int)(a.Size.X * b);
            a.Size.Y = (int)(a.Size.Y * b);
            return a;
        }
    }
}
