namespace Ghumante.Core.Meshing.Bridges
{
    /// <summary>
    /// Elevated road decks (bridges and flyovers) near a point, so ground queries pick the right level
    /// (docs/W2_DETAIL_CONTRACT.md §3). Implemented by Track BRIDGES as BridgeDeckIndex.
    /// </summary>
    public interface IBridgeDeckQuery
    {
        /// <summary>
        /// The deck surface at (x, z) closest to <paramref name="nearY"/> that the body can stand on (within a
        /// step-up of nearY, or below it); false when no deck covers the point or the body is under every deck.
        /// </summary>
        bool TryDeck(double x, double z, float nearY, out float deckY, out float nx, out float ny, out float nz);

        /// <summary>The lowest deck underside above (x, z) higher than <paramref name="fromY"/> (for clearance checks).</summary>
        bool TryCeiling(double x, double z, float fromY, out float undersideY);
    }
}
