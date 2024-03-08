using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

namespace BuildingQOLs
{
    [BepInPlugin(__GUID__, __NAME__, "1.0.1")]
    public class BuildingQOLs : BaseUnityPlugin
    {
        public const string __NAME__ = "BuildingQOLs";
        public const string __GUID__ = "com.Trol1face.dsp." + __NAME__;
        public static ConfigEntry<bool> holdReleaseBeltsBuilding;
        public static ConfigEntry<bool> holdReleaseSortersBuilding;
        public static ConfigEntry<bool> disableBeltProlongation;
        public static ConfigEntry<bool> altitudeValueInCursorText;
        public static ConfigEntry<bool> shortVerOfAltitudeAndLength;
        public static ConfigEntry<bool> autoTakeBeltsAltitude;
        public static ConfigEntry<bool> ejectDronesAtFasterSpeed;
        public static ConfigEntry<float> maxEjectSpeedMultiplier;
        

        private void Awake()
        {
            // Plugin startup logic
            //Logger.LogInfo($"Plugin {__GUID__} is loaded!");
            holdReleaseBeltsBuilding = Config.Bind("General", "holdReleaseBeltsBuilding", true,
                "Enable 1 click building for Belts");
            holdReleaseSortersBuilding = Config.Bind("General", "holdReleaseSortersBuilding", true,
                "Enable 1 click building for Sorters");
            disableBeltProlongation = Config.Bind("General", "disableBeltProlongation", true,
                "If set on TRUE ending belt on ground will not start another belt in the end of builded one. In vanilla if you build end a belt into nothing, end of the belt becomes a new start and you continue to build it or cancel with RMB. This feature disables that");
            altitudeValueInCursorText = Config.Bind("General", "AltitudeValueInCursorText", true,
                "There will be a text near cursor representing current belt's altitude (instead of tips about clicking to build)");
            shortVerOfAltitudeAndLength = Config.Bind("General", "ShortVerOfAltitudeAndLength", false,
                "Enable this in addition to previous config to change form from *Altitude: n/Length: n* to short version *A: n| L: n");
            autoTakeBeltsAltitude = Config.Bind("General", "autoTakeBeltsAltitude", true,
                "If you start a belt in another belt your current altitude will change to this belt's altitude automaticly");
            ejectDronesAtFasterSpeed = Config.Bind("General", "ejectDronesAtFasterSpeed", true,
            "Changes the maximum speed at which construction drones will be launched. Value is below");
            maxEjectSpeedMultiplier = Config.Bind("General", "maxEjectSpeedMultiplier", 2.0f,
            "Eject speed limit is 'walkspeed * this multiplier'. Default is 1.2");
            
            new Harmony(__GUID__).PatchAll(typeof(Patch));
        }

