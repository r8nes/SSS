using Akila.FPSFramework.Internal;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.PlayerLoop;

namespace Akila.FPSFramework
{
    /// <summary>
    /// An internal class, that holds the data for all settings of the framework settings. Settings are accessed from here.
    /// This class should not be used for any other purposes other than the purpose it exists for. Please don't modify.
    /// </summary>
    [CreateAssetMenu]
    internal class CoreConfig : ScriptableObject
    {
        public bool shortenMenus = false;
        public bool automaticUpdatesChecking = true;
        public float masterAudioVolume = 1;
        public float globalAnimationWeight = 1;
        public float globalAnimationSpeed = 1;
        public int maxAnimationFramerate = 120;

        public bool wasTagsManagerSetup = false;
        public bool wasPhysicsSetup = false;
        public bool wasPlayerSetup = false;
        public bool wasSceneManagerSetup = false;
        public bool wasRPSetup = false;

        public MessageLevel debugLevel = MessageLevel.Normal;

        public bool checkForUpdateThiSession = true;

        public RenderPipelineType renderPipelineType;

        public string cachedVersion;

        internal string url
        {
            get
            {
#if FPS_FRAMEWORK_PRO
                string uas = "https://assetstore.unity.com/packages/templates/systems/fps-framework-pro-2-0-290322";
#else
                string uas = "https://assetstore.unity.com/packages/templates/systems/fps-framework-2-0-278978";
#endif

                return uas;
            }
        }

        internal string reviewUrl
        {
            get
            {
#if FPS_FRAMEWORK_PRO
                string uas = "https://assetstore.unity.com/packages/templates/systems/fps-framework-pro-2-0-290322#reviews";
#else
                string uas = "https://assetstore.unity.com/packages/templates/systems/fps-framework-2-0-278978?srsltid=AfmBOopOvWUokOp_f7MVwHYRyhi1-HBmE45mEuc-wVVs2t8V028qqKSq#reviews";
#endif
                return uas;
            }
        }

        private void Awake()
        {
            checkForUpdateThiSession = true;
        }

        private void OnEnable()
        {
            if (cachedVersion != FPSFrameworkCore.version)
            {
                //FPSFrameworkUpdateSetupWindow.ShowWindow();
            }

            cachedVersion = FPSFrameworkCore.version;
        }

        internal void ResetAllSettings()
        {
            shortenMenus = false;
            automaticUpdatesChecking = true;
            masterAudioVolume = 1;
            globalAnimationWeight = 1;
            globalAnimationSpeed = 1;
            maxAnimationFramerate = 120;

#if UNITY_EDITOR
            FPSFrameworkCore.RemoveCustomDefineSymbol("FPS_FRAMEWORK_SHORTEN_MENUS");
#endif
        }

        internal enum RenderPipelineType
        {
            BuiltIn,
            UniversalRP,
            HighDefinitionRP
        }
    }
}   