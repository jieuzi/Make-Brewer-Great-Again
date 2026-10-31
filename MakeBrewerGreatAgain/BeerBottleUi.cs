using System;
using System.Collections.Generic;
using UnityEngine;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 干净的空啤酒瓶专属界面 + 酿造流程（和原版酒瓶分开，方便单独改啤酒瓶）。
    ///
    /// 界面：用原版机械窗口的外壳（配方槽 6×2 + 一个按钮），但按钮和判定是我们自己的。
    /// 开始酿造的判定顺序：
    ///   1) 有荧光莓 → 走原版酒瓶线（WineHelper.StartFermentation）；
    ///   2) 没有荧光莓：需要 ≥100 千卡的营养果原料（营养果 / 营养果块 / 营养果碎）、酵母、
    ///      以及接近满瓶的水 → 走啤酒线：清空原料、倒掉水，进入"酿造中的啤酒"状态，贴图 brewing beer；
    ///   3) 不满足 → 提示缺什么。
    /// 啤酒线评分见 BeerRecipes.ScoreBeer：
    ///   0 分起 + 水质分 + 营养果档位（整果 −1 / 果块 0 / 果碎 +1）+ 啤酒花 +1；
    ///   3 极品精酿、2 精酿啤酒、1~0 私酿啤酒、&lt;0 私酿毒药。
    /// </summary>
    internal static class BeerBottleUi
    {
        public const string BrewingTag = "GMTB_BEER_BREWING_INT";
        public const string DaysTag = "GMTB_BEER_DAYS_INT";

        /// <summary>啤酒发酵需要的天数。</summary>
        public const int BrewDays = 2;

        /// <summary>开始发酵要求的最低液量（600ml 瓶接近满瓶）。</summary>
        public const int MinMl = 594;

        /// <summary>啤酒线要求的最低营养果原料热量。</summary>
        public const int MinNutrifruitKcal = 100;

        private const string BrewingSprite = "brewing beer";
        private const string WineIngredientTag = "WINE_INGREDIENT_TAG";
        private const string YeastTag = "WINE_YEAST_TAG";

        // ------------------------------------------------------------------ 界面
        /// <summary>给干净瓶装上我们自己的酿酒窗口（InitWineBottle 之后调用）。</summary>
        public static void Install(GameItem bottle)
        {
            if (bottle == null) return;

            try
            {
                var inventory = new GameGridInventory(6, 2);
                inventory.mayInventoryAddItemFunc = (item, container) => AllowIngredient(item);

                var button = new MachineryButton("开始酿造", () => { }, -1, -1);
                button.SetCallback(() => StartBrewing(bottle));

                var panels = new[]
                {
                    MachineHelper.Panel("酿酒原料", inventory, "ingredients", true),
                    MachineHelper.Panel("操作", button, "control", true),
                };

                PixelWindow window = MachineHelper.CreateMachineryWindow(true, panels);
                if (window == null) return;

                bottle.SetContentWindow(window);

                // 每天推进：只在“啤酒酿造中”接管，其余交回原版（酒瓶线的陈年等）
                Action<GameItem, GameInventory, SlotMarker> previous = bottle.onCycleEndSlotItemFunc;
                bottle.onCycleEndSlotItemFunc = (item, inv, slot) =>
                {
                    if (IsBrewing(item))
                    {
                        Tick(item);
                        return;
                    }
                    if (previous != null) previous(item, inv, slot);
                };
            }
            catch (Exception e)
            {
                Warn("创建啤酒瓶界面失败：" + e.Message);
            }
        }

        /// <summary>配方槽允许放的东西：原版酿酒原料 + 酵母 + 营养果 / 营养果块 / 营养果碎 + 啤酒花。</summary>
        private static bool AllowIngredient(GameItem item)
        {
            if (item == null) return false;
            try
            {
                if (BeerRecipes.IsBeerIngredient(item)) return true;
                return item.IsTag(WineIngredientTag) || item.IsTag(YeastTag);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ------------------------------------------------------------------ 开始酿造
        private static void StartBrewing(GameItem bottle)
        {
            if (bottle == null) return;

            try
            {
                List<GameItem> children = GraphUtils.FindAllChildrenType<GameItem>(bottle, null, null);
                if (children == null) children = new List<GameItem>();

                bool hasBerry = false;
                bool hasYeast = false;

                for (int i = 0; i < children.Count; i++)
                {
                    GameItem child = children[i];
                    if (child == null) continue;
                    if (child.IsTag(WineIngredientTag)) hasBerry = true;
                    if (child.IsTag(YeastTag)) hasYeast = true;
                }

                int ml = 0;
                try { ml = WaterHelper.GetCurrentCapacityML(bottle); } catch (Exception) { }

                // 1) 有荧光莓：原版酒瓶线
                if (hasBerry)
                {
                    if (ml < MinMl) { ShowMessage("水不够：需要接近满瓶（594ml 以上）。"); return; }
                    WineHelper.StartFermentation(bottle);
                    return;
                }

                // 2) 没有荧光莓：啤酒线需要 ≥100 千卡的营养果原料 + 酵母
                if (BeerRecipes.NutrifruitKcal(children) < MinNutrifruitKcal)
                {
                    ShowMessage("营养不足：酿造啤酒至少需要 100 千卡的营养果原料（营养果 / 营养果块 / 营养果碎）。");
                    return;
                }
                if (!hasYeast) { ShowMessage("缺少酵母：啤酒配方必须配酵母。"); return; }
                if (ml < MinMl) { ShowMessage("水不够：需要接近满瓶（594ml 以上）。"); return; }

                int score = BeerRecipes.ScoreBeer(bottle, children);
                int quality = BeerRecipes.QualityFromScore(score);

                ConsumeIngredients(children);
                try { WineHelper.DrainBottle(bottle); } catch (Exception) { }

                bottle.ModifyTag(BrewingTag, state => state.Enable().SetInt(1));
                bottle.ModifyTag(BeerRecipes.QualityTag, state => state.Enable().SetInt(quality));
                bottle.ModifyTag(BeerRecipes.ScoreTag, state => state.Enable().SetInt(score));
                bottle.ModifyTag(DaysTag, state => state.Enable().SetInt(BrewDays));

                BeerHelper.SetModSprite(bottle, BrewingSprite);

                // 关掉窗口，酿造期间不能再加料
                try { bottle.CloseContentWindow(true); } catch (Exception) { }
                try { bottle.SetContentWindow(null); } catch (Exception) { }
            }
            catch (Exception e)
            {
                Warn("开始酿造失败：" + e.Message);
            }
        }

        /// <summary>用原版的确认弹窗提示玩家（和原版酒瓶"水不够/没有原料"用的是同一个面板）。</summary>
        private static void ShowMessage(string body)
        {
            try
            {
                GenericConfirmationUIManager ui = GenericConfirmationUIManager.Instance;
                if (ui == null)
                {
                    Warn(body);
                    return;
                }

                string confirm = "确定";
                try { confirm = LocHelper.GetLocalizedMainMenu("confirm"); } catch (Exception) { }

                ui.OpenPanelSingleButton(body, confirm, null);
            }
            catch (Exception e)
            {
                Warn(body + "（弹窗失败：" + e.Message + "）");
            }
        }

        private static void ConsumeIngredients(List<GameItem> children)
        {
            for (int i = 0; i < children.Count; i++)
            {
                GameItem child = children[i];
                if (child == null) continue;

                bool used = BeerRecipes.IsBeerIngredient(child);
                if (!used)
                {
                    try { used = child.IsTag(WineIngredientTag) || child.IsTag(YeastTag); }
                    catch (Exception) { used = false; }
                }
                if (!used) continue;

                // 可复用酵母（酵母生物反应器）：像原版酒瓶那样还回去，而不是消耗掉
                bool reusable = false;
                try { reusable = child.IsTag("WINE_YEAST_REUSABLE_TAG"); } catch (Exception) { }

                try { if (child.parentInventory != null) child.parentInventory.Expel(child); } catch (Exception) { }

                if (reusable)
                {
                    try { GraphUtils.EmporiumTryAdd(child); } catch (Exception) { }
                    continue;
                }

                try { child.Destroy(); } catch (Exception) { }
            }
        }

        // ------------------------------------------------------------------ 每日推进
        private static void Tick(GameItem bottle)
        {
            AdvanceBrewing(bottle);
        }

        /// <summary>
        /// 推进发酵：剩余天数 - days，归零就出成品。返回 true 表示这件物品确实在酿造、这一夜被处理过。
        /// 供瓶子自己的过夜回调（原版 EndNight）和恒温发酵机共用。
        /// </summary>
        /// <param name="bonusScore">完成时的评分加成（恒温发酵机里完成 +1）。</param>
        /// <param name="days">这一夜走几天。</param>
        public static bool AdvanceBrewing(GameItem bottle, int bonusScore = 0, int days = 1)
        {
            if (bottle == null || !IsBrewing(bottle) || days <= 0) return false;

            try
            {
                int remaining = ReadInt(bottle, DaysTag) - days;
                if (remaining > 0)
                {
                    bottle.ModifyTag(DaysTag, state => state.Enable().SetInt(remaining));
                    return true;
                }

                FinishBrewing(bottle, bonusScore);
                return true;
            }
            catch (Exception e)
            {
                Warn("啤酒酿造推进失败：" + e.Message);
                return true;
            }
        }

        private static void FinishBrewing(GameItem bottle, int bonusScore)
        {
            int quality;

            // 有记录评分时才重新算档位（旧存档里酿着的瓶子没有评分标签，直接沿用原档位）
            bool hasScore = false;
            try { hasScore = bottle.IsTag(BeerRecipes.ScoreTag); } catch (Exception) { }

            if (bonusScore != 0 && hasScore)
                quality = BeerRecipes.QualityFromScore(ReadInt(bottle, BeerRecipes.ScoreTag) + bonusScore);
            else
                quality = ReadInt(bottle, BeerRecipes.QualityTag);

            bottle.ModifyTag(BrewingTag, state => state.SetInt(0));

            string resultId = quality == 3 ? BeerRecipes.PremiumCraftBeerId
                            : quality == 0 ? BeerRecipes.CraftBeerId
                            : quality == 1 ? BeerRecipes.HomebrewedBeerId
                            : BeerRecipes.PoisonId;

            BeerHelper.ReplaceWith(bottle, resultId, null, "酿成啤酒");
        }

        // ------------------------------------------------------------------ 状态
        public static bool IsBrewing(GameItem item)
        {
            return ReadInt(item, BrewingTag) == 1;
        }

        public static void ApplyBrewingSprite(GameItem item)
        {
            BeerHelper.SetModSprite(item, BrewingSprite);
        }

        public static void ClearBrewing(GameItem item)
        {
            if (item == null) return;
            try
            {
                item.ModifyTag(BrewingTag, state => state.SetInt(0));
                item.ModifyTag(DaysTag, state => state.SetInt(0));
            }
            catch (Exception)
            {
            }
        }

        private static int ReadInt(GameItem item, string key)
        {
            if (item == null) return 0;
            try { return item.GetTagReadonly(key).GetInt(); }
            catch (Exception) { return 0; }
        }

        private static void Warn(string message)
        {
            if (ModEntry.Instance != null && ModEntry.Instance.Log != null) ModEntry.Instance.Log.Warning(message);
        }
    }
}