        static class Patch
        {
            [HarmonyTranspiler, HarmonyPatch(typeof(BuildTool_Path), "ConfirmOperation")]
            public static IEnumerable<CodeInstruction> BuildTool_Path_ConfirmOperation_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilgen)
            {
                if (holdReleaseBeltsBuilding.Value || autoTakeBeltsAltitude.Value) 
                {
                    CodeMatcher matcher = new(instructions);           
                    if (holdReleaseBeltsBuilding.Value)
                    {
                        Label falseLabel = ilgen.DefineLabel();//jump here if condition is false, to the end of method
                        Label continueLabel = ilgen.DefineLabel();//skip onUp && waitForConfirm if onDown is true
                        List<Label> falseLabelList = new();//matcher adds only IEnumerables of labels
                        falseLabelList.Add(falseLabel);
                        List<Label> continueLabelList = new();
                        continueLabelList.Add(continueLabel);
                        FieldInfo insertAnchor = typeof(VFInput.InputValue).GetField("onDown");
                        /*
                        find
                            call      valuetype VFInput/InputValue VFInput::get__buildConfirm()
                            ldfld     bool VFInput/InputValue::onDown
                            brfalse   IL_00C7
                            call      void VFInput::UseMouseLeft()
                        */
                        matcher.Start();
                        matcher.MatchForward(true, new CodeMatch(i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo f && f == insertAnchor));
                        if (matcher.Pos != 0) {
                            /*
                                four lines i'm using. Deleting first 3, and changing with my own condition. 
                                continueLabel is added to UseMouseLeft() method
                                falseLabel is added to ldc.i4.0 in the end of method
                            */
                            
                            //target and delete the condition.
                            matcher.Advance(-1);//current pos is ldfld onDown, i'm deleting call before it too
                            matcher.RemoveInstructions(3);
                            //put our own condition
                            matcher.InsertAndAdvance(
                                new CodeInstruction(OpCodes.Call, typeof(VFInput).GetMethod("get__buildConfirm")),
                                new CodeInstruction(OpCodes.Ldfld, typeof(VFInput.InputValue).GetField("onDown")),
                                new CodeInstruction(OpCodes.Brtrue, continueLabel),
                                new CodeInstruction(OpCodes.Call, typeof(VFInput).GetMethod("get__buildConfirm")),
                                new CodeInstruction(OpCodes.Ldfld, typeof(VFInput.InputValue).GetField("onUp")),
                                new CodeInstruction(OpCodes.Brfalse, falseLabel),
                                new CodeInstruction(OpCodes.Ldarg_0),
                                new CodeInstruction(OpCodes.Ldfld, typeof(BuildTool_Path).GetField("waitForConfirm")),
                                new CodeInstruction(OpCodes.Brfalse, falseLabel),
                                new CodeInstruction(OpCodes.Ldarg_0),
                                new CodeInstruction(OpCodes.Ldfld, typeof(BuildTool_Path).GetField("pathPointCount")),
                                new CodeInstruction(OpCodes.Ldc_I4_1),
                                new CodeInstruction(OpCodes.Ble, falseLabel)
                            );
                            matcher.AddLabels(continueLabelList);
                            matcher.End();
                            //Debug.Log("END pos is" + matcher.Pos);
                            /*
                            find
                                ...
                                ldc.i4.0 <--   at the end of the method
                                ret
                            */
                            matcher.MatchBack(true, new CodeMatch(i => i.opcode == OpCodes.Ldc_I4_0));
                            matcher.AddLabels(falseLabelList);
                            
                            //foreach (CodeInstruction ins in matcher.Instructions()) Debug.Log(".. " + ins.ToString());

                        }
                    }
                    if (autoTakeBeltsAltitude.Value) 
                    {
                        MethodInfo anchor = typeof(BuildTool).GetMethod("GetObjectPose");
                        MethodInfo rep = typeof(BuildingQOLs).GetMethod("TakeOnCursorBeltAltitude");
                        FieldInfo altitude = typeof(BuildTool_Path).GetField("altitude");
                        matcher.Start();
                        /*
                        find
                            call      instance valuetype [UnityEngine.CoreModule]UnityEngine.Pose BuildTool::GetObjectPose(int32)
                            ldfld     valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 [UnityEngine.CoreModule]UnityEngine.Pose::position
                            stfld     valuetype [UnityEngine.CoreModule]UnityEngine.Vector3 BuildTool_Path::startTarget
                            br.s      IL_007D <-- insert before this
                        */
                        matcher.MatchForward(true, 
                            new CodeMatch(i => i.opcode == OpCodes.Call && i.operand is MethodInfo m && m == anchor),
                            new CodeMatch(i => i.opcode == OpCodes.Ldfld),
                            new CodeMatch(i => i.opcode == OpCodes.Stfld),
                            new CodeMatch(i => i.opcode == OpCodes.Br)
                        );
                        if (matcher.Pos != 0) {
                            matcher.InsertAndAdvance(
                                new CodeInstruction(OpCodes.Ldarg_0),
                                new CodeInstruction(OpCodes.Call, rep),
                                new CodeInstruction(OpCodes.Stfld, altitude)
                            );
                            //foreach (CodeInstruction ins in matcher.Instructions()) Debug.Log(".. " + ins.ToString());
                        }
                    }
                    return matcher.InstructionEnumeration();
                }
                return instructions;
            }

