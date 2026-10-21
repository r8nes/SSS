using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Customers
{
    public sealed class CustomerAppearance : MonoBehaviour
    {
        [Required, ChildGameObjectsOnly]
        [SerializeField] private Animator _animator;

        [Required, ChildGameObjectsOnly]
        [SerializeField] private CustomerRagdoll _ragdoll;

        [Required, ChildGameObjectsOnly]
        [SerializeField] private Transform _rightHand;

        [Required, ChildGameObjectsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<Renderer> _renderers = new();

        public Animator Animator => _animator;
        public CustomerRagdoll Ragdoll => _ragdoll;
        public Transform RightHand => _rightHand;
        public IReadOnlyList<Renderer> Renderers => _renderers;

#if UNITY_EDITOR
        public void EditorSetup(Animator animator, CustomerRagdoll ragdoll, Transform rightHand, IEnumerable<Renderer> renderers)
        {
            _animator = animator;
            _ragdoll = ragdoll;
            _rightHand = rightHand;
            _renderers = new List<Renderer>(renderers);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
