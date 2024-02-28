using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace FastBeltsSortersBuild
{
    [BepInPlugin(__GUID__, __NAME__, "1.0.0")]
    public class FastBeltsSortersBuild : BaseUnityPlugin
    {
        public const string __NAME__ = "FastBeltsSortersBuild";
        public const string __GUID__ = "com.Trol1face.dsp." + __NAME__;
        public static ConfigEntry<bool> enableForBelts;
        public static ConfigEntry<bool> enableForSorters;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {__GUID__} is gladly loaded!");
            enableForBelts = Config.Bind("General", "enableForBelts", true,
                "Enable 1 click building for Belts");
            enableForSorters = Config.Bind("General", "enableForSorters", true,
                "Enable 1 click building for Sorters");
            new Harmony(__GUID__).PatchAll(typeof(Patch));
        }

        static class Patch
        {
            
            [HarmonyPrefix, HarmonyPatch(typeof(BuildTool_Path), "ConfirmOperation")]
            public static bool BuildTool_Path_ConfirmOperation_Prefix(BuildTool_Path __instance, bool condition, ref bool __result)
            {
                if (enableForBelts.Value)
                {
                    if (VFInput._buildConfirm.onDown || (VFInput._buildConfirm.onUp && (__instance.controller.cmd.stage > 0 && (__instance.waitForConfirm && condition))))
                    {
                        VFInput.UseMouseLeft();
                        if (__instance.controller.cmd.stage == 0)
                        {
                            if (condition)
                            {
                                __instance.startObjectId = __instance.castObjectId;
                                __instance.startNearestAddonAreaIdx = __instance.CalculateNearestAddonAreaIdx(__instance.startObjectId, __instance.castGroundPosSnapped);
                                if (__instance.startObjectId != 0)
                                {
                                    __instance.startTarget = __instance.GetObjectPose(__instance.startObjectId).position;
                                }
                                else
                                {
                                    __instance.startTarget = __instance.cursorTarget;
                                }
                                __instance.pathPointCount = 0;
                                __instance.controller.cmd.stage = 1;
                                __result = false;
                            }
                        }
                        else if (__instance.controller.cmd.stage > 0 && (__instance.waitForConfirm && condition))
                        {
                            __instance.controller.cmd.stage = 0;
                            __result = true;
                        }
                        return false;
                    }
                }
                return true;
            }
            [HarmonyPrefix, HarmonyPatch(typeof(BuildTool_Inserter), "ConfirmOperation")]
            public static bool BuildTool_Insterter_ConfirmOperation_Prefix(BuildTool_Inserter __instance, bool condition, ref bool __result)
            {
                if (enableForSorters.Value)
                {
                    if (condition)
                    {
                        UICursor.SetCursor(ECursor.TargetB);
                    }
                    if (__instance.waitForConfirm && VFInput._buildConfirm.onUp && condition)
                    {
                        __instance.controller.cmd.stage = 0;
                        __result = true;
                        return false;
                    }
                    __result = false;
                    return false;
                }
                return true;
            }
        }


    }
}
