using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Street
{
    [CreateAssetMenu(menuName = "Shooter Shop/Street/Pedestrian Config", fileName = "PedestrianConfig")]
    public sealed class PedestrianConfig : ScriptableObject
    {
        [BoxGroup("Spawning"), Required, AssetsOnly]
        [SerializeField] private Pedestrian _prefab;

        [BoxGroup("Spawning"), MinValue(0)]
        [SerializeField] private int _count = 6;

        [BoxGroup("Walking"), MinMaxSlider(0.5f, 3f, true), SuffixLabel("m/s")]
        [SerializeField] private Vector2 _walkSpeed = new(1.1f, 1.6f);

        [BoxGroup("Walking"), MinMaxSlider(0f, 20f, true), SuffixLabel("sec")]
        [InfoBox("How long a pedestrian stands at a waypoint before walking on")]
        [SerializeField] private Vector2 _idleTime = new(1f, 6f);

        [BoxGroup("Walking"), MinValue(0.1f)]
        [InfoBox("Blend tree MoveY value of the walk clip")]
        [SerializeField] private float _walkBlendValue = 5f;

        [BoxGroup("Walking"), MinValue(1)]
        [SerializeField] private float _animationSmoothing = 10f;

        [BoxGroup("Talking"), Required]
        [SerializeField] private string _interactionName = "Talk";

        [BoxGroup("Talking"), MinValue(0.5f), SuffixLabel("sec")]
        [SerializeField] private float _replyDuration = 3f;

        [BoxGroup("Talking"), MinValue(0f), SuffixLabel("sec")]
        [InfoBox("How long the pedestrian stops and looks at the player after being talked to")]
        [SerializeField] private float _talkPause = 3f;

        [BoxGroup("Talking"), SuffixLabel("m")]
        [SerializeField] private Vector3 _replyOffset = new(0f, 1.15f, 0f);

        [BoxGroup("Talking"), ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<string> _replies = new()
        {
            "Leave me alone.",
            "Not interested.",
            "Do I know you?",
            "Get lost, pal.",
            "I'm busy.",
            "Don't talk to me.",
            "Go bother someone else.",
            "Ugh. What?",
            "I've got no money.",
            "Back off."
        };

        [BoxGroup("Violence"), MinValue(1)]
        [SerializeField] private int _hitsToKill = 2;

        [BoxGroup("Violence"), MinValue(1), SuffixLabel("sec")]
        [InfoBox("After a pedestrian is shot everyone on the street runs around in panic for this long")]
        [SerializeField] private float _panicDuration = 20f;

        [BoxGroup("Violence"), MinValue(1)]
        [SerializeField] private float _panicSpeedMultiplier = 2.6f;

        [BoxGroup("Violence"), MinValue(0)]
        [SerializeField] private float _deathImpulse = 120f;

        [BoxGroup("Violence"), MinValue(0), SuffixLabel("sec")]
        [SerializeField] private float _corpseSinkDelay = 3f;

        [BoxGroup("Violence"), MinValue(0.5f), SuffixLabel("sec")]
        [SerializeField] private float _corpseSinkDuration = 6f;

        [BoxGroup("Violence"), MinValue(0.1f), SuffixLabel("m")]
        [SerializeField] private float _corpseSinkDepth = 1.2f;

        [BoxGroup("Violence"), MinValue(0), SuffixLabel("sec")]
        [InfoBox("A new pedestrian appears this long after one dies")]
        [SerializeField] private float _respawnDelay = 20f;

        [BoxGroup("Violence"), ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<string> _panicReplies = new() { "AAAH!", "Help!", "Run!", "He's got a gun!", "Call the police!" };

        public int HitsToKill => _hitsToKill;
        public float PanicDuration => _panicDuration;
        public float PanicSpeedMultiplier => _panicSpeedMultiplier;
        public float DeathImpulse => _deathImpulse;
        public float CorpseSinkDelay => _corpseSinkDelay;
        public float CorpseSinkDuration => _corpseSinkDuration;
        public float CorpseSinkDepth => _corpseSinkDepth;
        public float RespawnDelay => _respawnDelay;
        public IReadOnlyList<string> PanicReplies => _panicReplies;
        public Pedestrian Prefab => _prefab;
        public int Count => _count;
        public Vector2 WalkSpeed => _walkSpeed;
        public Vector2 IdleTime => _idleTime;
        public float WalkBlendValue => _walkBlendValue;
        public float AnimationSmoothing => _animationSmoothing;
        public string InteractionName => _interactionName;
        public float ReplyDuration => _replyDuration;
        public float TalkPause => _talkPause;
        public Vector3 ReplyOffset => _replyOffset;
        public IReadOnlyList<string> Replies => _replies;
    }
}
