namespace Backrooms.PlayerManager.Internal.Input
{
    /// <summary>
    /// Chooses which input stream the module acts on. Real hardware is always read; when simulation
    /// mode is enabled the module ignores it and uses the injected simulated intent instead. This is
    /// the module-owned inbound test seam — no interface is extracted and no global "we are testing"
    /// switch exists, so one module can be simulated while everything else runs live.
    /// </summary>
    internal sealed class PlayerInputSource
    {
        private readonly PlayerInputReader _reader = new PlayerInputReader();
        private PlayerInputState _simulated = PlayerInputState.None;

        /// <summary>Whether simulated input replaces real hardware input.</summary>
        public bool SimulationEnabled { get; set; }

        /// <summary>
        /// Returns the intent the module should act on this frame.
        /// </summary>
        /// <returns>Simulated input when simulation is enabled, otherwise live hardware input.</returns>
        public PlayerInputState Read() => SimulationEnabled ? _simulated : _reader.Read();

        /// <summary>How many times real hardware has actually been sampled, for tests.</summary>
        public int FreshReads => _reader.FreshReads;

        /// <summary>
        /// Sets the intent used while simulation mode is enabled.
        /// </summary>
        /// <param name="input">The simulated intent.</param>
        public void SetSimulated(PlayerInputState input) => _simulated = input;
    }
}
