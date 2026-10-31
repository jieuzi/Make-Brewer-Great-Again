using System;
using System.Collections.Generic;
using UnityEngine;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 恒温发酵机：从杰克逊博士（发明家）那里买的大型机器。
    ///
    /// 结构完全照原版催陈器（MachineWineRack.AgeWell）搭：
    ///   - 电池面板 MachineHelper.PowerPanel、模组面板 MachineHelper.Panel(... "module" ...)、
    ///     8×6 容器面板、说明面板 MachineHelper.NotePanel，外壳 MachineHelper.CreateMachineryWindow；
    ///   - SetupBatterySlot 插电池，InitMachinery(机, 30, false) 定基础功耗（不可开关）；
    ///   - MACHINE_EFFICIENCY_RATIO_INT / MACHINE_PERFORMANCE_RATIO_INT + MACHINE_PERFORMANCE_REDUCES_POWER_TAG
    ///     和催陈器一样让效率/性能模块影响功耗；
    ///   - 每晚 onCycleEndSlotItemFunc 里先 TryDrawCyclePower 扣电，再做正事：
    ///       1) 发酵中的葡萄酒瓶（WINE_CONTAINER_STATE == 1）：这一夜走「酵母强度 + 1」天，归零就 FinishFermentation，
    ///          并把 WINE_SCORE_INT +1（在机器里完成发酵 = 评级 +1）；
    ///       2) 我们模组正在发酵的啤酒瓶：这一夜走 2 天，完成时评分 +1 再定档；
    ///       3) 其余可陈化物品（AgableHelper.IsAgable）：每夜陈化 2 天（+ 质量加成/125）。
    /// </summary>
    internal static class FermentationMachine
    {
        public const string ItemId = "fermentation_machine";

        /// <summary>Textures/Items 里的图片名。</summary>
        private const string SpriteKey = "发酵机";

        /// <summary>每周期（每夜）耗电。</summary>
        private const int PowerDraw = 30;

        /// <summary>卖给玩家的价格。</summary>
        private const long Price = 1200;

        private const int ContainerWidth = 8;
        private const int ContainerHeight = 6;

        /// <summary>每夜的基础陈化天数（催陈器是 1，这台是 2）。</summary>
        private const int AgingDaysPerCycle = 2;

        private const string NoteName = "恒温发酵机说明";

        /// <summary>
        /// 说明文字：催陈器的说明原文 + 这台机器额外的一条 + 催陈器的功能介绍（物品描述原文）。
        /// </summary>
        private const string NoteText =
            "[模组影响]：\n" +
            "效率影响功耗\n" +
            "性能影响功耗\n" +
            "质量：每125质量额外陈化1天\n" +
            "在提供电力和一瓶已发酵完成的葡萄酒时会彻夜工作。\n" +
            "在恒温发酵机中的酒，发酵速度更快；在恒温发酵机中完成发酵的酒，品级会上升一级。\n" +
            "加速置于其中的特定物品的陈化过程。每个周期可使葡萄酒、奶酪等物品额外陈化一天。";

        private const string NoteShort = "催陈器的功能它都有，还能让发酵更快、品级更高。";

        private const string WineStateTag = "WINE_CONTAINER_STATE";
        private const string WineDaysTag = "FERMENTATION_TIME_REMAINING_INT";
        private const string WineYeastTag = "WINE_YEAST_POWER_INT";
        private const string WineScoreTag = "WINE_SCORE_INT";

        /// <summary>在这台机器里完成发酵的物品，评级 +1。</summary>
        private const int FinishScoreBonus = 1;

        // ------------------------------------------------------------------ 物品
        public static GameItem Create()
        {
            GameItem machine = ItemDirectory.CreateEmptyItem(ItemId);
            machine.name = "恒温发酵机";
            machine.shortDescription = "恒温发酵的大型机器。通上电、装好模块，一夜之间既能催发酵，也能催陈化。";
            machine.unitValue = Price;

            machine.SetSpriteAndShapeFromMod(ModId(), SpriteKey);
            ForceShape(machine, 4, 3);

            AudioHelper.InitMetalMachine(machine);
            machine.SetGameItemType("MACHINE");
            machine.EnableTag("CONTAINER_TAG", false);
            machine.EnableTag("STANDARD_MACHINE_TAG", false);

            // 面板：电池 / 模组 / 8x6 容器 / 说明
            var battery = new GameSlotInventory();
            var module = new GameGridInventory("10000010001000001110000010001000001", 7);
            var container = new GameGridInventory(ContainerWidth, ContainerHeight);
            var note = new GameSlotInventory();

            // 说明槽要按原版机器的规格设置（只收 postit、带便利贴底图），否则和其它机器不一样
            MachineHelper.SetupManualSlot(note);

            var panels = new[]
            {
                MachineHelper.PowerPanel(battery),
                MachineHelper.Panel(Loc("mech_purifier_label_module"), module, "module", true),
                MachineHelper.Panel(Loc("mech_evaporator_label_container"), container, "container", true),
                MachineHelper.NotePanel(note),
            };

            PixelWindow window = MachineHelper.CreateMachineryWindow(true, panels);
            if (window == null) return machine;
            machine.SetContentWindow(window);

            MachineHelper.SetupBatterySlot(battery, machine);

            MachineryHelper.InitMachinery(machine, PowerDraw, false);
            machine.ModifyTag("MACHINE_EFFICIENCY_RATIO_INT", state => state.Enable().SetInt(2));
            machine.ModifyTag("MACHINE_PERFORMANCE_RATIO_INT", state => state.Enable().SetInt(2));
            machine.EnableTag("MACHINE_PERFORMANCE_REDUCES_POWER_TAG", false);

            MachineHelper.SetupModuleBay(module, machine, () => machine.Validate(), null);

            // 说明书不能在这里塞：DirectoryMaster.Item 会把工厂里生成的子物品全部销毁
            // （"created with DirectoryMaster contains child items ... Removing all child items as a fix"），
            // 原版也是先建机器、再在 PreBuiltItemHelper.CreateAgeWell 里另外塞说明书。
            // 所以这里只搭好说明槽，说明书由 AttachManual 在机器创建之后补。

            machine.onCycleEndSlotItemFunc = (item, inventory, slot) => NightlyRun(machine, container, battery);

            return machine;
        }

        /// <summary>
        /// 把使用说明便利贴塞进机器的说明槽。
        /// 必须在机器**创建完成之后**调用（不能在物品工厂里塞，DirectoryMaster.Item 会销毁工厂内产生的子物品）。
        /// </summary>
        public static void AttachManual(GameItem machine)
        {
            if (machine == null) return;

            try
            {
                // 已经有说明书就不重复加（比如读档后原物品自带）
                List<GameItem> children = GraphUtils.FindAllChildrenType<GameItem>(machine, null, null);
                if (children != null)
                {
                    for (int i = 0; i < children.Count; i++)
                    {
                        GameItem child = children[i];
                        if (child == null) continue;

                        string childId = null;
                        try { childId = child.identifier; } catch (Exception) { }
                        if (childId == "postit") return;
                    }
                }

                GameItem manual = DirectoryMaster.Item("postit", true);
                if (manual == null)
                {
                    Warn("创建使用说明失败：找不到 postit。");
                    return;
                }

                manual.name = NoteName;
                manual.shortDescription = NoteShort;
                manual.flavorText = NoteText;
                manual.SetGameItemType("DOCUMENT");
                manual.EnableTag("IMPORTANT_TAG", false);
                manual.EnableTag("MACHINE_MANUAL_TAG", false);
                manual.unitValue = 0;

                int left = GraphUtils.TryAcceptAll(machine, manual, -1);
                if (left > 0)
                {
                    Warn("恒温发酵机的使用说明没能放进说明槽（剩余 " + left + "）。");
                    try { manual.Destroy(); } catch (Exception) { }
                }
            }
            catch (Exception e)
            {
                Warn("恒温发酵机的说明放不进去：" + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>贴图 64x48 = 4x3 格（原版按每格中心像素推导，图案居中会漏判，所以强制）。</summary>
        private static void ForceShape(GameItem item, int width, int height)
        {
            try
            {
                var builder = new GridShapeBuilder(width, height);
                builder.SetDataFill(width, height, 1);
                item.SetShape(builder.shape);
            }
            catch (Exception e)
            {
                Warn("设置恒温发酵机形状失败：" + e.Message);
            }
        }

        // ------------------------------------------------------------------ 每晚运行
        private static void NightlyRun(GameItem machine, GameGridInventory container, GameSlotInventory battery)
        {
            try
            {
                if (machine == null || container == null) return;

                List<GameItem> children = GraphUtils.FindAllChildrenType<GameItem>(container, null, null);
                if (children == null || children.Count == 0) return;

                // 没电就不干活（和催陈器一样）
                if (!MachineHelper.TryDrawCyclePower(machine, battery)) return;

                int agingDays = AgingDaysPerCycle;
                try
                {
                    int quality = MachineryHelper.GetCurrentQualityBonus(machine);
                    if (quality > 0) agingDays += quality / 125;
                }
                catch (Exception)
                {
                }

                for (int i = 0; i < children.Count; i++)
                {
                    GameItem child = children[i];
                    if (child == null) continue;

                    if (AdvanceWine(child, agingDays)) continue;

                    // 啤酒：这一夜走「基础 1 天 + 机器额外 1 天」，完成时评级 +1
                    if (BeerBottleUi.AdvanceBrewing(child, FinishScoreBonus, 2)) continue;

                    AgeOther(child, agingDays);
                }
            }
            catch (Exception e)
            {
                Warn("恒温发酵机运行失败：" + e.GetType().Name + ": " + e.Message);
            }
        }

        /// <summary>
        /// 葡萄酒瓶：发酵中（state 1）→ 这一夜走「酵母强度 + 1 天」，归零完成发酵并评级 +1；
        /// 已完成（state 2）→ 按催陈器的方式陈化（WineHelper.OnAgeWine 会跳过假酒）。
        /// </summary>
        private static bool AdvanceWine(GameItem item, int agingDays)
        {
            int state = ReadInt(item, WineStateTag);
            if (state == 1)
            {
                // 原版过夜是按酵母强度走，机器里再多走 1 天
                int yeastPower = Math.Max(0, ReadInt(item, WineYeastTag));
                int step = yeastPower + 1;

                int remaining = ReadInt(item, WineDaysTag) - step;
                try
                {
                    item.ModifyTag(WineDaysTag, s => s.Enable().SetInt(Math.Max(0, remaining)));
                }
                catch (Exception)
                {
                }

                if (remaining <= 0)
                {
                    // 在机器里完成发酵：评级 +1（写进 WINE_SCORE_INT，FinishFermentation 据此定档）。
                    // 原版是按分数分档的，分数再高也只会落到 Master Vintage，所以这里不加任何上限。
                    try
                    {
                        int score = ReadInt(item, WineScoreTag) + FinishScoreBonus;
                        item.ModifyTag(WineScoreTag, s => s.Enable().SetInt(score));
                    }
                    catch (Exception)
                    {
                    }

                    try { WineHelper.FinishFermentation(item); } catch (Exception) { }
                }

                return true;
            }

            if (state == 2)
            {
                for (int d = 0; d < agingDays; d++)
                {
                    try { WineHelper.OnAgeWine(item); } catch (Exception) { }
                }

                return true;
            }

            return false;
        }

        /// <summary>其它可陈化物品（奶酪、精酿啤酒、极品精酿……）。</summary>
        private static void AgeOther(GameItem item, int agingDays)
        {
            bool agable;
            try { agable = AgableHelper.IsAgable(item); }
            catch (Exception) { return; }
            if (!agable) return;

            for (int d = 0; d < agingDays; d++)
            {
                try { AgableHelper.OnAge(item); } catch (Exception) { }
            }
        }

        // ------------------------------------------------------------------ 工具
        private static int ReadInt(GameItem item, string tag)
        {
            if (item == null) return 0;
            try { return item.GetTagReadonly(tag).GetInt(); }
            catch (Exception) { return 0; }
        }

        private static string Loc(string key)
        {
            try { return LocHelper.GetLocalizedMechanic(key); }
            catch (Exception) { return key; }
        }

        private static string ModId()
        {
            return ModEntry.Instance != null && ModEntry.Instance.Manifest != null
                ? ModEntry.Instance.Manifest.ID
                : "make_brewer_great_again";
        }

        private static void Warn(string message)
        {
            if (ModEntry.Instance != null && ModEntry.Instance.Log != null)
                ModEntry.Instance.Log.Warning(message);
        }
    }
}
