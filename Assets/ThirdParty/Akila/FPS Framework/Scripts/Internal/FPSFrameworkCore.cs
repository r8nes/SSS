using Akila.FPSFramework.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
#endif

using UnityEngine;
using UnityEngine.InputSystem;

namespace Akila.FPSFramework
{
    public static class FPSFrameworkCore
    {
        public const string version = "2.2.8"; 

#if FPS_FRAMEWORK_PRO
        internal const string tierName = "FPS Framework Pro";
#else
        internal const string tierName = "FPS Framework";
#endif

#if UNITY_EDITOR
        internal static bool IsDeveloperMode
        {
            get
            {
                string path = "Assets/devmode.cs";

                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(path, typeof(UnityEngine.Object));

                return obj != null;
            }
        }
#endif

        /// <summary>
        /// Prints a log only if message level is on the same level or lower than the settings'
        /// </summary>
        /// <param name="message"></param>
        /// <param name="level"></param>
        /// <param name="context"></param>
        public static void Log(object message, MessageLevel level = MessageLevel.Normal, UnityEngine.Object context = null)
        {
            //Stop if Debug is set to None
            if (FPSFrameworkSettings.debugLevel == MessageLevel.Silent) return;

            //Check for current selected debug level
            //'(int)FPSFrameworkSettings.debugLevel' is the int value of the current debug level
            //'(int)level' is the int value of this message's level
            //If current message level value is higher than  the settings' don't print this message
            //If current message level value is less than the settings' print this message
            //Int is used to include all previous levels without having to select a very specific level

            if ((int)FPSFrameworkSettings.debugLevel > (int)level)
            {
                return;
            }

            Debug.Log(message, context);
        }

        /// <summary>
        /// Prints a log only if message level is on the same level or lower than the settings'
        /// </summary>
        /// <param name="message"></param>
        /// <param name="level"></param>
        /// <param name="context"></param>
        public static void LogWarning(object message, MessageLevel level = MessageLevel.Normal, UnityEngine.Object context = null)
        {
            //Stop if Debug is set to None
            if (FPSFrameworkSettings.debugLevel == MessageLevel.Silent) return;

            //Check for current selected debug level
            //'(int)FPSFrameworkSettings.debugLevel' is the int value of the current debug level
            //'(int)level' is the int value of this message's level
            //If current message level value is higher than  the settings' don't print this message
            //If current message level value is less than the settings' print this message
            //Int is used to include all previous levels without having to select a very specific level

            if ((int)FPSFrameworkSettings.debugLevel > (int)level)
            {
                return;
            }

            Debug.LogWarning(message, context);
        }

