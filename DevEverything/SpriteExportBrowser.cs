using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevEverything
{
    /// <summary>
    /// F9 里的“素材导出”：和全物品商店同一套 UI（分类下拉 + 搜索 + 物品按钮），
    /// 点一件就把这件物品的精灵图导出成 PNG；底部按钮把当前分类/搜索结果整批导出。
    /// 导出规则见 SpriteExporter：读物品自己的 spriteAtlasPath / spritePath。
    /// </summary>
    internal static class SpriteExportBrowser
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
                    "dev_everything.export_sprites",
                    "素材导出",
                    ExportSingle,
                    ExportAll,
                    "导出本类");
            }

            browser.Open();
        }

        /// <summary>点某个物品：只导出这一张。</summary>
        private static void ExportSingle(ItemEntry entry)
        {
            string folder = browser.CurrentTitle;
            string fileName = SpriteExporter.ExportSingle(entry, folder);

            if (string.IsNullOrEmpty(fileName))
            {
                SetStatus($"导出失败：{entry.Name}（{entry.Id}）");
                return;
            }

            SetStatus($"已导出 {fileName}　路径已复制：{SpriteExporter.LastExportFolder}");
            Log($"已导出素材 {entry.Name} ({entry.Id}) -> {SpriteExporter.LastExportFolder}\\{fileName}");
        }

        /// <summary>底部按钮：整批导出当前列表。</summary>
        private static void ExportAll(IList<ItemEntry> entries)
        {
            string folder = browser.CurrentTitle;
            SetStatus($"正在导出 {entries.Count} 张…（可能卡一下）");

            int count = SpriteExporter.ExportEntries(entries, folder);
            SetStatus($"已导出 {count}/{entries.Count} 张　路径已复制：{SpriteExporter.LastExportFolder}");
        }

        private static void SetStatus(string text)
        {
            if (browser != null) browser.SetStatus(text);
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
