using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace OreOverlay
{
    class OreOverlayModOptionHandler : Mod
    {
        public static OreOverlayModOptions m_Settings;

        public OreOverlayModOptionHandler(ModContentPack content) : base(content)
        {
            m_Settings = this.GetSettings<OreOverlayModOptions>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.CheckboxLabeled("OreOverlay.OptionCat".Translate(), ref m_Settings.m_ColourCodedOres);
            listingStandard.CheckboxLabeled("OreOverlay.ShowSteam".Translate(), ref m_Settings.m_ShowSteam);
            listingStandard.CheckboxLabeled("OreOverlay.ShowSeams".Translate(), ref m_Settings.m_ShowSeams);
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "OreOverlay.OptionCat".Translate();
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            m_Settings.Init();

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