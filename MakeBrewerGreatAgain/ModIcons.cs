using System;
using UnityEngine;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 把 Textures/icons 下的 UI 图标补进 LoadedMod.Sprites。
    /// 游戏只扫描 Textures、Textures/Items、Textures/Paper，且 Textures 那一层不递归，
    /// 所以 Textures/icons 里的图本来不会被载入，AddImage / iconKey / SetSpriteAndShapeFromMod
    /// 都查不到它。这里用 ModHelper.ReadModFileBytes 自己读出来、建精灵，注册到 LoadedMod.Sprites 里。
    /// </summary>
    internal static class ModIcons
    {
        /// <summary>人脉升级格子用的图标键（72x72 正好铺满）。</summary>
        public const string BeerBottleKey = "beer bottle";

        /// <summary>名片物品的图标键（32x16，和原版名片同样大小）。</summary>
        public const string CardKey = "card";

        private static readonly string[] Keys = { BeerBottleKey, CardKey };

        private static readonly string[] Paths =
        {
            "Textures/icons/beer bottle.png",
            "Textures/icons/card.png",
        };

        public static void Prepare(LoadedMod loadedMod)
        {
            if (loadedMod == null || loadedMod.Manifest == null) return;
            if (loadedMod.Sprites == null) return;

            for (int i = 0; i < Keys.Length; i++)
            {
                // Textures/Items 里已经有同名精灵时以它为准，避免覆盖。
                if (loadedMod.Sprites.ContainsKey(Keys[i])) continue;

                Load(loadedMod, Keys[i], Paths[i]);
            }
        }

        private static void Load(LoadedMod loadedMod, string key, string path)
        {
            try
            {
                byte[] png = ModHelper.ReadModFileBytes(loadedMod.Manifest.ID, path);
                if (png == null || png.Length == 0) return;

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(png))
                {
                    UnityEngine.Object.Destroy(texture);
                    return;
                }

                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.Apply();

                var sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                sprite.name = key;

                loadedMod.Sprites[key] = sprite;
            }
            catch (Exception e)
            {
                if (ModEntry.Instance != null && ModEntry.Instance.Log != null)
                    ModEntry.Instance.Log.Warning("图标读取失败（" + path + "）：" + e.GetType().Name + ": " + e.Message);
            }
        }
    }
}
