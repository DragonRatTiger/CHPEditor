using System;

namespace CHPEditor
{
    public static class Timeline
    {
        public static bool UseLoop = false;
        public static int CurrentFrame = 0;
        public static int CurrentTime = 0;
        public static int CurrentLoop = 0;

        public static void Clear() { CurrentFrame = 0; CurrentTime = 0; }
        public static void Update(ref CHPFile.AnimeData data, int default_frametime, double ms)
        {
            if (!data.Loaded)
            {
                Clear();
                return;
            }

            if (data.Frame > 0)
            {
                if (data.Loop > 0 && UseLoop)
                {
                    int loop = Math.Clamp(data.Loop, 0, data.FrameCount - 1);
                    CurrentFrame = (((int)ms / data.Frame) % (data.FrameCount - loop)) + loop;
                    CurrentTime = ((int)ms % (data.Frame * (data.FrameCount - loop)) + (data.Frame * loop));
                }
                else
                {
                    CurrentFrame = ((int)ms / data.Frame) % data.FrameCount;
                    CurrentTime = ((int)ms % (data.Frame * data.FrameCount));
                }
            }
            else
            {
                if (data.Loop > 0 && UseLoop)
                {
                    int loop = Math.Clamp(data.Loop, 0, data.FrameCount - 1);
                    CurrentFrame = ((int)ms / default_frametime) % (data.FrameCount - loop) + loop;
                    CurrentTime = ((int)ms % (default_frametime * (data.FrameCount - loop)) + (default_frametime * loop));
                }
                else
                {
                    CurrentFrame = ((int)ms / default_frametime) % data.FrameCount;
                    CurrentTime = ((int)ms % (default_frametime * data.FrameCount));
                }
            }
        }
    }
}
