using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 给刀类工具（切肉刀等）加“切营养果”的功能：对营养果 / 巨型营养果使用，
    /// 按它的热量每 100 千卡切出 1 个营养果块。
    /// 原版刀类工具的屠宰交互挂在 HusbandryHelper.InitBladeItem 里（用 Delegate.Combine 追加），
    /// 所以这里也在同一个方法后面追加，互不干扰。
    /// </summary>
    internal static class NutrifruitCutting
    {
        public const string NutrifruitId = "nutrifruit";
        public const string ChunkId = "nutrifruit_chunk";
        public const string ChunkSpriteKey = "cutten_nutrifruit";
        public const string MashedId = "mashed_nutrifruit";
        public const string MashedSpriteKey = "mashed_nutrifruit";

        /// <summary>每 100 千卡切出 1 块。</summary>
        public const int CaloriesPerChunk = 100;

        /// <summary>已经切过的果实实例，防止一次操作切出两份。</summary>
        private static readonly HashSet<GameItem> cut = new HashSet<GameItem>();

        // ------------------------------------------------------------------ 物品工厂
        /// <summary>营养果块：每块 100 千卡的食物。</summary>
        public static GameItem CreateChunk()
        {
            GameItem item = ItemDirectory.CreateEmptyItem(ChunkId);
            item.name = "营养果块";
            item.shortDescription = "切开的营养果，没人愿意买这种边角料";
            item.unitValue = 2;

            // 贴图用模组里的 cutten_nutrifruit.png（Textures/Items 下的文件名就是键）
            item.SetSpriteAndShapeFromMod(ModId(), ChunkSpriteKey);

            // 原版按“每格中心像素”推导形状，图案居中时会漏判，
            // 这里直接强制成 2x2（贴图 32x32 = 2x2 格，刚好对齐）。
            try
            {
                var builder = new GridShapeBuilder(2, 2);
                builder.SetDataFill(2, 2, 1);
                item.SetShape(builder.shape);
            }
            catch (Exception e)
            {
                Warn("设置营养果块形状失败：" + e.Message);
            }

            AudioHelper.InitProduce(item);
            item.SetGameItemType("FOOD");
            FoodItemHelper.InitFoodItem(item, CaloriesPerChunk, false, false);

            try
            {
                GridShape shape = item.modifiedShape;
                if (shape != null) Log($"营养果块贴图 {ChunkSpriteKey}.png，占格 {shape.width}x{shape.height}，单价 {item.unitValue}");
            }
            catch (Exception)
            {
            }

            return item;
        }

        /// <summary>营养果碎：营养果块再切一半的产物，热量为原料的一半。</summary>
        public static GameItem CreateMashed()
        {
            GameItem item = ItemDirectory.CreateEmptyItem(MashedId);
            item.name = "营养果碎";
            item.shortDescription = "细细切出来的精华部分，没有一点发霉变质的地方，或许有人会被你的细心感动？";
            item.unitValue = 4;

            item.SetSpriteAndShapeFromMod(ModId(), MashedSpriteKey);

            // 贴图 32x32，直接强制成 2x2
            try
            {
                var builder = new GridShapeBuilder(2, 2);
                builder.SetDataFill(2, 2, 1);
                item.SetShape(builder.shape);
            }
            catch (Exception)
            {
            }

            AudioHelper.InitProduce(item);
            item.SetGameItemType("FOOD");
            FoodItemHelper.InitFoodItem(item, CaloriesPerChunk / 2, false, false);

            InitMashedInteractions(item);
            return item;
        }

        // ------------------------------------------------------------------ 营养果碎：双击对半切、拖到另一份上合并
        /// <summary>
        /// 仿照原版工业鱼饲料（IndustrialFishFeedHelper）的那套交互：
        ///   - 双击：千卡对半，生成另一份（原版是 5000 千卡起可切，这里只要有 2 千卡就能切）；
        ///   - 把一份拖到另一份上：来源的千卡并进目标，然后删掉来源。
        /// 原版饲料用 INdustrialFishFeedHelper.Split/Merge，这里用同样的委托字段自己实现。
        /// </summary>
        private static void InitMashedInteractions(GameItem item)
        {
            item.mayActivateSlotItemFunc = (it, inventory, slot) => CanSplitMashed(it);
            item.canActivateSlotItemFunc = (it, inventory, slot) => CanSplitMashed(it);
            item.onActivateSlotItemFunc = (it, inventory, slot) => SplitMashed(it);

            // 这一份拖到别的营养果碎上
            item.mayThisTargetItemFunc = (a, b) => CanMergeMashed(a, b);
            item.canThisTargetItemFunc = (a, b) => CanMergeMashed(a, b);
            item.onThisTargetItemFunc = (a, b) => MergeMashed(a, b);

            // 别的营养果碎拖到这一份上
            item.mayItemTargetThisFunc = (a, b) => CanMergeMashed(a, b);
            item.canItemTargetThisFunc = (a, b) => CanMergeMashed(a, b);
            item.onItemTargetThisFunc = (a, b) => MergeMashed(a, b);
        }

        private static bool IsMashed(GameItem item)
        {
            if (item == null) return false;
            try { return item.identifier == MashedId; }
            catch (Exception) { return false; }
        }

        private static int GetCalories(GameItem item)
        {
            if (item == null) return 0;
            try { return item.GetTagReadonly("CALORIE_VALUE_TAG").GetInt(); }
            catch (Exception) { return 0; }
        }

        private static bool IsOwned(GameItem item)
        {
            try { return item != null && GeneralHelper.IsItemOwned(item); }
            catch (Exception) { return false; }
        }

        private static bool CanSplitMashed(GameItem item)
        {
            return IsMashed(item) && IsOwned(item) && GetCalories(item) >= 2;
        }

        /// <summary>双击：一分为二，各拿一半千卡。</summary>
        private static void SplitMashed(GameItem item)
        {
            if (!CanSplitMashed(item)) return;

            try
            {
                int total = GetCalories(item);
                int half = total / 2;

                GameItem created = DirectoryMaster.Item(MashedId, true);
                if (created == null) return;

                FoodItemHelper.SetCalories(created, half);
                FoodItemHelper.SetCalories(item, total - half);

                GraphUtils.EmporiumTryAdd(created);

                var audio = AudioManager.Instance;
                if (audio != null) audio.Play3("click_vintage");
            }
            catch (Exception e)
            {
                Warn("对半切营养果碎失败：" + e.GetType().Name + ": " + e.Message);
            }
        }

        private static bool CanMergeMashed(GameItem source, GameItem target)
        {
            if (!IsMashed(source) || !IsMashed(target)) return false;
            if (ReferenceEquals(source, target)) return false;
            if (!IsOwned(source) || !IsOwned(target)) return false;
            return GetCalories(source) > 0;
        }

        /// <summary>
        /// 把来源的千卡并进目标，然后删掉来源。
        ///
        /// 注意：GameItem.Target 会依次调用 both 方向的委托（this 的 onThisTargetItemFunc 和
        /// 目标身上的 onItemTargetThisFunc），所以合并会被触发两次。这里先把来源的千卡清零再删，
        /// 第二次进来时 CanMergeMashed 的 "来源千卡 > 0" 就不成立，不会重复加。
        /// </summary>
        private static void MergeMashed(GameItem source, GameItem target)
        {
            if (!CanMergeMashed(source, target)) return;

            try
            {
                int kcal = GetCalories(source);
                if (kcal <= 0) return;

                FoodItemHelper.SetCalories(target, GetCalories(target) + kcal);

                // 先清零，避免同一次交互里被第二个委托再合并一次
                FoodItemHelper.SetCalories(source, 0);
                source.Destroy();

                var audio = AudioManager.Instance;
                if (audio != null) audio.Play3("click_vintage");
            }
            catch (Exception e)
            {
                Warn("合并营养果碎失败：" + e.GetType().Name + ": " + e.Message);
            }
        }
        // ------------------------------------------------------------------ 刀类工具追加交互
        public static void AddBladeSupport(GameItem blade)
        {
            if (blade == null) return;

            Func<GameItem, GameItem, bool> previousMay = blade.mayThisTargetItemFunc;
            Func<GameItem, GameItem, bool> previousCan = blade.canThisTargetItemFunc;
            Action<GameItem, GameItem> previousOn = blade.onThisTargetItemFunc;

            blade.mayThisTargetItemFunc = (self, target) =>
                IsCuttableTarget(target) || (previousMay != null && previousMay(self, target));
            blade.canThisTargetItemFunc = (self, target) =>
                IsCuttableTarget(target) || (previousCan != null && previousCan(self, target));
            blade.onThisTargetItemFunc = (self, target) =>
            {
                if (IsCuttableTarget(target))
                {
                    CutInto(target);
                    return;
                }
                if (previousOn != null) previousOn(self, target);
            };
        }

        // ------------------------------------------------------------------ 实现
        private static bool IsCuttableTarget(GameItem target)
        {
            if (target == null) return false;

            try
            {
                if (!GeneralHelper.IsItemOwned(target)) return false;
                string id = target.identifier;
                return id == NutrifruitId || id == ChunkId || target.IsTag("NUTRIFRUIT_TAG");
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>按目标类型分派：营养果 → 切块；营养果块 → 再切碎。</summary>
        private static void CutInto(GameItem target)
        {
            if (target == null) return;
            if (!cut.Add(target)) return;

            try
            {
                string id = target.identifier;
                if (id == ChunkId) MashChunk(target);
                else CutNutrifruit(target);
            }
            catch (Exception e)
            {
                Warn("切割营养果失败：" + e.Message);
            }
        }

        /// <summary>营养果 → 每 100 千卡一块营养果块。</summary>
        private static void CutNutrifruit(GameItem fruit)
        {
            int kcal = ReadCalories(fruit, 240);
            int pieces = Mathf.Max(1, kcal / CaloriesPerChunk);

            GameInventory inventory = fruit.parentInventory;
            GridShape shape = ShapeOf(fruit);
            if (inventory != null) inventory.Expel(fruit);

            int made = 0;
            for (int i = 0; i < pieces; i++)
            {
                GameItem chunk = DirectoryMaster.Item(ChunkId, true);
                if (chunk == null) break;
                Place(inventory, shape, chunk);
                made++;
            }

            fruit.Destroy();
            PlayCutSound(null);
        }

        /// <summary>营养果块 → 营养果碎，热量减半。</summary>
        private static void MashChunk(GameItem chunk)
        {
            int kcal = ReadCalories(chunk, CaloriesPerChunk);
            int mashedKcal = Mathf.Max(1, kcal / 2);

            GameInventory inventory = chunk.parentInventory;
            GridShape shape = ShapeOf(chunk);
            if (inventory != null) inventory.Expel(chunk);

            GameItem mashed = DirectoryMaster.Item(MashedId, true);
            if (mashed != null)
            {
                try { FoodItemHelper.SetCalories(mashed, mashedKcal); } catch (Exception) { }
                Place(inventory, shape, mashed);
            }

            chunk.Destroy();
            PlayCutSound(null);
        }

        private static GridShape ShapeOf(GameItem item)
        {
            try
            {
                return item != null && item.modifiedShape != null ? item.modifiedShape.Clone() : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Place(GameInventory inventory, GridShape shape, GameItem item)
        {
            if (item == null) return;

            bool placed = false;
            if (inventory != null && shape != null)
            {
                SlotMarker slot = inventory.TryInventorySlot(item, shape, null);
                if (slot != null && slot.TryAcceptOnce(-1) > 0) placed = true;
            }
            if (!placed) GraphUtils.EmporiumTryAdd(item);

            item.Validate();
        }

        private static int ReadCalories(GameItem item, int fallback)
        {
            try
            {
                int kcal = item.GetTagReadonly("CALORIE_VALUE_TAG").GetInt();
                return kcal > 0 ? kcal : fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private static void PlayCutSound(GameItem blade)
        {
            try
            {
                if (!AudioManager.Exists) return;
                AudioManager audio = AudioManager.Instance;
                if (audio == null) return;

                string own = null;
                try { own = blade != null ? blade.soundUse : null; } catch (Exception) { }

                var candidates = new List<string>();
                if (!string.IsNullOrEmpty(own)) candidates.Add(own);
                candidates.Add(AudioHelper.BLADE_DROP);
                if (candidates.Count > 0) audio.TryPlayOne(candidates.ToArray());
            }
            catch (Exception)
            {
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

    /// <summary>刀类工具初始化之后追加“切营养果”的交互（原版屠宰逻辑保持不变）。</summary>
    [HarmonyPatch(typeof(HusbandryHelper), "InitBladeItem")]
    internal static class BladeNutrifruitPatch
    {
        private static void Postfix(GameItem item)
        {
            NutrifruitCutting.AddBladeSupport(item);
        }
    }
}