        /// <summary>
        /// Prints a log only if message level is on the same level or lower than the settings'
        /// </summary>
        /// <param name="message"></param>
        /// <param name="level"></param>
        /// <param name="context"></param>
        public static void LogError(object message, MessageLevel level = MessageLevel.Normal, UnityEngine.Object context = null)
        {
            //Stop if Debug is set to None
            if (FPSFrameworkSettings.debugLevel == MessageLevel.Silent) return;

            //Check for current selected debug level
            //'(int)FPSFrameworkSettings.debugLevel' is the int value of the current debug level
            //'(int)level' is the int value of this message's level
            //If current message level value is higher than  the settings' don't print this message
            //If current message level value is less than the settings' print this message
            //Int is used to include all previous levels without having to select a very specific level

            if ((int)FPSFrameworkSettings.debugLevel > (int)level)
            {
                return;
            }

            Debug.LogError(message, context);
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        public static async void CheckForUpdatesAutomatically()
        {
            if (FPSFrameworkCore.IsDeveloperMode) return;

            if (FPSFrameworkSettings.preset == null) return;

            if (FPSFrameworkSettings.preset.checkForUpdateThiSession == false) return;

            FPSFrameworkVersionChecker checker = new FPSFrameworkVersionChecker();

            await checker.CheckForUpdateAsync();

            if (checker.IsUpdateAvailable)
                Debug.Log($"{checker.StatusMessage}");
        }
        
        public static bool IsFPSFrameworkProInstalled()
        {
            return CheckIfDefineSymbolExists("FPS_FRAMEWORK_PRO");
        }

#endif

        public static Type CheckForTypeInProject(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.Name == typeName)
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        //Used to invoke component upgrading in FPS Framework Pro
        public static void InvokeConvertMethod(string methodName, object obj, object[] parameters)
        {
#if UNITY_EDITOR
            if(IsFPSFrameworkProInstalled())
            {
                Type type = CheckForTypeInProject("FPSFrameworkProCore");

                if(type == null)
                {
                    Debug.LogError("'FPSFrameworkProCore.cs' file is missing from your project. Please ensure that the FPS Framework Pro package is correctly installed. If the issue persists, try reinstalling the package or verify the integrity of your project files.");

                    return;
                }

                type.GetMethod(methodName).Invoke(obj, parameters);
            }
            else
            {
                Debug.LogError("'FPSFrameworkPro' package is not installed in your project. Please install the FPS Framework Pro package via the Package Manager and try again. If the issue persists, ensure that the installation was successful.");
            }
#endif
        }

        public static int GetRefreshRate()
        {
            double result = 0;
            Resolution[] resolutions = Screen.resolutions;

            foreach (Resolution res in resolutions)
            {
                if (res.refreshRateRatio.value > result) result = res.refreshRateRatio.value;
            }

            return (int)result;
        }

        public static Resolution[] GetResolutions()
        {
            return Screen.resolutions;
        }


        public static Vector3 MultiplyVectors(Vector3 a, Vector3 b)
        {
            a.x *= b.x;
            a.y *= b.y;
            a.z *= b.z;

            return a;
        }

        public static Vector3 GetVector3Direction(Vector3Direction direction)
        {
            Vector3 vector = Vector3.zero;

            switch (direction)
            {
                case Vector3Direction.forward:
                    vector = Vector3.forward;
                    break;
                case Vector3Direction.back:
                    vector = Vector3.back;
                    break;
                case Vector3Direction.right:
                    vector = Vector3.right;
                    break;
                case Vector3Direction.left:
                    vector = Vector3.left;
                    break;
                case Vector3Direction.up:
                    vector = Vector3.up;
                    break;
                case Vector3Direction.down:
                    vector = Vector3.down;
                    break;
            }

            return vector;
        }

        public static Quaternion GetFromToRotation(RaycastHit raycastHit, Vector3Direction direction)
        {
            Quaternion result = new Quaternion();

            switch (direction)
            {
                case Vector3Direction.forward:
                    result = Quaternion.FromToRotation(Vector3.forward, raycastHit.normal);
                    break;

                case Vector3Direction.back:
                    result = Quaternion.FromToRotation(Vector3.back, raycastHit.normal);
                    break;

                case Vector3Direction.right:
                    result = Quaternion.FromToRotation(Vector3.right, raycastHit.normal);
                    break;

                case Vector3Direction.left:
                    result = Quaternion.FromToRotation(Vector3.left, raycastHit.normal);
                    break;

                case Vector3Direction.up:
                    result = Quaternion.FromToRotation(Vector3.up, raycastHit.normal);
                    break;

                case Vector3Direction.down:
                    result = Quaternion.FromToRotation(Vector3.down, raycastHit.normal);
                    break;
            }

            return result;
        }

        private static ControlScheme currentControlScheme;

        public static ControlScheme GetActiveControlScheme()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad gamepad = Gamepad.current;
            Touchscreen touchscreen = Touchscreen.current;

            if(keyboard != null)
            {
                if (IsKeyboardInputReceived(keyboard))
                    currentControlScheme = ControlScheme.Keyboard;
            }

            if(mouse != null)
            {
                if (IsMouseInputReceived(mouse))
                    currentControlScheme = ControlScheme.Mouse;
            }

            if(gamepad != null)
            {
                if (IsGamepadInputReceived(gamepad))
                    currentControlScheme = ControlScheme.Gamepad;
            }

            if(touchscreen != null)
            {
                if(IsTouchInputReceived(touchscreen))
                    currentControlScheme = ControlScheme.TouchScreen;
            }

            return currentControlScheme;
        }

        public static bool IsMouseInputReceived(Mouse mouse)
        {
            // Check mouse buttons
            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed)
                return true;

            // Check extra buttons (if the mouse has them)
            if (mouse.forwardButton?.isPressed == true || mouse.backButton?.isPressed == true)
                return true;

            // Check mouse movement
            if (mouse.delta.ReadValue() != Vector2.zero)
                return true;

            // Check scroll wheel
            if (mouse.scroll.ReadValue() != Vector2.zero)
                return true;

            return false; // No mouse input detected
        }

        public static bool IsKeyboardInputReceived(Keyboard keyboard)
        {
            if (keyboard.anyKey.IsPressed()) 
                return true;

            return false;
        }

