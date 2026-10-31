using System;
using HarmonyLib;
using UnityEngine;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 水培机里的啤酒花外观。
    ///
    /// 原版在 MachineHydroponic 的贴图修饰函数里这样取作物图：
    ///   RenderHandler.LoadFromAtlas("Items/items_hydroponic_plant", harvestItemId + "_p")   // 生长中
    ///   RenderHandler.LoadFromAtlas("Items/items_hydroponic_plant", harvestItemId + "_p2")  // 成熟待收
    /// 原版图集我们加不进新图，所以这里在取图前截住：只要图集是水培图集、名字是我们的作物，
    /// 就换成模组贴图（hops_p2 → hops_p → hops，取到哪个用哪个）。
    /// </summary>
    [HarmonyPatch(typeof(RenderHandler), "LoadFromAtlasRaw")]
    internal static class HopsPlantSpritePatch
    {
        private static bool Prefix(string atlasPath, string name, ref Sprite __result)
        {
            if (atlasPath != HopsCrop.PlantAtlas) return true;
            if (string.IsNullOrEmpty(name)) return true;
            if (!name.StartsWith("hops_p", StringComparison.Ordinal)) return true;

            Sprite sprite = HopsCrop.FindPlantSprite(name);
            if (sprite == null) return true;

            __result = sprite;
            return false;
        }
    }
}
