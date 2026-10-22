using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Hud
{
    [CreateAssetMenu(menuName = "Shooter Shop/Hud Config", fileName = "HudConfig")]
    public sealed class HudConfig : ScriptableObject
    {
        [BoxGroup("Hints"), TextArea(1, 3)]
        [SerializeField] private string _defaultHint = "[F] Interact    [Esc] Menu";

        [BoxGroup("Hints"), TextArea(1, 3)]
        [InfoBox("{0} is the drop key, {1} is the throw key")]
        [SerializeField] private string _carryHint = "[F] Put on a stand    [{0}] Drop    [{1}] Throw";

        [BoxGroup("Hints"), TextArea(1, 3)]
        [SerializeField] private string _weaponHint = "[LMB] Shoot    [RMB] Aim    [R] Reload    Put the weapon away to carry boxes";

        [BoxGroup("Hints"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _hintFade = 0.2f;

        [BoxGroup("Save indicator"), Required]
        [SerializeField] private string _savingText = "Saving...";

        [BoxGroup("Save indicator"), Required]
        [SerializeField] private string _savedText = "Game saved";

        [BoxGroup("Save indicator"), MinValue(0f), SuffixLabel("sec")]
        [SerializeField] private float _savingDuration = 0.8f;

        [BoxGroup("Save indicator"), MinValue(0f), SuffixLabel("sec")]
        [SerializeField] private float _savedDuration = 1.5f;

        [BoxGroup("Save indicator"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _saveFade = 0.25f;

        [BoxGroup("Save indicator"), SuffixLabel("°/sec")]
        [SerializeField] private float _iconSpinSpeed = -360f;

        public string DefaultHint => _defaultHint;
        public string CarryHint => _carryHint;
        public string WeaponHint => _weaponHint;
        public float HintFade => _hintFade;
        public string SavingText => _savingText;
        public string SavedText => _savedText;
        public float SavingDuration => _savingDuration;
        public float SavedDuration => _savedDuration;
        public float SaveFade => _saveFade;
        public float IconSpinSpeed => _iconSpinSpeed;
    }
}
