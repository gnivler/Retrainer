using System;
using System.Linq;
using System.Reflection;
using BattleTech;
using BattleTech.UI;
using Harmony;
using HBS;
using Newtonsoft.Json;
using UnityEngine;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable once ClassNeverInstantiated.Global
// ReSharper disable InconsistentNaming

namespace Retrainer
{
    public class Retrainer
    {
        private static Settings modSettings;

        public static void Init(string settingsJson)
        {
            Log("Startup");

            try
            {
                modSettings = JsonConvert.DeserializeObject<Settings>(settingsJson);
            }
            catch (Exception e)
            {
                Log(e);
                modSettings = new Settings();
            }

            var harmony = HarmonyInstance.Create("ca.gnivler.BattleTech.Retrainer");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        private static void Log(object input)
        {
            FileLog.Log($"[Retrainer] {input}");
        }

        [HarmonyPatch(typeof(SGBarracksMWDetailPanel), nameof(SGBarracksMWDetailPanel.OnSkillsSectionClicked), MethodType.Normal)]
        public static class SGBarracksMWDetailPanel_OnSkillsSectionClicked_Patch
        {
            private static readonly UIColorRef backfill = LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill;

            public static bool Prefix(SGBarracksMWDetailPanel __instance, Pilot ___curPilot)
            {
                var hotkeyPerformed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (!hotkeyPerformed)
                {
                    return true;
                }

                var simState = UnityGameInstance.BattleTechGame.Simulation;
                if (modSettings.onceOnly && ___curPilot.pilotDef.PilotTags.Contains("HasRetrained"))
                {
                    GenericPopupBuilder
                        .Create("Unable To Retrain", "Each pilot can only retrain once.")
                        .AddButton("Acknowledged")
                        .CancelOnEscape()
                        .AddFader(backfill)
                        .Render();
                    return true;
                }

                if (modSettings.trainingModuleRequired && !simState.ShipUpgrades.Any(u => u.Tags.Any(t => t.Contains("argo_trainingModule2"))))
                {
                    GenericPopupBuilder
                        .Create("Unable To Retrain", "You must have built the Training Module 2 upgrade aboard the Argo.")
                        .AddButton("Acknowledged")
                        .CancelOnEscape()
                        .AddFader(backfill)
                        .Render();
                    return true;
                }

                if (simState.Funds < modSettings.cost)
                {
                    GenericPopupBuilder
                        .Create("Unable To Retrain", $"You need ¢{modSettings.cost:N0}.")
                        .AddButton("Acknowledged")
                        .CancelOnEscape()
                        .AddFader(backfill)
                        .Render();
                    return true;
                }

                var message = modSettings.onceOnly
                    ? $"This will set skills to 1 and refund all XP.\nIt will cost ¢{modSettings.cost:N0} and each pilot can only retrain once."
                    : $"This will set skills to 1 and refund all XP.\nIt will cost ¢{modSettings.cost:N0}";

                GenericPopupBuilder
                    .Create("Retrain", message)
                    .AddButton("Cancel")
                    .AddButton("Retrain Pilot", () => RespecAndRefresh(__instance, ___curPilot))
                    .CancelOnEscape()
                    .AddFader(backfill)
                    .Render();
                return false;
            }
        }

        private static void RespecAndRefresh(SGBarracksMWDetailPanel __instance, Pilot pilot)
        {
            WipePilotStats(pilot);
            Log(UnityGameInstance.BattleTechGame.Simulation.GetType().GetMethod("AddFunds"));
            UnityGameInstance.BattleTechGame.Simulation.AddFunds(-modSettings.cost);
            pilot.pilotDef.PilotTags.Add("HasRetrained");
            __instance.DisplayPilot(pilot);
        }

        // copied and changed from RespecPilot()
        private static void WipePilotStats(Pilot pilot)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var pilotDef = pilot.pilotDef.CopyToSim();

            foreach (var value in sim.Constants.Story.CampaignCommanderUpdateTags)
            {
                if (!sim.CompanyTags.Contains(value))
                {
                    sim.CompanyTags.Add(value);
                }
            }

            try
            {
                Log($"Base:\t {pilotDef.BaseGunnery} / {pilotDef.BasePiloting} / {pilotDef.BaseGuts} / {pilotDef.BaseTactics}");
                Log($"Bonus:\t {pilotDef.BonusGunnery} / {pilotDef.BonusPiloting} / {pilotDef.BonusGuts} / {pilotDef.BonusTactics}");

                var num = 0;
                num += sim.GetLevelRangeCost(1, pilotDef.SkillGunnery - 1);
                num += sim.GetLevelRangeCost(1, pilotDef.SkillPiloting - 1);
                num += sim.GetLevelRangeCost(1, pilotDef.SkillGuts - 1);
                num += sim.GetLevelRangeCost(1, pilotDef.SkillTactics - 1);

                Traverse.Create(pilotDef).Property("BaseGunnery").SetValue(1);
                Traverse.Create(pilotDef).Property("BasePiloting").SetValue(1);
                Traverse.Create(pilotDef).Property("BaseGuts").SetValue(1);
                Traverse.Create(pilotDef).Property("BaseTactics").SetValue(1);
                Traverse.Create(pilotDef).Property("BonusGunnery").SetValue(1);
                Traverse.Create(pilotDef).Property("BonusPiloting").SetValue(1);
                Traverse.Create(pilotDef).Property("BonusGuts").SetValue(1);
                Traverse.Create(pilotDef).Property("BonusTactics").SetValue(1);

                pilotDef.abilityDefNames.Clear();
                pilotDef.SetSpentExperience(0);
                pilotDef.ForceRefreshAbilityDefs();
                pilotDef.ResetBonusStats();
                pilot.FromPilotDef(pilotDef);
                pilot.AddExperience(0, "Respec", num);
            }
            catch (Exception ex)
            {
                Log(ex);
            }
        }

        public class Settings
        {
            public int cost;
            public bool onceOnly;
            public bool trainingModuleRequired;
        }
    }
}
