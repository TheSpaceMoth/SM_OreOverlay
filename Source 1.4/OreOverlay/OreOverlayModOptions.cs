using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace OreOverlay
{
	//[StaticConstructorOnStartup]
	class OreOverlayModOptions : ModSettings
    {
        private const bool DefaultColourCodedOres = true;
        private const bool DefaultShowSteam = true;
        private const bool DefaultShowSeams = true;

        public static bool m_ColourCodedOres = DefaultColourCodedOres;
        public static bool m_ShowSteam = DefaultShowSteam;
        public static bool m_ShowSeams = DefaultShowSeams;

        public OreOverlayModOptions()
        {
            Init();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref m_ColourCodedOres, "colourCodedOres", DefaultColourCodedOres);
            Scribe_Values.Look(ref m_ShowSteam, "showSteamVents", DefaultShowSteam);
            Scribe_Values.Look(ref m_ShowSeams, "showSteams", DefaultShowSeams);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                this.Init();
            }
        }

        public void Init()
        {

        }

		public static void DoSettingsWindowContents(Rect inRect)
		{
			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(inRect);

			listingStandard.CheckboxLabeled("OreOverlay.OptionCat".Translate(), ref m_ColourCodedOres);
			listingStandard.CheckboxLabeled("OreOverlay.ShowSteam".Translate(), ref m_ShowSteam);
			listingStandard.CheckboxLabeled("OreOverlay.ShowSeams".Translate(), ref m_ShowSeams);
			listingStandard.End();

//			base.DoSettingsWindowContents(inRect);
		}
		
		/*
		public override void WriteSettings()
		{
			base.WriteSettings();
			Init();

			OreOverlayGrid Overlay = Find.CurrentMap.GetComponent<OreOverlayGrid>();

			if (Overlay != null)
			{
				Overlay.RefreshOreOverlayData(true); // Settings changed, force a refresh.
			}
		}*/
	}
}
