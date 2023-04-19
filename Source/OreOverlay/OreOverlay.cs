using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace OreOverlay
{
    public class OreOverlayGrid : MapComponent, ICellBoolGiver
    {
		// State checks toggled by the on-screen buttons
        public static bool m_ShowOres = false;
        public static bool m_ShowDeepOres = false;

		#region Colour Defines
		private static readonly Color m_DefaultOreColor = Color.green;
        private static readonly Color m_ChemfuelDeposit = CreateColor(194, 104, 45);
        private static readonly Color m_SteamVent = CreateColor(41, 68, 194);
        private static readonly Color m_HexDeposit = CreateColor(96, 194, 82);

		List<KeyValuePair<string, Color>> m_ColorOverrides = new List<KeyValuePair<string, Color>>()
		{
			new KeyValuePair<string, Color>("MineableSteel",      CreateColor(214, 141, 91)),
			new KeyValuePair<string, Color>("MineableSilver",      CreateColor(186, 179, 157)),
			new KeyValuePair<string, Color>("MineableGold",      CreateColor(209, 166, 17)),
			new KeyValuePair<string, Color>("MineableUranium",      CreateColor(164, 189, 173)),
			new KeyValuePair<string, Color>("MineablePlasteel",      CreateColor(160, 206, 214)),
			new KeyValuePair<string, Color>("MineableJade",      CreateColor(25, 190, 0)),
			new KeyValuePair<string, Color>("MineableComponentsIndustrial",      CreateColor(194, 120, 24)),
			new KeyValuePair<string, Color>("MineableBlueJade",      CreateColor(44,96,165)),
			new KeyValuePair<string, Color>("MineableRedJade",      CreateColor(185,35,31)),
			new KeyValuePair<string, Color>("MineableWhiteJade",      CreateColor(255,255,255)),
			new KeyValuePair<string, Color>("MineableBlackJade",      CreateColor(32,32,32)),
			new KeyValuePair<string, Color>("MineableYellowJade",      CreateColor(230,194,44)),
			new KeyValuePair<string, Color>("VPE_EltexOre",      CreateColor(181,230,224)),
		};

		public Color Color => Color.white;
		#endregion

		private Map m_Map;

		// Variables used to control the overlay's data for display.
        private CellBoolDrawer m_Drawer;
        private static List<KeyValuePair<ThingDef, Color>> m_MineableList = null;
        private ushort[] m_ThingMap = null; // Short map
        private Color[] m_ColorMap = null;
		
		// Refresh monitering
		private static int m_LastTick = 0;
		private static bool m_LastCheckActiveScanner = false;
		private static bool m_LastCheckOverlayEnabled = false;
		
		// Initialiser
        public OreOverlayGrid(Map map) : base(map)
        {
			// Save the map for checking later
            m_Map = map;

            this.m_Drawer = new CellBoolDrawer((ICellBoolGiver)this, map.Size.x, map.Size.z, 3640, 0.33f);

			PrepareMinableColourmap();

			// Create the map
			this.m_ThingMap = new ushort[map.cellIndices.NumGridCells];
            this.m_ColorMap = new Color[map.cellIndices.NumGridCells];

			RefreshOreOverlayData(true);
        }

		#region Ore data refresh
		// This function prepares a list used to determine what colour ores should be. Update here to support new ores.
		private void PrepareMinableColourmap()
		{
			if (m_MineableList == null)
			{
				List<KeyValuePair<ThingDef, Color>> EntryList = new List<KeyValuePair<ThingDef, Color>>();

				// Make a list mineables that we will highlight. Update here to ignore.
				foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
				{
					if ((thingDef.mineable == true) && (thingDef.IsNonResourceNaturalRock == false))
					{
						if ((thingDef.defName.ToLower().StartsWith("smooth") == false) && (thingDef.defName.ToLower().StartsWith("raised") == false) && (thingDef.defName != "CollapsedRocks"))
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

				// Just in case of multi threading? unlikely..
				if (m_MineableList == null)
					m_MineableList = new List<KeyValuePair<ThingDef, Color>>(EntryList);
			}
		}

		// This is called each tick while the game is running, and is paused when the game is paused.
		public override void MapComponentTick()
		{
			RefreshOreOverlayData(false);
		}

		// This recalculates the data used to produce the overlay. This takes time and causes the majority of the lag..
		public void RefreshOreOverlayData(bool ForceRefresh = true)
		{
			bool PerformUpdate = false;

			// Only check for updates while the overlay is shown
			if (m_ShowOres == true)
			{
				// Update due to ticks passed
				if ((m_LastTick == 0) || ((m_LastTick + 500) < Find.TickManager.TicksGame))
				{
					PerformUpdate = true;
				}

				// Update due to change in active scanner state
				bool ActiveScanner = this.m_Map.deepResourceGrid.AnyActiveDeepScannersOnMap();
				if (m_LastCheckActiveScanner != ActiveScanner)
				{
					m_LastCheckActiveScanner = ActiveScanner;
					PerformUpdate = true;
				}

				// Update because ores were just turned on
				if (m_LastCheckOverlayEnabled == false)
				{
					PerformUpdate = true;
					m_LastCheckOverlayEnabled = true;
				}
			}
			else
			{
				m_LastCheckOverlayEnabled = false;
			}
			
			// Update the grid itself 
			if ((PerformUpdate == true) || (ForceRefresh == true))
			{
				m_LastTick = Find.TickManager.TicksGame;

				// Now search every tile
				for (int i = 0; i < map.cellIndices.NumGridCells; i++)
				{
					this.m_ThingMap[i] = 0;
					this.m_ColorMap[i] = m_DefaultOreColor;

					IntVec3 MapCell = m_Map.cellIndices.IndexToCell(i);

					if (!MapCell.InBounds(this.m_Map))
						continue;

					Mineable MineCell = MapCell.GetFirstMineable(m_Map);

					if (MineCell != null)
					{
						foreach (KeyValuePair<ThingDef, Color> MineEntry in m_MineableList)
						{
							if (MineEntry.Key == MineCell.def)
							{
								this.m_ThingMap[i] = MineCell.def.shortHash;
								this.m_ColorMap[i] = MineEntry.Value;
								break;
							}
						}
					}

					foreach (Thing Th in MapCell.GetThingList(m_Map))
					{
						if (Th.def.defName == "SteamGeyser")
						{
							this.m_ThingMap[i] = Th.def.shortHash;
							this.m_ColorMap[i] = m_SteamVent;
						}
						else if (Th.def.defName == "VPE_ChemfuelPond")
						{
							this.m_ThingMap[i] = Th.def.shortHash;
							this.m_ColorMap[i] = m_ChemfuelDeposit;
						}
						else if (Th.def.defName == "VPE_HelixienGeyser")
						{
							this.m_ThingMap[i] = Th.def.shortHash;
							this.m_ColorMap[i] = m_HexDeposit;
						}
					}
				}

				m_Drawer.SetDirty();
			}			
        }

		static Color CreateColor(int r, int g, int b)
		{
			float Red = (float)r / 255f;
			float Green = (float)g / 255f;
			float Blue = (float)b / 255f;

			return (new Color(Red, Green, Blue));
		}
		#endregion
		#region Ore Drawing
		// This function draws the ore overlay, and is called from the same place existing overlays are called from both paused and unpaused.
		public void OreGridDraw()
		{
			if (m_ShowOres == true)
			{
				// Check for force refresh.
				if (m_LastCheckOverlayEnabled == false)
					RefreshOreOverlayData(true);

				this.MarkForDraw();
			}

			m_Drawer.CellBoolDrawerUpdate();

			// Check for draw debug so we dont do it twice.
			if ((DebugViewSettings.drawDeepResources == false) && (m_ShowDeepOres == true))
                this.m_Map.deepResourceGrid.MarkForDraw();
        }

        public void MarkForDraw()
        {
            if (this.map != Find.CurrentMap)
                return;

            this.m_Drawer.MarkForDraw();
        }

		// This is called during the overlay drawing for each cell to get the state.
		public bool GetCellBool(int index)
        {
            if (index >= map.cellIndices.NumGridCells)
                return false;

            if (m_ThingMap[index] != 0)
            {
                if (OreOverlayModOptionHandler.m_Settings.m_ShowSteam == false)
                {
                    // Hide if steam
                    ThingDef CellDef = DefDatabase<ThingDef>.GetByShortHash(m_ThingMap[index]);

                    if ((CellDef.defName == "SteamGeyser") || (CellDef.defName == "VPE_ChemfuelPond") || (CellDef.defName == "VPE_HelixienGeyser"))
                        return false;
                }

                IntVec3 MapCell = m_Map.cellIndices.IndexToCell(index);


                if ((this.m_Map.deepResourceGrid.AnyActiveDeepScannersOnMap() == true) || (MapCell.Fogged(this.m_Map) == false))
                    return true;
                else
                    return false;
            }
            else
            {
                return false;
            }
        }
        
		// This is called during the overlay drawing to colour a cell if flagged above.
        public Color GetCellExtraColor(int index)
        {
            Color RetCol = m_DefaultOreColor;

            if (index >= map.cellIndices.NumGridCells)
                return RetCol;

            RetCol = m_ColorMap[index];

            return RetCol;
        }
		#endregion
		#region Mouseovers
		// Handle mouse-over
		public override void MapComponentOnGUI()
        {
            // Dont loose other hover-overs
            base.MapComponentOnGUI();
            bool DrawHoverover = true;

			// Handle the mouse-over for deep ore
            if (m_ShowDeepOres == true)
            {
                Thing singleSelectedThing = Find.Selector.SingleSelectedThing;
                if (singleSelectedThing != null)
                {
                    CompDeepScanner comp1 = singleSelectedThing.TryGetComp<CompDeepScanner>();
                    CompDeepDrill comp2 = singleSelectedThing.TryGetComp<CompDeepDrill>();

                    if ((comp1 != null) || (comp2 != null))
                        DrawHoverover = false;
                }

				/* Even if scanner is offline, you know where the deposit is by descovering it.
                if (DrawHoverover == true)
                {
                    if (this.m_Map.deepResourceGrid.AnyActiveDeepScannersOnMap() == false)
                        DrawHoverover = false;
                }
				*/

                if (DrawHoverover == true)
                    this.RenderDeepDrillDeposit();
            }
            
			// Handle the mouse over for cliff ores.
            if ((OreOverlayModOptionHandler.m_Settings.m_ShowSeams == true) && (m_ShowOres == true))
            {
                DrawHoverover = true;

                IntVec3 MapCell = UI.MouseCell();
                if (!MapCell.InBounds(this.m_Map))
                    DrawHoverover = false;


                if (DrawHoverover == true)
                {
                    int index = m_Map.cellIndices.CellToIndex(MapCell);

                    if (index < map.cellIndices.NumGridCells)
                    {
                        if (m_ThingMap[index] != 0)
                        {

                            // Hide if steam
                            ThingDef CellDef = DefDatabase<ThingDef>.GetByShortHash(m_ThingMap[index]);

                            if ((CellDef.defName == "SteamGeyser") || (CellDef.defName == "VPE_ChemfuelPond") || (CellDef.defName == "VPE_HelixienGeyser"))
                                DrawHoverover = false;

							if (DrawHoverover == true)
							{
								//IntVec3 MapCell = m_Map.cellIndices.IndexToCell(index);

								if ((this.m_Map.deepResourceGrid.AnyActiveDeepScannersOnMap() == false) && (MapCell.Fogged(this.m_Map) == true))
									DrawHoverover = false;
							}

							if (DrawHoverover == true)
							{
                                RenderCliffDeposit(CellDef);
                            }
                        }
                    }
                }
            }
        }

		// Draw the cliff ore mouseover
        private void RenderCliffDeposit(ThingDef TargetThing)
        {
            IntVec3 MapCell = UI.MouseCell();
            if (!MapCell.InBounds(this.m_Map))
                return;
            
            //Mineable MineCell = MapCell.GetFirstMineable(m_Map);

            ThingDef thingDef = null;

            try
            {
                thingDef = TargetThing.building.mineableThing;
            }
            catch (Exception)
            {
                thingDef = null;
            }


            if (thingDef == null)
                return;

            Vector2 uiPosition = MapCell.ToVector3().MapToUIPosition();
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            float num2 = (float)(((double)UI.CurUICellSize() - 27.0) / 2.0);
            Rect rect = new Rect(uiPosition.x + num2, uiPosition.y - UI.CurUICellSize() + num2, 27f, 27f);



            Widgets.ThingIcon(rect, thingDef);
            Widgets.Label(new Rect(rect.xMax + 4f, rect.y, 999f, 29f), NamedArgumentUtility.Named(thingDef, "RESOURCE").arg.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
        }

		// Draw the deep ore mouseover
        private void RenderDeepDrillDeposit()
        {
            IntVec3 c = UI.MouseCell();
            if (!c.InBounds(this.m_Map))
                return;
            ThingDef thingDef = this.m_Map.deepResourceGrid.ThingDefAt(c);
            if (thingDef == null)
                return;
            int num1 = this.m_Map.deepResourceGrid.CountAt(c);
            if (num1 <= 0)
                return;
            Vector2 uiPosition = c.ToVector3().MapToUIPosition();
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            float num2 = (float)(((double)UI.CurUICellSize() - 27.0) / 2.0);
            Rect rect = new Rect(uiPosition.x + num2, uiPosition.y - UI.CurUICellSize() + num2, 27f, 27f);
            Widgets.ThingIcon(rect, thingDef);
            Widgets.Label(new Rect(rect.xMax + 4f, rect.y, 999f, 29f), "DeepResourceRemaining".Translate(NamedArgumentUtility.Named(thingDef, "RESOURCE"), num1.Named("COUNT")));
            Text.Anchor = TextAnchor.UpperLeft;
        }
		#endregion
	}
}
