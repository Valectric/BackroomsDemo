namespace Backrooms.EntityManager
{
    /// <summary>
    /// What a Dweller is currently doing. Dwellers are the nightmare creatures that inhabit the
    /// floors of the Backrooms; they wander until they notice you, then come after you.
    /// </summary>
    public enum DwellerState
    {
        /// <summary>Wandering the floor, unaware of the player.</summary>
        Patrol = 0,

        /// <summary>Aware of the player and closing in.</summary>
        Chase = 1,

        /// <summary>Has reached the player; the run is over.</summary>
        Caught = 2
    }
}
