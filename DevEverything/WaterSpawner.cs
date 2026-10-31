using System;
using UnityEngine;

namespace DevEverything
{
    /// <summary>
    /// F9 → Spawn 里的「瓶装水生成」：一键生成装着指定水质的瓶装水。
    ///
    /// 原版其实也有类似工具（DebugPanel.SpawnWaterTestingKit → WaterHelper.SpawnWaterTestingKit，
    /// 会往仓库塞一大排水瓶和净水设备），但混在一整套测试工具里、而且是 2000ml 大瓶一大堆；
    /// 这里单独做一个按钮，按原版水质档位各生成一瓶，方便测酿酒的水质评分。
    ///
    /// 水质档位来自 WaterFeatureHelper（索引与 WATER_PURITIES 一致）：
    ///   0 纯水 9990+ ／ 1 优质水 9900 ／ 2 基础水 9800 ／ 3 幽灵水 9600 ／ 4 铁锈水 9000 ／ 5 沟渠水 3500。
    /// 填充用 WaterHelper.AddWater(容器, 档位, -1 /*装满*/, false, 0, 1, true)，和原版 FillWithPureWater 同一套逻辑。
    /// </summary>
    internal static class WaterSpawner
    {
        private const string WindowId = "dev_everything.water_bottles";

        /// <summary>2000ml 大水瓶，一瓶就够灌满 600ml 的啤酒瓶。</summary>
        private const string BottleId = "large_bottled_water";

        private const string StatusId = "status";

        private static readonly string[] TierNames =
        {
            "纯水（99.9%+）",
            "优质水（99% - 99.9%）",
            "基础水（98% - 99%）",
            "幽灵水（96% - 98%）",
            "铁锈水（90% - 96%）",
            "沟渠水（低于 90%）",
        };

        private static CustomUIWindow window;

        public static void Open()
        {
            if (CustomUIManager.Instance == null)
            {
                Warn("CustomUIManager 还没准备好。");
                return;
            }

            CustomUIBuilder builder = CustomUIManager.Window(WindowId, "瓶装水生成", CustomUIManager.LAYER_OVERLAY)
                .SetSize(460f, 430f)
                .Center()
                .SetDraggable(true)
                .SetCloseOnEscape(true);

            builder.AddLabel("点一个档位，生成一瓶 2000ml 的对应水质瓶装水（放在店里）。", StatusId);

            for (int i = 0; i < TierNames.Length; i++)
            {
                int grade = i;
                builder.AddButton(TierNames[i], () => Spawn(grade), "tier" + i);
            }

            builder.AddFlexibleSpace()
                .AddButton("六种各一瓶", SpawnAll, null)
                .AddButton("关闭", Close, null);

            window = builder.Show();
        }

        private static void SpawnAll()
        {
            for (int grade = 0; grade < TierNames.Length; grade++) Spawn(grade);
            SetStatus("已生成六种水质各一瓶。");
        }

        private static void Spawn(int grade)
        {
            try
            {
                GameItem bottle = ModHelper.SpawnItem(BottleId);
                if (bottle == null)
                {
                    SetStatus("生成失败：找不到 large_bottled_water。");
                    return;
                }

                ModHelper.SetItemOwnerShip(bottle, true);

                // 装满指定档位的水（-1 = 装到满）
                WaterHelper.AddWater(bottle, grade, -1, false, 0, 1, true);

                int purity = 0;
                int ml = 0;
                try { purity = WaterHelper.GetWaterPurity(bottle); } catch (Exception) { }
                try { ml = WaterHelper.GetCurrentCapacityML(bottle); } catch (Exception) { }

                string placedIn = AllItemsBrowser.PlaceInStore(bottle);
                if (placedIn == null)
                {
                    SetStatus($"没有空位：{TierNames[grade]}（水已经灌好了，但没地方放）");
                    return;
                }

                SetStatus($"已放进{placedIn}：{TierNames[grade]}，{ml}ml，纯净度 {purity}");
            }
            catch (Exception e)
            {
                SetStatus("生成瓶装水失败：" + e.Message);
            }
        }

        private static void Close()
        {
            if (CustomUIManager.Instance != null) CustomUIManager.Instance.CloseWindow(WindowId);
            window = null;
        }

        private static void SetStatus(string text)
        {
            if (window == null) return;
            CustomUIElement status = window.Get(StatusId);
            if (status != null) status.SetText(text);
        }

        private static void Warn(string message)
        {
            if (ModEntry.Instance != null && ModEntry.Instance.Log != null)
                ModEntry.Instance.Log.Warning(message);
        }
    }
}
