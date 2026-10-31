using System;
using HarmonyLib;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 让红魔鬼啤酒箱也能装酒瓶。
    /// 原版啤酒箱的过滤条件是 ContainerHelper.AllowOnlyTaggedItem(inv, "RED_IMP_BEER_TAG", ...)，
    /// 该函数只是把 inv.mayInventoryAddItemFunc 设成一个闭包，所以这里在它执行完之后包一层：
    ///   - 带 WINE_CONTAINER_TAG 的物品放行：酒瓶、发酵中/已发酵的酒瓶；
    ///   - 另外按物品 id 兜底放行 wine_bottle / clean_empty_beer_bottle / empty_beer_bottle：
    ///     原版空啤酒瓶本来就不带酒瓶标签（从田所浩二那里订来的也一样），只靠标签会漏掉它们。
    /// 其余物品仍走原版判断，不影响啤酒箱只收啤酒的默认行为。
    /// </summary>
    [HarmonyPatch(typeof(ContainerHelper), "AllowOnlyTaggedItem")]
    internal static class BeerCasePatch
    {
        private const string BeerCaseTag = "RED_IMP_BEER_TAG";
        private const string WineContainerTag = "WINE_CONTAINER_TAG";
        private const string WineBottleId = "wine_bottle";
        private const string EmptyBeerBottleId = "empty_beer_bottle";
        private const string CleanBottleId = "clean_empty_beer_bottle";

        private static void Postfix(GameInventory itemInventory, string tag)
        {
            if (itemInventory == null) return;
            if (!string.Equals(tag, BeerCaseTag, StringComparison.Ordinal)) return;

            try
            {
                Func<GameItem, GameInventory, bool> original = itemInventory.mayInventoryAddItemFunc;

                itemInventory.mayInventoryAddItemFunc = (item, container) =>
                {
                    if (Accepts(item)) return true;
                    return original != null && original(item, container);
                };

                Log("红魔鬼啤酒箱已允许装入酒瓶 / 空啤酒瓶 / 发酵中的酒瓶。");
            }
            catch (Exception e)
            {
                Warn("扩展啤酒箱可装物品失败：" + e.Message);
            }
        }

        /// <summary>酒瓶类：酒瓶、空啤酒瓶、干净的空啤酒瓶、发酵中/发酵好的酒瓶都算。</summary>
        private static bool Accepts(GameItem item)
        {
            if (item == null) return false;

            bool tagged;
            try
            {
                tagged = item.IsTag(WineContainerTag);
            }
            catch (Exception)
            {
                tagged = false;
            }
            if (tagged) return true;

            // 标签没了（旧存档物品被恢复的 tag 覆盖）时按 id 兜底。
            // 注意：打电话跟田所浩二订来的空瓶也是原版空啤酒瓶（不带酒瓶标签），
            // 所以这里静默放行，不再提示"旧存档物品"。
            string id = SafeIdentifier(item);
            if (id == WineBottleId) return true;
            if (id == CleanBottleId) return true;
            if (id != EmptyBeerBottleId) return false;

            return true;
        }

        private static string SafeIdentifier(GameItem item)
        {
            try { return item.identifier; } catch (Exception) { return null; }
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
