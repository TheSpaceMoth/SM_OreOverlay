using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace OreOverlay
{
    // Initialise
    [StaticConstructorOnStartup]
    class HarmonyPatches
    {
        public static readonly Texture2D m_HudIcon = null;
        public static readonly Texture2D m_DeepHudIcon = null;

        static HarmonyPatches()
        {
            try
            {
                m_HudIcon = ContentFinder<Texture2D>.Get("UI/Buttons/ShowOreOverlay");
                m_DeepHudIcon = ContentFinder<Texture2D>.Get("UI/Buttons/ShowDeepOverlay");
            }
            catch(Exception)
            {
                Log.Warning("Exception loading icons for Ore Overlay");
            }
            
            Harmony harmony = new Harmony("Spacemoth.OreOverlay");
            harmony.PatchAll();
        }
    }

    // Add GUI buttons
    [HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls")]
    internal static class HarmoneyPatchPlaySettings
    {
        [HarmonyPostfix]
        private static void PostFix(WidgetRow row, bool worldView)
        {
            if (worldView)
                return;

            if (row == null)
                return;

            if ((HarmonyPatches.m_HudIcon == null) || (HarmonyPatches.m_DeepHudIcon == null))
                return;

            row.ToggleableIcon(ref OreOverlayGrid.m_ShowOres, HarmonyPatches.m_HudIcon, "OreOverlay.ShowOverlay".Translate(), SoundDefOf.Mouseover_ButtonToggle);
            row.ToggleableIcon(ref OreOverlayGrid.m_ShowDeepOres, HarmonyPatches.m_DeepHudIcon, "OreOverlay.ShowDrillOverlay".Translate(), SoundDefOf.Mouseover_ButtonToggle);
        }
    }

    // Update 'map' when something is mined or descovered underground
    // DeepResourceGrid.DeepResourceGridUpdate()
    [HarmonyPatch(typeof(DeepResourceGrid))]
    internal static class HarmoneyPatchUpdate
    {
        [HarmonyPostfix]
        [HarmonyPatch("DeepResourceGridUpdate")]
        private static void Postfix(DeepResourceGrid __instance)
        {
            foreach (OreOverlayGrid Overlay in OreOverlayGrid.m_OverlayList)
            {
                try
                {
                    Overlay.RefreshData();
                }
                catch (Exception)
                {

                }
            }
        }
    }

    // Update 'map' when something is mined
    // Mineable.DestroyMined
    [HarmonyPatch(typeof(Mineable))]
    internal static class HarmoneyPatchUpdateMine
    {
        [HarmonyPostfix]
        [HarmonyPatch("DestroyMined")]
        private static void Postfix(Mineable __instance)
        {
            foreach (OreOverlayGrid Overlay in OreOverlayGrid.m_OverlayList)
            {
                try
                {
                    Overlay.RefreshData();
                }
                catch (Exception)
                {

                }
            }
        }
    }

    // Update 'map' when something is broken
    // Mineable.Destroy
    [HarmonyPatch(typeof(Mineable))]
    internal static class HarmoneyPatchUpdateDestroy
    {
        [HarmonyPostfix]
        [HarmonyPatch("Destroy")]
        private static void Postfix(Mineable __instance)
        {
            foreach (OreOverlayGrid Overlay in OreOverlayGrid.m_OverlayList)
            {
                try
                {
                    Overlay.RefreshData();
                }
                catch (Exception)
                {

                }
            }
        }
    }

    // Update 'map' when something is revealed
    // TriggerUnfogged.Activated
    [HarmonyPatch(typeof(TriggerUnfogged))]
    internal static class HarmoneyPatchUpdateUnfog
    {
        [HarmonyPostfix]
        [HarmonyPatch("Activated")]
        private static void Postfix(TriggerUnfogged __instance)
        {
            foreach (OreOverlayGrid Overlay in OreOverlayGrid.m_OverlayList)
            {
                try
                {
                    Overlay.RefreshData();
                }
                catch (Exception)
                {

                }
            }
        }
    }
}
