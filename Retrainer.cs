using BattleTech;
using BattleTech.UI;
using HarmonyLib;
using HBS;
using Localize;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Retrainer {
  public class RetrainButtonSupervisor: MonoBehaviour {
    public SGBarracksMWDetailPanel parent { get; set; } = null;
    private HBSDOTweenButton retrainButton = null;
    private bool ui_inited = true;
    public void initUI() {
      try {
        if (retrainButton == null) { return; }
        HorizontalLayoutGroup layout = retrainButton.transform.parent.gameObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 15f;
        var pos = retrainButton.transform.parent.transform.localPosition;
        pos.x -= (retrainButton.gameObject.GetComponent<RectTransform>().sizeDelta.x*0.6f);
        retrainButton.transform.parent.transform.localPosition = pos;
      } catch(Exception e) {
        Log.TWL(0, e.ToString());
      }
      ui_inited = true;
    }
    public void Update() {
      if (ui_inited == false) { this.initUI(); }
    }
    public void OnClicked() {
      if (Core.Retrain(this.parent)) { this.parent.DisplayPilot(this.parent.curPilot); }
    }

    public void Instantine() {
      this.retrainButton = GameObject.Instantiate(this.parent.advancementReset.gameObject).GetComponent<HBSDOTweenButton>();
      this.retrainButton.gameObject.transform.SetParent(this.parent.advancementReset.gameObject.transform.parent);
      this.retrainButton.gameObject.transform.SetAsFirstSibling();
      this.retrainButton.OnClicked = new UnityEngine.Events.UnityEvent();
      this.retrainButton.OnClicked.AddListener(new UnityEngine.Events.UnityAction(OnClicked));
      this.retrainButton.SetText(Strings.CurrentCulture==Strings.Culture.CULTURE_RU_RU?"ПЕРЕПОДГОТОВКА":"RETRAIN");
      this.retrainButton.SetState(ButtonState.Enabled);
      this.parent.advancement.gameObject.FindObject<Image>("line")?.gameObject.SetActive(false);
      ui_inited = false;
    }
    public void Init(SGBarracksMWDetailPanel parent) {
      this.parent = parent;
      if (retrainButton == null) { this.Instantine(); }
    }
  }
  [HarmonyPatch(typeof(SGBarracksMWDetailPanel))]
  [HarmonyPatch("Initialize")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(SimGameState) })]
  public static class MechComponent_InitPassiveSelfEffects {
    public static void Prefix(SGBarracksMWDetailPanel __instance, SimGameState state) {
      try {
        RetrainButtonSupervisor retrainBnt = __instance.gameObject.GetComponent<RetrainButtonSupervisor>();
        if (retrainBnt == null) { retrainBnt = __instance.gameObject.AddComponent<RetrainButtonSupervisor>(); }
        retrainBnt.Init(__instance);
      } catch (Exception e) {
        Log.TWL(0, e.ToString());
      }
    }
  }

  public static class Core {
    private static Settings modSettings;
    public static T FindObject<T>(this GameObject go, string name) where T : Component {
      T[] arr = go.GetComponentsInChildren<T>(true);
      foreach (T component in arr) { if (component.gameObject.transform.name == name) { return component; } }
      return null;
    }

    public static void Init(string directory,string settingsJson) {
      Log.BaseDirectory = directory;
      Log.InitLog();
      try {
        Log.TWL(0, "Initing... " + directory + " version: " + Assembly.GetExecutingAssembly().GetName().Version);
        modSettings = JsonConvert.DeserializeObject<Settings>(settingsJson);
      } catch (Exception e) {
        Log.WL(0,e.ToString());
        modSettings = new Settings();
      }

      var harmony = new Harmony("ca.gnivler.BattleTech.Retrainer");
      harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
    public static bool Retrain(SGBarracksMWDetailPanel instance) {
      UIColorRef backfill = LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill;
      var simState = instance.simState;
      if (modSettings.onceOnly && instance.curPilot.pilotDef.PilotTags.Contains("HasRetrained")) {
        GenericPopupBuilder
            .Create(Strings.CurrentCulture == Strings.Culture.CULTURE_RU_RU ? "Не могу переподготовить" : "Unable To Retrain"
              , Strings.CurrentCulture == Strings.Culture.CULTURE_RU_RU ? "Каждый пилот может пилот может быть переподготовлен только один раз" : "Each pilot can only retrain once."
            )
            .AddButton(Strings.CurrentCulture == Strings.Culture.CULTURE_RU_RU ? "Понятненько" : "Acknowledged")
            .CancelOnEscape()
            .AddFader(backfill)
            .Render();
        return false;
      }

      if (modSettings.trainingModuleRequired && !simState.ShipUpgrades.Any(u => u.Tags.Any(t => t.Contains("argo_trainingModule2")))) {
        GenericPopupBuilder
            .Create(Strings.CurrentCulture == Strings.Culture.CULTURE_RU_RU ? "Не могу переподготовить" : "Unable To Retrain",
            Strings.CurrentCulture == Strings.Culture.CULTURE_RU_RU ? "Нужно иметь дважды улучшенный тренировочный модуль" : "You must have built the Training Module 2 upgrade aboard the Argo."
            )
            .AddButton(Strings.CurrentCulture == Strings.Culture.CULTURE_RU_RU ? "Понятненько" : "Acknowledged")
            .CancelOnEscape()
            .AddFader(backfill)
            .Render();
        return false;
      }

      if (simState.Funds < modSettings.cost) {
        GenericPopupBuilder
            .Create(Strings.CurrentCulture == Strings.Culture.CULTURE_RU_RU ? "Не могу переподготовить" : "Unable To Retrain",
             Strings.CurrentCulture == Strings.Culture.CULTURE_RU_RU ? $"Нужно иметь ¢{modSettings.cost:N0}." : $"You need ¢{modSettings.cost:N0}.")
            .AddButton(Strings.CurrentCulture == Strings.Culture.CULTURE_RU_RU ? "Понятненько": "Acknowledged")
            .CancelOnEscape()
            .AddFader(backfill)
            .Render();
        return false;
      }

      var message = modSettings.onceOnly
          ?(Strings.CurrentCulture == Strings.Culture.CULTURE_RU_RU ? $"Переподготовка сбросит все навыки на 1 и вернет очки опыта\nОбойдется в ¢{modSettings.cost:N0}, каждый пилот может быть переподготовлен один раз" :
          $"This will set skills to 1 and refund all XP.\nIt will cost ¢{modSettings.cost:N0} and each pilot can only retrain once."
          )
          : (Strings.CurrentCulture == Strings.Culture.CULTURE_RU_RU ? $"Переподготовка сбросит все навыки на 1 и вернет очки опыта\nОбойдется в ¢{modSettings.cost:N0}.":
            $"This will set skills to 1 and refund all XP.\nIt will cost ¢{modSettings.cost:N0}"
          );

      GenericPopupBuilder
          .Create(Strings.CurrentCulture == Strings.Culture.CULTURE_RU_RU ? "Переподготовка" :"Retrain", message)
          .AddButton("Cancel")
          .AddButton(Strings.CurrentCulture == Strings.Culture.CULTURE_RU_RU ? "Переподготовить пилота" : "Retrain Pilot", () => RespecAndRefresh(instance, instance.curPilot))
          .CancelOnEscape()
          .AddFader(backfill)
          .Render();
      return true;
    }
    [HarmonyPatch(typeof(SGBarracksMWDetailPanel), nameof(SGBarracksMWDetailPanel.OnSkillsSectionClicked), MethodType.Normal)]
    public static class SGBarracksMWDetailPanel_OnSkillsSectionClicked_Patch {

      public static bool Prefix(SGBarracksMWDetailPanel __instance, Pilot ___curPilot) {
        var hotkeyPerformed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (!hotkeyPerformed) {
          return true;
        }
        return Core.Retrain(__instance) == false;
      }
    }

    private static void RespecAndRefresh(SGBarracksMWDetailPanel __instance, Pilot pilot) {
      WipePilotStats(pilot);
      UnityGameInstance.BattleTechGame.Simulation.AddFunds(-modSettings.cost);
      pilot.pilotDef.PilotTags.Add("HasRetrained");
      __instance.DisplayPilot(pilot);
    }
    public static void OnAcceptPilotConfirm(SGBarracksAdvancementPanel advancement, SimGameState simState, SGBarracksWidget barracks, Pilot tempPilot)
    {
        advancement.Close();
        simState.UpgradePilot(tempPilot);
        barracks.Reset(tempPilot);
        tempPilot = null;
    }

    [HarmonyPatch(typeof(SGBarracksMWDetailPanel), "OnPilotConfirmed")]
    public static class SGBarracksMWDetailPanel_OnPilotConfirmed
    {
        public static bool Prefix(SGBarracksMWDetailPanel __instance)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (__instance.advancement.PendingPrimarySkillUpgrades())
            {
                GenericPopupBuilder.Create("Complete Training?", $"{modSettings.confirmAbilityText}").AddButton("Cancel", null, true, null).AddFader(new UIColorRef?(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.FadeToHalfBlack), 0f, true).CancelOnEscape().AddButton("Confirm",
                    delegate { OnAcceptPilotConfirm(__instance.advancement, sim, __instance.barracks, __instance.tempPilot); }, true, null).AddFader(new UIColorRef?(LazySingletonBehavior<UIManager>.Instance.UILookAndColorConstants.PopupBackfill), 0f, true).Render();
                return false;
            }

            OnAcceptPilotConfirm(__instance.advancement, sim, __instance.barracks, __instance.tempPilot);
            return false;
        }
    }

    // copied and changed from RespecPilot()
    private static void WipePilotStats(Pilot pilot) {
      var sim = UnityGameInstance.BattleTechGame.Simulation;
      var pilotDef = pilot.pilotDef.CopyToSim();

      foreach (var value in sim.Constants.Story.CampaignCommanderUpdateTags) {
        if (!sim.CompanyTags.Contains(value)) {
          sim.CompanyTags.Add(value);
        }
      }

      try {
        Log.TWL(0, $"Base:\t {pilotDef.BaseGunnery} / {pilotDef.BasePiloting} / {pilotDef.BaseGuts} / {pilotDef.BaseTactics}");
        Log.TWL(0, $"Bonus:\t {pilotDef.BonusGunnery} / {pilotDef.BonusPiloting} / {pilotDef.BonusGuts} / {pilotDef.BonusTactics}");

        var num = 0;
        num += sim.GetLevelRangeCost(1, pilotDef.SkillGunnery - 1);
        num += sim.GetLevelRangeCost(1, pilotDef.SkillPiloting - 1);
        num += sim.GetLevelRangeCost(1, pilotDef.SkillGuts - 1);
        num += sim.GetLevelRangeCost(1, pilotDef.SkillTactics - 1);

        pilotDef.BaseGunnery = 1;
        pilotDef.BasePiloting = 1;
        pilotDef.BaseGuts = 1;
        pilotDef.BaseTactics = 1;
        pilotDef.BonusGunnery = 1;
        pilotDef.BonusPiloting = 1;
        pilotDef.BonusGuts = 1;
        pilotDef.BonusTactics = 1;
        //Traverse.Create(pilotDef).Property("BaseGunnery").SetValue(1);
        //Traverse.Create(pilotDef).Property("BasePiloting").SetValue(1);
        //Traverse.Create(pilotDef).Property("BaseGuts").SetValue(1);
        //Traverse.Create(pilotDef).Property("BaseTactics").SetValue(1);
        //Traverse.Create(pilotDef).Property("BonusGunnery").SetValue(1);
        //Traverse.Create(pilotDef).Property("BonusPiloting").SetValue(1);
        //Traverse.Create(pilotDef).Property("BonusGuts").SetValue(1);
        //Traverse.Create(pilotDef).Property("BonusTactics").SetValue(1);



        // pilotDef.abilityDefNames.Clear();
        pilotDef.abilityDefNames.RemoveAll(x => !modSettings.ignoredAbilities.Contains(x));


        pilotDef.SetSpentExperience(0);
        pilotDef.ForceRefreshAbilityDefs();
        pilotDef.ResetBonusStats();
        pilot.FromPilotDef(pilotDef);
        pilot.AddExperience(0, "Respec", num);
      } catch (Exception ex) {
        Log.TWL(0, ex.ToString());
      }
    }

    public class Settings {
      public int cost;
      public bool onceOnly;
      public bool trainingModuleRequired;
      public List<string> ignoredAbilities = new List<string>();

      public string confirmAbilityText = "Confirming this Ability selection is permanent. You may only have two Primary Abilities and one Specialist Ability, and MechWarriors cannot be retrained.";
        }
  }
}
