using System;
using System.Linq;
using System.Reflection;
using BattleTech;
using BattleTech.UI;
using Harmony;
using HBS;
using Newtonsoft.Json;
using UnityEngine;
using static Retrainer.Logger;

namespace Retrainer
{
    public class Retrainer
    {
        public static string modDirectory;
        public static Settings modSettings;

        public static void Init(string directory, string settingsJson)
        {
            var harmony = HarmonyInstance.Create("ca.gnivler.Retrainer");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            modDirectory = directory;
            try
            {
                modSettings = JsonConvert.DeserializeObject<Settings>(settingsJson);
                if (modSettings.enableDebug)
                {
                    LogClear();
                }
            }
            catch (Exception e)
            {
                LogError(e);
                modSettings = new Settings();
            }
        }

        [HarmonyPatch(typeof(SGBarracksMWDetailPanel), nameof(SGBarracksMWDetailPanel.OnSkillsSectionClicked), MethodType.Normal)]
        public static class SGBarracksMWDetailPanel_OnSkillsSectionClicked_Patch
        {
            public static bool Prefix(SGBarracksMWDetailPanel __instance)
            {
                var hotkeyPerformed = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
                if (!hotkeyPerformed) return true;

                object instance = __instance;
                SimGameState simState = (SimGameState)instance.GetType().GetField("simState", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
                Pilot curPilot = (Pilot)instance.GetType().GetField("curPilot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);

                if (modSettings.onceOnly && curPilot.pilotDef.PilotTags.Contains("HasRetrained"))
                {
                    GenericPopupBuilder
                        .Create("Unable To Retrain", "Each pilot can only retrain once.")
                        .AddButton("Acknowledged")
                        .CancelOnEscape()
                        .AddFader(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill, 0.0f, true)
                        .Render();
                    return true;
                }

                if (!simState.ShipUpgrades.Any(u => u.Tags.Any(t => t.Contains("argo_trainingModule2"))))
                {
                    GenericPopupBuilder
                        .Create("Unable To Retrain", "You must have built the Training Module 2 upgrade aboard the Argo.")
                        .AddButton("Acknowledged")
                        .CancelOnEscape()
                        .AddFader(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill, 0.0f, true)
                        .Render();
                    return true;
                }

                if (simState.Funds < modSettings.cost)
                {
                    GenericPopupBuilder
                        .Create("Unable To Retrain", $"You need ¢{modSettings.cost:N0}.")
                        .AddButton("Acknowledged")
                        .CancelOnEscape()
                        .AddFader(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill, 0.0f, true)
                        .Render();
                    return true;
                }

                var message = modSettings.onceOnly
                    ? $"This will return skills to their original values and refund all XP.\nIt will cost ¢{modSettings.cost:N0} and each pilot can only retrain once."
                    : $"This will return skills to their original values and refund all XP.\nIt will cost ¢{modSettings.cost:N0}";

                GenericPopupBuilder
                    .Create("Retrain", message)
                        .AddButton("Cancel")
                        .AddButton("Retrain Pilot", delegate { RespecAndRefresh(__instance, curPilot, simState); })
                        .CancelOnEscape()
                        .AddFader(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill, 0.0f, true)
                        .Render();
                return false;
            }
        }

        public static void RespecAndRefresh(SGBarracksMWDetailPanel __instance, Pilot pilot, SimGameState simState)
        {
            object instance = simState;
            instance.GetType().GetMethod("RespecPilot", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, new object[] { pilot });
            simState.AddFunds(-modSettings.cost);
            pilot.pilotDef.PilotTags.Add("HasRetrained");
            __instance.DisplayPilot(pilot);
        }

        public class Settings
        {
            public bool enableDebug;
            public int cost;
            public bool onceOnly;
        }
    }
}