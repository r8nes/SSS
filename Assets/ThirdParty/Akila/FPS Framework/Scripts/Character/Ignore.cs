using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Akila.FPSFramework
{
    public class Ignore : MonoBehaviour
    {
        [Tooltip("If true, this object will ignore hits from firearms.")]
        [FormerlySerializedAs("ignoreHitDetection")]
        public bool ignoreFirearmHits = false;

        [Tooltip("If true, this object will ignore melee attack hits.")]
        public bool ignoreMeleeHits;

        [Tooltip("If true, this object will be ignored by laser detection systems.")]
        public bool ignoreLaserDetection = false;

        [Tooltip("If true, this object will not trigger wall avoidance logic.")]
        public bool ignoreWallAvoidance = false;

        [Tooltip("If true, this object will not take fall damage.")]
        public bool ignoreFallDamage = false;

        [Tooltip("If true, this object will not interact with moving platforms.")]
        public bool ignoreMovingPlatform = false;

        //It's there to show a toggle to disable this component in editor
        private void Start() { }
    }
}
