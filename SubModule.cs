using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Engine.Screens;
using TaleWorlds.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Missions;

namespace UsefulUiDebug
{
    public class SubModule : MBSubModuleBase
    {
        Harmony harmony = new Harmony("UsefulUiDebug");
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            harmony.PatchAll();

            //UIConfig.DebugModeEnabled = true;
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();

        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

        }

        public static bool newDebugTick = true;

        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            newDebugTick = true;


            if (Input.IsKeyDown(InputKey.LeftShift) && Input.IsKeyReleased(InputKey.F1))
            {
                UIConfig.DebugModeEnabled = !UIConfig.DebugModeEnabled;
            }
        }
    }

    [HarmonyPatch(typeof(UIContext), "UpdateWidgetInformationPanel")]
    public class UIContextPatch : HarmonyPatch
    {

        static Dictionary<Widget, (int, string)> rootToMovie = new Dictionary<Widget, (int, string)>();
        static bool screenManagerLayers = true;
        static bool gameStates = true;

        static bool missionViews = false;
        static bool missionLogics = false;
        static bool missionBehaviors = false;
        static void PageTopPatch(UIContext uiContext)
        {
            if (SubModule.newDebugTick)
            {
                rootToMovie.Clear();

                SubModule.newDebugTick = false;

                Imgui.Checkbox("ScreenManager layers", ref screenManagerLayers); Imgui.SameLine(0f, 10f);
                Imgui.Checkbox("GameStates", ref gameStates); Imgui.SameLine(0f, 10f);


                Imgui.Checkbox("Mission Views", ref missionViews); Imgui.SameLine(0f, 10f);
                Imgui.Checkbox("Mission Logics", ref missionLogics); Imgui.SameLine(0f, 10f);
                Imgui.Checkbox("Mission Behaviors", ref missionBehaviors);

                int i = 0;
                if (screenManagerLayers)
                {
                    uiContext.TwoDimensionContext.DrawDebugText("ScreenManager Layers:");
                    foreach (var l in ScreenManager.SortedLayers)
                    {
                        string info = "";

                        if (l is GauntletLayer gauntletLayer)
                        {
                            foreach (var movieAndVM in gauntletLayer._moviesAndDatasources)
                            {
                                info += string.Format("({0}, {1}) ", movieAndVM.Item1.MovieName, movieAndVM.Item2.GetType().Name);
                                rootToMovie[movieAndVM.Item1.RootWidget] = (i, movieAndVM.Item1.MovieName);
                            }
                        }

                        uiContext.TwoDimensionContext.DrawDebugText(string.Format("{0}) {1}: {2}", i, l.GetType(), info));
                        i++;
                    }
                    uiContext.TwoDimensionContext.DrawDebugText("");
                }
                if (gameStates)
                {
                    uiContext.TwoDimensionContext.DrawDebugText("GameStates:");
                    i = 0;
                    if (GameStateManager.Current != null)
                    {
                        foreach (var s in GameStateManager.Current.GameStates)
                        {
                            uiContext.TwoDimensionContext.DrawDebugText(string.Format("{0}) {1}", i++, s.ToString()));
                        }
                    }
                    uiContext.TwoDimensionContext.DrawDebugText("");
                }
                if (missionViews && Mission.Current != null)
                {
                    uiContext.TwoDimensionContext.DrawDebugText("Mission Views:");
                    var behaviors = Mission.Current.MissionBehaviors;
                    var views = (from v in behaviors
                                 where v is MissionView
                                 orderby ((MissionView)v).ViewOrderPriority
                                 select v).ToList<MissionBehavior>();
                    i = 0;
                    foreach (var mb in views)
                    {
                        uiContext.TwoDimensionContext.DrawDebugText(string.Format("{0}) {1}", i++, mb.ToString()));
                    }

                    uiContext.TwoDimensionContext.DrawDebugText("");
                }
                if (missionLogics && Mission.Current != null)
                {
                    uiContext.TwoDimensionContext.DrawDebugText("Mission Logics:");
                    i = 0;
                    foreach (var b in Mission.Current.MissionBehaviors)
                    {
                        if (b is MissionLogic ml)
                        {
                            uiContext.TwoDimensionContext.DrawDebugText(string.Format("{0}) {1}", i++, ml.ToString()));
                        }
                    }

                    uiContext.TwoDimensionContext.DrawDebugText("");
                }
                if (missionBehaviors && Mission.Current != null)
                {

                    uiContext.TwoDimensionContext.DrawDebugText("Mission Behaviors:");
                    i = 0;
                    foreach (var mb in Mission.Current.MissionBehaviors)
                    {
                        if (mb is not MissionView && mb is not MissionLogic)
                        {
                            uiContext.TwoDimensionContext.DrawDebugText(string.Format("{0}) {1}", i++, mb.ToString()));
                        }
                    }

                    uiContext.TwoDimensionContext.DrawDebugText("");
                }

            }
        }
        static void HoveredWidgetPatch(UIContext uiContext)
        {
            foreach (var p in uiContext.EventManager.HoveredView.Parents)
            {
                if (rootToMovie.ContainsKey(p))
                {
                    var x = rootToMovie[p];
                    uiContext.TwoDimensionContext.DrawDebugText(string.Format("Hovered widget layer: {0}, movie: {1}", x.Item1, x.Item2));
                    return;
                }
            }

            uiContext.TwoDimensionContext.DrawDebugText("Unknown widget source");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var beginDebugPanelMethod = AccessTools.Method("TaleWorlds.TwoDimension.TwoDimensionContext:BeginDebugPanel");
            var getIdMethod = AccessTools.Method("TaleWorlds.GauntletUI.Widget:get_Id");

            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode != OpCodes.Callvirt) continue;
                if (instruction.operand == (object)beginDebugPanelMethod)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UIContextPatch), nameof(PageTopPatch)));
                }
                else if (instruction.operand == (object)getIdMethod)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UIContextPatch), nameof(HoveredWidgetPatch)));
                }
            }
        }

    }
}