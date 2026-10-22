using System;
using System.Collections.Generic;
using ShooterShop.Customers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Intro
{
    [Serializable]
    public sealed class DialogLine
    {
        [TextArea(2, 5)]
        [SerializeField] private string _text;

        public string Text => _text;
    }

    [Serializable]
    public sealed class BossVisit
    {
        [MinValue(1)]
        [SerializeField] private int _day = 1;

        [InfoBox("Bandits take positions across the street once the talk is over")]
        [SerializeField] private bool _startsAmbush;

        [ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<DialogLine> _lines = new();

        public int Day => _day;
        public bool StartsAmbush => _startsAmbush;
        public IReadOnlyList<DialogLine> Lines => _lines;
    }

    [CreateAssetMenu(menuName = "Shooter Shop/Intro/Intro Config", fileName = "IntroConfig")]
    public sealed class IntroConfig : ScriptableObject
    {
        [BoxGroup("Boss"), Required, AssetsOnly]
        [SerializeField] private CustomerAppearance _bossAppearance;

        [BoxGroup("Boss"), Required]
        [SerializeField] private string _bossName = "Boss";

        [BoxGroup("Boss"), Required]
        [SerializeField] private string _interactionName = "Talk to the boss";

        [BoxGroup("Boss"), MinValue(0.5f), SuffixLabel("m/s")]
        [SerializeField] private float _walkSpeed = 1.4f;

        [BoxGroup("Boss"), MinValue(0.1f)]
        [InfoBox("Blend tree MoveY value of the walk clip")]
        [SerializeField] private float _walkBlendValue = 5f;

        [BoxGroup("Boss"), MinValue(1)]
        [SerializeField] private float _animationSmoothing = 10f;

        [BoxGroup("Dialog"), ListDrawerSettings(ShowFoldout = false)]
        [InfoBox("The visit of day 1 is the intro that turns the power on")]
        [SerializeField] private List<BossVisit> _visits = new();

        [BoxGroup("Dialog"), Required]
        [SerializeField] private string _nextLabel = "Next";

        [BoxGroup("Dialog"), Required]
        [SerializeField] private string _finishLabel = "Got it";

        [BoxGroup("Dialog"), Required]
        [SerializeField] private string _talkFirstMessage = "Talk to the boss before opening the shop";

        [BoxGroup("Dialog"), Required]
        [SerializeField] private string _ambushFirstMessage = "Deal with the bandits across the street first";

        [BoxGroup("Lights"), MinMaxSlider(1, 12, true)]
        [SerializeField] private Vector2Int _flickers = new(3, 6);

        [BoxGroup("Lights"), MinMaxSlider(0.02f, 0.6f, true), SuffixLabel("sec")]
        [SerializeField] private Vector2 _flickerStep = new(0.05f, 0.25f);

        [BoxGroup("Lights"), MinValue(0f), SuffixLabel("sec")]
        [InfoBox("Delay between the end of the dialog and the first flicker")]
        [SerializeField] private float _lightsDelay = 0.5f;

        [BoxGroup("Doors"), MinValue(0.05f), SuffixLabel("sec")]
        [SerializeField] private float _doorOpenDuration = 1.2f;

        [BoxGroup("Doors"), MinValue(0f), SuffixLabel("sec")]
        [InfoBox("Delay after the lights come on before the doors open")]
        [SerializeField] private float _doorDelay = 0.4f;

        public CustomerAppearance BossAppearance => _bossAppearance;
        public string BossName => _bossName;
        public string InteractionName => _interactionName;
        public float WalkSpeed => _walkSpeed;
        public float WalkBlendValue => _walkBlendValue;
        public float AnimationSmoothing => _animationSmoothing;
        public IReadOnlyList<BossVisit> Visits => _visits;
        public string NextLabel => _nextLabel;
        public string FinishLabel => _finishLabel;
        public string TalkFirstMessage => _talkFirstMessage;
        public string AmbushFirstMessage => _ambushFirstMessage;
        public Vector2Int Flickers => _flickers;
        public Vector2 FlickerStep => _flickerStep;
        public float LightsDelay => _lightsDelay;
        public float DoorOpenDuration => _doorOpenDuration;
        public float DoorDelay => _doorDelay;

        public int VisitIndexOf(int day) => _visits.FindIndex(v => v.Day == day);

        public bool TryGetVisit(int index, out BossVisit visit)
        {
            visit = index >= 0 && index < _visits.Count ? _visits[index] : null;
            return visit != null;
        }
    }
}
