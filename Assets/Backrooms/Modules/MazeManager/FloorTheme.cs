using UnityEngine;

namespace Backrooms.MazeManager
{
    /// <summary>
    /// The look of one floor of the Backrooms. In this setting the Backrooms are not a single
    /// yellow space but an ever-changing dungeon stitched together from malls, laundromats,
    /// carnivals and asylums, so every floor gets its own palette and name.
    /// </summary>
    public sealed class FloorTheme
    {
        /// <summary>Display name of the floor, shown to the player on arrival.</summary>
        public string Name { get; }

        /// <summary>Colour of the wall surfaces.</summary>
        public Color Wall { get; }

        /// <summary>Colour of the floor surface.</summary>
        public Color Floor { get; }

        /// <summary>Colour of the ceiling surface.</summary>
        public Color Ceiling { get; }

        /// <summary>Colour and tint of the ceiling lights.</summary>
        public Color Light { get; }

        /// <summary>Fog colour, which also sets the mood of the distance.</summary>
        public Color Fog { get; }

        /// <summary>Skirting and structural trim colour, darker than the walls.</summary>
        public Color Trim { get; }

        /// <summary>Signage and highlight colour for this floor's props.</summary>
        public Color Accent { get; }

        /// <summary>What kind of props dress this floor.</summary>
        public PropStyle Props { get; }

        /// <summary>
        /// Ceiling height in metres.
        /// </summary>
        /// <remarks>
        /// The single strongest thing separating one floor from another, and for a long time every
        /// floor was the same 3m box with a different palette. Colour alone reads as a filter over
        /// the same room; height changes how the space feels to stand in — a laundromat you could
        /// touch the ceiling of is a different kind of unpleasant from a carnival tent vanishing into
        /// the dark above the lights.
        /// </remarks>
        public float CeilingHeight { get; set; } = 3f;

        /// <summary>How many open rooms are carved into the grid.</summary>
        public int RoomCount { get; set; } = 4;

        /// <summary>Smallest room side, in cells.</summary>
        public int RoomMinSize { get; set; } = 3;

        /// <summary>Largest room side, in cells.</summary>
        public int RoomMaxSize { get; set; } = 6;

        /// <summary>
        /// Creates a floor theme.
        /// </summary>
        /// <param name="name">Display name of the floor.</param>
        /// <param name="wall">Wall colour.</param>
        /// <param name="floor">Floor colour.</param>
        /// <param name="ceiling">Ceiling colour.</param>
        /// <param name="light">Ceiling light colour.</param>
        /// <param name="fog">Fog colour.</param>
        /// <param name="trim">Skirting and trim colour.</param>
        /// <param name="accent">Signage and highlight colour.</param>
        /// <param name="props">Which prop set dresses the floor.</param>
        public FloorTheme(string name, Color wall, Color floor, Color ceiling, Color light, Color fog,
            Color trim, Color accent, PropStyle props)
        {
            Name = name;
            Wall = wall;
            Floor = floor;
            Ceiling = ceiling;
            Light = light;
            Fog = fog;
            Trim = trim;
            Accent = accent;
            Props = props;
        }
    }

    /// <summary>
    /// The kind of clutter that dresses a floor. The Backrooms here are assembled from real places,
    /// so each floor is furnished like the place it was stolen from.
    /// </summary>
    public enum PropStyle
    {
        /// <summary>Bare offices: support pillars, wall pipes, stacked boxes.</summary>
        Office = 0,

        /// <summary>Shopfronts with signage, planters and benches.</summary>
        Mall = 1,

        /// <summary>Banks of washing machines and folding counters.</summary>
        Laundromat = 2,

        /// <summary>Striped booths and ticket stands.</summary>
        Carnival = 3,

        /// <summary>Doorframes, gurneys and stacked chairs.</summary>
        Asylum = 4
    }

