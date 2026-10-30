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

		// Colours for ores whose own def data gives a poor or ambiguous colour. Everything else is discovered in FindOwnColor.
		private static readonly Dictionary<string, Color> m_ColorOverrides = new Dictionary<string, Color>()
		{
			{ "MineableSteel",      CreateColor(214, 141, 91) }, // Discovered beige, too close to silver
			{ "MineableUranium",      CreateColor(164, 189, 173) }, // Discovered dark grey, same as obsidian
			{ "MineableComponentsIndustrial",      CreateColor(194, 120, 24) }, // Discovered from the stuff colour, too close to gold
			// Coloured jade from a mod that isn't available for checking what discovery gives
			{ "MineableBlueJade",      CreateColor(44,96,165) },
			{ "MineableRedJade",      CreateColor(185,35,31) },
			{ "MineableWhiteJade",      CreateColor(255,255,255) },
			{ "MineableBlackJade",      CreateColor(32,32,32) },
			{ "MineableYellowJade",      CreateColor(230,194,44) },
		};

		// Colours by mined resource, for resources whose def data gives no usable colour (the icon average comes out grey).
		private static readonly Dictionary<string, Color> m_ResourceColorOverrides = new Dictionary<string, Color>()
		{
			{ "ComponentSpacer",      CreateColor(230,214,90) },
			{ "GravlitePanel",      CreateColor(150,120,230) },
		};

		// How good a colour source is, best first. Ores take the best colour found for their resource.
		private const int SourceOverride = 0;
		private const int SourceFleckColor = 1;
		private const int SourceRockColor = 2;
		private const int SourceStuffColor = 3;
		private const int SourceIcon = 4;
		private const int SourceNone = 5;
		private static readonly string[] SourceNames = { "override", "rock colorTwo", "rock colour", "resource stuff colour", "resource icon", "uiIconColor" };

		public Color Color => Color.white;
		#endregion

		private Map m_Map;

		// Variables used to control the overlay's data for display.
        private CellBoolDrawer m_Drawer;
        private static List<KeyValuePair<ThingDef, Color>> m_MineableList = null;
        private ushort[] m_ThingMap = null; // Short map
        private Color[] m_ColorMap = null;
		private List<int> m_GlobalOreCellList = null;	// A list of every cell containing ore regardless if its shown or not
		
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

			PrepareMinableColourmap(); // Normally already done on startup

			// Create the map
			this.m_ThingMap = new ushort[map.cellIndices.NumGridCells];
            this.m_ColorMap = new Color[map.cellIndices.NumGridCells];

			RefreshOreOverlayData(true);
        }

		#region Ore data refresh
		// This function prepares a list used to determine what colour ores should be. Update here to support new ores.
		public static void PrepareMinableColourmap()
		{
			if (m_MineableList != null)
				return;

			// Make a list of the mineables that we will highlight.
			List<ThingDef> OreDefs = new List<ThingDef>();
			foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
			{
				if (IsOreDef(thingDef))
					OreDefs.Add(thingDef);
			}

			// Every ore gets the best colour found for its resource, so the same resource has the same colour everywhere,
			// e.g. VGE compressed steel and twisted steel get the vanilla steel colour and VEE jadeite the vanilla jade colour.
			Dictionary<ThingDef, Color> ResourceColor = new Dictionary<ThingDef, Color>();
			Dictionary<ThingDef, int> ResourceSource = new Dictionary<ThingDef, int>();
			Dictionary<ThingDef, string> ResourceOrigin = new Dictionary<ThingDef, string>();

			foreach (KeyValuePair<string, Color> Entry in m_ResourceColorOverrides)
			{
				ThingDef Resource = DefDatabase<ThingDef>.GetNamedSilentFail(Entry.Key);
				if (Resource == null)
					continue;

				ResourceColor[Resource] = Entry.Value;
				ResourceSource[Resource] = SourceOverride;
				ResourceOrigin[Resource] = "resource override";
			}

			Dictionary<ThingDef, Color> OwnColor = new Dictionary<ThingDef, Color>();
			Dictionary<ThingDef, int> OwnSource = new Dictionary<ThingDef, int>();
			foreach (ThingDef thingDef in OreDefs)
			{
				OwnColor[thingDef] = FindOwnColor(thingDef, out int Source);
				OwnSource[thingDef] = Source;

				ThingDef Resource = thingDef.building.mineableThing;
				if (!ResourceSource.TryGetValue(Resource, out int BestSource) || (Source < BestSource))
				{
					ResourceColor[Resource] = OwnColor[thingDef];
					ResourceSource[Resource] = Source;
					ResourceOrigin[Resource] = thingDef.defName;
				}
			}

			List<KeyValuePair<ThingDef, Color>> EntryList = new List<KeyValuePair<ThingDef, Color>>();
			foreach (ThingDef thingDef in OreDefs)
			{
				ThingDef Resource = thingDef.building.mineableThing;
				Color OreColor = OwnColor[thingDef];
				string Source = SourceNames[OwnSource[thingDef]];
				if (ResourceSource[Resource] < OwnSource[thingDef])
				{
					OreColor = ResourceColor[Resource];
					Source = "same resource as " + ResourceOrigin[Resource];
				}

				if (Prefs.DevMode && !m_ColorOverrides.ContainsKey(thingDef.defName))
					Log.Message("[Ore Overlay] " + thingDef.defName + ": colour " + OreColor.ToString() + " from " + Source);

				EntryList.Add(new KeyValuePair<ThingDef, Color>(thingDef, OreColor));
			}

			m_MineableList = EntryList;
		}

		// Picks the overlay colour of an ore from the override table or the def data of the rock or its resource.
		private static Color FindOwnColor(ThingDef thingDef, out int Source)
		{
			Source = SourceOverride;
			if (m_ColorOverrides.TryGetValue(thingDef.defName, out Color OverrideColor))
				return OverrideColor;

			// Resource rocks draw their flecks in colorTwo, e.g. vanilla ores, Jewelry's gems and Odyssey's obsidian.
			Source = SourceFleckColor;
			Color FleckColor = thingDef.graphicData?.colorTwo ?? Color.white;
			if (FleckColor != Color.white)
				return Brighten(FleckColor);

			// Some ores tint the whole rock instead, e.g. VPE's eltex. Grey is just the stone colour.
			Source = SourceRockColor;
			Color RockColor = thingDef.graphicData?.color ?? Color.white;
			if ((RockColor != Color.white) && !IsGrey(RockColor))
				return Brighten(RockColor);

			ThingDef Resource = thingDef.building.mineableThing;

			Source = SourceStuffColor;
			if ((Resource.stuffProps != null) && (Resource.stuffProps.color.a > 0f))
				return Brighten(Resource.stuffProps.color);

			// Last resort: the average colour of the resource icon.
			// This needs the graphics device, so it only works on the main thread.
			Source = SourceIcon;
			if (UnityData.IsInMainThread && TryGetAverageIconColor(Resource, out Color IconColor))
				return Brighten(IconColor);

			Source = SourceNone;
			return thingDef.uiIconColor;
		}

		private static bool IsGrey(Color color)
		{
			Color.RGBToHSV(color, out float H, out float S, out float V);
			return S < 0.15f;
		}

		// The def colours are meant for textures and look dull on the overlay. Lift coloured ones to the brightness of the
		// override table, but leave greys alone so dark ores like obsidian stay dark.
		private static Color Brighten(Color color)
		{
			if (IsGrey(color))
				return new Color(color.r, color.g, color.b, 1f);

			Color.RGBToHSV(color, out float H, out float S, out float V);
			return Color.HSVToRGB(H, S, Mathf.Max(V, 0.75f));
		}

		// Textures are not readable, so draw the icon scaled down into a render texture and read that back.
		private static bool TryGetAverageIconColor(ThingDef def, out Color Average)
		{
			Average = Color.white;

			Texture2D Icon = def.uiIcon;
			if ((Icon == null) || (Icon == BaseContent.BadTex))
				return false;

			const int Size = 16;
			RenderTexture Previous = RenderTexture.active;
			RenderTexture Target = null;
			Texture2D Readback = null;

			// Everything that touches the renderer stays inside the try. On 1.7 these calls go through the engine's Unity shim,
			// and a failure here must only cost the discovered colour, not the whole def lookup.
			try
			{
				Target = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32);
				Readback = new Texture2D(Size, Size, TextureFormat.ARGB32, false);
				Graphics.Blit(Icon, Target);
				RenderTexture.active = Target;
				Readback.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
				Readback.Apply();

				// Weight by alpha so the transparent background doesn't count
				float R = 0f, G = 0f, B = 0f, A = 0f;
				foreach (Color Pixel in Readback.GetPixels())
				{
					R += Pixel.r * Pixel.a;
					G += Pixel.g * Pixel.a;
					B += Pixel.b * Pixel.a;
					A += Pixel.a;
				}

				if (A <= 0f)
					return false;

				Color Tint = def.graphicData?.color ?? Color.white;
				Average = new Color(R / A * Tint.r, G / A * Tint.g, B / A * Tint.b, 1f);
				return true;
			}
			catch (Exception)
			{
				return false;
			}
			finally
			{
				try
				{
					RenderTexture.active = Previous;
					if (Target != null)
						RenderTexture.ReleaseTemporary(Target);
					if (Readback != null)
						UnityEngine.Object.Destroy(Readback);
				}
				catch (Exception)
				{
				}
			}
		}

		// Mineables we will highlight. Update here to ignore.
		private static bool IsOreDef(ThingDef thingDef)
		{
			if ((thingDef.mineable == false) || (thingDef.building == null))
				return false;

			// Plain rock is not an ore. Don't use ThingDef.IsNonResourceNaturalRock for this: since 1.6 it returns false for
			// rock with mineablePreventNaturalRockOnSurface set, like Odyssey's vacstone, which then lit up whole asteroids.
			if ((thingDef.building.isNaturalRock == true) && (thingDef.building.isResourceRock == false))
				return false;

			if (thingDef.IsSmoothed || thingDef.IsBlueprint || thingDef.IsFrame)
				return false;

			// Ores drop something when mined. This skips boulders, VGE's sealant goop and VEE's deep snow.
			ThingDef Resource = thingDef.building.mineableThing;
			if (Resource == null)
				return false;

			// Walls that drop rock chunks are terrain, not ore, e.g. the ice and sand walls from Biomes! Core.
			if ((Resource.thingCategories != null) && Resource.thingCategories.Contains(ThingCategoryDefOf.StoneChunks))
				return false;

			if ((thingDef.defName.ToLower().StartsWith("smooth") == true) || (thingDef.defName.ToLower().StartsWith("raised") == true) || (thingDef.defName == "CollapsedRocks"))
				return false;

			return true;
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

			if (m_GlobalOreCellList == null)
				m_GlobalOreCellList = new List<int>();

			if (ForceRefresh == true)
			{
				// Deep refresh over every cell
				m_GlobalOreCellList.Clear();

				// Now search every tile
				for (int i = 0; i < map.cellIndices.NumGridCells; i++)
				{
					// Reset the map
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
								m_GlobalOreCellList.AddUnique(i);
								break;
							}
						}
					}

					foreach (Thing Th in MapCell.GetThingList(m_Map))
					{
						if (Th.def.defName == "SteamGeyser")
						{
							m_GlobalOreCellList.AddUnique(i);
						}
						else if (Th.def.defName == "VPE_ChemfuelPond")
						{
							m_GlobalOreCellList.AddUnique(i);
						}
						else if ((Th.def.defName == "VPE_HelixienGeyser") || (Th.def.defName == "VHGE_GasGeyser"))
						{
							m_GlobalOreCellList.AddUnique(i);
						}
					}
				}
			}
			
			
			if ((PerformUpdate == true) || (ForceRefresh == true))
			{
				// Light refresh over the already known ore locations
				m_LastTick = Find.TickManager.TicksGame;

				// Sanity check
				if (m_GlobalOreCellList != null)
				{
					// Now search every tile
					foreach (int i in m_GlobalOreCellList)
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
							else if ((Th.def.defName == "VPE_HelixienGeyser") || (Th.def.defName == "VHGE_GasGeyser"))
							{
								this.m_ThingMap[i] = Th.def.shortHash;
								this.m_ColorMap[i] = m_HexDeposit;
							}
						}
					}

					m_Drawer.SetDirty();
				}
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
			try
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
			catch (NullReferenceException)
			{
				// Something has cleared
			}
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
				if (OreOverlayModOptions.m_ShowSteam == false)
                {
                    // Hide if steam
                    ThingDef CellDef = DefDatabase<ThingDef>.GetByShortHash(m_ThingMap[index]);

                    if ((CellDef.defName == "SteamGeyser") || (CellDef.defName == "VPE_ChemfuelPond") || (CellDef.defName == "VPE_HelixienGeyser") || (CellDef.defName == "VHGE_GasGeyser"))
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
            if ((OreOverlayModOptions.m_ShowSeams == true) && (m_ShowOres == true))
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

                            if ((CellDef.defName == "SteamGeyser") || (CellDef.defName == "VPE_ChemfuelPond") || (CellDef.defName == "VPE_HelixienGeyser") || (CellDef.defName == "VHGE_GasGeyser"))
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
