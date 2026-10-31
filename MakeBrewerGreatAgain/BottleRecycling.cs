using System;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 人脉升级"空瓶回收项目"。
    /// 王尔德牵线疏通红魔鬼酿酒厂的关系，之后玩家能以较低价格从酒厂回收用过的旧瓶子。
    /// 卡片与原版"红魔鬼酿酒厂"升级同属"引荐"卡，放在"基础"标签页，也会出现在推荐横幅里。
    ///
    /// 注意：不能在 Init / OnEnable 里注册。此时主菜单还没有 PlayerStore，
    /// NetworkUpgrade.RegisterModUpgrade 内部会访问 PlayerStore.Instance 并抛 NullReferenceException，
    /// 而 OnEnable 抛异常会让整个模组被判为载入失败（物品、补丁全都不生效）。
    /// 所以改在"一局游戏就绪"的钩子里注册：载入存档走 OnGameLoadedInit，新开局/载入都会走 OnDifficultyApplied。
    /// </summary>
    internal static class BottleRecycling
    {
        public const string UpgradeId = "give_me_the_bottle.EMPTY_BOTTLE_RECYCLING";

        public const int Cost = 100;

        /// <summary>原版副标题键：引荐（BREWER / BREWER_SHAREHOLDER 所在的那张卡）。</summary>
        private const string CardSubtitle = "Introduction";

        /// <summary>Textures/Items 或本模组自注册的精灵图键。</summary>
        private const string IconName = "beer bottle";

        /// <summary>升级状态随存档按 ID 保存，所以直接问游戏，不要自己缓存布尔。</summary>
        public static bool Unlocked
        {
            get
            {
                try { return ModHelper.IsNetworkUpgradeUnlocked(UpgradeId); }
                catch (Exception) { return false; }
            }
        }

        public static void RegisterHooks()
        {
            ModHook.OnGameLoadedInit += OnRunReady;
            ModHook.OnDifficultyApplied += OnDifficultyApplied;
        }

        public static void UnregisterHooks()
        {
            ModHook.OnGameLoadedInit -= OnRunReady;
            ModHook.OnDifficultyApplied -= OnDifficultyApplied;
        }

        public static void Unregister(string modId)
        {
            if (string.IsNullOrEmpty(modId)) return;

            try { ModHelper.RemoveNetworkUpgrades(modId); }
            catch (Exception) { }
        }

        private static void OnRunReady()
        {
            TryRegister();
        }

        private static void OnDifficultyApplied(string difficultyId)
        {
            TryRegister();
        }

        /// <summary>幂等：同一 ID 重复注册只会替换定义，玩家买了的状态按存档保留。</summary>
        public static void TryRegister()
        {
            try
            {
                string modId = ModEntry.Instance != null && ModEntry.Instance.Manifest != null
                    ? ModEntry.Instance.Manifest.ID
                    : "make_brewer_great_again";

                ModHelper.AddNetworkUpgrade(
                    modId,
                    UpgradeId,
                    cost: Cost,
                    subtitle: CardSubtitle,
                    title: () => "空瓶回收项目",
                    description: () => "王尔德向你提议，他可以帮你联系红魔鬼酿酒厂的工人，他可以通过”合理损耗“，偷偷运些空瓶子出来，你现在可以从他手里回收那些用过的空瓶子。\n--由于酿酒师的大手，空间站内的玻璃瓶比金子都要珍贵，所以你甚至要用空啤酒瓶酿酒--",
                    onUnlock: OnUnlock,
                    iconKey: modId + ":" + IconName,
                    prerequisites: null,
                    cooldownDays: 0,
                    recommended: true);

                // 已经买过本升级的存档：把 114514 库房电话补进号码表（名片只在升级时发一次）
                if (Unlocked) RedImpWarehouse.EnsureProgram();
            }
            catch (Exception e)
            {
                if (ModEntry.Instance != null && ModEntry.Instance.Log != null)
                    ModEntry.Instance.Log.Warning("人脉升级注册失败：" + e.GetType().Name + ": " + e.Message);
            }
        }

        private static void OnUnlock()
        {
            // 买下本升级：建库房电话 + 发一张名片（王尔德牵线的结果）
            RedImpWarehouse.EnsureProgram();
        }
    }
}
