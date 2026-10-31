using System;
using HarmonyLib;
using UnityEngine;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 模组入口。
    /// 三个物品的关系：红魔鬼啤酒 --排水口--> 空啤酒瓶 --水龙头--> 干净的空啤酒瓶（唯一可酿酒的）。
    /// </summary>
    public class ModEntry : IMod
    {
        public static ModEntry Instance { get; private set; }

        public ModManifest Manifest { get; private set; }

        public ModLog Log { get; private set; }

        public void Init(ModManifest manifest)
        {
            Instance = this;
            Manifest = manifest;
            Log = new ModLog(manifest);
           
        }

        public void OnEnable()
        {
            // 注册新物品：干净的空啤酒瓶
            ModHook.OnModItemDirectoryInit += RegisterItems;

            // 资源全部就绪后，把一张图集切成本模组要用的子精灵
            ModHook.OnModAssetsLoaded += OnAssetsLoaded;

            // 人脉升级：空瓶回收项目。不能在 OnEnable 里直接注册（主菜单还没有 PlayerStore），
            // 见 BottleRecycling 的说明，这里只挂"一局就绪"的钩子。
            BottleRecycling.RegisterHooks();

            // 电话订购空瓶用的快递员：等一局载入后再注册进游戏的未来顾客表，第二天会按 id 生成
            BottleDelivery.RegisterHooks();

            // 啤酒花作物：先注册进 PlantRegistry，种子和水培机才能认得它
            HopsCrop.RegisterPlant();

            // 啤酒花/种子的来源：种子偶尔能在废料里翻到（传说酒厂老板就是这么起家的）
            RegisterHopsLoot();

            // 精酿 / 极品精酿的提示框里补上原版酒瓶的"可陈化 / 已陈化 N 天"两行
            ModHook.OnCreateTooltipLate += BeerRecipes.AddAgingTooltip;

            // 杰克逊博士（发明家）进货时，把恒温发酵机摆上去卖
            ModHook.OnPlaceInventorInventoryItemEarly += OnPlaceInventorInventory;

        }

        /// <summary>点了杰克逊投资 II 之后，发明家会把这台机器摆出来卖。</summary>
        private static void OnPlaceInventorInventory(System.Collections.Generic.List<GameItem> ownedItems)
        {
            try
            {
                if (!NetworkUpgrade.IsUnlocked(NetworkUpgrade.JACKSON2)) return;

                PlayerStore store = PlayerStore.Instance;
                if (store == null) return;

                // 先建机器、补好使用说明，再交给商店售卖
                // （说明书不能在物品工厂里塞：DirectoryMaster.Item 会销毁工厂内生成的子物品）
                GameItem machine = DirectoryMaster.Item(FermentationMachine.ItemId, true);
                if (machine == null) return;

                FermentationMachine.AttachManual(machine);
                store.AddDirectSellingItemToTable(machine, false, false, false, 0);
            }
            catch (Exception e)
            {
                Instance.Log.Warning("上架恒温发酵机失败：" + e.GetType().Name + ": " + e.Message);
            }
        }

        private static void RegisterHopsLoot()
        {
            try
            {
                ModHelper.AddLootEntry(Instance.Manifest.ID, "junkTable", HopsCrop.SeedId, 2);
                ModHelper.AddLootEntry(Instance.Manifest.ID, "packedFoodTable", HopsCrop.ItemId, 5);
            }
            catch (Exception e)
            {
                Instance.Log.Warning("啤酒花掉落表注册失败：" + e.GetType().Name + ": " + e.Message);
            }
        }

        private static void RemoveHopsLoot()
        {
            try
            {
                ModHelper.RemoveLootEntry(Instance.Manifest.ID, "junkTable", HopsCrop.SeedId);
                ModHelper.RemoveLootEntry(Instance.Manifest.ID, "packedFoodTable", HopsCrop.ItemId);
            }
            catch (Exception)
            {
            }
        }

        public void OnDisable()
        {
            ModHook.OnModItemDirectoryInit -= RegisterItems;
            ModHook.OnModAssetsLoaded -= OnAssetsLoaded;

            BottleRecycling.UnregisterHooks();
            BottleRecycling.Unregister(Manifest.ID);

            BottleDelivery.UnregisterHooks();
            BottleDelivery.UnregisterClient();

            RedImpWarehouse.Unregister();

            RemoveHopsLoot();

            ModHook.OnCreateTooltipLate -= BeerRecipes.AddAgingTooltip;

            ModHook.OnPlaceInventorInventoryItemEarly -= OnPlaceInventorInventory;

        }

        private static void OnAssetsLoaded(LoadedMod loadedMod)
        {
            if (loadedMod == null || loadedMod.Manifest == null) return;
            if (Instance == null || Instance.Manifest == null) return;
            if (loadedMod.Manifest.ID != Instance.Manifest.ID) return;

            SheetSprites.Prepare(loadedMod);

            // Textures/icons 不被游戏扫描，这里手动把图标补进精灵表，供人脉升级的 iconKey 使用
            ModIcons.Prepare(loadedMod);
        }

        private static void RegisterItems(ModItemDirectory directory)
        {
            if (directory == null) return;

            directory.Add(BeerHelper.CleanBottleId, BeerHelper.CreateCleanBottle);
            directory.Add(NutrifruitCutting.ChunkId, NutrifruitCutting.CreateChunk);
            directory.Add(NutrifruitCutting.MashedId, NutrifruitCutting.CreateMashed);
            directory.Add(BeerRecipes.CraftBeerId, BeerRecipes.CreateCraftBeer);
            directory.Add(BeerRecipes.PremiumCraftBeerId, BeerRecipes.CreatePremiumCraftBeer);
            directory.Add(BeerRecipes.HomebrewedBeerId, BeerRecipes.CreateHomebrewedBeer);
            directory.Add(BeerRecipes.PoisonId, BeerRecipes.CreatePoison);

            // 红魔鬼库房电话的名片（买下"空瓶回收项目"时发放）
            directory.Add(RedImpWarehouse.CardItemId, RedImpWarehouse.CreateCard);

            // 啤酒花：作物 + 种子（走原版水培系统）
            directory.Add(HopsCrop.ItemId, HopsCrop.CreateHops);
            directory.Add(HopsCrop.SeedId, HopsCrop.CreateSeed);

            // 恒温发酵机（杰克逊博士处购买）
            directory.Add(FermentationMachine.ItemId, FermentationMachine.Create);


        }
    }
}
