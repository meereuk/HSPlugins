﻿using System;
using System.IO;
using System.Xml;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Studio;
using Harmony;
using IllusionPlugin;
using JetBrains.Annotations;
using System.Collections.Generic;

namespace HSSSS
{
    public class HSSSS : IEnhancedPlugin
    {
        #region Plugin Info
        public string Name { get { return "HSSSS";  } }
        public string Version { get { return "2.0.0"; } }
        public string[] Filter { get { return new[] { "HoneySelect_32", "HoneySelect_64", "StudioNEO_32", "StudioNEO_64" }; } }
        #endregion

        #region Global Variables
        // info
        public static string pluginName;
        public static string pluginVersion;
        public static string pluginLocation;
        public static string configLocation;
        public static string configFile;

        // camera effects
        public static CameraProjector CameraProjector = null;
        public static DeferredRenderer DeferredRenderer = null;
        public static SSAORenderer SSAORenderer = null;
        public static SSGIRenderer SSGIRenderer = null;
        public static TAAURenderer TAAURenderer = null;
        private static GameObject mainCamera = null;

        public static Dictionary<Guid, ScreenSpaceShadows> spotDict = new Dictionary<Guid, ScreenSpaceShadows>();

        // modprefs.ini options
        public static bool isStudio;
        public static bool isEnabled;
        public static bool hsrCompatible;
        public static bool useCustomThickness;

        public static string femaleBodyCustom;
        public static string femaleHeadCustom;
        public static string maleBodyCustom;
        public static string maleHeadCustom;

        private static KeyCode[] hotKey;
        private static int uiScale;

        // ui window
        public GameObject windowObj;

        // singleton
        public static HSSSS instance = null;
        #endregion

        #region Unity Methods
        public void OnApplicationStart()
        {
            instance = this;

            if (instance != this)
            {
                Console.WriteLine("#### HSSSS: It seems HSSSS is totally fucked up :(");
            }

            isStudio = "StudioNEO" == Application.productName;

            pluginName = this.Name;
            pluginVersion = this.Version;

            pluginLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            configLocation = Path.Combine(pluginLocation, this.Name);
            configFile = Path.Combine(configLocation, "config.xml");

            if (!Directory.Exists(configLocation))
            {
                Directory.CreateDirectory(configLocation);
            }

            this.IPAConfigParser();

            if (isEnabled)
            {
                Console.WriteLine("#### HSSSS: The global maximum LOD value is... " + Shader.globalMaximumLOD.ToString());

                if (XmlParser.LoadExternalFile())
                {
                    Console.WriteLine("#### HSSSS: Successfully loaded config.xml");
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Could not load config.xml; writing a new one...");

                    if (XmlParser.SaveExternalFile())
                    {
                        Console.WriteLine("#### HSSSS: Successfully wrote a new configuration file");
                    }

                    else
                    {
                        Console.WriteLine("#### HSSSS: Could not write config.xml. What the fuck?");
                    }
                }

                AssetLoader.LoadEverything();
                this.InternalShaderReplacer();
                
                // hsextsave compatibility
                if (isStudio)
                {
                    HSExtSave.HSExtSave.RegisterHandler("HSSSS", null, null, this.OnSceneLoad, null, this.OnSceneSave, null, null);
                }

                // harmony
                this.InjectHarmonyMethods();
            }
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnLevelWasLoaded(int level)
        {
            if (isEnabled && level >= 3)
            {
                if (this.SetupImageEffects())
                {
                    Console.WriteLine("#### HSSSS: Successfully initialized the camera effects");

                    Properties.UpdateSkin();
                    Properties.UpdateSSAO();
                    Properties.UpdateSSGI();
                    Properties.UpdateTAAU();
                    Properties.UpdatePCSS();
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Couldn't initialize the camera effects");
                }

                if (isStudio)
                {
                    MaterialReplacer.StoreMilk();
                    MaterialReplacer.ReplaceMilk();
                }
            }
        }

        public void OnUpdate()
        {
        }

        public void OnFixedUpdate()
        {
        }

        public void OnLateUpdate()
        {
            if (isEnabled && isStudio && this.GetHotKeyPressed())
            {
                if (this.windowObj == null)
                {
                    ConfigWindow.hsrCompatible = hsrCompatible;
                    ConfigWindow.uiScale = uiScale;
                    this.windowObj = new GameObject("HSSSS.ConfigWindow");
                    this.windowObj.AddComponent<ConfigWindow>();
                }

                else
                {
                    UnityEngine.Object.DestroyImmediate(this.windowObj);
                    Studio.Studio.Instance.cameraCtrl.enabled = true;
                }
            }
        }

        public void OnApplicationQuit()
        {
        }
        #endregion

        #region Scene Methods
        private void OnSceneLoad(string path, XmlNode node)
        {
            spotDict = new Dictionary<Guid, ScreenSpaceShadows>();

            if (node != null)
            {
                try
                {
                    XmlParser.LoadXml(node);
                    Properties.UpdateSkin();
                    Properties.UpdateSSAO();
                    Properties.UpdateSSGI();
                    Properties.UpdateTAAU();
                    Properties.UpdatePCSS();
                    Console.WriteLine("#### HSSSS: Loaded Configurations from the Scene File");
                }

                catch
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Configurations in the Scene File");
                }
            }

            else
            {
                Console.WriteLine("#### HSSSS: Could not Find Configurations in the Scene File");
            }

            MaterialReplacer.RestoreMilk();
            MaterialReplacer.ReplaceMilk();
        }

