using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.IO;
using ImGuiNET;
using System.Runtime.InteropServices;
using System.Linq;
using Newtonsoft.Json;

namespace CHPEditor
{
    public class LangData
    {
        public struct FontInfo
        {
            [JsonProperty("Font")]
            public string Font;
            [JsonProperty("Size")]
            public float Size;
            [JsonProperty("GlyphSets")]
            public string[] GlyphSets;
            [JsonProperty("CustomGlyphs")]
            public string[] CustomGlyphs;
            public FontInfo()
            {
                Font = "font.ttf";
                Size = 16.0f;
                GlyphSets = [];
                CustomGlyphs = [];
            }
        }
        public string Id = "?";

        [JsonProperty("Language")]
        public string Language = "?";
        [JsonProperty("InvalidEntry")]
        public string InvalidEntry = "INVALID ENTRY: {0}";
        [JsonProperty("DefaultFont")]
        public string DefaultFont = "font.ttf";
        [JsonProperty("DefaultFontSize")]
        public float DefaultFontSize = 16.0f;

        [JsonProperty("Fonts")]
        public FontInfo[] Fonts = [];
        [JsonProperty("Entries")]
        public Dictionary<string, string> Entries = [];
    }
    public static class LangManager
    {
        private static LangData langData = new LangData();
        public static string Id => langData.Id;
        public static string Language => langData.Language;
        public static Dictionary<string, string> Entries => langData.Entries;
        private static string InvalidEntry => langData.InvalidEntry;

        private static string DefaultFont => langData.DefaultFont;
        private static float DefaultFontSize => langData.DefaultFontSize;
        private static LangData.FontInfo[] Fonts => langData.Fonts;

        public static void Initalize(string id)
        {
            string langpath = Path.Combine("lang", id, "lang.json");
            if (File.Exists(langpath))
            {
                var settings = new JsonSerializerSettings()
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore
                };
                langData = JsonConvert.DeserializeObject<LangData>(File.ReadAllText(langpath, Encoding.UTF8), settings) ?? new LangData();
            }

            langData.Id = id;
        }

        public static unsafe void UseFont()
        {
            #region ImGUI Font Setup
            CHPEditor.IO.Fonts.Clear();

            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, DefaultFont), DefaultFontSize, null, CHPEditor.IO.Fonts.GetGlyphRangesDefault());

            ImFontConfigPtr config = new ImFontConfigPtr(ImGuiNative.ImFontConfig_ImFontConfig());
            config.MergeMode = true;

            #region Extra Glyphs
            foreach (LangData.FontInfo font in Fonts)
            {
                if (font.GlyphSets != null)
                foreach (string glyph in font.GlyphSets)
                {
                    switch (glyph.ToLower())
                    {
                        case "japanese":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Font), font.Size, config, CHPEditor.IO.Fonts.GetGlyphRangesJapanese());
                            break;
                        case "chinese_full":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Font), font.Size, config, CHPEditor.IO.Fonts.GetGlyphRangesChineseFull());
                            break;
                        case "chinese_simplified_common":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Font), font.Size, config, CHPEditor.IO.Fonts.GetGlyphRangesChineseSimplifiedCommon());
                            break;
                        case "cyrillic":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Font), font.Size, config, CHPEditor.IO.Fonts.GetGlyphRangesCyrillic());
                            break;
                        case "greek":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Font), font.Size, config, CHPEditor.IO.Fonts.GetGlyphRangesGreek());
                            break;
                        case "korean":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Font), font.Size, config, CHPEditor.IO.Fonts.GetGlyphRangesKorean());
                            break;
                        case "thai":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Font), font.Size, config, CHPEditor.IO.Fonts.GetGlyphRangesThai());
                            break;
                        case "vietnamese":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Font), font.Size, config, CHPEditor.IO.Fonts.GetGlyphRangesVietnamese());
                            break;
                    }
                }

                if (font.CustomGlyphs != null)
                {
                    byte[] glyphs = font.CustomGlyphs.Select(byte.Parse).ToArray();
                    fixed (byte* glyph_data = glyphs)
                        CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Font), font.Size, config, (IntPtr)glyph_data);
                }
            }
            #endregion

            CHPEditor.IO.Fonts.GetTexDataAsRGBA32(out byte* font_pixels, out int font_width, out int font_height);

            byte[] arr = new byte[font_width * font_height * 4];
            Marshal.Copy((IntPtr)font_pixels, arr, 0, arr.Length);
            Marshal.FreeHGlobal((IntPtr)font_pixels);

            CHPEditor._imguiFontAtlas?.Dispose();
            CHPEditor._imguiFontAtlas = new ImageManager(arr, font_width, font_height);

            CHPEditor.IO.Fonts.SetTexID((nint)CHPEditor._imguiFontAtlas.Pointer);

            #endregion
        }
        public static string GetValue(string key)
        {
            return Entries.TryGetValue(key, out string value) ? value : string.Format(InvalidEntry, key);
        }
        public static string GetValue(string key, params object?[] args)
        {
            return Entries.TryGetValue(key, out string value) ? string.Format(value, args) : string.Format(InvalidEntry, key);
        }
    }
}
