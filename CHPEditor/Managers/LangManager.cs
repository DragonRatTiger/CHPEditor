using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;
using ImGuiNET;
using Silk.NET.OpenGL;
using StbImageSharp;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Linq;

namespace CHPEditor
{
    internal class LangManager
    {
        public string Id { get; private set; }
        public string Language { get; private set; }
        public Dictionary<string, string> Entries { get; private set; }
        private string InvalidEntry;

        private string DefaultFont;
        private float DefaultFontSize;
        private List<(string, float, string[]?, string[]?)> Fonts;

        public LangManager(string id)
        {
            Id = id;
            Language = "?";
            Entries = new Dictionary<string, string>();
            InvalidEntry = "INVALID_ENTRY";

            DefaultFont = "font.ttf";
            DefaultFontSize = 16.0f;
            Fonts = new List<(string, float, string[]?, string[]?)>();

            string langpath = Path.Combine("lang", Id, "lang.json");
            string langjson = File.ReadAllText(langpath, HEncodingDetector.DetectEncoding(langpath, Encoding.UTF8));

            JsonNodeOptions nodeOptions = new JsonNodeOptions() { PropertyNameCaseInsensitive = false };
            JsonDocumentOptions docOptions = new JsonDocumentOptions() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            JsonNode node = JsonNode.Parse(langjson, nodeOptions, docOptions);

            Language = node["Language"].Deserialize<string>();
            InvalidEntry = node["InvalidEntry"].Deserialize<string>();
            Entries = node["Entries"].Deserialize<Dictionary<string, string>>();

            foreach (JsonObject arr in node["Fonts"].AsArray())
            {
                Fonts.Add(
                    (arr["Font"].Deserialize<string>(), 
                    arr["Size"].Deserialize<float>(), 
                    arr["GlyphSets"].Deserialize<string[]?>(), 
                    arr["CustomGlyphs"].Deserialize<string[]?>())
                    );
            }
        }
        public unsafe void UseFont()
        {
            #region ImGUI Font Setup
            CHPEditor._io.Fonts.Clear();

            CHPEditor._io.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, "font.ttf"), 16, null, CHPEditor._io.Fonts.GetGlyphRangesDefault());

            ImFontConfigPtr config = new ImFontConfigPtr(ImGuiNative.ImFontConfig_ImFontConfig());
            config.MergeMode = true;

            #region Extra Glyphs
            foreach ((string, float, string[]?, string[]?) font in Fonts)
            {
                if (font.Item3 != null)
                foreach (string glyph in font.Item3)
                {
                    switch (glyph.ToLower())
                    {
                        case "japanese":
                            CHPEditor._io.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), 16, config, CHPEditor._io.Fonts.GetGlyphRangesJapanese());
                            break;
                        case "chinese_full":
                            CHPEditor._io.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), 16, config, CHPEditor._io.Fonts.GetGlyphRangesChineseFull());
                            break;
                        case "chinese_simplified_common":
                            CHPEditor._io.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), 16, config, CHPEditor._io.Fonts.GetGlyphRangesChineseSimplifiedCommon());
                            break;
                        case "cyrillic":
                            CHPEditor._io.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), 16, config, CHPEditor._io.Fonts.GetGlyphRangesCyrillic());
                            break;
                        case "greek":
                            CHPEditor._io.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), 16, config, CHPEditor._io.Fonts.GetGlyphRangesGreek());
                            break;
                        case "korean":
                            CHPEditor._io.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), 16, config, CHPEditor._io.Fonts.GetGlyphRangesKorean());
                            break;
                        case "thai":
                            CHPEditor._io.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), 16, config, CHPEditor._io.Fonts.GetGlyphRangesThai());
                            break;
                        case "vietnamese":
                            CHPEditor._io.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), 16, config, CHPEditor._io.Fonts.GetGlyphRangesVietnamese());
                            break;
                    }
                }

                if (font.Item4 != null)
                {
                    byte[] glyphs = font.Item4.Select(byte.Parse).ToArray();
                    fixed (byte* glyph_data = glyphs)
                        CHPEditor._io.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), 16, config, (IntPtr)glyph_data);
                }
            }
            #endregion

            CHPEditor._io.Fonts.GetTexDataAsRGBA32(out byte* font_pixels, out int font_width, out int font_height);

            byte[] arr = new byte[font_width * font_height * 4];
            Marshal.Copy((IntPtr)font_pixels, arr, 0, arr.Length);

            CHPEditor._imguiFontAtlas?.Dispose();
            CHPEditor._imguiFontAtlas = new ImageManager(arr, font_width, font_height);

            CHPEditor._io.Fonts.SetTexID((nint)CHPEditor._imguiFontAtlas.Pointer);

            #endregion
        }
        public string GetValue(string key)
        {
            if (Entries.TryGetValue(key, out string value))
                return value;
            else
                return string.Format(InvalidEntry, key);
        }
        public string GetValue(string key, params object?[] args)
        {
            if (Entries.TryGetValue(key, out string value))
                return string.Format(value, args);
            else
                return string.Format(InvalidEntry, key);
        }
    }
}