        private void OnSceneSave(string path, XmlWriter writer)
        {
            try
            {
                XmlParser.SaveXml(writer);
                Console.WriteLine("#### HSSSS: Saved Configurations in the Scene File");
            }
            catch
            {
                Console.WriteLine("#### HSSSS: Failed to Save Configurations in the Scene File");
            }
        }
        #endregion

        #region Custom Methods
        private void IPAConfigParser()
        {
            // enable & disable plugin
            isEnabled = ModPrefs.GetBool("HSSSS", "Enabled", true, true);
            // whether to use custom thickness map instead of the built-in texture
            useCustomThickness = ModPrefs.GetBool("HSSSS", "CustomThickness", false, true);
            // custom thickness texture location
            femaleBodyCustom = ModPrefs.GetString("HSSSS", "FemaleBody", "HSSSS/FemaleBody.png", true);
            femaleHeadCustom = ModPrefs.GetString("HSSSS", "FemaleHead", "HSSSS/FemaleHead.png", true);
            maleBodyCustom = ModPrefs.GetString("HSSSS", "MaleBody", "HSSSS/MaleBody.png", true);
            maleHeadCustom = ModPrefs.GetString("HSSSS", "MaleHead", "HSSSS/MaleHead.png", true);
            // toggle automatic skin replacer
            hsrCompatible = ModPrefs.GetBool("HSSSS", "HSRCompatibleMode", false, true);
            // shortcut for the ui window (deferred only)
            try
            {
                string[] hotKeyString = ModPrefs.GetString("HSSSS", "ShortcutKey", KeyCode.ScrollLock.ToString(), true).Split('+');

                hotKey = new KeyCode[hotKeyString.Length];

                for (int i = 0; i < hotKeyString.Length; i++)
                {
                    switch (hotKeyString[i])
                    {
                        case "Shift":
                            hotKey[i] = KeyCode.LeftShift;
                            break;

                        case "Ctrl":
                            hotKey[i] = KeyCode.LeftControl;
                            break;

                        case "Alt":
                            hotKey[i] = KeyCode.LeftAlt;
                            break;

                        default:
                            hotKey[i] = (KeyCode)Enum.Parse(typeof(KeyCode), hotKeyString[i], true);
                            break;
                    }
                }
            }
            catch (Exception)
            {
                hotKey = new KeyCode[] { KeyCode.ScrollLock };
            }
            // ui window scale
            uiScale = ModPrefs.GetInt("HSSSS", "UIScale", 4, true);
        }

        private void InternalShaderReplacer()
        {
            GraphicsSettings.SetShaderMode(BuiltinShaderType.DeferredShading, BuiltinShaderMode.UseCustom);
            GraphicsSettings.SetShaderMode(BuiltinShaderType.DeferredReflections, BuiltinShaderMode.UseCustom);
            GraphicsSettings.SetCustomShader(BuiltinShaderType.DeferredShading, AssetLoader.deferredLighting);
            GraphicsSettings.SetCustomShader(BuiltinShaderType.DeferredReflections, AssetLoader.deferredReflection);

            if (GraphicsSettings.GetCustomShader(BuiltinShaderType.DeferredShading) != AssetLoader.deferredLighting)
            {
                Console.WriteLine("#### HSSSS: Couldn't replace deferred lighting shader");
            }

            if (GraphicsSettings.GetCustomShader(BuiltinShaderType.DeferredReflections) != AssetLoader.deferredReflection)
            {
                Console.WriteLine("#### HSSSS: Couldn't replace deferred reflection Shader");
            }
        }