            [HarmonyTranspiler, HarmonyPatch(typeof(BuildTool_Inserter), "ConfirmOperation")]
            public static IEnumerable<CodeInstruction> BuildTool_Inserter_ConfirmOperation_Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                if (holdReleaseSortersBuilding.Value)
                {
                    FieldInfo anchor = typeof(VFInput.InputValue).GetField("onDown");
                    FieldInfo rep = typeof(VFInput.InputValue).GetField("onUp");
                    int insertIndex = -1;
                    var codes = new List<CodeInstruction>(instructions);
                    for(int i = 0; i < codes.Count; i++)
                    {
                        if(codes[i].opcode == OpCodes.Ldfld && codes[i].operand is FieldInfo o && o == anchor)
                        {
                            insertIndex = i;
                            break;

                        }
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
                    List<Label> labelList = new();//matcher adds only IEnumerables of labels
                    CodeInstruction jumpPoint = new(OpCodes.Br, jump);
                    CodeMatcher matcher = new(instructions);
                    MethodInfo anchor = typeof(BuildTool).GetMethod("get_buildPreviews");
                    labelList.Add(jump);
                    /*
                    find
                    ..ldarg.0
                    ..ldarg.0
                    ..call instance class [netstandard]System.Collections.Generic.List`1<class BuildPreview> BuildTool::get_buildPreviews()
                    ..call instance void BuildTool_Path::AddUpBuildingPathLength(class [netstandard]System.Collections.Generic.List`1<class BuildPreview>)
                    */
                    matcher.MatchForward(false,
                        new CodeMatch(i => i.opcode == OpCodes.Ldarg_0),
                        new CodeMatch(i => i.opcode == OpCodes.Ldarg_0),
                        new CodeMatch(i => i.opcode == OpCodes.Call && i.operand is MethodInfo m && m == anchor),
                        new CodeMatch(i => i.opcode == OpCodes.Call)
                    );
                    if (matcher.Pos != -1) 
                    {
                        matcher.AddLabels(labelList);
                        matcher.Start();
                        /*
                        find
                        ..ble       IL_07BF
                            WILL ADD JUMP HERE
                        ..ldc.i4.1
                        ..stloc.s   V_32
                        ..ldarg.0
                        ..call instance class [netstandard]System.Collections.Generic.List`1<class BuildPreview> BuildTool::get_buildPreviews()
                        */
                        matcher.MatchForward(false,
                        new CodeMatch(i => i.opcode == OpCodes.Ble),// only for matcher, jump point is the next one
                        new CodeMatch(i => i.opcode == OpCodes.Ldc_I4_1),//need to place jump before this instruction
                        new CodeMatch(i => i.opcode == OpCodes.Stloc_S),
                        new CodeMatch(i => i.opcode == OpCodes.Ldarg_0),
                        new CodeMatch(i => i.opcode == OpCodes.Call  && i.operand is MethodInfo m && m == anchor)
                        );
                        if (matcher.Pos != 0) {
                            matcher.Advance(1);//moving from Ble
                            matcher.Insert(jumpPoint);
                            //foreach (CodeInstruction ins in matcher.Instructions()) Debug.Log(".. " + ins.ToString());
                            return matcher.InstructionEnumeration();
                        }
                    }
                }
                return instructions;
            }

            //Adding altitude value to cursor text when building you take belt in hands
            [HarmonyTranspiler, HarmonyPatch(typeof(BuildTool_Path), "DeterminePreviews")]
            public static object BuildTool_Path_DeterminePreviews_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilgen)
            {
                if(altitudeValueInCursorText.Value) 
                {
                    CodeMatcher matcher = new(instructions);
                    MethodInfo rep = typeof(BuildingQOLs).GetMethod("CursorText_DeterminePreviews");
                    matcher.MatchForward(true, new CodeMatch(i => i.opcode == OpCodes.Ldstr && (String)i.operand == "选择起始位置"));
                    if (matcher.Pos != -1) 
                    {
                        matcher.RemoveInstructions(2);
                        matcher.Insert(new CodeInstruction(OpCodes.Call, rep));
                    }
                    return matcher.InstructionEnumeration();
                }
                return instructions;
            }
            
