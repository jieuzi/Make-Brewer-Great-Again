using System;
using HarmonyLib;

namespace MakeBrewerGreatAgain
{
    /// <summary>
    /// 库房电话的名字：StorePhoneClient.GetLocalizedDisplayName() 只认游戏本地化表里的
    /// ClientName 键，模组没法往那张表里塞东西，所以这里按 dialogFuncId 认人，直接给名字。
    /// </summary>
    [HarmonyPatch(typeof(StorePhoneClient), nameof(StorePhoneClient.GetLocalizedDisplayName))]
    internal static class WarehousePhoneNamePatch
    {
        private static void Postfix(StorePhoneClient __instance, ref string __result)
        {
            if (__instance == null) return;
            if (__instance.dialogFuncId != RedImpWarehouse.DialogFuncId) return;
            __result = RedImpWarehouse.DisplayName;
        }
    }
}
