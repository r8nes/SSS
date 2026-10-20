using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Akila.FPSFramework
{
    /// <summary>
    /// Base class for all InventoryItems and all PickableItems
    /// </summary>
    public class Item : MonoBehaviour
    {
        [Header("Base")]
        public string Name = "Default";

        public bool isActive { get; set; } = true;

        public Renderer[] renderers { get; protected set; }

        protected virtual void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();

            HideAllRenderers();

            Invoke(nameof(ShowAllRenderers), Time.fixedDeltaTime);
        }

        protected virtual void HideAllRenderers()
        {
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = false;
            }
        }

        protected virtual void ShowAllRenderers()
        {
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = true;
            }
        }
    }
}