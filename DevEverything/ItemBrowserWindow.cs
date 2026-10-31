using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevEverything
{
    /// <summary>
    /// “按分类 + 搜索浏览物品”的通用窗口。
    /// 全物品商店（点击生成）和素材导出（点击导出）共用它，UI 和交互因此完全一致。
    /// </summary>
    internal sealed class ItemBrowserWindow
    {
        private const string STATUS_ID = "status";
        private const string CATEGORY_ID = "category";
        private const string SEARCH_ID = "search";
        private const int MAX_RESULTS = 60;

        private const float WINDOW_WIDTH = 660f;
        private const float WINDOW_HEIGHT = 740f;

        private readonly string windowId;
        private readonly string title;
        private readonly Action<ItemEntry> onPick;
        private readonly Action<IList<ItemEntry>> onBatch;
        private readonly string batchLabel;

        private CustomUIWindow window;
        private RectTransform listHost;
        private int categoryIndex;
        private string searchQuery = string.Empty;
        private string appliedSearch = string.Empty;
        private IList<ItemEntry> currentEntries = new List<ItemEntry>();

        /// <param name="windowId">窗口 id（用模组 id 做前缀避免冲突）。</param>
        /// <param name="title">窗口标题。</param>
        /// <param name="onPick">点击某个物品时执行。</param>
        /// <param name="onBatch">底部批量按钮执行；传 null 就不显示该按钮。</param>
        /// <param name="batchLabel">批量按钮文字。</param>
        public ItemBrowserWindow(string windowId, string title, Action<ItemEntry> onPick,
                                 Action<IList<ItemEntry>> onBatch, string batchLabel)
        {
            this.windowId = windowId;
            this.title = title;
            this.onPick = onPick;
            this.onBatch = onBatch;
            this.batchLabel = batchLabel;
        }

        public IList<ItemEntry> CurrentEntries
        {
            get { return currentEntries; }
        }

        /// <summary>当前列表对应的名字（分类名，或“搜索_关键字”），也用作导出子文件夹名。</summary>
        public string CurrentTitle { get; private set; }

        public void Open()
        {
            if (CustomUIManager.Instance == null)
            {
                Warn("CustomUIManager 还没准备好，无法打开窗口。");
                return;
            }

            ItemCatalog.EnsureBuilt(ModEntry.Instance != null ? ModEntry.Instance.Log : null);

            var options = new List<string>();
            foreach (string category in ItemCatalog.Categories)
            {
                options.Add($"{ItemCatalog.Label(category)} ({ItemCatalog.Entries(category).Count})");
            }

            if (options.Count == 0)
            {
                Warn("没有扫描到任何物品。");
                return;
            }

            categoryIndex = 0;
            searchQuery = string.Empty;
            appliedSearch = string.Empty;

            CustomUIBuilder builder = CustomUIManager.Window(windowId, title, CustomUIManager.LAYER_OVERLAY)
                .SetSize(WINDOW_WIDTH, WINDOW_HEIGHT)
                .Center()
                .SetDraggable(true)
                .SetCloseOnEscape(true);

            builder.AddLabel("共 0 件物品", STATUS_ID);

            builder.BeginRow(8f, TextAnchor.MiddleLeft)
                .AddLabel("分类", null)
                .AddDropdown(options, 0, OnCategoryChanged, CATEGORY_ID)
                .AddSpace(12f)
                .AddLabel("搜索", null)
                .AddInput("中文名或英文 id（跨分类）", string.Empty, OnSearchSubmitted, SEARCH_ID, OnSearchTyping)
            .End();

            builder.BeginScrollFlexible(520f);
            listHost = builder.CurrentContainer;
            builder.End();

            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            if (onBatch != null && !string.IsNullOrEmpty(batchLabel))
            {
                builder.AddButton(batchLabel, OnBatchClicked, null);
            }
            builder.AddFlexibleSpace().AddButton("关闭", Close, null);
            builder.End();

            window = builder.Show();
            FillList();
        }

        public void SetStatus(string text)
        {
            if (window == null) return;
            CustomUIElement status = window.Get(STATUS_ID);
            if (status != null) status.SetText(text);
        }

        public void Close()
        {
            if (CustomUIManager.Instance != null) CustomUIManager.Instance.CloseWindow(windowId);
            listHost = null;
            window = null;
        }

        // ------------------------------------------------------------------ 交互
        private void OnBatchClicked()
        {
            if (onBatch == null) return;
            if (currentEntries == null || currentEntries.Count == 0)
            {
                SetStatus("当前列表是空的。");
                return;
            }
            onBatch(currentEntries);
        }

        private void OnCategoryChanged(int index)
        {
            categoryIndex = index;

            // 换分类就退出搜索状态，否则搜索会一直盖住分类。
            if (appliedSearch.Length > 0)
            {
                ClearSearchBox();
                searchQuery = string.Empty;
                appliedSearch = string.Empty;
            }

            FillList();
        }

        private void OnSearchTyping(string value)
        {
            searchQuery = value ?? string.Empty;

            string normalized = ItemCatalog.Normalize(searchQuery);
            if (normalized == appliedSearch) return; // 只有真的变了才重建列表
            appliedSearch = normalized;
            FillList();
        }

        private void OnSearchSubmitted(string value)
        {
            searchQuery = value ?? string.Empty;

            string normalized = ItemCatalog.Normalize(searchQuery);
            if (normalized == appliedSearch) return;
            appliedSearch = normalized;
            FillList();
        }

        private void ClearSearchBox()
        {
            if (window == null) return;
            CustomUIElement input = window.Get(SEARCH_ID);
            if (input != null) input.SetText(string.Empty);
        }

        // ------------------------------------------------------------------ 列表
        private void FillList()
        {
            if (listHost == null || window == null) return;

            // 只重建列表容器，窗口和输入框保持不动，否则输入框会失去焦点。
            CustomUIBuilder.ClearContainer(listHost);
            var builder = new CustomUIBuilder(CustomUIManager.Instance, window, listHost);

            bool searching = appliedSearch.Length > 0;
            IList<ItemEntry> entries;

            if (searching)
            {
                int total;
                entries = ItemCatalog.Search(searchQuery, MAX_RESULTS, out total);
                SetStatus(total > entries.Count
                    ? $"搜索“{searchQuery.Trim()}”：{total} 件，只列出前 {entries.Count} 件"
                    : $"搜索“{searchQuery.Trim()}”：{total} 件（全部分类）");
                CurrentTitle = "搜索_" + searchQuery.Trim();
            }
            else
            {
                string category = ItemCatalog.Categories[categoryIndex];
                entries = ItemCatalog.Entries(category);
                SetStatus($"{ItemCatalog.Label(category)}：{entries.Count} 件　·　共 {ItemCatalog.TotalCount} 件");
                CurrentTitle = ItemCatalog.Label(category);
            }

            currentEntries = entries;

            if (entries.Count == 0)
            {
                builder.AddLabel(searching ? "没有匹配的物品。" : "这个分类里没有物品。", null);
                return;
            }

            builder.BeginGrid(2, 292f, 40f, 6f);
            foreach (ItemEntry entry in entries)
            {
                ItemEntry captured = entry;
                builder.AddButton(ButtonLabel(captured, searching), () => onPick(captured), null);
                builder.WithTooltip($"{captured.Id}  ·  {ItemCatalog.Label(captured.Category)}");
            }
            builder.End();
        }

        private static string ButtonLabel(ItemEntry entry, bool withCategory)
        {
            string label = entry.Price > 0 ? $"{entry.Name}  {entry.Price}" : entry.Name;
            if (withCategory) label += $"  · {ItemCatalog.Label(entry.Category)}";
            return label;
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
