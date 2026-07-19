using Fusion;
using UnityEngine;

namespace FusionFoundry.Input
{
    /// <summary>
    /// Per-tick player input replicated by Fusion.
    /// </summary>
    public struct FusionInputData : INetworkInput
    {
        /// <summary>
        /// Camera-relative movement expressed in world XZ coordinates.
        /// </summary>
        public Vector2 Movement;

        /// <summary>
        /// Signed player yaw input. Negative turns left; positive turns right.
        /// </summary>
        public float Turn;
    }
}
