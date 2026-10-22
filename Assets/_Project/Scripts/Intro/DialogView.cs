using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShooterShop.Intro
{
    public sealed class DialogView : MonoBehaviour
    {
        [Required, ChildGameObjectsOnly]
        [SerializeField] private GameObject _root;

        [Required, ChildGameObjectsOnly]
        [SerializeField] private TMP_Text _speaker;

        [Required, ChildGameObjectsOnly]
        [SerializeField] private TMP_Text _text;

        [Required, ChildGameObjectsOnly]
        [SerializeField] private Button _nextButton;

        [Required, ChildGameObjectsOnly]
        [SerializeField] private TMP_Text _nextLabel;

        public Button NextButton => _nextButton;

        public void SetOpen(bool isOpen) => _root.SetActive(isOpen);

        public void Show(string speaker, string text, string nextLabel)
        {
            _speaker.text = speaker;
            _text.text = text;
            _nextLabel.text = nextLabel;
        }
    }
}
