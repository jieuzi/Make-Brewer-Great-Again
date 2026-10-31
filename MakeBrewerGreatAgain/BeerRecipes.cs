using System;
using System.Collections.Generic;
using UnityEngine;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 啤酒线的“成品物品”与公用判定。
    /// 配方流程在 BeerBottleUi（干净瓶自己的界面），这里只负责：
    ///   - 三种成品（精酿啤酒 / 私酿啤酒 / 私酿毒药）的物品定义；
    ///   - 评分 → 品质档位的换算（水质分 + 营养果原料档位 + 啤酒花）；
    ///   - 酿好的果酿倒排水口 → 空啤酒瓶；
    ///   - 判断某个物品是不是啤酒原料（营养果 / 营养果块 / 营养果碎 / 啤酒花）。
    /// </summary>
    internal static class BeerRecipes
    {
        /// <summary>水质档位（0 精酿 / 1 私酿 / 2 毒药）。</summary>
        public const string QualityTag = "GMTB_BEER_QUALITY_INT";

        /// <summary>开始酿造时记下的评分，机器里完成发酵时用来 +1 分。</summary>
        public const string ScoreTag = "GMTB_BEER_SCORE_INT";

        public const string NutrifruitId = "nutrifruit";
        public const string ChunkId = "nutrifruit_chunk";

        /// <summary>啤酒花（原料，+1 分）。</summary>
        public const string HopsId = HopsCrop.ItemId;

        public const string CraftBeerId = "craft_beer";
        public const string PremiumCraftBeerId = "premium_craft_beer";
        public const string HomebrewedBeerId = "homebrewed_beer";
        public const string PoisonId = "homebrewed_poison";

        private const string CraftBeerSprite = "craft beer";
        private const string PremiumCraftBeerSprite = "Premium craft beer";
        private const string HomebrewedSprite = "homebrewed beer";
        private const string PoisonSprite = "homebrewed poison";

        // ------------------------------------------------------------------ 四个成品
        /// <summary>极品精酿：3 分产物，唯一带奢侈品标签、价格最高、可陈化。</summary>
        public static GameItem CreatePremiumCraftBeer()
        {
            return CreateBeerItem(PremiumCraftBeerId, "极品精酿", PremiumCraftBeerSprite, 300,
                luxury: true, junk: false, agable: true, ageValuePerDay: 30,
                desc: "顶级的啤酒，倒进杯子里泡沫细密得能挂住杯壁，入口是烘烤麦芽的甜，收尾带着一点松针和柑橘的苦味。据说整个空间站里喝过它的人不超过十个。");
        }

        public static GameItem CreateCraftBeer()
        {
            return CreateBeerItem(CraftBeerId, "精酿啤酒", CraftBeerSprite, 60,
                luxury: false, junk: false, agable: true, ageValuePerDay: 5,
                desc: "这种品质的啤酒在下层区可不多见，尽管如此，还是有不少人想要搞到尝尝。");
        }

        public static GameItem CreateHomebrewedBeer()
        {
            return CreateBeerItem(HomebrewedBeerId, "私酿啤酒", HomebrewedSprite, 20,
                luxury: false, junk: false, agable: false,
                desc: "比红魔鬼的量产货要好一点，但也就一点。");
        }

        public static GameItem CreatePoison()
        {
            return CreateBeerItem(PoisonId, "私酿毒药", PoisonSprite, 2,
                luxury: false, junk: true, agable: false,
                desc: "简直是在浪费营养果！光是闻闻都令人作呕！");
        }

        private static GameItem CreateBeerItem(string id, string name, string sprite, long price,
                                              bool luxury, bool junk, bool agable, string desc,
                                              int ageValuePerDay = 3)
        {
            GameItem item = ItemDirectory.CreateEmptyItem(id);
            item.name = name;
            item.shortDescription = desc;
            item.unitValue = price;

            item.SetSpriteAndShapeFromMod(ModId(), sprite);
            AudioHelper.InitGlass(item);

            if (junk)
            {
                item.SetGameItemType("JUNK");
            }
            else
            {
                item.SetGameItemType("SUBSTANCE");
                item.SetGameItemType("ALCOHOL");
                item.SetGameItemType("BREVAGE");
                if (luxury) item.SetGameItemType("LUXURY_ITEM");

                // 可以喝：喝掉后留下空啤酒瓶
                ConsumableHelper.InitAlcohol(item, 1);
                item.onActivateSlotItemFunc = (it, inv, slot) => DrinkAndKeepBottle(it);
            }

            if (agable)
            {
                AgableHelper.InitAgable(item, ageValuePerDay);
                item.onCycleEndSlotItemFunc = (it, inv, slot) =>
                {
                    try { AgableHelper.OnAge(it); } catch (Exception) { }
                };
            }

            // 三种啤酒都能倒进排水口换回空啤酒瓶
            BeerHelper.AddDrainSupport(item);
            return item;
        }

        private static void DrinkAndKeepBottle(GameItem beer)
        {
            try
            {
                int alcohol = 0;
                try { alcohol = beer.GetTagReadonly("ALCOHOL_VALUE").GetInt(); } catch (Exception) { }

                PlayerStore store = PlayerStore.Instance;
                if (store != null && store.healthData != null) store.healthData.ConsumeAlcohol(alcohol);

                BeerHelper.ReplaceWith(beer, BeerHelper.EmptyBottleId, null, "喝掉啤酒");
            }
            catch (Exception)
            {
            }
        }

        // ------------------------------------------------------------------ 原料评分 → 品质
        /// <summary>
        /// 啤酒评分：从 0 分起算
        ///   + 原版水质分（+1 / +1 / 0 / −1 / −2 / −3）
        ///   + 营养果原料：有营养果 −1；否则有营养果块 0；否则有营养果碎 +1
        ///   + 有啤酒花 +1
        /// 结果：≥3 精酿；2 ~ −1 私酿；≤−2 毒药。
        /// 必须在倒掉瓶里的液体之前调用（水质分要读瓶里的水）。
        /// </summary>
        public static int ScoreBeer(GameItem bottle, List<GameItem> children)
        {
            int score = 0;

            try { score += WineHelper.GetWaterQualityScore(bottle); }
            catch (Exception) { }

            bool hasNutrifruit = false;
            bool hasChunk = false;
            bool hasMashed = false;
            bool hasHops = false;

            if (children != null)
            {
                for (int i = 0; i < children.Count; i++)
                {
                    string id = Identifier(children[i]);
                    if (id == null) continue;

                    if (id == NutrifruitId) hasNutrifruit = true;
                    else if (id == ChunkId) hasChunk = true;
                    else if (id == NutrifruitCutting.MashedId) hasMashed = true;
                    else if (id == HopsId) hasHops = true;
                }
            }

            if (hasNutrifruit) score -= 1;
            else if (hasChunk) { }
            else if (hasMashed) score += 1;

            if (hasHops) score += 1;

            return score;
        }

        /// <summary>
        /// 评分 → 品质档：3 → 极品精酿（3）；2 → 精酿（0）；1 / 0 → 私酿（1）；&lt;0 → 私酿毒药（2）。
        /// 0/1/2 的旧含义保持不变，这样中途正在酿造的旧存档也不会错乱。
        /// </summary>
        public static int QualityFromScore(int score)
        {
            if (score >= 3) return 3;   // 极品精酿
            if (score == 2) return 0;   // 精酿啤酒
            if (score >= 0) return 1;   // 私酿啤酒
            return 2;                   // 私酿毒药
        }

        /// <summary>营养果原料的总千卡（营养果 / 营养果块 / 营养果碎）。</summary>
        public static int NutrifruitKcal(List<GameItem> children)
        {
            int kcal = 0;
            if (children == null) return 0;

            for (int i = 0; i < children.Count; i++)
            {
                GameItem child = children[i];
                if (child == null) continue;
                if (!IsBeerNutrifruit(child)) continue;

                try { kcal += child.GetTagReadonly("CALORIE_VALUE_TAG").GetInt(); }
                catch (Exception) { }
            }

            return kcal;
        }

        // ------------------------------------------------------------------ 公用判定
        /// <summary>
        /// 精酿 / 极品精酿的工具提示里补上原版酒瓶那两行"可陈化 / 已陈化 N 天"
        /// （酒瓶是在 WineHelper.CreateTooltip 里加的，啤酒不是酒瓶，所以走模组的工具提示钩子）。
        /// </summary>
        public static void AddAgingTooltip(RichTextBuilder builder, GameItem item)
        {
            if (builder == null || item == null) return;

            string id = Identifier(item);
            if (id != CraftBeerId && id != PremiumCraftBeerId) return;

            try
            {
                if (!AgableHelper.IsAgable(item)) return;

                AgableHelper.AddAgingInfoTooltipLine(builder);
                if (AgableHelper.HasAged(item))
                    AgableHelper.AddAgeTooltipLine(builder, item, "mech_wine_age");
            }
            catch (Exception)
            {
            }
        }

        /// <summary>营养果类原料：营养果 / 营养果块 / 营养果碎。</summary>
        public static bool IsBeerNutrifruit(GameItem item)
        {
            string id = Identifier(item);
            return id == NutrifruitId || id == ChunkId || id == NutrifruitCutting.MashedId;
        }

        /// <summary>啤酒花。</summary>
        public static bool IsHops(GameItem item)
        {
            return Identifier(item) == HopsId;
        }

        /// <summary>能放进啤酒瓶配方槽、并且会被消耗的原料。</summary>
        public static bool IsBeerIngredient(GameItem item)
        {
            return IsBeerNutrifruit(item) || IsHops(item);
        }

        private static string Identifier(GameItem item)
        {
            if (item == null) return null;
            try { return item.identifier; }
            catch (Exception) { return null; }
        }

        // ------------------------------------------------------------------ 排水口
        /// <summary>
        /// 酿好的果酿（荧光莓果酿）对排水口使用 → 换成原版空啤酒瓶。
        /// 原版只会把物品重置成“酒瓶”，所以这里抢在它前面接管。
        /// </summary>
        public static void AddDrainSupport(GameItem bottle)
        {
            if (bottle == null) return;

            Func<GameItem, GameItem, bool> previousMay = bottle.mayThisTargetItemFunc;
            Func<GameItem, GameItem, bool> previousCan = bottle.canThisTargetItemFunc;
            Action<GameItem, GameItem> previousOn = bottle.onThisTargetItemFunc;

            bottle.mayThisTargetItemFunc = (a, b) => IsFinishedDrain(bottle, a, b) || (previousMay != null && previousMay(a, b));
            bottle.canThisTargetItemFunc = (a, b) => IsFinishedDrain(bottle, a, b) || (previousCan != null && previousCan(a, b));
            bottle.onThisTargetItemFunc = (a, b) =>
            {
                if (IsFinishedDrain(bottle, a, b))
                {
                    BeerHelper.ReplaceWith(bottle, BeerHelper.EmptyBottleId, AudioHelper.DumpWater, "倒掉果酿");
                    return;
                }
                if (previousOn != null) previousOn(a, b);
            };
        }

        private static bool IsFinishedDrain(GameItem bottle, GameItem a, GameItem b)
        {
            if (bottle == null) return false;

            int state = 0;
            try { state = bottle.GetTagReadonly("WINE_CONTAINER_STATE").GetInt(); }
            catch (Exception) { }
            if (state != 2) return false;

            try
            {
                return (a != null && WaterHelper.IsDrain(a)) || (b != null && WaterHelper.IsDrain(b));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string ModId()
        {
            return ModEntry.Instance != null && ModEntry.Instance.Manifest != null
                ? ModEntry.Instance.Manifest.ID
                : "make_brewer_great_again";
        }
    }
}
