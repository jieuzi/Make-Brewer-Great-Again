using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 运行时从一张图集里切子精灵。
    /// 模组只需要放一张 Textures\beer_winemaking.png（原版 Items/items_winemaking 的同位置改版），
    /// 这里按矩形切出各状态用的精灵，注入到 LoadedMod.Sprites，
    /// 之后 SetSpriteAndShapeFromMod(modId, key) 就能像普通模组物品图一样使用。
    ///
    /// 矩形坐标来自游戏内实测（DevEverything 的“导出酿酒图集坐标”），
    /// 用“左上角为原点”的像素坐标书写，内部换算成 Unity 的左下角原点。
    /// 原版图集是 512x512，瓶子和酒标都是 16x64。
    /// </summary>
    internal static class SheetSprites
    {
        /// <summary>图集文件名（放在 Textures\ 下，不是 Textures\Items\）。</summary>
        public const string SheetKey = "beer_winemaking";

        private struct Cell
        {
            public string Key;
            public int X;      // 左上角 x
            public int TopY;   // 左上角 y
            public int W;
            public int H;
        }

        // 与 Items/items_winemaking 完全相同的矩形（实测值）
        private static readonly Cell[] Cells =
        {
            // 空瓶 / 发酵中
            new Cell { Key = "beer_wine_bottle", X = 480, TopY = 176, W = 16, H = 64 },
            new Cell { Key = "beer_fermenting",  X = 448, TopY = 176, W = 16, H = 64 },

            // 等级瓶身（原版里是乱序放的，这里是实测对应）
            new Cell { Key = "beer_bottle0", X = 304, TopY = 176, W = 16, H = 64 },
            new Cell { Key = "beer_bottle1", X = 208, TopY = 176, W = 16, H = 64 },
            new Cell { Key = "beer_bottle2", X = 176, TopY = 176, W = 16, H = 64 },
            new Cell { Key = "beer_bottle3", X = 48,  TopY = 176, W = 16, H = 64 },
            new Cell { Key = "beer_bottle4", X = 240, TopY = 176, W = 16, H = 64 },
            new Cell { Key = "beer_bottle5", X = 16,  TopY = 176, W = 16, H = 64 },

            // 酒标叠加层（与瓶身同画布，位置 0,0）
            new Cell { Key = "beer_label0", X = 176, TopY = 96, W = 16, H = 64 },
            new Cell { Key = "beer_label1", X = 144, TopY = 96, W = 16, H = 64 },
            new Cell { Key = "beer_label2", X = 112, TopY = 96, W = 16, H = 64 },
            new Cell { Key = "beer_label3", X = 80,  TopY = 96, W = 16, H = 64 },
            new Cell { Key = "beer_label4", X = 48,  TopY = 96, W = 16, H = 64 },
            new Cell { Key = "beer_label5", X = 16,  TopY = 96, W = 16, H = 64 },
            new Cell { Key = "beer_label_red", X = 208, TopY = 96, W = 16, H = 64 },
        };

        private static LoadedMod owner;

        /// <summary>把图集切成子精灵并注册到该模组的 Sprites 字典。资源全部加载完成后调用。</summary>
        public static void Prepare(LoadedMod mod)
        {
            if (mod == null) return;

            Texture2D sheet = null;
            if (mod.Textures != null) mod.Textures.TryGetValue(SheetKey, out sheet);
            if (sheet == null)
            {
                Warn($"图集 Textures\\{SheetKey}.png 没加载到，无法切图。");
                return;
            }

            if (mod.Sprites == null) return;
            owner = mod;

            // 以原版图集里的精灵为基准（缩放/锚点完全一致，避免放大后发糊）
            float pixelsPerUnit = 100f;
            Vector2 pivot = new Vector2(0.5f, 0.5f);
            bool haveReference = false;

            try
            {
                Sprite reference = RenderHandler.TryLoadFromAtlas("Items/items_winemaking", "bottle0")
                                   ?? RenderHandler.TryLoadFromAtlas("Items/items_winemaking", "wine_bottle");
                if (reference != null)
                {
                    pixelsPerUnit = reference.pixelsPerUnit;
                    Rect r = reference.rect;
                    if (r.width > 0f && r.height > 0f)
                    {
                        pivot = new Vector2(reference.pivot.x / r.width, reference.pivot.y / r.height);
                    }
                    haveReference = true;
                }
            }
            catch (Exception)
            {
            }

            if (!haveReference)
            {
                // 拿不到原版精灵时退回到模组里的物品图当基准
                Sprite sample;
                if (mod.Sprites.TryGetValue("clear_empty_beer_bottle", out sample) && sample != null && sample.texture != null)
                {
                    pixelsPerUnit = sample.pixelsPerUnit;
                    Rect sr = sample.rect;
                    if (sr.width > 0f && sr.height > 0f)
                    {
                        pivot = new Vector2(sample.pivot.x / sr.width, sample.pivot.y / sr.height);
                    }
                }
            }

            // 像素画必须点采样，否则放大就糊
            try
            {
                sheet.filterMode = FilterMode.Point;
                sheet.wrapMode = TextureWrapMode.Clamp;
                sheet.Apply();
            }
            catch (Exception)
            {
            }

            int made = 0;
            foreach (Cell cell in Cells)
            {
                try
                {
                    int unityY = sheet.height - cell.TopY - cell.H;
                    if (unityY < 0 || cell.X < 0) continue;

                    var rect = new Rect(cell.X, unityY, cell.W, cell.H);
                    Sprite sprite = Sprite.Create(sheet, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    if (sprite == null) continue;

                    sprite.name = cell.Key;
                    mod.Sprites[cell.Key] = sprite;
                    made++;
                }
                catch (Exception e)
                {
                    Warn($"切图失败 {cell.Key}：{e.Message}");
                }
            }

            Log($"图集切图完成：{made}/{Cells.Length} 张（{sheet.width}x{sheet.height}，ppu={pixelsPerUnit}，pivot={pivot.x:F2},{pivot.y:F2}，filter={sheet.filterMode}）");
        }

        /// <summary>取一张已经切好的子精灵（没切到返回 null）。</summary>
        public static Sprite Get(string key)
        {
            if (string.IsNullOrEmpty(key) || owner == null || owner.Sprites == null) return null;

            Sprite sprite;
            return owner.Sprites.TryGetValue(key, out sprite) ? sprite : null;
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
