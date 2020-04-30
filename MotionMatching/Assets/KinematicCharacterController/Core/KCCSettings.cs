using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KinematicCharacterController
{
    [CreateAssetMenu]
    public class KCCSettings : ScriptableObject
    {
        /// <summary>
        /// Determines if the system simulates automatically.
        /// If true, the simulation is done on FixedUpdate
        /// </summary>
        [Tooltip("Determines if the system simulates automatically. If true, the simulation is done on FixedUpdate")]
        public bool AutoSimulation = true;
        /// <summary>
        /// Should interpolation of characters and PhysicsMovers be handled
        /// </summary>
        [Tooltip("Should interpolation of characters and PhysicsMovers be handled")]
        public bool Interpolate = true;
        /// <summary>
        /// Determines if the system calls Physics.SyncTransforms() in interpolation frames, for interpolated collider position information
        /// </summary>
        [Tooltip("Determines if the system calls Physics.SyncTransforms() in interpolation frames, for interpolated collider position information")]
        public bool SyncInterpolatedPhysicsTransforms = false;
    }
}