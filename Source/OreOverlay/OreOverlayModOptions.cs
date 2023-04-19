using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace OreOverlay
{
    class OreOverlayModOptions : ModSettings
    {
        private const bool DefaultColourCodedOres = true;
        private const bool DefaultShowSteam = true;
        private const bool DefaultShowSeams = true;

        public bool m_ColourCodedOres = DefaultColourCodedOres;
        public bool m_ShowSteam = DefaultShowSteam;
        public bool m_ShowSeams = DefaultShowSeams;

        public OreOverlayModOptions()
        {
            Init();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref this.m_ColourCodedOres, "colourCodedOres", DefaultColourCodedOres);
            Scribe_Values.Look(ref this.m_ShowSteam, "showSteamVents", DefaultShowSteam);
            Scribe_Values.Look(ref this.m_ShowSeams, "showSteams", DefaultShowSeams);
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                this.Init();
            }
        }

        public void Init()
        {

        }
    }
}
