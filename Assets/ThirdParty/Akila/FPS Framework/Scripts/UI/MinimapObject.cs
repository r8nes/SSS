using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Akila.FPSFramework
{
    public class MinimapObject : MonoBehaviour
    {
        [FormerlySerializedAs("iconSprite")]
        public Texture texture;
        public Color color = Color.white;
        public float orientation = 0f;
        public int order = -1; // Added ordering

        [Space]
        [Tooltip("Makes the minimap image size scale with this object's transform.")]
        public bool scaleWithTransform = true;
        public bool rotateWithObject = false;
        [FormerlySerializedAs("size")]
        public Vector2 scale = Vector2.one;
        public Vector2 offset = Vector2.zero;

        Vector3 localSize;

        public Vector2 Size
        {
            get
            {
                Vector2 finalSize = scale;

                if (scaleWithTransform)
                {
                    Vector3 transformScale = transform.lossyScale;
                    finalSize.x *= transformScale.x;
                    finalSize.y *= transformScale.z;
                }

                return finalSize;
            }

            set
            {
                Vector2 newScale = value;

                if (scaleWithTransform)
                {
                    Vector3 transformScale = transform.lossyScale;

                    if (transformScale.x != 0f)
                        newScale.x /= transformScale.x;

                    if (transformScale.z != 0f)
                        newScale.y /= transformScale.z;
                }

                scale = newScale;

                if (Icon != null)
                    Icon.sizeDelta = Size;
            }
        }

        public RectTransform Icon {  get; private set; }
        public RawImage IconImage { get; private set; }

        public bool visible { get; set; } = true;

        private void Start()
        {
            if(Minimap.Instance == null)
            {
                return;
            }

            Minimap.Instance.Register(this);

            GameObject iconGO = new GameObject($"{gameObject.name} [MinimapObject]", typeof(RectTransform), typeof(RawImage));
            Icon = iconGO.GetComponent<RectTransform>();
            IconImage = iconGO.GetComponent<RawImage>();

            Icon.SetParent(Minimap.Instance.iconsContainer, false);

            if (texture)
                IconImage.texture = texture;

            IconImage.color = color;

            Vector2 imageScale = new Vector2(scale.x, scale.y);

            if(scaleWithTransform)
            {
                Vector3 transformScale = transform.lossyScale;

                imageScale.x *= transformScale.x;
                imageScale.y *= transformScale.z;
            }

            Icon.sizeDelta = imageScale;
        }

        private void Update()
        {
            if(Icon == null || IconImage == null)
                return;

            IconImage.enabled = visible;
        }

        public void Show() => visible = true;
        public void Hide() => visible = false;

        private void OnDisable()
        {
            if (Minimap.Instance != null)
                Minimap.Instance.Unregister(this);

            if (Icon != null)
                Destroy(Icon.gameObject);
        }
    }
}