    /// <summary>
    /// The floor palettes the game cycles through as the player descends. Floor 1 is the familiar
    /// yellow entry level; deeper floors move through the other spaces the dungeon is assembled
    /// from. Lookup is deterministic, so a given floor number always looks the same.
    /// </summary>
    public static class FloorThemes
    {
        private static readonly FloorTheme[] Themes =
        {
            new FloorTheme("THE YELLOW ROOMS",
                wall: new Color(0.91f, 0.85f, 0.55f),
                floor: new Color(0.62f, 0.55f, 0.33f),
                ceiling: new Color(0.88f, 0.86f, 0.75f),
                light: new Color(1f, 0.97f, 0.82f),
                fog: new Color(0.74f, 0.70f, 0.52f),
                trim: new Color(0.46f, 0.41f, 0.24f),
                accent: new Color(0.75f, 0.68f, 0.40f),
                props: PropStyle.Office)
            {
                // Suspended office ceiling, low enough to feel like a lid. This is the floor people
                // picture when they hear "Backrooms", so it is the one that should feel most like an
                // office nobody has worked in for years.
                CeilingHeight = 2.6f,
                RoomCount = 4, RoomMinSize = 3, RoomMaxSize = 6
            },

            new FloorTheme("ABANDONED MALL",
                wall: new Color(0.62f, 0.64f, 0.72f),
                floor: new Color(0.44f, 0.45f, 0.50f),
                ceiling: new Color(0.74f, 0.76f, 0.82f),
                light: new Color(0.85f, 0.92f, 1f),
                fog: new Color(0.50f, 0.53f, 0.62f),
                trim: new Color(0.33f, 0.34f, 0.39f),
                accent: new Color(0.95f, 0.45f, 0.35f),
                props: PropStyle.Mall)
            {
                // An atrium. Twice the height of anywhere else and the widest rooms, so arriving here
                // after the Yellow Rooms is a physical change of scale rather than a change of colour.
                CeilingHeight = 5.6f,
                RoomCount = 6, RoomMinSize = 4, RoomMaxSize = 9
            },

            new FloorTheme("JANKY LAUNDROMAT",
                wall: new Color(0.53f, 0.72f, 0.66f),
                floor: new Color(0.36f, 0.44f, 0.43f),
                ceiling: new Color(0.72f, 0.82f, 0.78f),
                light: new Color(0.80f, 1f, 0.93f),
                fog: new Color(0.40f, 0.55f, 0.52f),
                trim: new Color(0.28f, 0.36f, 0.35f),
                accent: new Color(0.95f, 0.92f, 0.85f),
                props: PropStyle.Laundromat)
            {
                // The tightest floor in the game: a low ceiling and small rooms, so it is all
                // corners and no sightlines. Being chased here is worse than anywhere else.
                CeilingHeight = 2.3f,
                RoomCount = 3, RoomMinSize = 2, RoomMaxSize = 4
            },

            new FloorTheme("TWISTED CARNIVAL",
                wall: new Color(0.70f, 0.35f, 0.48f),
                floor: new Color(0.34f, 0.20f, 0.30f),
                ceiling: new Color(0.45f, 0.26f, 0.40f),
                light: new Color(1f, 0.72f, 0.55f),
                fog: new Color(0.34f, 0.18f, 0.28f),
                trim: new Color(0.24f, 0.13f, 0.21f),
                accent: new Color(1f, 0.82f, 0.25f),
                props: PropStyle.Carnival)
            {
                // A big top. The tallest floor, and tall enough that the lights do not reach the
                // ceiling — the dark above them is the point.
                CeilingHeight = 6.8f,
                RoomCount = 5, RoomMinSize = 4, RoomMaxSize = 8
            },

            new FloorTheme("CONDEMNED ASYLUM",
                wall: new Color(0.56f, 0.57f, 0.49f),
                floor: new Color(0.28f, 0.29f, 0.26f),
                ceiling: new Color(0.44f, 0.45f, 0.41f),
                light: new Color(0.78f, 0.82f, 0.72f),
                fog: new Color(0.22f, 0.24f, 0.22f),
                trim: new Color(0.20f, 0.21f, 0.19f),
                accent: new Color(0.55f, 0.62f, 0.58f),
                props: PropStyle.Asylum)
            {
                // Institutional: a tall-ish corridor ceiling but very few open rooms, so it reads as
                // a building of narrow halls and closed doors rather than of spaces.
                CeilingHeight = 3.4f,
                RoomCount = 2, RoomMinSize = 3, RoomMaxSize = 5
            }
        };

        /// <summary>How many distinct palettes exist before they repeat.</summary>
        public static int Count => Themes.Length;

        /// <summary>
        /// The theme for a floor number. Floor 1 is the first palette; deeper floors advance through
        /// the list and wrap around, so the dungeon keeps going past the authored set.
        /// </summary>
        /// <param name="floor">One-based floor number.</param>
        /// <returns>The palette for that floor.</returns>
        public static FloorTheme ForFloor(int floor)
        {
            int index = (Mathf.Max(1, floor) - 1) % Themes.Length;
            return Themes[index];
        }
    }
}
