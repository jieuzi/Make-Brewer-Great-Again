using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevEverything
{
    /// <summary>
    /// 按物品导出精灵图：从物品自己的 spriteAtlasPath / spritePath 取图。
    /// 原版物品：spriteAtlasPath="Items/items_food"、spritePath="red_beer"，
    ///           用 RenderHandler.LoadFromAtlas 从 Addressables 图集里取。
    /// 模组物品：SetSpriteAndShapeFromMod 会把 modId 写进 spriteAtlasPath、精灵键写进 spritePath，
    ///           并置 useSpriteFromMod=true，这时去 ModLoader.Mods 里对应模组的 Sprites 字典取。
    /// 导出用 ModHelper.WriteModDataFileBytes（模组数据目录，不需要 files 能力）。
    /// </summary>
    internal static class SpriteExporter
    {
        private struct SpriteRef
        {
            public string Atlas;      // 原版=图集路径，模组=modId
            public string Sprite;     // 精灵名 / 模组精灵键
            public bool FromMod;
        }

        /// <summary>已经导过整版图集的图集路径，避免重复写同一张。</summary>
        private static readonly HashSet<string> exportedAtlases = new HashSet<string>();

        /// <summary>最近一次导出的目录完整路径（状态栏/按钮用它显示并复制）。</summary>
        public static string LastExportFolder { get; private set; }

        // ------------------------------------------------------------------ 入口
        /// <summary>按物品目录里的条目导出（不重新实例化物品，窗口里用这个）。</summary>
        public static int ExportEntries(IList<ItemEntry> entries, string folder)
        {
            if (entries == null || entries.Count == 0) return 0;

            string modId = ModId();
            int exported = 0;

            foreach (ItemEntry entry in entries)
            {
                if (entry == null) continue;
                try
                {
                    var reference = new SpriteRef
                    {
                        Atlas = entry.SpriteAtlasPath,
                        Sprite = entry.SpritePath,
                        FromMod = entry.UseSpriteFromMod,
                    };

                    if (ExportSprite(reference, SafeFileName(entry.Id) + ".png", folder, modId))
                    {
                        exported++;
                        ExportAtlasOnce(reference, modId);
                    }
                }
                catch (Exception e)
                {
                    Warn($"导出 {entry.Id} 失败：{e.Message}");
                }
            }

            AnnounceExport(folder, exported, entries.Count);
            return exported;
        }

        /// <summary>只导出单件物品，返回生成的文件名；失败返回 null。</summary>
        public static string ExportSingle(ItemEntry entry, string folder)
        {
            if (entry == null) return null;

            try
            {
                string modId = ModId();
                var reference = new SpriteRef
                {
                    Atlas = entry.SpriteAtlasPath,
                    Sprite = entry.SpritePath,
                    FromMod = entry.UseSpriteFromMod,
                };

                string fileName = SafeFileName(entry.Id) + ".png";
                if (!ExportSprite(reference, fileName, folder, modId)) return null;

                ExportAtlasOnce(reference, modId);

                LastExportFolder = ExportFolderPath(folder);
                CopyPathToClipboard(LastExportFolder);
                Log($"已导出 {fileName} → {LastExportFolder}（路径已复制到剪贴板）");
                return fileName;
            }
            catch (Exception e)
            {
                Warn($"导出 {entry.Id} 失败：{e.Message}");
                return null;
            }
        }

        /// <summary>同一张图集只导出一次（批量导出时避免重复写同一个文件）。</summary>
        private static void ExportAtlasOnce(SpriteRef reference, string modId)
        {
            if (reference.FromMod) return;
            if (string.IsNullOrEmpty(reference.Atlas) || string.IsNullOrEmpty(reference.Sprite)) return;
            if (!exportedAtlases.Add(reference.Atlas)) return;
            ExportAtlas(reference.Atlas, reference.Sprite, modId);
        }

        // ------------------------------------------------------------------ 取图
        private static Sprite Resolve(SpriteRef reference)
        {
            if (string.IsNullOrEmpty(reference.Sprite)) return null;

            if (reference.FromMod)
            {
                List<LoadedMod> mods = ModLoader.Mods;
                if (mods == null) return null;

                foreach (LoadedMod mod in mods)
                {
                    if (mod == null || mod.Manifest == null) continue;
                    if (mod.Manifest.ID != reference.Atlas) continue;

                    Sprite sprite;
                    if (mod.Sprites != null && mod.Sprites.TryGetValue(reference.Sprite, out sprite)) return sprite;
                    return null;
                }
                return null;
            }

            if (string.IsNullOrEmpty(reference.Atlas)) return null;
            return RenderHandler.LoadFromAtlas(reference.Atlas, reference.Sprite);
        }

        private static bool ExportSprite(SpriteRef reference, string fileName, string folder, string modId)
        {
            Sprite sprite = Resolve(reference);
            if (sprite == null)
            {
                Warn($"取不到精灵图 {reference.Atlas}/{reference.Sprite}（物品的图集或精灵名可能为空）");
                return false;
            }

            Texture2D full = ReadTexture(sprite.texture);
            if (full == null) return false;

            try
            {
                Rect rect = sprite.textureRect;
                int x = Mathf.RoundToInt(rect.x);
                int y = Mathf.RoundToInt(rect.y);
                int w = Mathf.RoundToInt(rect.width);
                int h = Mathf.RoundToInt(rect.height);
                if (w <= 0 || h <= 0) return false;

                x = Mathf.Clamp(x, 0, Mathf.Max(0, full.width - w));
                y = Mathf.Clamp(y, 0, Mathf.Max(0, full.height - h));

                var cropped = new Texture2D(w, h, TextureFormat.ARGB32, false, false);
                cropped.SetPixels(full.GetPixels(x, y, w, h));
                cropped.Apply();

                byte[] png = cropped.EncodeToPNG();
                UnityEngine.Object.Destroy(cropped);
                if (png == null || png.Length == 0) return false;

                ModHelper.WriteModDataFileBytes(modId, JoinExportPath(folder, fileName), png);
                return true;
            }
            finally
            {
                UnityEngine.Object.Destroy(full);
            }
        }

        private static void ExportAtlas(string atlasPath, string seedSprite, string modId)
        {
            try
            {
                if (string.IsNullOrEmpty(atlasPath) || string.IsNullOrEmpty(seedSprite)) return;

                // 借图集里的一张精灵拿到整张图集纹理。
                Sprite any = RenderHandler.LoadFromAtlas(atlasPath, seedSprite);
                if (any == null || any.texture == null) return;

                Texture2D full = ReadTexture(any.texture);
                if (full == null) return;

                try
                {
                    byte[] png = full.EncodeToPNG();
                    if (png == null || png.Length == 0) return;

                    string name = atlasPath;
                    int slash = name.LastIndexOf('/');
                    if (slash >= 0 && slash + 1 < name.Length) name = name.Substring(slash + 1);

                    ModHelper.WriteModDataFileBytes(modId, JoinExportPath("图集", SafeFileName(name) + "_full.png"), png);
                    Log($"已导出整版图集 {name}（{full.width}x{full.height}）");
                }
                finally
                {
                    UnityEngine.Object.Destroy(full);
                }
            }
            catch (Exception e)
            {
                Warn($"导出图集 {atlasPath} 失败：{e.Message}");
            }
        }

        /// <summary>导出结果提示：记下路径、复制到剪贴板、写日志。</summary>
        private static void AnnounceExport(string folder, int count, int total)
        {
            LastExportFolder = ExportFolderPath(folder);
            bool copied = CopyPathToClipboard(LastExportFolder);
            Log($"导出完成：{count}/{total} 张 → {LastExportFolder}" + (copied ? "（路径已复制到剪贴板）" : "（剪贴板写入失败）"));
        }

        /// <summary>导出目录的完整路径（不含文件名）。</summary>
        public static string ExportFolderPath(string folder)
        {
            string path = ModHelper.GetModDataFolder(ModId()) + "\\exports";
            if (!string.IsNullOrEmpty(folder)) path += "\\" + SafeFileName(folder);
            return path;
        }

        /// <summary>把文本放进系统剪贴板（Unity 的 GUIUtility，不需要额外能力）。</summary>
        public static bool CopyPathToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            try
            {
                GUIUtility.systemCopyBuffer = text;
                return true;
            }
            catch (Exception e)
            {
                Warn("写剪贴板失败：" + e.Message);
                return false;
            }
        }

        // ------------------------------------------------------------------ 工具
        private static Texture2D ReadTexture(Texture texture)
        {
            if (texture == null) return null;

            RenderTexture temporary = RenderTexture.GetTemporary(
                texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;

            try
            {
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;

                var copy = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false, false);
                copy.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0);
                copy.Apply();
                return copy;
            }
            catch (Exception e)
            {
                Warn("读取纹理失败：" + e.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static string JoinExportPath(string folder, string fileName)
        {
            if (string.IsNullOrEmpty(folder)) return "exports/" + fileName;
            return "exports/" + SafeFileName(folder) + "/" + fileName;
        }

        /// <summary>把不能做文件名的字符换掉（不引用 System.IO，避免触发 files 能力）。</summary>
        private static string SafeFileName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "unnamed";

            var builder = new System.Text.StringBuilder(raw.Length);
            foreach (char c in raw)
            {
                bool invalid = c < ' ' || c == '\\' || c == '/' || c == ':' || c == '*' ||
                               c == '?' || c == '"' || c == '<' || c == '>' || c == '|';
                builder.Append(invalid ? '_' : c);
            }
            return builder.ToString();
        }

        private static string ModId()
        {
            return ModEntry.Instance != null && ModEntry.Instance.Manifest != null
                ? ModEntry.Instance.Manifest.ID
                : "dev_everything";
        }

        // 静音：常规信息不再输出到 F8（只保留警告/错误）
        private static void Log(string message)
        {
        }

        private static void Warn(string message)
        {
            if (ModEntry.Instance != null && ModEntry.Instance.Log != null) ModEntry.Instance.Log.Warning(message);
        }
    }
}
