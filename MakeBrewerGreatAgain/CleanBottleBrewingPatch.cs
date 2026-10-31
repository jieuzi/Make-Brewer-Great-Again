using System;
using HarmonyLib;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 干净瓶的贴图/容量相关补丁（酿酒流程见 BeerBottleUi）。
    ///   - 容量固定 600ml（旧存档里的瓶子读取时也会被纠正）；
    ///   - 原版改贴图后立刻压回模组贴图（否则“模组贴图”标记残留会变成占位图）；
    ///   - 发酵中 / 酿好 / 倒空时切换模组贴图；
    ///   - 初始化时换上我们自己的酿酒窗口。
    /// </summary>
    [HarmonyPatch(typeof(WaterHelper), "GetTotalCapacityML")]
    internal static class CleanBottleCapacityPatch
    {
        // 参数名必须和原方法一致（WaterHelper.GetTotalCapacityML(GameItem item)）
        private static void Postfix(GameItem item, ref int __result)
        {
            if (!BeerHelper.IsCleanBottle(item)) return;
            if (__result <= BeerHelper.CleanBottleCapacityMl) return;

            BeerHelper.ForceCleanBottleCapacityTag(item);
            __result = BeerHelper.CleanBottleCapacityMl;
        }
    }

    /// <summary>原版每次给干净瓶换图之后，立刻改回模组贴图。</summary>
    [HarmonyPatch(typeof(GameItem), "SetSpriteAndShape")]
    internal static class CleanBottleSpriteGuardPatch
    {
        private static void Postfix(GameItem __instance)
        {
            if (__instance == null) return;
            if (!BeerHelper.IsCleanBottle(__instance)) return;
            if (__instance.useSpriteFromMod) return;

            BeerHelper.RefreshCleanBottleSprite(__instance);
        }
    }

    [HarmonyPatch(typeof(WineHelper), "StartFermentation")]
    internal static class CleanBottleFermentingSpritePatch
    {
        private static void Postfix(GameItem item)
        {
            BeerHelper.RefreshCleanBottleSprite(item);
        }
    }

    [HarmonyPatch(typeof(WineHelper), "FinishFermentation")]
    internal static class CleanBottleFinishedSpritePatch
    {
        private static void Postfix(GameItem item)
        {
            BeerHelper.RefreshCleanBottleSprite(item);
        }
    }

    [HarmonyPatch(typeof(WineHelper), "ResetToEmptyBottle")]
    internal static class CleanBottleResetSpritePatch
    {
        private static void Postfix(GameItem item)
        {
            BeerHelper.RefreshCleanBottleSprite(item);
        }
    }

    /// <summary>干净瓶初始化（新建 / 倒空后重建）时换上我们自己的酿酒窗口。</summary>
    [HarmonyPatch(typeof(WineHelper), "InitWineBottle")]
    internal static class BeerBottleWindowPatch
    {
        private static void Postfix(GameItem item)
        {
            if (!BeerHelper.IsCleanBottle(item)) return;
            BeerBottleUi.Install(item);
        }
    }

    /// <summary>
    /// 原版酒瓶提示框里那行水量是硬编码的「水：X / 750 ml」（WineHelper.CreateTooltip 里直接 ldc.i4 750），
    /// 干净瓶只有 600ml。空瓶状态这一行是原版唯一的固定内容，所以这里自己按 600ml 写一行，
    /// 其余状态（发酵中/已酿好）原版本来就不加这行，交回原版处理。
    /// </summary>
    [HarmonyPatch(typeof(WineHelper), "CreateTooltip")]
    internal static class CleanBottleTooltipPatch
    {
        private static bool Prefix(RichTextBuilder builder, GameItem item)
        {
            if (builder == null || item == null) return true;
            if (!BeerHelper.IsCleanBottle(item)) return true;

            int state = 0;
            try { state = item.GetTagReadonly("WINE_CONTAINER_STATE").GetInt(); } catch (Exception) { }
            if (state != 0) return true;

            try
            {
                int current = WaterHelper.GetCurrentCapacityML(item);
                string text = LocHelper.GetLocalizedMechanic("liquid_water")
                              + ": " + current + " / " + BeerHelper.CleanBottleCapacityMl + " ml";

                builder.AddLine(text, true, RenderHandler.ColorPalette.LightCyan,
                    false, false, false, false,
                    RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None);

                // 原版水量行后面还有“可陈化 / 已陈化”两行（分数 > 0 且不是假酒时），照抄过来
                if (WineHelper.GetWineQualityTier(item) > 0 && !item.IsTag("WINE_COUNTERFEIT_TAG"))
                {
                    AgableHelper.AddAgingInfoTooltipLine(builder);
                    if (AgableHelper.HasAged(item))
                        AgableHelper.AddAgeTooltipLine(builder, item, "mech_wine_age");
                }
            }
            catch (Exception)
            {
                // 出错就交回原版，至少别让提示框变空
                return true;
            }

            return false;
        }
    }
}
