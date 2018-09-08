using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using BattleTech;
using BattleTech.UI;
using Harmony;
using HBS;
using HeurekaGames;
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

                if (!modSettings.onceOnly && __instance.curPilot.pilotDef.PilotTags.Contains("HasRetrained"))
                {
                    GenericPopupBuilder
                        .Create("Unable To Retrain", "Each pilot can only retrain once.")
                        .AddButton("Acknowledged")
                        .CancelOnEscape()
                        .AddFader(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill, 0.0f, true)
                        .Render();
                    return true;
                }

                if (!__instance.simState.shipUpgrades.Any(u => u.Tags.Any(t => t.Contains("argo_trainingModule2"))))
                {
                    GenericPopupBuilder
                        .Create("Unable To Retrain", "You must have built a Training Module 2 upgrade aboard the Argo.")
                        .AddButton("Acknowledged")
                        .CancelOnEscape()
                        .AddFader(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill, 0.0f, true)
                        .Render();
                    return true;
                }

                if (__instance.simState.Funds < 500_000)
                {
                    GenericPopupBuilder
                        .Create("Unable To Retrain", $"Not enough money.  This will cost will cost ¢{modSettings.cost} (and you have ¢{__instance.simState.Funds}.")
                        .AddButton("Acknowledged")
                        .CancelOnEscape()
                        .AddFader(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill, 0.0f, true)
                        .Render();
                    return true;
                }

                GenericPopupBuilder
                    .Create("Retrain", "This will return skills to their original values and refund XP.\nIt will cost ¢500,000 and each pilot can only retrain once.")
                    .AddButton("Cancel")
                    .AddButton("Retrain Pilot", delegate { RespecAndRefresh(__instance, __instance.curPilot); })
                    .CancelOnEscape()
                    .AddFader(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill, 0.0f, true)
                    .Render();
                return false;
            }
        }

        public static void RespecAndRefresh(SGBarracksMWDetailPanel __instance, Pilot pilot)
        {
            __instance.simState.RespecPilot(pilot);
            __instance.simState.AddFunds(-500_000);
            pilot.pilotDef.PilotTags.Add("HasRetrained");
            __instance.DisplayPilot(pilot);
        }

        public class Settings
        {
            public bool enableDebug = true;
            public int cost;
            public bool onceOnly;
        }
    }
}