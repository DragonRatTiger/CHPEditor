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
            Entries = node["Entries"].Deserialize<Dictionary<string, string>>();
            InvalidEntry = node["InvalidEntry"].Deserialize<string>();
            
            DefaultFont = node["DefaultFont"].Deserialize<string>();
            DefaultFontSize = node["DefaultFontSize"].Deserialize<float>();

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
            CHPEditor.IO.Fonts.Clear();

            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, DefaultFont), DefaultFontSize, null, CHPEditor.IO.Fonts.GetGlyphRangesDefault());

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
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), font.Item2, config, CHPEditor.IO.Fonts.GetGlyphRangesJapanese());
                            break;
                        case "chinese_full":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), font.Item2, config, CHPEditor.IO.Fonts.GetGlyphRangesChineseFull());
                            break;
                        case "chinese_simplified_common":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), font.Item2, config, CHPEditor.IO.Fonts.GetGlyphRangesChineseSimplifiedCommon());
                            break;
                        case "cyrillic":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), font.Item2, config, CHPEditor.IO.Fonts.GetGlyphRangesCyrillic());
                            break;
                        case "greek":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), font.Item2, config, CHPEditor.IO.Fonts.GetGlyphRangesGreek());
                            break;
                        case "korean":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), font.Item2, config, CHPEditor.IO.Fonts.GetGlyphRangesKorean());
                            break;
                        case "thai":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), font.Item2, config, CHPEditor.IO.Fonts.GetGlyphRangesThai());
                            break;
                        case "vietnamese":
                            CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), font.Item2, config, CHPEditor.IO.Fonts.GetGlyphRangesVietnamese());
                            break;
                    }
                }

                if (font.Item4 != null)
                {
                    byte[] glyphs = font.Item4.Select(byte.Parse).ToArray();
                    fixed (byte* glyph_data = glyphs)
                        CHPEditor.IO.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, font.Item1), font.Item2, config, (IntPtr)glyph_data);
                }
            }
            #endregion

            CHPEditor.IO.Fonts.GetTexDataAsRGBA32(out byte* font_pixels, out int font_width, out int font_height);

            byte[] arr = new byte[font_width * font_height * 4];
            Marshal.Copy((IntPtr)font_pixels, arr, 0, arr.Length);

            CHPEditor._imguiFontAtlas?.Dispose();
            CHPEditor._imguiFontAtlas = new ImageManager(arr, font_width, font_height);

            CHPEditor.IO.Fonts.SetTexID((nint)CHPEditor._imguiFontAtlas.Pointer);

            #endregion
        }
        public string GetValue(string key)
        {
            return Entries.TryGetValue(key, out string value) ? value : string.Format(InvalidEntry, key);
        }
        public string GetValue(string key, params object?[] args)
        {
            return Entries.TryGetValue(key, out string value) ? string.Format(value, args) : string.Format(InvalidEntry, key);
        }
    }
}
