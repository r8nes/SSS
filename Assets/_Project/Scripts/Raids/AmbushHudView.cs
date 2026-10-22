using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace ShooterShop.Raids
{
    public sealed class AmbushHudView : MonoBehaviour
    {
        [Required, ChildGameObjectsOnly]
        [SerializeField] private GameObject _root;

        [Required, ChildGameObjectsOnly]
        [SerializeField] private TMP_Text _counter;

        public void SetVisible(bool isVisible) => _root.SetActive(isVisible);

        public void SetCounter(string text) => _counter.text = text;
    }
}
