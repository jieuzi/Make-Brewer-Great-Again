using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DevEverything
{
    /// <summary>物品目录里的一条记录，只保留显示和生成需要的信息。</summary>
    public sealed class ItemEntry
    {
        public string Id;
        public string Name;
        /// <summary>游戏根据 id 生成的英文显示名，用于英文搜索。</summary>
        public string IdentifierName;
        public long Price;
        public string Category;
        /// <summary>预先拼好的搜索键：中文名 + 英文 id + 英文显示名 + 分类名，全部归一化。</summary>
        public string SearchKey;
        /// <summary>物品的图集路径（模组物品存的是 modId）。</summary>
        public string SpriteAtlasPath;
        /// <summary>精灵名（模组物品是模组里的精灵键）。</summary>
        public string SpritePath;
        /// <summary>true 表示图在某个模组的 Sprites 字典里。</summary>
        public bool UseSpriteFromMod;
    }

    /// <summary>
    /// 扫描游戏的全部物品目录，按物品类型（GameItemType）分组。
    /// 结果缓存在内存里，一次会话只扫一遍。
    /// </summary>
    public static class ItemCatalog
    {
        /// <summary>认不出类型的物品都归到这里。</summary>
        public const string OTHER = "OTHER";

        private static readonly List<string> majorTypes = new List<string>();
        private static readonly List<string> categories = new List<string>();
        private static readonly Dictionary<string, string> labels = new Dictionary<string, string>();
        private static readonly Dictionary<string, List<ItemEntry>> groups = new Dictionary<string, List<ItemEntry>>();
        private static bool built;

        public static int TotalCount { get; private set; }

        public static IList<string> Categories
        {
            get { return categories; }
        }

        public static IList<ItemEntry> Entries(string category)
        {
            List<ItemEntry> list;
            return groups.TryGetValue(category, out list) ? list : (IList<ItemEntry>)new ItemEntry[0];
        }

        public static string Label(string category)
        {
            string label;
            return labels.TryGetValue(category, out label) ? label : category;
        }

        public static void EnsureBuilt(ModLog log)
        {
            if (built) return;
            built = true;

            float started = Time.realtimeSinceStartup;
            try
            {
                Build(log);
            }
            catch (Exception e)
            {
                if (log != null) log.Error("扫描物品目录失败：" + e.Message);
            }


        }

        /// <summary>给日志用的一行分类统计。</summary>
        private static string CategorySummary()
        {
            var builder = new StringBuilder();
            foreach (string category in categories)
            {
                if (builder.Length > 0) builder.Append("，");
                builder.Append(Label(category)).Append(' ').Append(Entries(category).Count);
            }
            return builder.ToString();
        }

        private static void Build(ModLog log)
        {
            CollectMajorTypes();

            List<string> identifiers = CollectIdentifiers(log);
            foreach (string id in identifiers)
            {
                if (ItemSpawnerHiddenList.IsHidden(id)) continue;

                ItemEntry entry = TryReadEntry(id);
                if (entry == null) continue;

                if (!labels.ContainsKey(entry.Category)) labels[entry.Category] = TypeDisplayName(entry.Category);
                GetGroup(entry.Category).Add(entry);
                TotalCount++;
            }

            OrderCategories();
        }

        /// <summary>
        /// 和游戏自带的调试生成器一样，按目录类名拿到全部物品 id。
        /// 万一那套 API 在这个版本行为不同，退回直接读 DirectoryMaster.directories。
        /// </summary>
        private static List<string> CollectIdentifiers(ModLog log)
        {
            var ordered = new List<string>();
            var seen = new HashSet<string>();

            try
            {
                List<string> classNames = DirectoryMaster.GetDictionaryClassNameList<GameItem>();
                if (classNames != null)
                {
                    foreach (string className in classNames)
                    {
                        List<string> ids;
                        try
                        {
                            ids = DirectoryMaster.GetIdentifierList<GameItem>(className);
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                        if (ids == null) continue;
                        foreach (string id in ids) AddIdentifier(ordered, seen, id);
                    }
                }
            }
            catch (Exception e)
            {
                if (log != null) log.Warning("GetDictionaryClassNameList 失败：" + e.Message);
            }

            if (ordered.Count > 0) return ordered;

            try
            {
                Directory<GameItem>[] directories = Resources.FindObjectsOfTypeAll<Directory<GameItem>>();
                if (directories != null)
                {
                    foreach (Directory<GameItem> directory in directories)
                    {
                        if (directory == null) continue;

                        Dictionary<string, Func<GameItem>> factories = directory.factoryDictionary;
                        if (factories == null) continue;
                        foreach (string id in factories.Keys) AddIdentifier(ordered, seen, id);
                    }
                }


            }
            catch (Exception e)
            {
                if (log != null) log.Error("兜底枚举物品目录失败：" + e.Message);
            }

            return ordered;
        }

        private static void AddIdentifier(List<string> ordered, HashSet<string> seen, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (seen.Add(id)) ordered.Add(id);
        }

        /// <summary>游戏把主要类型列在 TypeHelper.MAJOR_TYPES_LIST 里；读不到就用一份兜底列表。</summary>
        private static void CollectMajorTypes()
        {
            majorTypes.Clear();
            try
            {
                List<string> majors = TypeHelper.MAJOR_TYPES_LIST;
                if (majors != null) majorTypes.AddRange(majors);
            }
            catch (Exception)
            {
            }

            if (majorTypes.Count == 0)
            {
                majorTypes.Add(TypeHelper.HOUSEHOLD_GOOD);
                majorTypes.Add(TypeHelper.FOOD);
                majorTypes.Add(TypeHelper.MATERIAL);
                majorTypes.Add(TypeHelper.TOOL);
                majorTypes.Add(TypeHelper.MEDICAL);
                majorTypes.Add(TypeHelper.WEAPON);
                majorTypes.Add(TypeHelper.SUBSTANCE);
                majorTypes.Add(TypeHelper.LUXURY_ITEM);
            }

            foreach (string major in majorTypes) labels[major] = TypeDisplayName(major);
            labels[OTHER] = "其他";
        }

        /// <summary>实例化一件物品只为读它的名字、价格和类型，不会放进任何容器。</summary>
        private static ItemEntry TryReadEntry(string id)
        {
            GameItem item;
            try
            {
                item = DirectoryMaster.Item(id, true);
            }
            catch (Exception)
            {
                return null; // 不是物品目录里的 id（技能、天赋等），跳过
            }
            if (item == null) return null;

            List<string> types;
            try
            {
                types = item.GetGameItemType();
            }
            catch (Exception)
            {
                return null;
            }
            if (types == null || types.Count == 0) return null;

            string name = null;
            try
            {
                name = item.name;
            }
            catch (Exception)
            {
            }
            if (string.IsNullOrEmpty(name)) return null;

            var entry = new ItemEntry();
            entry.Id = id;
            entry.Name = name;
            entry.Category = ResolveCategory(types);
            try
            {
                entry.SpriteAtlasPath = item.spriteAtlasPath;
                entry.SpritePath = item.spritePath;
                entry.UseSpriteFromMod = item.useSpriteFromMod;
            }
            catch (Exception)
            {
            }
            try
            {
                entry.Price = item.unitValue;
            }
            catch (Exception)
            {
            }
            try
            {
                entry.IdentifierName = item.identifierName;
            }
            catch (Exception)
            {
            }

            // 中文名、英文 id、英文显示名、分类名（例如“垃圾”）都能被搜到。
            entry.SearchKey = Normalize(entry.Name)
                + "|" + Normalize(id)
                + "|" + Normalize(entry.IdentifierName)
                + "|" + Normalize(TypeDisplayName(entry.Category));
            return entry;
        }

        /// <summary>
        /// 分类顺序：垃圾（JUNK）单独成一类，其次是游戏的主类型，
        /// 没有主类型的物品退到它自己的第一个类型，最后才是「其他」。
        /// </summary>
        private static string ResolveCategory(List<string> types)
        {
            if (types.Contains(TypeHelper.JUNK)) return TypeHelper.JUNK;

            foreach (string major in majorTypes)
            {
                if (types.Contains(major)) return major;
            }

            foreach (string type in types)
            {
                if (!string.IsNullOrEmpty(type)) return type;
            }
            return OTHER;
        }

        private static List<ItemEntry> GetGroup(string category)
        {
            List<ItemEntry> list;
            if (!groups.TryGetValue(category, out list))
            {
                list = new List<ItemEntry>();
                groups[category] = list;
            }
            return list;
        }

        /// <summary>主类型按游戏顺序，认不出的类别排在最后。</summary>
        private static void OrderCategories()
        {
            categories.Clear();
            foreach (string major in majorTypes)
            {
                if (HasEntries(major)) categories.Add(major);
            }
            foreach (string key in groups.Keys)
            {
                if (key == OTHER) continue;
                if (majorTypes.Contains(key)) continue;
                if (HasEntries(key)) categories.Add(key);
            }
            if (HasEntries(OTHER)) categories.Add(OTHER);

            foreach (List<ItemEntry> list in groups.Values) list.Sort(CompareEntries);
        }

        private static bool HasEntries(string category)
        {
            List<ItemEntry> list;
            return groups.TryGetValue(category, out list) && list.Count > 0;
        }

        private static int CompareEntries(ItemEntry a, ItemEntry b)
        {
            int byPrice = b.Price.CompareTo(a.Price);
            if (byPrice != 0) return byPrice;
            return string.CompareOrdinal(a.Name, b.Name);
        }

        /// <summary>归一化：忽略大小写、空格、下划线、连字符，让 "health pack" 也能匹配 health_pack。</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var builder = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) continue;
                if (c == '_' || c == '-' || c == '.' || c == '\'' || c == '·') continue;
                builder.Append(char.ToLowerInvariant(c));
            }
            return builder.ToString();
        }

        /// <summary>
        /// 按名字搜索：中文名、英文 id、游戏给的英文显示名都会匹配，
        /// 所以输入 "罐头" 或 "can" 都能搜到同一件东西。
        /// </summary>
        public static List<ItemEntry> Search(string query, int max, out int totalMatches)
        {
            var results = new List<ItemEntry>();
            totalMatches = 0;

            string needle = Normalize(query);
            if (needle.Length == 0) return results;

            foreach (List<ItemEntry> list in groups.Values)
            {
                foreach (ItemEntry entry in list)
                {
                    if (!Matches(entry, needle)) continue;
                    totalMatches++;
                    if (max <= 0 || results.Count < max) results.Add(entry);
                }
            }

            results.Sort(CompareEntries);
            return results;
        }

        private static bool Matches(ItemEntry entry, string needle)
        {
            return entry.SearchKey != null && entry.SearchKey.Contains(needle);
        }

        private static string TypeDisplayName(string typeConst)
        {
            try
            {
                string display = TypeHelper.GetTypeDisplayName(typeConst);
                if (!string.IsNullOrEmpty(display)) return display;
            }
            catch (Exception)
            {
            }
            return typeConst;
        }
    }
}
