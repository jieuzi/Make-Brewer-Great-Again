using System;
using HarmonyLib;
using UnityEngine;

namespace DevEverything
{
    /// <summary>
    /// 模组入口。加载器在主菜单阶段调用 Init / OnEnable，此时还没有载入存档。
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
            // 在 F9 开发菜单的 Spawn 栏里加按钮（主菜单阶段就能注册）。
            ModHelper.AddDebugFunction(
                Manifest.ID,
                ModHelper.DEBUG_CATEGORY_SPAWN,
                "全物品商店",
                AllItemsBrowser.Open,
                tall: true);

            // 按物品导出素材的菜单（和全物品商店同一套 UI，点谁导谁）。
            ModHelper.AddDebugFunction(
                Manifest.ID,
                ModHelper.DEBUG_CATEGORY_SPAWN,
                "素材导出",
                SpriteExportBrowser.Open,
                tall: true);

            // 一键生成各种水质的瓶装水（测酿酒评分用）
            ModHelper.AddDebugFunction(
                Manifest.ID,
                ModHelper.DEBUG_CATEGORY_SPAWN,
                "瓶装水生成",
                WaterSpawner.Open,
                tall: true);


        }

        public void OnDisable()
        {
            ModHelper.RemoveDebugFunctions(Manifest.ID);

        }
    }
}