            //Adding altitude value & length of the belt to cursor text when you started belt building
            [HarmonyTranspiler, HarmonyPatch(typeof(BuildTool_Path), "CheckBuildConditions")]
            public static object BuildTool_Path_CheckBuildConditions_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilgen)
            {
                if(altitudeValueInCursorText.Value) 
                {
                    CodeMatcher matcher = new(instructions);
                    MethodInfo rep = typeof(BuildingQOLs).GetMethod("CursorText_CheckBuildConditions_ConditionOK");
                    matcher.MatchForward(true, new CodeMatch(i => i.opcode == OpCodes.Ldstr && (String)i.operand == "点击鼠标建造"));
                    if (matcher.Pos != -1) 
                    {
                        matcher.RemoveInstructions(8);
                        matcher.Insert(new CodeInstruction(OpCodes.Call, rep));
                    }
                    return matcher.InstructionEnumeration();
                }
                return instructions;
            }

            [HarmonyTranspiler, HarmonyPatch(typeof(Mecha), "CheckEjectConstructionDroneCondition")]
            public static object Mecha_CheckEjectConstructionDroneCondition_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilgen)
            {
                if(ejectDronesAtFasterSpeed.Value) 
                {
                    CodeMatcher matcher = new(instructions);
                    FieldInfo anchor = typeof(Mecha).GetField("walkSpeed");
                    matcher.MatchForward(true, 
                        new CodeMatch(i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo f && f == anchor),
                        new CodeMatch(i => i.opcode == OpCodes.Ldc_R4)
                        );
                    foreach (CodeInstruction ins in matcher.Instructions()) Debug.Log(".. " + ins.ToString());

                    if (matcher.Pos != -1) 
                    {
                        matcher.SetOperandAndAdvance(maxEjectSpeedMultiplier.Value);
                    }
                    return matcher.InstructionEnumeration();
                }
                return instructions;
            }

        }

        //Method from SnapBeltHeight mod
        public static int ObjectAltitude(Vector3 pos)
        {
            PlanetAuxData aux = GameMain.mainPlayer.controller.actionBuild.planetAux;
            if (aux == null)
            {
                return 0;
            }
            Vector3 ground = aux.Snap(pos, true);
            float distance = Vector3.Distance(pos, ground);
            return (int)Math.Round(distance / PlanetGrid.kAltGrid);
        }

        public static int TakeOnCursorBeltAltitude() {
            BuildTool_Path tool = GameMain.mainPlayer.controller.actionBuild.pathTool;
            int result = tool.altitude;
            if (tool.ObjectIsBelt(tool.castObjectId))
            {
                result = ObjectAltitude(tool.castObjectPos);
            }
            return result;
        }
        public static String CursorText_DeterminePreviews() {
            BuildTool_Path tool = GameMain.mainPlayer.controller.actionBuild.pathTool;
            String altitude = (tool.altitude + 1).ToString();
            if (shortVerOfAltitudeAndLength.Value) {
                return "A: " + altitude + " | L: 0";
            }
            return "Altitude: " + altitude + System.Environment.NewLine + "Length: 0";
        }
        public static String CursorText_CheckBuildConditions_ConditionOK() {
            BuildTool_Path tool = GameMain.mainPlayer.controller.actionBuild.pathTool;
            String altitude = (tool.altitude + 1).ToString();
            String length = tool.pathPointCount.ToString();
            if (shortVerOfAltitudeAndLength.Value) {
                return "A: " + altitude + " | L: " + length;
            }
            return "Altitude: " + altitude + System.Environment.NewLine + "Length: " + length;
        }

    }
}
