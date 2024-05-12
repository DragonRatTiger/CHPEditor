using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;
using ImGuiNET;
using Silk.NET.OpenGL;

namespace CHPEditor
{
    internal class LangManager
    {
        public string Id { get; private set; }
        public string? Language { get; private set; }
        private string? InvalidEntry;
        public Dictionary<string, string>? Entries { get; private set; }

        public LangManager(string id)
        {
            Id = id;
            Language = "?";
            InvalidEntry = "INVALID_ENTRY";
            Entries = new Dictionary<string, string>();

            string langpath = Path.Combine("lang", Id, "lang.json");
            string langjson = File.ReadAllText(langpath, HEncodingDetector.DetectEncoding(langpath, Encoding.UTF8));

            JsonNodeOptions nodeOptions = new JsonNodeOptions() { PropertyNameCaseInsensitive = false };
            JsonDocumentOptions docOptions = new JsonDocumentOptions() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
            JsonNode node = JsonNode.Parse(langjson, nodeOptions, docOptions);

            Language = node[nameof(Language)].GetValue<string>();
            InvalidEntry = node[nameof(InvalidEntry)].GetValue<string>();
            Entries = JsonSerializer.Deserialize<Dictionary<string,string>>(node[nameof(Entries)]);
        }
        public unsafe void UseFont()
        {
            #region ImGUI Font Setup
            CHPEditor._io.Fonts.Clear();

            CHPEditor._io.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, "font.ttf"), 16, null, CHPEditor._io.Fonts.GetGlyphRangesDefault());

            ImFontConfigPtr config = new ImFontConfigPtr(ImGuiNative.ImFontConfig_ImFontConfig());
            config.MergeMode = true;

            ImFontGlyphRangesBuilderPtr builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
            builder.AddRanges(CHPEditor._io.Fonts.GetGlyphRangesJapanese());
            builder.BuildRanges(out ImVector out_ranges);

            CHPEditor._io.Fonts.AddFontFromFileTTF(Path.Combine("lang", Id, "font.ttf"), 16, config, out_ranges.Data);

            CHPEditor._io.Fonts.GetTexDataAsRGBA32(out byte* font_pixels, out int font_width, out int font_height);

            if (CHPEditor.imFontTex == 0)
                CHPEditor.imFontTex = CHPEditor._gl.GenTexture();
            CHPEditor._gl.BindTexture(GLEnum.Texture2D, CHPEditor.imFontTex);
            CHPEditor._gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMinFilter, (int)TextureMinFilter.Nearest);
            CHPEditor._gl.TexParameterI(GLEnum.Texture2D, GLEnum.TextureMagFilter, (int)TextureMagFilter.Nearest);
            CHPEditor._gl.TexImage2D(GLEnum.Texture2D, 0, (int)InternalFormat.Rgba, (uint)font_width, (uint)font_height, 0, GLEnum.Rgba, GLEnum.UnsignedByte, font_pixels);
            CHPEditor._io.Fonts.SetTexID((nint)CHPEditor.imFontTex);
            CHPEditor._gl.BindTexture(GLEnum.Texture2D, 0);

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