        public static bool IsGamepadInputReceived(Gamepad gamepad)
        {
            // Check buttons
            if (gamepad.buttonSouth.isPressed || gamepad.buttonNorth.isPressed ||
                gamepad.buttonEast.isPressed || gamepad.buttonWest.isPressed)
                return true;

            // Check triggers
            if (gamepad.leftTrigger.isPressed || gamepad.rightTrigger.isPressed)
                return true;

            // Check bumpers
            if (gamepad.leftShoulder.isPressed || gamepad.rightShoulder.isPressed)
                return true;

            // Check thumbsticks
            if (gamepad.leftStick.ReadValue() != Vector2.zero ||
                gamepad.rightStick.ReadValue() != Vector2.zero)
                return true;

            // Check thumbstick button presses (L3, R3)
            if (gamepad.leftStickButton.isPressed || gamepad.rightStickButton.isPressed)
                return true;

            // Check D-pad
            if (gamepad.dpad.ReadValue() != Vector2.zero)
                return true;

            // Check start/select buttons
            if (gamepad.startButton.isPressed || gamepad.selectButton.isPressed)
                return true;

            return false; // No gamepad input detected
        }

        public static bool IsTouchInputReceived(Touchscreen touchscreen)
        {
            // Check if there are any active touches
            if (touchscreen.touches.Count > 0)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (touch.phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.None)
                        return true; // Active touch detected
                }
            }

            return false; // No touch input detected
        }

        #region Unity Editor
#if UNITY_EDITOR
        readonly static string FPSF_DEFINE = "FPS_FRAMEWORK";
        readonly static string FPSF_PRO_DEFINE = "FPS_FRAMEWORK_PRO";
        readonly static string FPSF_PRO_CORE_NAME = "FPSFrameworkProCore";

        [InitializeOnLoadMethod]
        internal static void InstallFPSF()
        {
            if(CheckIfDefineSymbolExists(FPSF_DEFINE) == false)
            {
                AddCustomDefineSymbol(FPSF_DEFINE);
            }
        }

        [InitializeOnLoadMethod]
        internal static void InstallFPSFPro()
        {
            if(CheckForTypeInProject(FPSF_PRO_CORE_NAME) != null)
            {
                if (CheckIfDefineSymbolExists(FPSF_PRO_DEFINE) == false)
                    AddCustomDefineSymbol(FPSF_PRO_DEFINE);
            }
        }

        [InitializeOnLoadMethod]
        internal static void UninstallFPSFPro()
        {
            if(CheckForTypeInProject(FPSF_PRO_CORE_NAME) == null)
            {
                if (CheckIfDefineSymbolExists(FPSF_PRO_DEFINE) == true)
                    RemoveCustomDefineSymbol(FPSF_PRO_DEFINE);
            }
        }

        internal static void DisposeDevMode()
        {
            string devModeModifierPath = AssetDatabase.GUIDToAssetPath(WizardGUIDs.devMode);

            AssetDatabase.DeleteAsset(devModeModifierPath);
        }

        public static bool CheckIfDefineSymbolExists(string defineSymbol)
        {
            string currentDefines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);

            return currentDefines
                .Split(';')
                .Any(d => d.Trim() == defineSymbol);
        }

        public static void AddCustomDefineSymbol(string defineSymbol)
        {
            string currentDefines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);

            var defines = currentDefines
                .Split(';')
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .ToList();

            if (!defines.Contains(defineSymbol))
            {
                defines.Add(defineSymbol);
                PlayerSettings.SetScriptingDefineSymbols(
                    NamedBuildTarget.Standalone,
                    string.Join(";", defines)
                );
            }
        }

        public static void RemoveCustomDefineSymbol(string defineSymbol)
        {
            string currentDefines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);

            var defines = currentDefines
                .Split(';')
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .ToList();

            if (defines.Remove(defineSymbol))
            {
                PlayerSettings.SetScriptingDefineSymbols(
                    NamedBuildTarget.Standalone,
                    string.Join(";", defines)
                );
            }
        }
#endif
        #endregion

        public static bool IsActive { get; set; } = true;

        public static bool IsInputActive { get; set; } = true;

        public static bool IsPaused { get; set; } = false;

        internal static bool IsFreezOnPauseActive { get; set; } = true;
        
        public static float FieldOfView { get; set; } = 60;
        public static float WeaponFieldOfView { get; set; } = 60;
        public static bool IsFieldOfViewOverridden { get; set; }
        public static bool IsWeaponFieldOfViewOverridden { get; set; }

        public static float SensitivityMultiplier { get; set; } = 1;
        public static float XSensitivityMultiplier { get; set; } = 1;
        public static float YSensitivityMultiplier { get; set; } = 1;
    }
}