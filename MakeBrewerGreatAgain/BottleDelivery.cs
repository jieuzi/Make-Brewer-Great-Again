using System;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 库房电话里"订购 8 个空瓶"的送货逻辑。
    ///
    /// 送货靠游戏自己的"未来顾客队列"：PlayerStore.QueueFuturClient(id, 1) 会在第二天
    /// 通过 StoreClientListDict.CreateStoreClient(id) 生成那个顾客，所以只要把自己的
    /// 工厂注册进 StoreClientListDict.storeClientDict，快递员就会按原版流程正常到店。
    /// 快递员本体复用原版红魔鬼快递员，只把"到店后把货放到收购台"的动作换成我们的啤酒箱。
    /// </summary>
    internal static class BottleDelivery
    {
        /// <summary>未来顾客队列里用的 id，必须以模组 id 开头避免撞车。</summary>
        public const string ClientId = "gmtb_bottle_courier";

        private const string BeerCaseId = "beerCase";
        private const string EmptyBottleId = "empty_beer_bottle";
        private const int BottleCount = 8;

        private static bool registered;

        /// <summary>
        /// 在主菜单（模组加载时）不去碰 StoreClientListDict：那是游戏自己的静态表，
        /// 载入一局之后再访问最安全，所以改挂在"存档载入完成"的钩子上。
        /// </summary>
        public static void RegisterHooks()
        {
            ModHook.OnGameLoadedInit += OnGameLoaded;
        }

        public static void UnregisterHooks()
        {
            ModHook.OnGameLoadedInit -= OnGameLoaded;
        }

        private static void OnGameLoaded()
        {
            RegisterClient();
        }

        public static void RegisterClient()
        {
            if (registered) return;

            try
            {
                var dict = StoreClientListDict.storeClientDict;
                if (dict == null)
                {
                    Warn("未来顾客表不可用，空瓶快递员注册失败。");
                    return;
                }

                dict[ClientId] = CreateCourier;
                registered = true;
            }
            catch (Exception e)
            {
                Warn("注册空瓶快递员失败：" + e.GetType().Name + ": " + e.Message);
            }
        }

        public static void UnregisterClient()
        {
            try
            {
                var dict = StoreClientListDict.storeClientDict;
                if (dict != null) dict.Remove(ClientId);
            }
            catch (Exception)
            {
            }

            registered = false;
        }

        /// <summary>电话里下了单之后调用：把快递员排到明天。</summary>
        public static void QueueBottleDelivery()
        {
            try
            {
                RegisterClient();
                PlayerStore.Instance.QueueFuturClient(ClientId, 1);
            }
            catch (Exception e)
            {
                Warn("预约空瓶快递失败：" + e.GetType().Name + ": " + e.Message);
            }
        }

        private static StoreClient CreateCourier()
        {
            // 复用原版红魔鬼快递员：外形、台词、批发折扣都是现成的。
            StoreClient client = StoreClientListSupplier.CreateCourrierBeer();
            if (client == null) return null;

            client.identifier = ClientId;

            Dialogue main = client.mainDialogue;
            if (main != null)
            {
                // 原版到店动作是往收购台放 1~2 箱红魔鬼啤酒，这里换成我们的旧瓶啤酒箱。
                main.SetEndAction(() =>
                {
                    try
                    {
                        GameItem box = BuildBottleCase();
                        if (box != null)
                            PlayerStore.Instance.AddDirectSellingItemToTable(box, false, false, false, 0);
                    }
                    catch (Exception e)
                    {
                        Warn("快递员送货失败：" + e.GetType().Name + ": " + e.Message);
                    }
                });
            }

            return client;
        }

        /// <summary>红魔鬼啤酒箱（8x4 格）+ 8 个空啤酒瓶。</summary>
        private static GameItem BuildBottleCase()
        {
            GameItem box = DirectoryMaster.Item(BeerCaseId, true);
            if (box == null) return null;

            for (int i = 0; i < BottleCount; i++)
            {
                GameItem bottle = DirectoryMaster.Item(EmptyBottleId, true);
                if (bottle == null) break;

                // 保持原版空啤酒瓶的样子：JUNK 类型、没有日用品标签、不是酒瓶容器，
                // 所以不能直接拿来酿酒，得先拿到水龙头下面冲成干净的空啤酒瓶。
                int notPlaced = GraphUtils.TryAcceptAll(box, bottle, -1);
                if (notPlaced > 0)
                {
                    // 箱子放不下了，多余的瓶子直接销毁，避免刷在场景里。
                    try { bottle.Destroy(); } catch (Exception) { }
                    break;
                }
            }

            return box;
        }

        private static void Warn(string message)
        {
            if (ModEntry.Instance != null && ModEntry.Instance.Log != null)
                ModEntry.Instance.Log.Warning(message);
        }
    }
}
