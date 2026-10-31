using System;
using System.Collections.Generic;
using UnityEngine;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 三个物品的关系：
    ///   红魔鬼啤酒(red_beer) 对排水口使用 → 原版空啤酒瓶(empty_beer_bottle)；
    ///   原版空啤酒瓶 对水龙头使用 → 干净的空啤酒瓶(clean_empty_beer_bottle)；
    ///   干净的空啤酒瓶 = 唯一的酿酒容器（600ml，酿酒等级系统沿用原版酒瓶那套）。
    /// </summary>
    internal static class BeerHelper
    {
        public const string BeerId = "red_beer";
        public const string EmptyBottleId = "empty_beer_bottle";
        public const string CleanBottleId = "clean_empty_beer_bottle";

        /// <summary>干净瓶容量 600ml（原版酒瓶是 750ml）。</summary>
        public const int CleanBottleCapacityMl = 600;

        /// <summary>开始发酵要求的最低液量：接近满瓶即可（原版是 749/750）。</summary>
        public const int CleanBottleMinMl = 594;

        // 模组贴图（Textures/Items/ 下的文件名）
        private const string SpriteEmpty = "clear_empty_beer_bottle";
        private const string SpriteFermenting = "beer_fermenting";
        private const string SpriteBottleFormat = "beer_bottle{0}";

        /// <summary>已经转换过的物品实例，防止同一次交互重复换物品。</summary>
        private static readonly HashSet<GameItem> converted = new HashSet<GameItem>();

        // ------------------------------------------------------------------ 物品工厂
        /// <summary>干净的空啤酒瓶：600ml，贴图来自模组，酿酒行为复用酒瓶的实现。</summary>
        public static GameItem CreateCleanBottle()
        {
            GameItem item = ItemDirectory.CreateEmptyItem(CleanBottleId);
            item.name = "干净的空啤酒瓶";
            item.shortDescription = "冲洗干净的空啤酒瓶，可以拿去酿酒。";
            item.unitValue = 12;
            AudioHelper.InitGlass(item);

            // 酒瓶那套初始化：液体容器、酿酒标签、倒水/排水口交互、陈化。
            // 窗口会在 InitWineBottle 的补丁里换成我们自己的（BeerBottleUi）。
            WineHelper.InitWineBottle(item);

            // 容量改成 600ml（原版 InitWineBottle 会写成 750ml）
            ApplyCleanBottleCapacity(item);

            // 贴图用模组里的，形状按不透明像素推导
            item.SetSpriteAndShapeFromMod(ModId(), SpriteEmpty);

            // 酒标叠加层：酿好后按等级/假酒显示，位置与瓶身同画布（0,0），和原版一致
            item.getItemSpriteModifierForItemFunc = BuildLabels;

            // 酿好的果酿倒进排水口 → 原版空啤酒瓶（而不是原版重置成的酒瓶）
            BeerRecipes.AddDrainSupport(item);


            return item;
        }

        /// <summary>酒标叠加：只有“酿好”状态才画，假酒用红标。</summary>
        private static List<ItemSpriteModifier> BuildLabels(GameItem item)
        {
            var labels = new List<ItemSpriteModifier>();

            try
            {
                if (item == null) return labels;
                if (ReadState(item) != 2) return labels;

                bool counterfeit = item.IsTag("WINE_COUNTERFEIT_TAG");
                string key;

                if (counterfeit)
                {
                    key = "beer_label_red";
                }
                else
                {
                    int tier = 0;
                    try { tier = WineHelper.GetWineQualityTier(item); } catch (Exception) { }
                    if (tier < 0) tier = 0;
                    if (tier > 5) tier = 5;
                    key = "beer_label" + tier;
                }

                Sprite label = SheetSprites.Get(key);
                if (label != null)
                {
                    labels.Add(new ItemSpriteModifier(label, Vector2.zero, 0, false, 0, false, Color.white));
                }
            }
            catch (Exception)
            {
            }

            return labels;
        }

        // ------------------------------------------------------------------ 容量
        /// <summary>
        /// 把干净瓶的最大容量固定为 600ml。容量实际存在标签 LIQUID_CONTAINER_CAPACITY 里，
        /// 单位是 1/1000 ml（原版酒瓶写的是 750000），所以 600ml = 600000。
        /// </summary>
        public static void ApplyCleanBottleCapacity(GameItem item)
        {
            if (item == null) return;

            try
            {
                ForceCleanBottleCapacityTag(item);
                int actual = WaterHelper.GetTotalCapacityML(item);
                Log($"干净的空啤酒瓶容量：{actual}ml（目标 {CleanBottleCapacityMl}ml）");
            }
            catch (Exception e)
            {
                Warn("设置容量失败：" + e.Message);
            }
        }

        /// <summary>只改写容量标签，不写日志（给高频调用处用）。</summary>
        public static void ForceCleanBottleCapacityTag(GameItem item)
        {
            if (item == null) return;

            try
            {
                int value = CleanBottleCapacityMl * 1000;

                // 实际装多少看这个（单位：1/1000 ml）
                item.ModifyTag("LIQUID_CONTAINER_CAPACITY", state => state.Enable().SetInt(value));

                // 提示框“水：x/750ml”里的分母看这个（单位：ml）
                item.ModifyTag("LIQUID_CONTAINER_MAX", state => state.Enable().SetInt(CleanBottleCapacityMl));
            }
            catch (Exception)
            {
            }
        }

        // ------------------------------------------------------------------ 贴图
        public static bool IsCleanBottle(GameItem item)
        {
            if (item == null) return false;
            try { return item.identifier == CleanBottleId; } catch (Exception) { return false; }
        }

        /// <summary>把物品贴图换成模组里的某张（同时按不透明像素重算占格形状）。</summary>
        public static void SetModSprite(GameItem item, string spriteKey)
        {
            if (item == null || string.IsNullOrEmpty(spriteKey)) return;
            try { item.SetSpriteAndShapeFromMod(ModId(), spriteKey); }
            catch (Exception e) { Warn("换贴图失败：" + e.Message); }
        }

        public static void ApplyEmptySprite(GameItem item)
        {
            SetModSprite(item, SpriteEmpty);
        }

        public static void ApplyFermentingSprite(GameItem item)
        {
            SetModSprite(item, SpriteFermenting);
        }

        public static void ApplyFinishedSprite(GameItem item)
        {
            int tier = 0;
            try { tier = WineHelper.GetWineQualityTier(item); } catch (Exception) { }
            if (tier < 0) tier = 0;
            if (tier > 5) tier = 5;
            SetModSprite(item, string.Format(SpriteBottleFormat, tier));
        }

        /// <summary>酿好后的名字：与原版酒区分开，标明是啤酒瓶装的。</summary>
        public const string FinishedWineName = "荧光莓果酿（啤酒瓶）";

        /// <summary>空瓶/发酵中的名字。</summary>
        public const string CleanBottleName = "干净的空啤酒瓶";

        /// <summary>
        /// 按 WINE_CONTAINER_STATE 把干净瓶的贴图重新压成模组里的那一套：
        /// 0 空瓶 / 1 发酵中 / 2 已酿好（按等级）。
        /// 必须在每次原版改动贴图之后调用——原版 SetSpriteAndShape 只改图集和精灵名，
        /// 不会清掉物品的“模组贴图”标记，渲染时就会拿图集路径当 modId 查不到而变占位图。
        /// </summary>
        public static void RefreshCleanBottleSprite(GameItem item)
        {
            if (!IsCleanBottle(item)) return;

            int state = ReadState(item);

            // 啤酒自酿中：显示 brewing beer 贴图
            if (BeerBottleUi.IsBrewing(item))
            {
                BeerBottleUi.ApplyBrewingSprite(item);
                return;
            }

            if (state == 1) ApplyFermentingSprite(item);
            else if (state == 2) ApplyFinishedSprite(item);
            else ApplyEmptySprite(item);

            if (state == 2) RenameFinishedWine(item);
            else RestoreCleanBottleName(item);
        }

        /// <summary>倒空/喝空后把名字和价格改回“干净的空啤酒瓶”，并清掉啤酒配方标记。</summary>
        public static void RestoreCleanBottleName(GameItem item)
        {
            if (!IsCleanBottle(item)) return;

            try
            {
                BeerBottleUi.ClearBrewing(item);

                string name = item.name;
                if (name == FinishedWineName || name == "精酿啤酒" || name == "私酿啤酒" || name == "私酿毒药")
                {
                    item.name = CleanBottleName;
                    item.unitValue = 12;
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>把酿好的酒改名成“荧光莓果酿（啤酒瓶）”。</summary>
        public static void RenameFinishedWine(GameItem item)
        {
            if (!IsCleanBottle(item)) return;

            try
            {
                if (item.name == FinishedWineName) return;
                item.name = FinishedWineName;
                Log($"酿酒完成：{FinishedWineName}");
            }
            catch (Exception)
            {
            }
        }

        private static int ReadState(GameItem item)
        {
            try { return item.GetTagReadonly("WINE_CONTAINER_STATE").GetInt(); }
            catch (Exception) { return 0; }
        }

        /// <summary>配方槽里有没有酿酒原料（或非法酵母，原版允许它跳过原料要求）。</summary>
        public static bool HasFermentationIngredients(GameItem bottle)
        {
            try
            {
                List<GameItem> children = GraphUtils.FindAllChildrenType<GameItem>(bottle, null, null);
                if (children == null) return false;

                for (int i = 0; i < children.Count; i++)
                {
                    GameItem child = children[i];
                    if (child == null) continue;
                    if (child.IsTag("WINE_INGREDIENT_TAG")) return true;
                    if (child.IsTag("WINE_YEAST_ILLEGAL_TAG")) return true;
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        // ------------------------------------------------------------------ 红魔鬼啤酒 → 排水口
        public static void AddDrainSupport(GameItem beer)
        {
            if (beer == null) return;

            beer.mayThisTargetItemFunc = (a, b) => HasTarget(a, b, WaterHelper.IsDrain);
            beer.canThisTargetItemFunc = (a, b) => HasTarget(a, b, WaterHelper.IsDrain);
            beer.onThisTargetItemFunc = (a, b) =>
            {
                if (HasTarget(a, b, WaterHelper.IsDrain)) ReplaceWith(beer, EmptyBottleId, AudioHelper.DumpWater, "倒掉啤酒");
            };

            beer.mayItemTargetThisFunc = (a, b) => HasTarget(a, b, WaterHelper.IsDrain);
            beer.canItemTargetThisFunc = (a, b) => HasTarget(a, b, WaterHelper.IsDrain);
            beer.onItemTargetThisFunc = (a, b) =>
            {
                if (HasTarget(a, b, WaterHelper.IsDrain)) ReplaceWith(beer, EmptyBottleId, AudioHelper.DumpWater, "倒掉啤酒");
            };

            Log("红魔鬼啤酒已支持对排水口使用（得到空啤酒瓶）。");
        }

        // ------------------------------------------------------------------ 空啤酒瓶 → 水龙头
        public static void AddFaucetSupport(GameItem emptyBottle)
        {
            if (emptyBottle == null) return;

            emptyBottle.mayThisTargetItemFunc = (a, b) => HasTarget(a, b, WaterHelper.IsFaucet);
            emptyBottle.canThisTargetItemFunc = (a, b) => HasTarget(a, b, WaterHelper.IsFaucet);
            emptyBottle.onThisTargetItemFunc = (a, b) =>
            {
                if (HasTarget(a, b, WaterHelper.IsFaucet)) ReplaceWith(emptyBottle, CleanBottleId, AudioHelper.UseFaucet, "清洗空瓶");
            };

            emptyBottle.mayItemTargetThisFunc = (a, b) => HasTarget(a, b, WaterHelper.IsFaucet);
            emptyBottle.canItemTargetThisFunc = (a, b) => HasTarget(a, b, WaterHelper.IsFaucet);
            emptyBottle.onItemTargetThisFunc = (a, b) =>
            {
                if (HasTarget(a, b, WaterHelper.IsFaucet)) ReplaceWith(emptyBottle, CleanBottleId, AudioHelper.UseFaucet, "清洗空瓶");
            };

            Log("空啤酒瓶已支持对水龙头使用（得到干净的空啤酒瓶）。");
        }

        // ------------------------------------------------------------------ 内部
        private static bool HasTarget(GameItem a, GameItem b, Func<GameItem, bool> predicate)
        {
            try
            {
                return (a != null && predicate(a)) || (b != null && predicate(b));
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void ReplaceWith(GameItem source, string newItemId, Action playSound, string actionName)
        {
            if (source == null) return;
            if (!converted.Add(source)) return;

            try
            {
                GameInventory inventory = source.parentInventory;
                GridShape shape = null;
                try
                {
                    shape = source.modifiedShape != null ? source.modifiedShape.Clone() : null;
                }
                catch (Exception)
                {
                }

                if (inventory != null) inventory.Expel(source);

                GameItem created = DirectoryMaster.Item(newItemId, true);
                if (created == null)
                {
                    Warn($"生成 {newItemId} 失败。");
                    return;
                }

                bool placed = false;
                if (inventory != null && shape != null)
                {
                    SlotMarker slot = inventory.TryInventorySlot(created, shape, null);
                    if (slot != null && slot.TryAcceptOnce(-1) > 0) placed = true;
                }
                if (!placed) GraphUtils.EmporiumTryAdd(created);

                created.Validate();

                // 原来的物品被消耗掉（它已经被 Expel 出容器）
                try { source.Destroy(); } catch (Exception) { }

                if (playSound != null) playSound();
                Log($"{actionName}：{source.name} → {created.name}");
            }
            catch (Exception e)
            {
                Warn($"{actionName}失败：" + e.Message);
            }
        }

        private static string ModId()
        {
            return ModEntry.Instance != null && ModEntry.Instance.Manifest != null
                ? ModEntry.Instance.Manifest.ID
                : "make_brewer_great_again";
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
