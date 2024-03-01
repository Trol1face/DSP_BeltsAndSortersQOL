using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

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
            //Logger.LogInfo($"Plugin {__GUID__} is loaded!");
            enableForBelts = Config.Bind("General", "enableForBelts", true,
                "Enable 1 click building for Belts");
            enableForSorters = Config.Bind("General", "enableForSorters", true,
                "Enable 1 click building for Sorters");
            new Harmony(__GUID__).PatchAll(typeof(Patch));
        }

        static class Patch
        {
            [HarmonyTranspiler, HarmonyPatch(typeof(BuildTool_Path), "ConfirmOperation")]
            public static IEnumerable<CodeInstruction> BuildTool_Path_ConfirmOperation_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilgen)
            {
                if (enableForBelts.Value)
                {
                    Label falseLabel = ilgen.DefineLabel();
                    Label continueLabel = ilgen.DefineLabel();
                    FieldInfo insertAnchor = typeof(VFInput.InputValue).GetField("onDown");
                    MethodInfo continueLabelAnchor = typeof(VFInput).GetMethod("UseMouseLeft");
                    bool foundInsertIndex = false;
                    bool foundContinueLabelIndex = false;
                    int insertIndex = -1;
                    int continueLabelIndex = -1;
                    int falseLabelIndex = -1;
                    // Grab all the instructions
                    var codes = new List<CodeInstruction>(instructions);
                    for(int i = 0; i < codes.Count; i++)
                    {
                        if(!foundInsertIndex && codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo f && f == insertAnchor)
                        {
                            //Debug.Log(" insertIndex Detected in this line: " + i);
                            foundInsertIndex = true;
                            insertIndex = i;
                        }
                        if(!foundContinueLabelIndex && codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo m && m == continueLabelAnchor){
                            //Debug.Log(" continueLabelIndex Detected in this line: " + i);
                            foundContinueLabelIndex = true;
                            continueLabelIndex = i;
                        }
                        if(codes[i].opcode == OpCodes.Ldc_I4_0) {
                            //Debug.Log(" falseLabelIndex Detected in this line: " + i);
                            falseLabelIndex = i;
                        }
                    }
                    if (insertIndex > -1 && continueLabelIndex > -1 && falseLabelIndex > -1)
                    {
                        //target and delete the condition
                        insertIndex -= 1;    
                        codes[continueLabelIndex].labels.Add(continueLabel);
                        codes[falseLabelIndex].labels.Add(falseLabel);
                        codes.RemoveRange(insertIndex, 3);
                        //put our own condition
                        codes.Insert(insertIndex++, new CodeInstruction(OpCodes.Call, typeof(VFInput).GetMethod("get__buildConfirm")));
                        codes.Insert(insertIndex++, new CodeInstruction(OpCodes.Ldfld, typeof(VFInput.InputValue).GetField("onDown")));  
                        codes.Insert(insertIndex++, new CodeInstruction(OpCodes.Brtrue, continueLabel));
                        codes.Insert(insertIndex++, new CodeInstruction(OpCodes.Call, typeof(VFInput).GetMethod("get__buildConfirm")));
                        codes.Insert(insertIndex++, new CodeInstruction(OpCodes.Ldfld, typeof(VFInput.InputValue).GetField("onUp")));  
                        codes.Insert(insertIndex++, new CodeInstruction(OpCodes.Brfalse, falseLabel));
                        codes.Insert(insertIndex++, new CodeInstruction(OpCodes.Ldarg_0));
                        codes.Insert(insertIndex++, new CodeInstruction(OpCodes.Ldfld, typeof(BuildTool_Path).GetField("waitForConfirm"))); 
                        codes.Insert(insertIndex++, new CodeInstruction(OpCodes.Brfalse, falseLabel));
                    }
                    //debug log
                    //for(int i = 0; i < codes.Count; i++) Debug.Log("[" + i + "]: " + codes[i].ToString());

                    return codes.AsEnumerable();
                }
                return instructions;
            }

            [HarmonyTranspiler, HarmonyPatch(typeof(BuildTool_Inserter), "ConfirmOperation")]
            public static IEnumerable<CodeInstruction> BuildTool_Inserter_ConfirmOperation_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                if (enableForSorters.Value)
                {
                    FieldInfo anchor = typeof(VFInput.InputValue).GetField("onDown");
                    FieldInfo rep = typeof(VFInput.InputValue).GetField("onUp");
                    int insertIndex = -1;
                    // Grab all the instructions
                    var codes = new List<CodeInstruction>(instructions);
                    for(int i = 0; i < codes.Count; i++)
                    {
                        if(codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo o && o == anchor)
                        {
                        //Debug.Log(" insertIndex etected in this line: " + i);
                            insertIndex = i;
                            break;

                        }
                        //Debug.Log("");
                    }
                    if (insertIndex > -1)
                    {
                        codes[insertIndex].operand = rep;
                    }
                    return codes.AsEnumerable();
                }
                return instructions;
            }
        }


    }
}
