﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace UnderTheSea.Patches;
internal static class UseEquipPatches
{

    private static bool UpdatingEquipment = false;
    private static bool EquipingItem = false;

    /// <summary>
    ///     Set whether equipment is being updated.
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UpdateEquipment))]
    public static void UpdateRotation_Prefix(Humanoid __instance)
    {
        UpdatingEquipment = true;
    }

    /// <summary>
    ///     Reset whether equipment is being updated.
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UpdateEquipment))]
    public static void UpdateRotation_Postfix(Humanoid __instance)
    {
        UpdatingEquipment = false;
    }

    /// <summary>
    ///     Set whether item is being equiped.
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    public static void Equpitem_Prefix(Humanoid __instance)
    {
        EquipingItem = true;
    }

    /// <summary>
    ///     Reset whether equipment is being updated.
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    public static void EquipItm_Postfix(Humanoid __instance)
    {
        EquipingItem = false;
    }

    /// <summary>
    ///     Pretend player is not swimming if it is for checking about equipement.
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="__result"></param>
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Character), nameof(Character.IsSwimming))]
    public static void IsSwimming_Postfic(Character __instance, ref bool __result)
    {
        if ((!EquipingItem && !UpdatingEquipment) || !__instance || !__instance.IsPlayer())
        {
            return;
        }
        __result = false;
    }
}
