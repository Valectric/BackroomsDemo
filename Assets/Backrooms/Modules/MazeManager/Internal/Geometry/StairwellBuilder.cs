using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Builds one stairwell down to the next floor: a shaft sunk into the floor, a flight of steps
    /// descending into it, and a lit overhead sign so the way down can be found across a large floor.
    /// </summary>
    /// <remarks>
    /// The floor mesh omits the stairwell's cell, which is what makes the shaft visible, but the
    /// floor <i>collider</i> is left whole. Descending is triggered by proximity in the gameplay
    /// layer the moment the player steps onto the cell, so they never get far enough onto the
    /// invisible span to notice it — and a player who could physically fall into a decorative hole
    /// would be stuck at the bottom of it.
    /// </remarks>
    internal sealed class StairwellBuilder
    {
        /// <summary>How far the shaft is sunk below floor level, in metres.</summary>
        private const float ShaftDepth = 3f;

        /// <summary>How many treads the flight has.</summary>
        private const int StepCount = 6;

        /// <summary>Rise of one tread, in metres.</summary>
        private const float StepRise = 0.45f;

        /// <summary>Thickness of the shaft lining, in metres.</summary>
        private const float LiningThickness = 0.12f;

        /// <summary>Colour the way down is signed with, on every floor.</summary>
        private static readonly Color StairsGreen = new Color(0.35f, 1f, 0.45f);

        /// <summary>
        /// Colour of the way up. Deliberately cold and dim next to the green: the player never needs
        /// to find one — they arrive out of it — so it must not compete with the beacon that marks
        /// the way onward.
        /// </summary>
        private static readonly Color StairsPale = new Color(0.62f, 0.72f, 0.85f);

        /// <summary>
        /// Builds a stairwell up in one cell: the flight the player emerges from.
        /// </summary>
        /// <remarks>
        /// It is a real staircase rising into an opening cut in the ceiling, not a decoration. The
        /// point is continuity — you came down stairs, so when you look back there are stairs. The
        /// opening is the ceiling's version of the hole the way down cuts in the floor.
        /// </remarks>
        /// <param name="layout">The floor being built.</param>
        /// <param name="cell">Cell to raise the stairwell in.</param>
        /// <param name="wallHeight">Floor-to-ceiling height in metres.</param>
        /// <param name="theme">Palette for the shaft lining and treads.</param>
        /// <param name="parent">Parent transform for the stairwell.</param>
        public void BuildUp(MazeLayout layout, Vector2Int cell, float wallHeight, FloorTheme theme,
            Transform parent)
        {
            float cellSize = layout.CellSize;
            Vector3 centre = layout.CellCenterToWorld(cell);

            var root = new GameObject($"StairwellUp_{cell.x}_{cell.y}");
            root.transform.SetParent(parent, worldPositionStays: false);

            Material lining = MazeMaterials.Lit(theme.Trim * 0.8f);
            Material tread = MazeMaterials.Lit(theme.Wall * 0.6f);

            LineRise(root.transform, centre, cellSize, wallHeight, lining);

            // Climb away from the doorway, the same way the down stairs descend away from it. Rising
            // towards the doorway puts the tallest step in the player's face and the flight reads as
            // a blank slab rather than as stairs.
            BuildRise(root.transform, centre, cellSize, wallHeight,
                DescentDirection(layout, cell), tread);
            AddLight(root.transform, "UpGlow", centre + Vector3.up * (wallHeight + 0.6f),
                1.6f, 5f, StairsPale);
        }

        /// <summary>
        /// Lines the shaft above the ceiling, so looking up the stairs shows a stairwell rather than
        /// straight out of the level.
        /// </summary>
        /// <param name="parent">Stairwell root transform.</param>
        /// <param name="centre">World centre of the cell at floor level.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <param name="wallHeight">Floor-to-ceiling height in metres.</param>
        /// <param name="lining">Material for the shaft walls and the landing above.</param>
        private static void LineRise(Transform parent, Vector3 centre, float cellSize,
            float wallHeight, Material lining)
        {
            float half = cellSize * 0.5f;
            float midY = wallHeight + RiseShaft * 0.5f;

            foreach (Direction dir in Directions.All)
            {
                Vector2Int d = Directions.Delta(dir);
                var outward = new Vector3(d.x, 0f, d.y);
                bool alongX = d.x == 0;

                Vector3 size = alongX
                    ? new Vector3(cellSize, RiseShaft, LiningThickness)
                    : new Vector3(LiningThickness, RiseShaft, cellSize);

                MazeMaterials.Cube(parent, $"RiseWall_{dir}",
                    centre + outward * (half - LiningThickness * 0.5f) + Vector3.up * midY,
                    size, lining);
            }

            MazeMaterials.Cube(parent, "RiseLanding",
                centre + Vector3.up * (wallHeight + RiseShaft + LiningThickness * 0.5f),
                new Vector3(cellSize, LiningThickness, cellSize), lining);
        }

        /// <summary>
        /// Builds the ascending flight. Each tread is a solid block from the floor up to its top
        /// face, so the staircase reads as one mass rather than as floating slabs.
        /// </summary>
        /// <param name="parent">Stairwell root transform.</param>
        /// <param name="centre">World centre of the cell at floor level.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <param name="wallHeight">Floor-to-ceiling height in metres.</param>
        /// <param name="ascent">Direction the steps climb towards.</param>
        /// <param name="tread">Material for the treads.</param>
        private static void BuildRise(Transform parent, Vector3 centre, float cellSize,
            float wallHeight, Direction ascent, Material tread)
        {
            Vector2Int d = Directions.Delta(ascent);
            var along = new Vector3(d.x, 0f, d.y);
            float half = cellSize * 0.5f;
            float run = cellSize / StepCount;
            float width = cellSize - LiningThickness * 4f;
            float rise = (wallHeight + 0.2f) / StepCount;

            Vector3 foot = centre - along * half;

            for (int i = 0; i < StepCount; i++)
            {
                float top = (i + 1) * rise;
                Vector3 footprint = along * ((i + 0.5f) * run);
                var size = d.x != 0
                    ? new Vector3(run, top, width)
                    : new Vector3(width, top, run);

                MazeMaterials.Cube(parent, $"RiseStep_{i}",
                    foot + footprint + Vector3.up * (top * 0.5f), size, tread);
            }
        }

        /// <summary>How far the shaft above the ceiling extends, in metres.</summary>
        private const float RiseShaft = 2.4f;

        /// <summary>
        /// Builds a stairwell in one cell.
        /// </summary>
        /// <param name="layout">The floor being built.</param>
        /// <param name="cell">Cell to sink the stairwell into.</param>
        /// <param name="wallHeight">Floor-to-ceiling height in metres.</param>
        /// <param name="theme">Palette for the shaft lining and treads.</param>
        /// <param name="parent">Parent transform for the stairwell.</param>
        public void Build(MazeLayout layout, Vector2Int cell, float wallHeight, FloorTheme theme,
            Transform parent)
        {
            float cellSize = layout.CellSize;
            Vector3 centre = layout.CellCenterToWorld(cell);

            var root = new GameObject($"Stairwell_{cell.x}_{cell.y}");
            root.transform.SetParent(parent, worldPositionStays: false);

            Material lining = MazeMaterials.Lit(theme.Trim * 0.7f);
            Material tread = MazeMaterials.Lit(theme.Wall * 0.55f);

            LineShaft(root.transform, centre, cellSize, lining);
            BuildFlight(root.transform, centre, cellSize, DescentDirection(layout, cell), tread);
            SignTheWayDown(root.transform, centre, cellSize, wallHeight);
        }

        /// <summary>
        /// The direction the flight descends in: away from the doorway the player walks in through,
        /// so the steps drop in front of them rather than behind.
        /// </summary>
        /// <param name="layout">The floor being built.</param>
        /// <param name="cell">The stairwell's cell.</param>
        /// <returns>Direction the steps descend towards.</returns>
        private static Direction DescentDirection(MazeLayout layout, Vector2Int cell)
        {
            foreach (Direction dir in Directions.All)
            {
                if (layout.CanMove(cell.x, cell.y, dir)) return Directions.Opposite(dir);
            }

            return Direction.North;
        }

        /// <summary>
        /// Lines the shaft with four walls and a landing, so looking into the hole shows a stairwell
        /// rather than straight out of the level.
        /// </summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="centre">World centre of the cell at floor level.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <param name="lining">Material for the shaft walls and landing.</param>
        private static void LineShaft(Transform parent, Vector3 centre, float cellSize,
            Material lining)
        {
            float half = cellSize * 0.5f;
            float midY = -ShaftDepth * 0.5f;

            foreach (Direction dir in Directions.All)
            {
                Vector2Int d = Directions.Delta(dir);
                var outward = new Vector3(d.x, 0f, d.y);
                bool alongX = d.x == 0;

                Vector3 size = alongX
                    ? new Vector3(cellSize, ShaftDepth, LiningThickness)
                    : new Vector3(LiningThickness, ShaftDepth, cellSize);

                MazeMaterials.Cube(parent, $"ShaftWall_{dir}",
                    centre + outward * (half - LiningThickness * 0.5f) + Vector3.up * midY,
                    size, lining);
            }

            MazeMaterials.Cube(parent, "ShaftLanding",
                centre + Vector3.up * (-ShaftDepth - LiningThickness * 0.5f),
                new Vector3(cellSize, LiningThickness, cellSize), lining);
        }

        /// <summary>
        /// Builds the flight itself. Each tread is a solid block from its top face down to the
        /// landing, so the staircase reads as one mass from any angle instead of as floating slabs.
        /// </summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="centre">World centre of the cell at floor level.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <param name="descent">Direction the steps descend towards.</param>
        /// <param name="tread">Material for the treads.</param>
        private static void BuildFlight(Transform parent, Vector3 centre, float cellSize,
            Direction descent, Material tread)
        {
            Vector2Int d = Directions.Delta(descent);
            var along = new Vector3(d.x, 0f, d.y);
            float half = cellSize * 0.5f;
            float run = cellSize / StepCount;
            float width = cellSize - LiningThickness * 4f;

            // The flight starts at the edge the player walks in over and works away from it.
            Vector3 entry = centre - along * half;

            for (int i = 0; i < StepCount; i++)
            {
                float top = -(i + 1) * StepRise;
                float height = ShaftDepth + top;
                if (height <= 0f) break;

                Vector3 footprint = along * ((i + 0.5f) * run);
                var size = d.x != 0
                    ? new Vector3(run, height, width)
                    : new Vector3(width, height, run);

                MazeMaterials.Cube(parent, $"Step_{i}",
                    entry + footprint + Vector3.up * (top - height * 0.5f), size, tread);
            }
        }

        /// <summary>
        /// Hangs a glowing sign above the opening and drops a green light into the shaft. On a
        /// 128-metre floor the stairwell itself is invisible until you are on top of it; the sign is
        /// what makes it findable, and green is the colour the player already reads as "this way out".
        /// </summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="centre">World centre of the cell at floor level.</param>
        /// <param name="cellSize">World size of one cell in metres.</param>
        /// <param name="wallHeight">Floor-to-ceiling height in metres.</param>
        private static void SignTheWayDown(Transform parent, Vector3 centre, float cellSize,
            float wallHeight)
        {
            // Strength stays at 1. There is no tonemapping in the pipeline yet, so anything brighter
            // clips the green channel first and the sign renders as a blank white slab — the colour
            // that was carrying the meaning is the first thing lost.
            Material glow = MazeMaterials.Glowing(StairsGreen, 1f);

            // A box rather than a quad: a quad is single-sided, so half the approaches to the
            // stairwell would show nothing at all.
            MazeMaterials.Cube(parent, "StairsSign",
                centre + Vector3.up * (wallHeight - 0.45f),
                new Vector3(cellSize * 0.38f, 0.28f, cellSize * 0.38f), glow);

            // Two lights, because one cannot do both jobs: a green pool spilling across the floor is
            // what makes the stairwell findable from across a 96m floor, and a dim light down in the
            // shaft is what stops the treads reading as a black rectangle once you are standing over
            // it.
            AddLight(parent, "StairsGlow", centre + Vector3.up * 0.6f, 2.6f, 7f);
            AddLight(parent, "ShaftGlow", centre + Vector3.up * (-ShaftDepth * 0.55f), 1.6f, 4f);
        }

        /// <summary>
        /// Adds one shadowless green point light to a stairwell.
        /// </summary>
        /// <param name="parent">Parent transform.</param>
        /// <param name="name">Object name.</param>
        /// <param name="position">World position of the light.</param>
        /// <param name="intensity">Light intensity.</param>
        /// <param name="range">Light range in metres. A 12m range washed green through three cells
        /// of solid wall and gave the way down away from the far side of the floor.</param>
        /// <param name="colour">Colour of the light.</param>
        private static void AddLight(Transform parent, string name, Vector3 position, float intensity,
            float range, Color? colour = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = position;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour ?? StairsGreen;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }
    }
}
