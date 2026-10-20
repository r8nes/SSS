using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Akila.FPSFramework
{
    public class Minimap : MonoBehaviour
    {
        public static Minimap Instance;

        public Transform player;
        public RectTransform mapRect;
        public RectTransform iconsContainer;
        public float zoom = 1;
        public bool rotateWithPlayer = true;

        private List<MinimapObject> objects = new List<MinimapObject>();

        public bool Visible { get; set; } = true;

        public bool AutoFindPlayer { get; set; } = true; 

        private void Awake()
        {
            Instance = this;
        }

        public MinimapObject MarkPosition(Vector3 position, float lifeTime = float.MaxValue)
        {
            GameObject iconGO = new GameObject("Minimap Icon", typeof(MinimapObject));

            MinimapObject minimapObject = iconGO.GetComponent<MinimapObject>(); 

            iconGO.transform.position = position;

            if(lifeTime != float.MaxValue)
            {
                GameObject.Destroy(iconGO, lifeTime);
            }

            return minimapObject;
        }

        public void Register(MinimapObject obj)
        {
            if (!objects.Contains(obj)) objects.Add(obj);
        }

        public void Unregister(MinimapObject obj)
        {
            if (objects.Contains(obj)) objects.Remove(obj);
        }

        private void LateUpdate()
        {
            mapRect.gameObject.SetActive(Visible);

            if(player == null)
            {
                Visible = false;

                return;
            }

            float halfW = mapRect.rect.width * 0.5f;
            float halfH = mapRect.rect.height * 0.5f;

            // Scale world size by zoom
            float scaledWorldSize = 50 / zoom;

            iconsContainer.rotation = Quaternion.Euler(0, 0, player.transform.eulerAngles.y);

            if(player == null && AutoFindPlayer)
            {
                CharacterManager characterManager = FindFirstObjectByType<CharacterManager>();

                if (characterManager != null)
                    player = characterManager.transform;
            }

            foreach (var obj in objects)
            {
                if (obj == null || obj.Icon == null || player == null || obj.visible == false) continue;

                Vector3 relative = obj.transform.position - player.position;

                float x = (relative.x / scaledWorldSize) * halfW;
                float y = (relative.z / scaledWorldSize) * halfH;

                // Scale offset and icon size by zoom
                Vector2 scaledSize = obj.Size * zoom;
                Vector2 scaledOffset = obj.offset * zoom;

                Vector2 finalPos = new Vector2(x, y) + scaledOffset;
                RawImage iconImg = obj.IconImage;

                // Half size for clamp
                {
                    obj.Icon.anchoredPosition = finalPos;
                }
                

                // Apply scaled size
                obj.Icon.sizeDelta = scaledSize;

                if (obj.rotateWithObject)
                {
                    obj.Icon.localEulerAngles = new Vector3(
                        0,
                        0,
                        -(obj.transform.eulerAngles.y + obj.orientation)
                    );
                }

                obj.Icon.sizeDelta = scaledSize;

                // Sorting order
                if (obj.order > -1)
                    obj.Icon.SetSiblingIndex(obj.order);
            }
        }
    }
}