        private bool SetupImageEffects()
        {
            if (isStudio)
            {
                mainCamera = GameObject.Find("StudioScene/Camera/Main Camera");
                mainCamera.GetComponent<Camera>();
            }

            else
            {
                mainCamera = Camera.main.gameObject;
                mainCamera.GetComponent<Camera>();
            }

            if (mainCamera)
            {
                // main sss renderer
                if (DeferredRenderer == null)
                {
                    DeferredRenderer = mainCamera.gameObject.AddComponent<DeferredRenderer>();
                }

                if (hsrCompatible)
                {
                    // no problem?
                    if (DeferredRenderer)
                    {
                        return true;
                    }

                    // problem!
                    else
                    {
                        return false;
                    }
                }

                else
                {
                    // projection matrix calculator
                    if (CameraProjector == null)
                    {
                        CameraProjector = mainCamera.gameObject.AddComponent<CameraProjector>();
                    }

                    // ssao
                    if (SSAORenderer == null)
                    {
                        SSAORenderer = mainCamera.gameObject.AddComponent<SSAORenderer>();
                    }

                    // ssgi
                    if (SSGIRenderer == null)
                    {
                        SSGIRenderer = mainCamera.gameObject.AddComponent<SSGIRenderer>();
                    }

                    // taau
                    if (TAAURenderer == null)
                    {
                        TAAURenderer = mainCamera.gameObject.AddComponent<TAAURenderer>();
                    }

                    // is everything okay?
                    if (DeferredRenderer && CameraProjector && SSAORenderer && SSGIRenderer && TAAURenderer)
                    {
                        return true;
                    }

                    // not okay
                    else
                    {
                        return false;
                    }
                }
            }

            else
            {
                Console.WriteLine("#### HSSSS: Couldn't find the main camera");
            }

            return false;
        }

        private bool GetHotKeyPressed()
        {
            bool isPressed = true;

            for (int i = 0; i < hotKey.Length - 1; i ++)
            {
                isPressed = isPressed && Input.GetKey(hotKey[i]); 
            }

            isPressed = isPressed && Input.GetKeyDown(hotKey[hotKey.Length - 1]);

            return isPressed;
        }
        #endregion

        #region Harmony Patches
        private void InjectHarmonyMethods()
        {
            HarmonyInstance harmony = HarmonyInstance.Create("com.kkul.hssss");

            if (!hsrCompatible)
            {
                harmony.Patch(
                    AccessTools.Method(typeof(CharCustom), nameof(CharCustom.SetBaseMaterial)), null,
                    new HarmonyMethod(typeof(MaterialReplacer), nameof(MaterialReplacer.ReplaceSkin))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(CharFemaleCustom), nameof(CharFemaleCustom.ChangeNailColor)), null,
                    new HarmonyMethod(typeof(MaterialReplacer), nameof(MaterialReplacer.ReplaceNail))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(CharFemaleBody), nameof(CharFemaleBody.Reload)), null,
                    new HarmonyMethod(typeof(MaterialReplacer), nameof(MaterialReplacer.ReplaceMiscFemale))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(CharMaleBody), nameof(CharMaleBody.Reload)), null,
                    new HarmonyMethod(typeof(MaterialReplacer), nameof(MaterialReplacer.ReplaceMiscMale))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(CharCustom), nameof(CharCustom.ChangeMaterial)), null,
                    new HarmonyMethod(typeof(MaterialReplacer), nameof(MaterialReplacer.ReplaceCommon))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(CharFemaleCustom), nameof(CharFemaleCustom.ChangeEyeWColor)), null,
                    new HarmonyMethod(typeof(MaterialReplacer), nameof(MaterialReplacer.ReplaceSclera))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(CharMaleCustom), nameof(CharMaleCustom.ChangeEyeWColor)), null,
                    new HarmonyMethod(typeof(MaterialReplacer), nameof(MaterialReplacer.ReplaceSclera))
                    );
            }

            if (isStudio)
            {
                harmony.Patch(
                    AccessTools.Method(typeof(OCILight), nameof(OCILight.SetEnable)), null,
                    new HarmonyMethod(typeof(HSSSS), nameof(SpotLightPatcher))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(OCILight), nameof(OCILight.SetEnable)), null,
                    new HarmonyMethod(typeof(HSSSS), nameof(ShadowMapPatcher))
                    );
            }
        }

        private static void SpotLightPatcher(OCILight __instance)
        {
            if (__instance.lightType == LightType.Spot)
            {
                if (__instance.light.gameObject.GetComponent<CookieUpdater>() == null)
                {
                    __instance.light.gameObject.AddComponent<CookieUpdater>();
                }
            }
        }

        private static void ShadowMapPatcher(OCILight __instance)
        {
            if (__instance.light.gameObject.GetComponent<ScreenSpaceShadows>() == null)
            {
                __instance.light.gameObject.AddComponent<ScreenSpaceShadows>();
            }
        }
        #endregion
    }
}