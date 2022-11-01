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

        /*
        public ThingDef[] m_MineableList = null;
        public Color[] m_MineableColorList;

        List<KeyValuePair<string, Color>> m_ColorOverrides = new List<KeyValuePair<string, Color>>()
        {
            new KeyValuePair<string, Color>("MineableSteel",      new Color(){r = 214, g = 141, b = 91 }),
            new KeyValuePair<string, Color>("MineableSilver",      new Color(){r = 186, g = 179, b = 157 }),
            new KeyValuePair<string, Color>("MineableGold",      new Color(){r = 209, g = 166, b = 17 }),
            new KeyValuePair<string, Color>("MineableUranium",      new Color(){r = 164, g = 189, b = 173 }),
            new KeyValuePair<string, Color>("MineablePlasteel",      new Color(){r = 160, g = 206, b = 214 }),
            new KeyValuePair<string, Color>("MineableJade",      new Color(){r = 25, g = 190, b = 0 }),
            new KeyValuePair<string, Color>("MineableComponentsIndustrial",      new Color(){r = 194, g = 120, b = 24 }),
        };*/

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
            /*
            if (this.m_MineableList == null)
            {
                List<KeyValuePair<ThingDef, Color>> EntryList = new List<KeyValuePair<ThingDef, Color>>();
                
                // Make a list mineables
                foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
                {
                    if ((thingDef.mineable == true) && (thingDef.IsNonResourceNaturalRock == false))
                    {
                        EntryList.Add(new KeyValuePair<ThingDef, Color>(thingDef, thingDef.uiIconColor));
                    }
                }

                // Override some colour defaults
                for (int i = 0; i < EntryList.Count; i++)
                {
                    foreach (KeyValuePair<string, Color> OverrideEntry in m_ColorOverrides)
                    {
                        if (OverrideEntry.Key == EntryList[i].Key.defName)
                        {
                            ThingDef TempThing = EntryList[i].Key;
                            EntryList.RemoveAt(i);
                            EntryList.Insert(0, new KeyValuePair<ThingDef, Color>(TempThing, OverrideEntry.Value));
                        }
                    }
                }

                m_MineableList = new ThingDef[EntryList.Count];
                m_MineableColorList = new Color[EntryList.Count];

                // Turn into arrays
                int i = 0;
                foreach (KeyValuePair<ThingDef, Color> ThingEntry in EntryList)
                {
                    m_MineableList[i] = ThingEntry.Key;
                    m_MineableColorList[i] = ThingEntry.Value;
                    i++;
                }
            }
            */

        }
    }
}
