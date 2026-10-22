using System;
using System.Collections.Generic;
using Akila.FPSFramework;
using Mirror;
using PrimeTween;
using ShooterShop.Catalog;
using ShooterShop.Feedback;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace ShooterShop.Checkout
{
    public sealed class CheckoutCounter : NetworkBehaviour, IInteractable
    {
        [Required, ChildGameObjectsOnly]
        [InfoBox("Goods are laid out from here along the goods spacing, a bit to the side of the clerk")]
        [SerializeField] private Transform _goodsAnchor;

        [Required, ChildGameObjectsOnly]
        [InfoBox("Money stacks start here, right in front of the clerk")]
        [SerializeField] private Transform _moneyAnchor;

        [Required, ChildGameObjectsOnly]
        [InfoBox("Trigger on layer 15 over the money, active only while money lies on the counter")]
        [SerializeField] private Collider _moneyCollider;

        [SyncVar]
        private int _productIndex = -1;

        [SyncVar]
        private int _amount;

        [SyncVar]
        private int _price;

        [SyncVar]
        private Vector3 _payerPosition;

        [SyncVar(hook = nameof(OnOfferedSynced))]
        private bool _isOffered;

        private readonly List<Transform> _goods = new();
        private readonly List<Transform> _bills = new();
        private CheckoutConfig _config;
        private ProductCatalog _catalog;
        private ItemAnimator _animator;
        private IViewCamera _camera;

        public event Action<NetworkIdentity> TakeRequested;

        public bool requireHoldToInteract => false;
        public bool isHighlighted { get; set; }

        [Inject]
        public void Construct(CheckoutConfig config, ProductCatalog catalog, ItemAnimator animator, IViewCamera camera)
        {
            _config = config;
            _catalog = catalog;
            _animator = animator;
            _camera = camera;
        }

        public override void OnStartClient()
        {
            _moneyCollider.enabled = _isOffered;

            if (_isOffered)
                LayOut();
        }

        [Server]
        public void ServerOffer(ProductConfig product, int amount, int price, Vector3 payerPosition)
        {
            _productIndex = _catalog.IndexOf(product);
            _amount = amount;
            _price = price;
            _payerPosition = payerPosition;
            _isOffered = true;
        }

        [Server]
        public void ServerClear(uint collectorNetId)
        {
            _isOffered = false;
            RpcClear(collectorNetId);
        }

        public string GetInteractionName() => _isOffered ? string.Format(_config.TakePrompt, _price) : string.Empty;

        public void Interact(InteractionsManager source)
        {
            if (!_isOffered)
                return;

            if (isServer)
                TakeRequested?.Invoke(NetworkClient.localPlayer);
            else
                CmdTake();
        }

        [Command(requiresAuthority = false)]
        private void CmdTake(NetworkConnectionToClient sender = null) => TakeRequested?.Invoke(sender?.identity);

        private void OnOfferedSynced(bool _, bool isOffered)
        {
            _moneyCollider.enabled = isOffered;

            if (isOffered)
                LayOut();
        }

        [ClientRpc]
        private void RpcClear(uint collectorNetId)
        {
            _moneyCollider.enabled = false;

            var collector = collectorNetId != 0 && NetworkClient.spawned.TryGetValue(collectorNetId, out var identity) ? identity.transform : null;
            var isLocalCollector = collector != null && NetworkClient.localPlayer != null && collector == NetworkClient.localPlayer.transform;

            for (var i = 0; i < _bills.Count; i++)
            {
                var delay = (_bills.Count - 1 - i) * _config.CollectStagger;

                if (collector == null)
                    FlyAway(_bills[i], () => _payerPosition, _config.CollectDuration, delay);
                else
                    FlyAway(_bills[i], () => CollectPoint(collector, isLocalCollector), _config.CollectDuration, delay);
            }

            foreach (var item in _goods)
                FlyAway(item, () => _payerPosition, _config.GoodsTakeDuration, 0f);

            _bills.Clear();
            _goods.Clear();
        }

        private Vector3 CollectPoint(Transform collector, bool isLocalCollector)
        {
            var camera = _camera.Current;

            if (isLocalCollector && camera != null)
                return camera.transform.TransformPoint(_config.CollectOffset);

            return collector != null ? collector.position + Vector3.up * 1.4f : _payerPosition;
        }

        private static void FlyAway(Transform item, Func<Vector3> target, float duration, float delay)
        {
            if (item == null)
                return;

            Tween.StopAll(item);
            item.SetParent(null, true);

            var start = item.position;
            var startScale = item.localScale;
            Tween.Custom(item, 0f, 1f, duration, (current, t) =>
                {
                    current.position = Vector3.Lerp(start, target(), t) + Vector3.up * (0.15f * 4f * t * (1f - t));
                    current.localScale = startScale * (1f - t);
                }, Ease.InQuad, startDelay: delay)
                .OnComplete(item.gameObject, Object.Destroy);
        }

        private void LayOut()
        {
            DestroyVisuals();

            var delay = 0f;

            if (_catalog.TryGet(_productIndex, out var product) && product.ShelfModel != null)
            {
                var shown = Mathf.Min(_amount, _config.MaxGoodsShown);

                for (var i = 0; i < shown; i++)
                {
                    _goods.Add(Place(product.ShelfModel, _goodsAnchor, _config.GoodsSpacing * i, product.IsAmmo ? _config.AmmoYaw : 0f, delay));
                    delay += _config.GoodsStagger;
                }
            }

            delay += _config.MoneyDelay;
            var stack = 0;

            foreach (var (denomination, count) in _config.Split(_price))
            {
                var shown = Mathf.Min(count, _config.MaxBillsPerStack);

                for (var i = 0; i < shown; i++)
                {
                    var offset = _config.StackSpacing * stack + Vector3.up * (denomination.StackStep * i);
                    _bills.Add(Place(denomination.Prefab, _moneyAnchor, offset, Random.Range(-_config.BillYawJitter, _config.BillYawJitter), delay));
                    delay += _config.BillStagger;
                }

                stack++;
            }
        }

        private Transform Place(GameObject prefab, Transform anchor, Vector3 localOffset, float yaw, float delay)
        {
            var slot = new GameObject(prefab.name).transform;
            slot.SetParent(anchor, false);
            slot.SetLocalPositionAndRotation(localOffset, Quaternion.Euler(0f, yaw, 0f));

            var visual = Instantiate(prefab, slot, false).transform;
            visual.gameObject.SetActive(false);

            Tween.Delay(visual, delay, item =>
            {
                item.gameObject.SetActive(true);
                _animator.FlyInto(item, _payerPosition);
            });

            return slot;
        }

        private void DestroyVisuals()
        {
            foreach (var item in _goods)
                if (item != null)
                    Destroy(item.gameObject);

            foreach (var item in _bills)
                if (item != null)
                    Destroy(item.gameObject);

            _goods.Clear();
            _bills.Clear();
        }
    }
}
