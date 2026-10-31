using System;
using UnityEngine;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 啤酒花：一种水培作物。
    ///   - 掉落物 / 原料：hops（食物 + 奢侈品，15 块，2x2 格）；
    ///   - 种子：hops_seed（走游戏自己的水培系统：SEED_ITEM_TAG + PLANT_ID_STRING）；
    ///   - 作物定义：PlantRegistry.Add(PlantDefinition)，这是游戏自带的公开注册表，
    ///     所以种植、生长、营养液、水质、收获都走原版水培机，不需要改机器逻辑。
    ///
    /// 唯一需要打补丁的是「作物在花盆里的外观」：原版按 harvestItemId + "_p" / "_p2"
    /// 去 Items/items_hydroponic_plant 图集里取图，模组没法往原版图集里加图，
    /// 所以 HopsPlantSpritePatch 会把这个名字换成我们自己的贴图。
    /// </summary>
    internal static class HopsCrop
    {
        /// <summary>啤酒花（收获物 / 啤酒原料）。</summary>
        public const string ItemId = "hops";

        /// <summary>啤酒花种子，同时也是 PlantDefinition 的 id（原版也是这么做的）。</summary>
        public const string SeedId = "hops_seed";

        /// <summary>Textures/Items 里的文件名（种子专用图）。</summary>
        private const string SeedSpriteKey = "hops_seed";

        /// <summary>啤酒花本体图（没有种子图时兜底）。</summary>
        private const string SpriteKey = "hops";

        /// <summary>原版水培机的作物图集。</summary>
        public const string PlantAtlas = "Items/items_hydroponic_plant";

        private const int Calories = 10;

        /// <summary>每轮结果掉一个种子的概率（%）。</summary>
        private const int SeedDropPercent = 10;

        // ------------------------------------------------------------------ 注册
        /// <summary>把啤酒花加进 PlantRegistry。重复调用安全。</summary>
        public static void RegisterPlant()
        {
            try
            {
                if (PlantRegistry.Contains(SeedId)) return;

                // 数值按你给的：生长周期 21、一次收 4 个、不返还种子、多年生（Refruit 10 抄蓝莓），
                // 营养/污染/先天特质照抄咖啡，收获逻辑用原版默认（数量/品质/模块加成都在里面）+ 10% 掉种子。
                PlantDefinition plant = new PlantDefinition(SeedId, "啤酒花", ItemId, 21, 4)
                    .Nutriment(250, 75, 30)
                    .Contaminant(1.01)
                    .Refruit(10)
                    .Innate("perennial", 1)
                    .Innate(PlantTraitList.ProductionId("caffeine"), 1)
                    .Innate(PlantTraitList.ProductionId("coffee_mush"), 1)
                    .Innate("potent", 1)
                    .OnHarvest(HarvestHops);

                PlantRegistry.Add(plant);
            }
            catch (Exception e)
            {
                Warn("注册啤酒花作物失败：" + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>原版默认收获（数量/品质/模块加成）+ 10% 额外掉一个种子。</summary>
        private static void HarvestHops(PlantHarvestContext ctx)
        {
            PlantHarvest.DefaultHarvest(ctx);

            try
            {
                if (UnityEngine.Random.Range(0, 100) < SeedDropPercent)
                    ctx.OutputById(SeedId, 1);
            }
            catch (Exception e)
            {
                Warn("啤酒花种子掉落失败：" + e.GetType().Name + ": " + e.Message);
            }
        }

        // ------------------------------------------------------------------ 物品
        /// <summary>啤酒花：食物 + 奢侈品，15 块，2x2 格。</summary>
        public static GameItem CreateHops()
        {
            GameItem item = ItemDirectory.CreateEmptyItem(ItemId);
            item.name = "啤酒花";
            item.shortDescription = "酿造啤酒的关键原料，但在太空站上很难搞到，传说红魔鬼酒厂的老板曾经是个垃圾佬，不知道从哪搞到了一包啤酒花种子，这才有了现在的红魔鬼酒厂。";
            item.unitValue = 15;

            item.SetSpriteAndShapeFromMod(ModId(), SpriteKey);
            ForceSquareShape(item, 1);

            AudioHelper.InitProduce(item);
            item.SetGameItemType("FOOD");
            item.SetGameItemType("LUXURY_ITEM");
            FoodItemHelper.InitFoodItem(item, Calories, false, false);

            return item;
        }

        /// <summary>啤酒花种子：走原版水培系统的种子（SEED_ITEM_TAG + PLANT_ID_STRING）。</summary>
        public static GameItem CreateSeed()
        {
            // InitSeedItem 会去 PlantRegistry 查作物，所以先确保注册过
            RegisterPlant();

            GameItem seed = ItemDirectory.CreateEmptyItem(SeedId);
            seed.name = "啤酒花种子";

            // 种子图 16x16（Textures/Items/hops_seed.png），没有就用啤酒花本体图
            string seedSprite = HasSprite(SeedSpriteKey) ? SeedSpriteKey : SpriteKey;
            seed.SetSpriteAndShapeFromMod(ModId(), seedSprite);
            ForceSquareShape(seed, 1);

            // 原版的顺序：先打基础数据，再写描述、类型、价格
            HydroponicHelper.InitSeedItem(seed, SeedId);

            seed.shortDescription = "没人会相信这包垃圾会是啤酒花种子，就像没人真的相信红魔鬼酒厂的传说一样。";
            seed.SetGameItemType("SEED");
            seed.SetGameItemType("JUNK");
            seed.unitValue = 0;

            return seed;
        }

        /// <summary>贴图直接强制成 size x size 格（原版按每格中心像素推导，居中图案会漏判）。</summary>
        private static void ForceSquareShape(GameItem item, int size)
        {
            try
            {
                var builder = new GridShapeBuilder(size, size);
                builder.SetDataFill(size, size, 1);
                item.SetShape(builder.shape);
            }
            catch (Exception e)
            {
                Warn("设置啤酒花形状失败：" + e.Message);
            }
        }

        /// <summary>贴图 32x32 直接强制成 2x2 格（原版按每格中心像素推导，居中图案会漏判）。</summary>
        private static void ForceSquareShape(GameItem item)
        {
            try
            {
                var builder = new GridShapeBuilder(2, 2);
                builder.SetDataFill(2, 2, 1);
                item.SetShape(builder.shape);
            }
            catch (Exception e)
            {
                Warn("设置啤酒花形状失败：" + e.Message);
            }
        }

        // ------------------------------------------------------------------ 作物外观
        /// <summary>按原版命名（harvestItemId + "_p" / "_p2"）从模组贴图里取作物外观。</summary>
        public static Sprite FindPlantSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;

            // 先找专门画的生长图（hops_p2 / hops_p），没有就用物品图（hops）
            string[] keys = { spriteName, "hops" };

            try
            {
                var mods = ModLoader.Mods;
                if (mods == null) return null;

                string modId = ModId();
                foreach (LoadedMod mod in mods)
                {
                    if (mod == null || mod.Manifest == null) continue;
                    if (mod.Manifest.ID != modId) continue;
                    if (mod.Sprites == null) return null;

                    foreach (string key in keys)
                    {
                        if (!mod.Sprites.ContainsKey(key)) continue;
                        return mod.Sprites[key];
                    }
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        /// <summary>模组贴图里有没有这个键（用来判断玩家有没有画专用图）。</summary>
        private static bool HasSprite(string key)
        {
            try
            {
                var mods = ModLoader.Mods;
                if (mods == null) return false;

                string modId = ModId();
                foreach (LoadedMod mod in mods)
                {
                    if (mod == null || mod.Manifest == null) continue;
                    if (mod.Manifest.ID != modId) continue;
                    return mod.Sprites != null && mod.Sprites.ContainsKey(key);
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        private static string ModId()
        {
            try
            {
                if (ModEntry.Instance != null && ModEntry.Instance.Manifest != null)
                    return ModEntry.Instance.Manifest.ID;
            }
            catch (Exception)
            {
            }

            return "make_brewer_great_again";
        }

        private static void Warn(string message)
        {
            if (ModEntry.Instance != null && ModEntry.Instance.Log != null)
                ModEntry.Instance.Log.Warning(message);
        }
    }
}
