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
using System.ComponentModel;

namespace FastBeltsSortersBuild
{
    [BepInPlugin(__GUID__, __NAME__, "1.0.0")]
    public class FastBeltsSortersBuild : BaseUnityPlugin
    {
        public const string __NAME__ = "FastBeltsSortersBuild";
        public const string __GUID__ = "com.Trol1face.dsp." + __NAME__;
        public static ConfigEntry<bool> enableForBelts;
        public static ConfigEntry<bool> enableForSorters;
        public static ConfigEntry<bool> disableBeltProlongation;

        private void Awake()
        {
            // Plugin startup logic
            //Logger.LogInfo($"Plugin {__GUID__} is loaded!");
            enableForBelts = Config.Bind("General", "enableForBelts", true,
                "Enable 1 click building for Belts");
            enableForSorters = Config.Bind("General", "enableForSorters", true,
                "Enable 1 click building for Sorters");
            disableBeltProlongation = Config.Bind("General", "disableBeltProlongation", true,
                "If set on TRUE ending belt on ground will not start another belt in the end of builded one. In vanilla if you build end a belt into nothing, end of the belt becomes a new start and you continue to build it or cancel with RMB. This feature disables that");
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
                    int continueLabelIndex = -1;//skip onUp && waitForConfirm if onDown is true
                    int falseLabelIndex = -1; //jump here if condition is false, to the end of method
                    // Grab all the instructions
                    var codes = new List<CodeInstruction>(instructions);
                    for(int i = 0; i < codes.Count; i++)
                    {
                        if(!foundInsertIndex && codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo f && f == insertAnchor)
                        {
                            //Debug.Log(" insertIndex Detected in this line: " + i);
                            foundInsertIndex = true;// Skip this if if we found it once
                            insertIndex = i;
                        }
                        if(!foundContinueLabelIndex && codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo m && m == continueLabelAnchor){
                            //Debug.Log(" continueLabelIndex Detected in this line: " + i);
                            foundContinueLabelIndex = true;// Skip this if if we found it once
                            continueLabelIndex = i;
                        }
                        if(codes[i].opcode == OpCodes.Ldc_I4_0) {
                            //Debug.Log(" falseLabelIndex Detected in this line: " + i);
                            falseLabelIndex = i;//Need the last of this opcodes
                        }
                    }
                    if (insertIndex > -1 && continueLabelIndex > -1 && falseLabelIndex > -1)
                    {
                        /*
                            four lines i'm using. Deleting first 3, and changing with my own condition. 
                            continueLabel is added to UseMouseLeft() method
                            falseLabel is added to ldc.i4.0 in the end of method
                        */
                        /*
                            IL_0000: call      valuetype VFInput/InputValue VFInput::get__buildConfirm()
                            IL_0005: ldfld     bool VFInput/InputValue::onDown
                            IL_000A: brfalse   IL_00C7
                            IL_000F: call      void VFInput::UseMouseLeft()
                            ...
                            IL_00C7: ldc.i4.0
                            IL_00C8: ret
                        */

                        //target and delete the condition.
                        insertIndex -= 1;//before onDown field here is a line with method get__buildConfirm, i'm deleting that too
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
            
            [HarmonyTranspiler, HarmonyPatch(typeof(BuildTool_Path), "CreatePrebuilds")]
            public static IEnumerable<CodeInstruction> BuildTool_Path_CreatePrebuilds_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilgen)
            {
                if (disableBeltProlongation.Value)
                {
                    Label jump = ilgen.DefineLabel();
                    MethodInfo anchor = typeof(BuildTool).GetMethod("get_buildPreviews");
                    bool jumpDestinationIndexFound = false;
                    int insertIndex = -1;
                    int jumpDestinationIndex = -1;
                    //Grab all the instructions
                    var codes = new List<CodeInstruction>(instructions);
                    for(int i = codes.Count-1; i >= 5; i--)
                    {
                        //Debug.Log("  [" + i + "]: " + codes[i].ToString());
                        if(!jumpDestinationIndexFound) {
                            if(codes[i].opcode == OpCodes.Call){
                                //Debug.Log("-----------Found Call at " + i);
                                if(codes[i-1].opcode == OpCodes.Call && codes[i-1].operand is MethodInfo m1 && m1 == anchor){
                                    //Debug.Log("-----------Found buildpreviews at " + (i-1));
                                    if(codes[i-2].opcode == OpCodes.Ldarg_0 && codes[i-3].opcode == OpCodes.Ldarg_0) {
                                        //Debug.Log("-----------Found jump destination: " + i);
                                        jumpDestinationIndexFound = true;
                                        jumpDestinationIndex = i - 3;
                                    }
                                }
                            }
                        }
                        if(codes[i].opcode == OpCodes.Call && codes[i].operand is MethodInfo m2 && m2 == anchor)
                        {
                            //Debug.Log("-----------Found anchor at " + i);
                            if(codes[i-1].opcode == OpCodes.Ldarg_0)
                            {
                                //Debug.Log("-----------Found Ldarg_0 " + (i - 1));
                                if(codes[i-2].opcode == OpCodes.Stloc_S)
                                {
                                    //Debug.Log("-----------Found Stloc_S " + (i - 2));
                                    if(codes[i-3].opcode == OpCodes.Ldc_I4_1)
                                    {
                                        //Debug.Log("-----------Found Ldc_I4_1, insertIndex is here" + (i - 3));
                                        if(codes[i-4].opcode == OpCodes.Ble) {
                                            insertIndex = i - 3;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (insertIndex > -1 && jumpDestinationIndex > -1)
                    {
                        codes[jumpDestinationIndex].labels.Add(jump);
                        codes.Insert(insertIndex, new CodeInstruction(OpCodes.Br, jump));
                        //Debug.Log("Added label");
                    }
                    //debug log
                    //for(int i = 500; i < codes.Count; i++) Debug.Log("[" + i + "]: " + codes[i].ToString());
                    return codes.AsEnumerable();
                }
                return instructions;
            }
        }


    }
}
