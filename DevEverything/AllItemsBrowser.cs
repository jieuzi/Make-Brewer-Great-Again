using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevEverything
{
    /// <summary>
    /// F9 里的“全物品商店”：列出游戏内全部物品，按物品类型分类，
    /// 点一件就生成出来并按顺序找位置：里面的柜台 → 仓库 → 外面的柜台。
    /// 窗口本体来自 ItemBrowserWindow，和「素材导出」菜单是同一套 UI。
    /// </summary>
    internal static class AllItemsBrowser
    {
        private static ItemBrowserWindow browser;

        public static void Open()
        {
            ItemCatalog.EnsureBuilt(ModEntry.Instance != null ? ModEntry.Instance.Log : null);

            if (ItemCatalog.Categories.Count == 0)
            {
                Warn("没有扫描到任何物品。");
                return;
            }

            if (browser == null)
            {
                browser = new ItemBrowserWindow(
                    "dev_everything.all_items",
                    "全物品商店",
                    Spawn,
                    ExportCurrentList,
                    "导出本类素材");
            }

            browser.Open();
        }

        // ------------------------------------------------------------------ 批量导出
        private static void ExportCurrentList(IList<ItemEntry> entries)
        {
            string folder = browser.CurrentTitle;
            int count = SpriteExporter.ExportEntries(entries, folder);
            SetStatus($"已导出 {count} 张　路径已复制：{SpriteExporter.LastExportFolder}");
        }

        // ------------------------------------------------------------------ 生成
        private static void Spawn(ItemEntry entry)
        {
            try
            {
                GameItem item = ModHelper.SpawnItem(entry.Id);
                if (item == null)
                {
                    SetStatus($"生成失败：{entry.Name}");
                    return;
                }

                ModHelper.SetItemOwnerShip(item, true);

                // 音效编号要在放置前就读出来：放进柜台后物品可能被柜台逻辑接管，
                // 此时再读字段可能读不到（拿到的候选为空就只剩兜底音效）。
                string dropSound = SafeSound(() => item.soundDragEnd);
                string startSound = SafeSound(() => item.soundDragStart);

                if (EmporiumEntry.Instance == null)
                {
                    SetStatus("店铺还没准备好，无法放置。");
                    return;
                }

                string placedIn = PlaceInStore(item);
                if (placedIn == null)
                {
                    SetStatus($"没有空位：{entry.Name}");
                    Warn($"三个容器都放不下 {entry.Name}（{entry.Id}）。");
                    return; // 没放进去就不播声音
                }

                PlayItemDropSound(dropSound, startSound);
                SetStatus($"已放进{placedIn}：{entry.Name}");
            }
            catch (Exception e)
            {
                SetStatus($"生成 {entry.Name} 失败：{e.Message}");
            }
        }

        /// <summary>
        /// 按 里面的柜台 → 仓库 → 外面的柜台 把物品放进店里，返回放进的容器名；放不下返回 null。
        /// 供“全物品商店”和“瓶装水生成”等按钮共用。
        /// </summary>
        internal static string PlaceInStore(GameItem item)
        {
            if (item == null) return null;

            EmporiumEntry store = EmporiumEntry.Instance;
            if (store == null) return null;

            // 对应关系（实测确认，和游戏内部命名一致）：
            //   backInvinvElement  = 里面的柜台
            //   invElement         = 仓库
            //   frontInvinvElement = 外面的柜台
            var details = new List<string>();
            if (TryPlace("里面的柜台", store.backInvinvElement, item, details)) return "里面的柜台";
            if (TryPlace("仓库", store.invElement, item, details)) return "仓库";
            if (TryPlace("外面的柜台", store.frontInvinvElement, item, details)) return "外面的柜台";

            Warn("三个容器都放不下：" + string.Join(" | ", details.ToArray()));
            return null;
        }

        /// <summary>
        /// 真正把物品放进去。注意 GraphUtils.TryAcceptAll 的返回值是“**没放进去的数量**”
        /// （源码末尾就是 amount - placed），所以 0 才代表全部放好，> 0 表示还有剩。
        /// 这里再叠加一次父链确认，避免柜台那种嵌套容器被误判。
        /// </summary>
        private static bool TryPlace(string label, GraphNodeStorage container, GameItem item, List<string> details)
        {
            if (container == null)
            {
                details.Add(label + ":不存在");
                return false;
            }

            int remaining;
            try
            {
                remaining = GraphUtils.TryAcceptAll(container, item, -1);
            }
            catch (Exception e)
            {
                details.Add($"{label}:异常({e.Message})");
                return false;
            }

            bool inside = IsInside(item, container);
            bool placed = inside || remaining <= 0;
            details.Add($"{label}:剩余{remaining}/在容器内={inside}");
            return placed;
        }

        /// <summary>物品的父链（parentInventory 往上，或 parents 列表）里是否包含这个容器。</summary>
        private static bool IsInside(GameItem item, GraphNodeStorage container)
        {
            try
            {
                var pending = new Stack<GraphNodeStorage>();
                var seen = new HashSet<GraphNodeStorage>();

                GraphNodeStorage parent = item.parentInventory;
                if (parent != null) pending.Push(parent);

                List<GraphNodeStorage> directParents = item.parents;
                if (directParents != null)
                {
                    for (int i = 0; i < directParents.Count; i++)
                    {
                        if (directParents[i] != null) pending.Push(directParents[i]);
                    }
                }

                while (pending.Count > 0)
                {
                    GraphNodeStorage node = pending.Pop();
                    if (node == null || !seen.Add(node)) continue;
                    if (node == container) return true;

                    List<GraphNodeStorage> parents = node.parents;
                    if (parents == null) continue;
                    for (int i = 0; i < parents.Count; i++)
                    {
                        if (parents[i] != null) pending.Push(parents[i]);
                    }
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        /// <summary>
        /// 播这件物品自己的“放下”音效：优先 soundDragEnd → soundDragStart（和游戏拖放逻辑一致），
        /// 两个都播不了才用通用落地声兜底。只有确认放进容器后才会调用。
        /// </summary>
        private static string PlayItemDropSound(string dropSound, string startSound)
        {
            try
            {
                if (!AudioManager.Exists) return "（AudioManager 不存在）";
                AudioManager audio = AudioManager.Instance;
                if (audio == null) return "（AudioManager 为空）";

                var own = new List<string>();
                AddSoundCandidate(own, dropSound);
                AddSoundCandidate(own, startSound);
                if (own.Count > 0 && audio.TryPlayOne(own.ToArray()))
                {
                    return string.Join("/", own.ToArray());
                }

                var fallback = new List<string>();
                AddSoundCandidate(fallback, AudioHelper.TINY_ITEM_DROP);
                AddSoundCandidate(fallback, AudioHelper.SCRAP_METAL_DROP);
                if (fallback.Count > 0 && audio.TryPlayOne(fallback.ToArray()))
                {
                    return fallback[0] + "(兜底)";
                }

                return "（没有可播的声音）";
            }
            catch (Exception e)
            {
                return "（异常：" + e.Message + "）";
            }
        }

        private static void AddSoundCandidate(List<string> candidates, string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return;
            if (candidates.Contains(identifier)) return;
            candidates.Add(identifier);
        }

        private static string SafeSound(Func<string> read)
        {
            try { return read(); } catch (Exception) { return null; }
        }

        private static void SetStatus(string text)
        {
            if (browser != null) browser.SetStatus(text);
        }

        private static void Warn(string message)
        {
            if (ModEntry.Instance != null && ModEntry.Instance.Log != null)
            {
                ModEntry.Instance.Log.Warning(message);
            }
        }
    }
}
