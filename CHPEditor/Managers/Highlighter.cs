using Silk.NET.Maths;
using Silk.NET.SDL;

namespace CHPEditor
{
    public class Highlighter
    {
        public Color Color { get; private set; }
        private ImageManager Highlight;

        public Highlighter(byte r = 0xff, byte g = 0xff, byte b = 0xff, byte a = 0xff)
        {
            Color = new Color(r, g, b, a);
            Highlight = new ImageManager([Color.R, Color.G, Color.B, Color.A], 1, 1);
        }
        public Highlighter(Color color)
        {
            Color = color;
            Highlight = new ImageManager([Color.R, Color.G, Color.B, Color.A], 1, 1);
        }

        public void UpdateColor(byte r = 0xff, byte g = 0xff, byte b = 0xff, byte a = 0xff)
        {
            UpdateColor(new Color(r, g, b, a));
        }
        public void UpdateColor(Color color)
        {
            Color = color;
            Highlight.UpdateImage([Color.R, Color.G, Color.B, Color.A]);
        }

        public void Draw(Rectangle<int> rect, double rot = 0.0)
        {
            if (rect.Size.X <= 0 || rect.Size.Y <= 0) return;

            var pos = new Rectangle<int>(0, 0, 1, 1);
            var offset = new Rectangle<int>(rect.Origin.X - 1, rect.Origin.Y - 1, rect.Size.X + 2, 1);
            Highlight.Draw(pos, offset);
            offset = new Rectangle<int>(rect.Origin.X - 1, rect.Origin.Y - 1, 1, rect.Size.Y + 2);
            Highlight.Draw(pos, offset);
            offset = new Rectangle<int>(rect.Max.X, rect.Origin.Y, 1, rect.Size.Y + 1);
            Highlight.Draw(pos, offset);
            offset = new Rectangle<int>(rect.Origin.X, rect.Max.Y, rect.Size.X + 1, 1);
            Highlight.Draw(pos, offset);

            offset = new Rectangle<int>(rect.Origin.X, rect.Origin.Y, rect.Size.X, rect.Size.Y);
            Highlight.Draw(pos, offset, rot, 0.25f);            
        }
    }
}
