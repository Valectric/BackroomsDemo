using UnityEngine;

namespace Backrooms.MazeManager.Internal.Geometry
{
    /// <summary>
    /// Shared placement arithmetic for furniture: measuring what a model actually occupies, dropping
    /// it onto the floor, and seating it against a wall plane. Split out of the decorator because the
    /// per-cell and per-wall-run placement paths both need it and neither owns it.
    /// </summary>
    /// <remarks>
    /// Everything here works from world-space renderer bounds rather than from the transform, because
    /// models in the pack are pivoted variously at their base, centre, top or an edge. Assuming the
    /// pivot sits at the centre buries half the catalogue in the floor and the rest in the wall.
    /// </remarks>
    internal static class PropPlacement
    {
        /// <summary>
        /// Combined world-space bounds of every renderer on an object.
        /// </summary>
        /// <param name="instance">Object to measure.</param>
        /// <param name="bounds">Receives the combined bounds.</param>
        /// <returns><c>true</c> if the object has any renderers.</returns>
        public static bool TryGetWorldBounds(GameObject instance, out Bounds bounds)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        /// <summary>
        /// Drops a piece so it rests exactly on the floor.
        /// </summary>
        /// <param name="instance">The placed object.</param>
        /// <param name="floorY">World height of the floor.</param>
        public static void SeatOnFloor(GameObject instance, float floorY)
        {
            if (!TryGetWorldBounds(instance, out Bounds bounds)) return;
            instance.transform.position += Vector3.up * (floorY - bounds.min.y);
        }

        /// <summary>
        /// Slides a piece along the wall normal until its rear face stands the given clearance from
        /// the wall plane. Works in both directions, so a piece spawned inside the wall and one
        /// spawned out in the room both end up equally seated.
        /// </summary>
        /// <param name="instance">The placed object.</param>
        /// <param name="wallPoint">Any world point on the wall plane.</param>
        /// <param name="intoRoom">Unit vector pointing away from the wall, into the room.</param>
        /// <param name="clearance">Gap to leave between the piece and the wall, in metres.</param>
        public static void SeatAgainstWall(GameObject instance, Vector3 wallPoint, Vector3 intoRoom,
            float clearance)
        {
            if (!TryGetWorldBounds(instance, out Bounds bounds)) return;

            Vector3 back = -intoRoom;
            var axis = new Vector3(Mathf.Abs(back.x), Mathf.Abs(back.y), Mathf.Abs(back.z));

            float wallFace = Vector3.Dot(wallPoint, back);
            float pieceBack = Vector3.Dot(bounds.center, back) + Vector3.Dot(bounds.extents, axis);

            instance.transform.position -= back * (pieceBack - (wallFace - clearance));
        }

        /// <summary>
        /// Removes every collider from a placed piece. Furniture here is scenery: the player walks
        /// through it rather than being stopped by a chair in a corridor.
        /// </summary>
        /// <param name="instance">The placed object.</param>
        public static void StripColliders(GameObject instance)
        {
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>())
            {
                Object.Destroy(collider);
            }
        }
    }
}
