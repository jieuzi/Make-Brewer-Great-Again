using HarmonyLib;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 用原版物品工厂的 Postfix 给现有物品补行为，不复制它们的名字/价格/图标等设置。
    ///   RedBeer            → 对排水口使用，换成原版空啤酒瓶
    ///   EmptyBeerBottle    → 对水龙头使用，换成干净的空啤酒瓶（原版空瓶本身保持原版行为，不能酿酒）
    /// </summary>
    [HarmonyPatch(typeof(FoodItemDirectory), "RedBeer")]
    internal static class RedBeerPatch
    {
        private static void Postfix(GameItem __result)
        {
            BeerHelper.AddDrainSupport(__result);
        }
    }

    [HarmonyPatch(typeof(FoodItemDirectory), "EmptyBeerBottle")]
    internal static class EmptyBeerBottlePatch
    {
        private static void Postfix(GameItem __result)
        {
            BeerHelper.AddFaucetSupport(__result);
        }
    }
}
