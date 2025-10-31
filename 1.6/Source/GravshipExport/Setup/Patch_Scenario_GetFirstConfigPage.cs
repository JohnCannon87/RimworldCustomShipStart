using HarmonyLib;
using RimWorld;
using Verse;
using System.Reflection;
using System.Linq;

namespace GravshipExport
{
    [HarmonyPatch(typeof(Scenario), "GetFirstConfigPage")]
    public static class Patch_Scenario_GetFirstConfigPage
    {
        // Postfix: mutate the returned linked list of pages
        public static void Postfix(ref Page __result)
        {
            try
            {
                if (__result == null)
                    return;

                if (!ScenarioUsesGravshipStart())
                    return;

                // If our page is already in the chain, bail
                if (ChainContains<Page_ChooseGravship>(__result))
                    return;

                // Find the first Page_CreateWorldParams in the chain
                Page targetPage = FindInChain<Page_SelectStoryteller>(__result);
                if (targetPage == null)
                {
                    GravshipLogger.Warning("[GravshipExport] Could not find target page in page chain; not inserting Gravship page.");
                    return;
                }

                // Splice our page right after world params
                var choose = new Page_ChooseGravship();
                choose.next = targetPage.next;
                targetPage.next = choose;
                choose.next.prev = choose;
                choose.prev = targetPage;

                GravshipLogger.Message("[GravshipExport] Inserted Page_ChooseGravship after Page_CreateWorldParams.");
            }
            catch (System.Exception ex)
            {
                GravshipLogger.Error("[GravshipExport] Failed to inject Gravship page: " + ex);
            }
        }

        private static bool ScenarioUsesGravshipStart()
        {
            var scenario = Find.Scenario;
            if (scenario == null || scenario.AllParts == null)
                return false;

            foreach (var part in scenario.AllParts)
            {
                var arrivePart = part as ScenPart_PlayerPawnsArriveMethod;
                if (arrivePart != null)
                {
                    var method = GetMethodSafe(arrivePart);
                    if (method == PlayerPawnsArriveMethod.Gravship)
                        return true;
                }
            }
            return false;
        }

        private static PlayerPawnsArriveMethod GetMethodSafe(ScenPart_PlayerPawnsArriveMethod part)
        {
            try
            {
                FieldInfo field = typeof(ScenPart_PlayerPawnsArriveMethod)
                    .GetField("method", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                    return (PlayerPawnsArriveMethod)field.GetValue(part);
            }
            catch { }
            return PlayerPawnsArriveMethod.Standing;
        }

        private static T FindInChain<T>(Page head) where T : Page
        {
            Page cur = head;
            int guard = 0;
            while (cur != null && guard++ < 256)
            {
                T typed = cur as T;
                if (typed != null)
                    return typed;
                cur = cur.next;
            }
            return null;
        }

        private static bool ChainContains<T>(Page head) where T : Page
        {
            return FindInChain<T>(head) != null;
        }
    }
}
