using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Customers
{
    [Serializable]
    public sealed class MuscleValue
    {
        [ValueDropdown(nameof(MuscleNames))]
        [SerializeField] private string _muscle;

        [Range(-1f, 1f)]
        [SerializeField] private float _value;

        public string Muscle => _muscle;
        public float Value => _value;

        public MuscleValue(string muscle, float value)
        {
            _muscle = muscle;
            _value = value;
        }

        private static string[] MuscleNames() => HumanTrait.MuscleName;
    }
}
