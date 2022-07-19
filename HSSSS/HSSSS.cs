﻿using System;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Studio;
using Harmony;
using IllusionPlugin;

namespace HSSSS
{
    public enum LUTProfile
    {
        penner,
        nvidia1,
        nvidia2
    };

    public enum PCFState
    {
        disable,
        poisson8x,
        poisson16x,
        poisson32x,
        poisson64x
    };

    public struct SkinSettings
    {
        public bool sssEnabled;
        public bool transEnabled;

        public LUTProfile lutProfile;

        public float sssWeight;

        public float skinLutBias;
        public float skinLutScale;
        public float shadowLutBias;
        public float shadowLutScale;

        public float normalBlurWeight;
        public float normalBlurRadius;
        public float normalBlurDepthRange;

        public int normalBlurIter;

        public Vector3 colorBleedWeights;
        public Vector3 transAbsorption;

        public bool bakedThickness;

        public float transWeight;
        public float transShadowWeight;
        public float transBlurRadius;
        public float transDistortion;
        public float transFalloff;

        public bool useWetSpecGloss;
    }

    public struct ShadowSettings
    {
        public PCFState pcfState;

        public bool dirPcfEnabled;
        public bool pcssEnabled;

        public Vector3 pointLightPenumbra;
        public Vector3 spotLightPenumbra;
        public Vector3 dirLightPenumbra;
    }

    public class HSSSS : IEnhancedPlugin
    {
        #region Plugin Info
        public string Name { get { return "HSSSS";  } }
        public string Version { get { return "1.0.9"; } }
        public string[] Filter { get { return new[] { "HoneySelect_32", "HoneySelect_64", "StudioNEO_32", "StudioNEO_64" }; } }
        #endregion

        #region Global Variables
        // info
        public static string pluginName;
        public static string pluginVersion;
        public static string pluginLocation;
        public static string configLocation;
        public static string configPath;

        // assetbundle file
        private static AssetBundle assetBundle;

        // internal deferred shaders
        private static Shader deferredShading;
        private static Shader deferredReflections;

        // camera effects
        internal static DeferredRenderer SSS = null;
        internal static SkinSettings skinSettings = new SkinSettings()
        {
            sssEnabled = true,
            transEnabled = true,

            lutProfile = LUTProfile.penner,

            sssWeight = 1.0f,

            skinLutBias = 0.0f,
            skinLutScale = 0.5f,

            shadowLutBias = 0.0f,
            shadowLutScale = 1.0f,

            normalBlurWeight = 1.0f,
            normalBlurRadius = 0.2f,
            normalBlurDepthRange = 1.0f,

            normalBlurIter = 1,

            colorBleedWeights = new Vector3(0.40f, 0.15f, 0.20f),
            transAbsorption = new Vector3(-8.00f, -48.0f, -64.0f),

            bakedThickness = true,

            transWeight = 1.0f,
            transShadowWeight = 0.5f,
            transBlurRadius = 1.0f,
            transDistortion = 0.5f,
            transFalloff = 2.0f
        };
        internal static ShadowSettings shadowSettings = new ShadowSettings()
        {
            pcfState = PCFState.disable,

            dirPcfEnabled = false,
            pcssEnabled = false,

            pointLightPenumbra = new Vector3(1.0f, 1.0f, 1.0f),
            spotLightPenumbra = new Vector3(1.0f, 1.0f, 1.0f),
            dirLightPenumbra = new Vector3(1.0f, 1.0f, 1.0f),
        };

        internal static Shader deferredTransmissionBlit;
        internal static Shader deferredBlurredNormals;

        internal static Texture2D defaultSkinLUT;
        internal static Texture2D faceWorksSkinLUT;
        internal static Texture2D faceWorksShadowLUT;
        internal static Texture2D skinJitter;

        // skin and body materials
        public static Material skinMaterial;
        public static Material milkMaterial;
        public static Material overlayMaterial;
        public static Material eyeBrowMaterial;
        public static Material eyeLashMaterial;
        public static Material eyeCorneaMaterial;
        public static Material eyeScleraMaterial;
        public static Material eyeOverlayMaterial;

        // thickness textures
        public static Texture2D femaleBodyThickness;
        public static Texture2D femaleHeadThickness;
        public static Texture2D maleBodyThickness;
        public static Texture2D maleHeadThickness;

        // light cookie (spot)
        public static Texture2D spotCookie;

        // modprefs.ini options
        private static bool isStudio;
        private static bool isEnabled;
        private static bool useDeferred;
        private static bool useTessellation;
        private static bool fixAlphaShadow;
        private static bool useEyePOMShader;
        private static bool useCustomThickness;

        private static string femaleBodyCustom;
        private static string femaleHeadCustom;
        private static string maleBodyCustom;
        private static string maleHeadCustom;

        private static KeyCode[] hotKey;
        private static int uiScale;

        // ui window
        internal static GUISkin windowSkin;
        internal GameObject windowObj;
        #endregion

        #region Unity Methods
        public void OnApplicationStart()
        {
            isStudio = "StudioNEO" == Application.productName;

            pluginName = this.Name;
            pluginVersion = this.Version;

            pluginLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            configLocation = Path.Combine(pluginLocation, this.Name);
            configPath = Path.Combine(configLocation, "config.xml");

            if (!Directory.Exists(configLocation))
            {
                Directory.CreateDirectory(configLocation);
            }

            this.IPAConfigParser();

            if (isEnabled)
            {
                this.BaseAssetLoader();

                if (isStudio && useDeferred)
                {
                    this.DeferredAssetLoader();
                    this.InternalShaderReplacer();
                }

                else
                {
                    this.ForwardAssetLoader();
                }

                #region Harmony
                HarmonyInstance harmony = HarmonyInstance.Create("com.kkul.hssss");

                harmony.Patch(
                    AccessTools.Method(typeof(CharCustom), nameof(CharCustom.SetBodyBaseMaterial)), null,
                    new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.BodyReplacer))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(CharCustom), nameof(CharCustom.SetFaceBaseMaterial)), null,
                    new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.FaceReplacer))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(CharCustom), nameof(CharCustom.ChangeMaterial)), null,
                    new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.CommonPartsReplacer))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(CharFemaleCustom), nameof(CharFemaleCustom.ChangeEyeWColor)), null,
                    new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.ScleraReplacer))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(CharMaleCustom), nameof(CharMaleCustom.ChangeEyeWColor)), null,
                    new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.ScleraReplacer))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(Manager.Character), nameof(Manager.Character.Awake)), null,
                    new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.JuicesReplacer))
                    );

                if (isStudio)
                {
                    harmony.Patch(
                        AccessTools.Method(typeof(CharFemaleBody), nameof(CharFemaleBody.Reload)), null,
                        new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.MiscReplacer))
                        );

                    harmony.Patch(
                        AccessTools.Method(typeof(OCILight), nameof(OCILight.SetEnable)), null,
                        new HarmonyMethod(typeof(HSSSS), nameof(SpotLightPatcher))
                        );

                    if (useDeferred)
                    {
                        harmony.Patch(
                            AccessTools.Method(typeof(OCILight), nameof(OCILight.SetEnable)), null,
                            new HarmonyMethod(typeof(HSSSS), nameof(ShadowMapPatcher))
                            );
                    }
                }
                #endregion
            }
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnLevelWasLoaded(int level)
        {
            if (isStudio && level == 3)
            {
                if (isEnabled)
                {
                    if (useDeferred)
                    {
                        this.PostFxInitializer();

                        if (!LoadExternalConfig())
                        {
                            Console.WriteLine("#### HSSSS: Could not load config.xml; writing a new one...");

                            if (SaveExternalConfig())
                            {
                                Console.WriteLine("#### HSSSS: Successfully wrote a new configuration file");
                            }
                        }

                        else
                        {
                            Console.WriteLine("#### HSSSS: Successfully loaded config.xml");
                        }

                        SSS.ImportSettings();
                        SSS.ForceRefresh();
                        UpdateShadowConfig();
                        UpdateMiscConfig();
                    }

                    else
                    {
                        Shader.SetGlobalTexture("_SssBrdfTex", defaultSkinLUT);
                    }
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
            if(isEnabled && useDeferred)
            {
                if (isStudio && this.GetHotKeyPressed())
                {
                    if (this.windowObj == null)
                    {
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
        }

        public void OnApplicationQuit()
        {
        }
        #endregion

        #region Custom Methods
        private void IPAConfigParser()
        {
            // enable & disable plugin
            isEnabled = ModPrefs.GetBool("HSSSS", "Enabled", true, true);
            // deferred & foward option
            useDeferred = ModPrefs.GetBool("HSSSS", "DeferredSkin", true, true);
            // tesellation skin shader (deferred only)
            useTessellation = ModPrefs.GetBool("HSSSS", "Tessellation", false, true);
            // semi-pbr wetness option for meta/nyaacho wet specgloss
            //useWetSpecGloss = ModPrefs.GetBool("HSSSS", "WetSpecGloss", false, true);
            // additional replacement option for some transparent materials
            fixAlphaShadow = ModPrefs.GetBool("HSSSS", "FixShadow", false, true);
            // dedicated eye shader which supports pom/sss
            useEyePOMShader = ModPrefs.GetBool("HSSSS", "EyePOMShader", false, true);
            // extensive PCF soft shadows for spotlight/pointlight
            //useSoftShadow = ModPrefs.GetBool("HSSSS", "SoftShadow", false, true);
            // whether to use custom thickness map instead of the built-in texture
            useCustomThickness = ModPrefs.GetBool("HSSSS", "CustomThickness", false, true);
            // custom texture location
            femaleBodyCustom = ModPrefs.GetString("HSSSS", "FemaleBody", "HSSSS/FemaleBody.png", true);
            femaleHeadCustom = ModPrefs.GetString("HSSSS", "FemaleHead", "HSSSS/FemaleHead.png", true);
            maleBodyCustom = ModPrefs.GetString("HSSSS", "MaleBody", "HSSSS/MaleBody.png", true);
            maleHeadCustom = ModPrefs.GetString("HSSSS", "MaleHead", "HSSSS/MaleHead.png", true);
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

        private void BaseAssetLoader()
        {
            // hssssresources.unity3d
            assetBundle = AssetBundle.LoadFromMemory(Resources.hssssresources);

            // built-in thickness textures
            if (useCustomThickness)
            {
                femaleBodyCustom = Path.Combine(pluginLocation, femaleBodyCustom);
                femaleHeadCustom = Path.Combine(pluginLocation, femaleHeadCustom);
                maleBodyCustom = Path.Combine(pluginLocation, maleBodyCustom);
                maleHeadCustom = Path.Combine(pluginLocation, maleHeadCustom);

                femaleBodyThickness = new Texture2D(4, 4, TextureFormat.ARGB32, true, true);
                femaleHeadThickness = new Texture2D(4, 4, TextureFormat.ARGB32, true, true);
                maleBodyThickness = new Texture2D(4, 4, TextureFormat.ARGB32, true, true);
                maleHeadThickness = new Texture2D(4, 4, TextureFormat.ARGB32, true, true);

                // female custom body
                if (femaleBodyThickness.LoadImage(File.ReadAllBytes(femaleBodyCustom)))
                {
                    femaleBodyThickness.Apply();
                }

                else
                {
                    Console.Write("#### HSSSS: Could not load " + femaleBodyCustom);
                    femaleBodyThickness = assetBundle.LoadAsset<Texture2D>("FemaleBodyThickness");
                }

                // female custom head
                if (femaleHeadThickness.LoadImage(File.ReadAllBytes(femaleHeadCustom)))
                {
                    femaleHeadThickness.Apply();
                }

                else
                {
                    Console.Write("#### HSSSS: Could not load " + femaleHeadCustom);
                    femaleHeadThickness = assetBundle.LoadAsset<Texture2D>("FemaleHeadThickness");
                }

                // male custom body
                if (maleBodyThickness.LoadImage(File.ReadAllBytes(maleBodyCustom)))
                {
                    maleBodyThickness.Apply();
                }

                else
                {
                    Console.Write("#### HSSSS: Could not load " + maleBodyCustom);
                    maleBodyThickness = assetBundle.LoadAsset<Texture2D>("MaleBodyThickness");
                }

                // male custom head
                if (maleHeadThickness.LoadImage(File.ReadAllBytes(maleHeadCustom)))
                {
                    maleHeadThickness.Apply();
                }

                else
                {
                    Console.Write("#### HSSSS: Could not load " + maleHeadCustom);
                    maleHeadThickness = assetBundle.LoadAsset<Texture2D>("MaleHeadThickness");
                }
            }

            else
            {
                femaleBodyThickness = assetBundle.LoadAsset<Texture2D>("FemaleBodyThickness");
                femaleHeadThickness = assetBundle.LoadAsset<Texture2D>("FemaleHeadThickness");
                maleBodyThickness = assetBundle.LoadAsset<Texture2D>("MaleBodyThickness");
                maleHeadThickness = assetBundle.LoadAsset<Texture2D>("MaleHeadThickness");
            }

            // sss lookup textures
            defaultSkinLUT = assetBundle.LoadAsset<Texture2D>("DefaultSkinLUT");
            faceWorksSkinLUT = assetBundle.LoadAsset<Texture2D>("FaceWorksSkinLUT");
            faceWorksShadowLUT = assetBundle.LoadAsset<Texture2D>("FaceWorksShadowLUT");

            // jitter texture
            skinJitter = assetBundle.LoadAsset<Texture2D>("SkinJitter");
            
            // spotlight cookie
            spotCookie = assetBundle.LoadAsset<Texture2D>("DefaultSpotCookie");

            #region Errors
            if (null == assetBundle)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Internal AssetBundle");
            }

            if (null == femaleBodyThickness || null == femaleHeadThickness)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Female Thickness Textures");
            }

            if (null == maleBodyThickness || null == maleHeadThickness)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Male Thickness Textures");
            }

            if (null == defaultSkinLUT || null == faceWorksSkinLUT || null == faceWorksShadowLUT)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Skin Lookup Textures");
            }

            if (null == skinJitter)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Jitter Texture");
            }

            if (null == spotCookie)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Spotlight Cookie");
            }
            #endregion
        }

        private void DeferredAssetLoader()
        {
            // tesellation materials
            if (useTessellation)
            {
                skinMaterial = assetBundle.LoadAsset<Material>("SkinTessellation");
                milkMaterial = assetBundle.LoadAsset<Material>("OverlayTessellationForward");
                overlayMaterial = assetBundle.LoadAsset<Material>("OverlayTessellation");
            }

            // non-tesellation materials
            else
            {
                skinMaterial = assetBundle.LoadAsset<Material>("SkinDeferred");
                milkMaterial = assetBundle.LoadAsset<Material>("OverlayForward");
                overlayMaterial = assetBundle.LoadAsset<Material>("Overlay");
            }

            // post fx shaders
            deferredTransmissionBlit = assetBundle.LoadAsset<Shader>("TransmissionBlit");
            deferredBlurredNormals = assetBundle.LoadAsset<Shader>("BlurredNormals");

            // internal deferred & reflection shaders
            // additional pcf filters for spot/point lights
            deferredShading = assetBundle.LoadAsset<Shader>("InternalDeferredShading");
            deferredReflections = assetBundle.LoadAsset<Shader>("InternalDeferredReflections");

            // configuration window skin
            windowSkin = assetBundle.LoadAsset<GUISkin>("GUISkin");

            // confirm materials are loaded
            #region Errors
            if (null == skinMaterial || null == overlayMaterial)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Skin Material");
            }

            if (null == deferredTransmissionBlit || null == deferredBlurredNormals)
            {
                Console.WriteLine("#### HSSSS: Failed to Load PostFX Shaders");
            }

            if (null == deferredShading || null == deferredReflections)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Deferred Internal Shaders");
            }

            if (null == windowSkin)
            {
                Console.WriteLine("#### HSSSS: Failed to Load UI Skin");
            }
            #endregion

            // materials for additional replacement
            if (fixAlphaShadow)
            {
                eyeBrowMaterial = assetBundle.LoadAsset<Material>("Overlay");
                eyeLashMaterial = assetBundle.LoadAsset<Material>("Overlay");
                eyeOverlayMaterial = assetBundle.LoadAsset<Material>("OverlayForward");
                eyeScleraMaterial = assetBundle.LoadAsset<Material>("Standard");

                // dedicated pom eye shader
                if (useEyePOMShader)
                {
                    eyeCorneaMaterial = assetBundle.LoadAsset<Material>("Eye");
                }

                // ordinary overlay eye shader
                else
                {
                    eyeCorneaMaterial = assetBundle.LoadAsset<Material>("Overlay");
                }

                // confirm materials are loaded
                #region Errors
                if (null == eyeBrowMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eyebrow Material");
                }

                if (null == eyeLashMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eyelash Material");
                }

                if (null == eyeCorneaMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eyepuil Material");
                }

                if (null == eyeScleraMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eyewhite Material");
                }

                if (null == eyeOverlayMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eye Overlay Material");
                }
                #endregion
            }
        }

        private void ForwardAssetLoader()
        {
            // forward skin materials
            skinMaterial = assetBundle.LoadAsset<Material>("SkinForward");
            milkMaterial = assetBundle.LoadAsset<Material>("OverlayForward");
            overlayMaterial = assetBundle.LoadAsset<Material>("OverlayForward");

            #region Errors
            if (null == skinMaterial || null == overlayMaterial)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Skin Material");
            }
            #endregion

            // materials for additional replacement
            if (fixAlphaShadow)
            {
                eyeBrowMaterial = assetBundle.LoadAsset<Material>("OverlayForward");
                eyeLashMaterial = assetBundle.LoadAsset<Material>("OverlayForward");
                eyeOverlayMaterial = assetBundle.LoadAsset<Material>("OverlayForward");
                eyeScleraMaterial = assetBundle.LoadAsset<Material>("Standard");

                // dedicated pom eye shader
                if (useEyePOMShader)
                {
                    eyeCorneaMaterial = assetBundle.LoadAsset<Material>("Eye");
                }

                // ordinary overlay eye shader
                else
                {
                    eyeCorneaMaterial = assetBundle.LoadAsset<Material>("Overlay");
                }

                // confirm materials are loaded
                #region Errors
                if (null == eyeBrowMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eyebrow Material");
                }

                if (null == eyeLashMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eyelash Material");
                }

                if (null == eyeCorneaMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eyepuil Material");
                }

                if (null == eyeScleraMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eyewhite Material");
                }

                if (null == eyeOverlayMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eye Overlay Material");
                }
                #endregion
            }
        }

        private void InternalShaderReplacer()
        {
            GraphicsSettings.SetShaderMode(BuiltinShaderType.DeferredShading, BuiltinShaderMode.UseCustom);
            GraphicsSettings.SetShaderMode(BuiltinShaderType.DeferredReflections, BuiltinShaderMode.UseCustom);
            GraphicsSettings.SetCustomShader(BuiltinShaderType.DeferredShading, deferredShading);
            GraphicsSettings.SetCustomShader(BuiltinShaderType.DeferredReflections, deferredReflections);

            if (GraphicsSettings.GetCustomShader(BuiltinShaderType.DeferredShading) != deferredShading)
            {
                Console.WriteLine("#### HSSSS: Failed to Replace Internal Deferred Shader");
            }

            if (GraphicsSettings.GetCustomShader(BuiltinShaderType.DeferredReflections) != deferredReflections)
            {
                Console.WriteLine("#### HSSSS: Failed to Replace Internal Reflection Shader");
            }
        }

        private void PostFxInitializer()
        {
            GameObject mainCamera = GameObject.Find("StudioScene/Camera/Main Camera");

            if (null != mainCamera)
            {
                if (SSS == null)
                {
                    SSS = mainCamera.gameObject.AddComponent<DeferredRenderer>();

                    if (null == SSS)
                    {
                        Console.WriteLine("#### HSSSS: Failed to Initialize Post FX");
                    }
                }
            }
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

        internal static void UpdateShadowConfig()
        {
            Shader.DisableKeyword("_PCF_TAPS_8");
            Shader.DisableKeyword("_PCF_TAPS_16");
            Shader.DisableKeyword("_PCF_TAPS_32");
            Shader.DisableKeyword("_PCF_TAPS_64");
            Shader.DisableKeyword("_DIR_PCF_ON");
            Shader.DisableKeyword("_PCSS_ON");

            switch (shadowSettings.pcfState)
            {
                case PCFState.disable:
                    Shader.DisableKeyword("_DIR_PCF_ON");
                    Shader.DisableKeyword("_PCSS_ON");
                    shadowSettings.dirPcfEnabled = false;
                    shadowSettings.pcssEnabled = false;
                    break;

                case PCFState.poisson8x:
                    Shader.EnableKeyword("_PCF_TAPS_8");
                    break;

                case PCFState.poisson16x:
                    Shader.EnableKeyword("_PCF_TAPS_16");
                    break;

                case PCFState.poisson32x:
                    Shader.EnableKeyword("_PCF_TAPS_32");
                    break;

                case PCFState.poisson64x:
                    Shader.EnableKeyword("_PCF_TAPS_64");
                    break;
            }

            if (shadowSettings.dirPcfEnabled)
            {
                Shader.EnableKeyword("_DIR_PCF_ON");
            }

            if (shadowSettings.pcssEnabled)
            {
                Shader.EnableKeyword("_PCSS_ON");
            }

            Shader.SetGlobalVector("_DirLightPenumbra", shadowSettings.dirLightPenumbra);
            Shader.SetGlobalVector("_SpotLightPenumbra", shadowSettings.spotLightPenumbra);
            Shader.SetGlobalVector("_PointLightPenumbra", shadowSettings.pointLightPenumbra);
        }

        internal static void UpdateMiscConfig()
        {
            Shader.DisableKeyword("_WET_SPECGLOSS");

            if (skinSettings.useWetSpecGloss)
            {
                Shader.EnableKeyword("_WET_SPECGLOSS");
            }
        }

        internal static bool LoadExternalConfig()
        {
            try
            {
                XmlDocument config = new XmlDocument();
                config.Load(configPath);
                LoadConfig(config.LastChild);

                SSS.ImportSettings();
                SSS.ForceRefresh();

                return true;
            }
            
            catch
            {
                return false;
            }
        }

        internal static bool SaveExternalConfig()
        {
            try
            {
                XmlWriterSettings settings = new XmlWriterSettings() { Indent = true };
                XmlWriter writer = XmlWriter.Create(configPath, settings);
                writer.WriteStartElement("HSSSS");
                SaveConfig(writer);
                writer.WriteEndElement();
                writer.Close();

                return true;
            }

            catch
            {
                return false;
            }
        }

        private static void SaveConfig(XmlWriter writer)
        {
            writer.WriteAttributeString("version", pluginVersion);
            // skin scattering
            writer.WriteStartElement("SkinScattering");
            {
                writer.WriteAttributeString("Enabled", XmlConvert.ToString(skinSettings.sssEnabled));
                // scattering weight
                writer.WriteElementString("Weight", XmlConvert.ToString(skinSettings.sssWeight));
                // scattering profile
                writer.WriteElementString("BRDF", Convert.ToString(skinSettings.lutProfile));

                // diffusion brdf control
                writer.WriteStartElement("Diffusion");
                {
                    writer.WriteElementString("Scale", XmlConvert.ToString(skinSettings.skinLutScale));
                    writer.WriteElementString("Bias", XmlConvert.ToString(skinSettings.skinLutBias));
                }
                writer.WriteEndElement();

                // shadow brdf control
                writer.WriteStartElement("Shadow");
                {
                    writer.WriteElementString("Scale", XmlConvert.ToString(skinSettings.shadowLutScale));
                    writer.WriteElementString("Bias", XmlConvert.ToString(skinSettings.shadowLutBias));
                }
                writer.WriteEndElement();

                // normal blur
                writer.WriteStartElement("NormalBlur");
                {
                    writer.WriteElementString("Weight", XmlConvert.ToString(skinSettings.normalBlurWeight));
                    writer.WriteElementString("Radius", XmlConvert.ToString(skinSettings.normalBlurRadius));
                    writer.WriteElementString("CorrectionDepth", XmlConvert.ToString(skinSettings.normalBlurDepthRange));
                    writer.WriteElementString("Iterations", XmlConvert.ToString(skinSettings.normalBlurIter));
                }
                writer.WriteEndElement();

                // ao bleeding
                writer.WriteStartElement("AOBleeding");
                {
                    writer.WriteElementString("Red", XmlConvert.ToString(skinSettings.colorBleedWeights.x));
                    writer.WriteElementString("Green", XmlConvert.ToString(skinSettings.colorBleedWeights.y));
                    writer.WriteElementString("Blue", XmlConvert.ToString(skinSettings.colorBleedWeights.z));
                }
                writer.WriteEndElement();

                // light absorption
                writer.WriteStartElement("Absorption");
                {
                    writer.WriteElementString("Red", XmlConvert.ToString(skinSettings.transAbsorption.x));
                    writer.WriteElementString("Green", XmlConvert.ToString(skinSettings.transAbsorption.y));
                    writer.WriteElementString("Blue", XmlConvert.ToString(skinSettings.transAbsorption.z));
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            // transmission
            writer.WriteStartElement("Transmission");
            {
                writer.WriteAttributeString("Enabled", XmlConvert.ToString(skinSettings.transEnabled));
                // baked thickness
                writer.WriteElementString("BakedThickness", XmlConvert.ToString(skinSettings.bakedThickness));
                // transmission weight
                writer.WriteElementString("Weight", XmlConvert.ToString(skinSettings.transWeight));
                // normal distortion
                writer.WriteElementString("NormalDistortion", XmlConvert.ToString(skinSettings.transDistortion));
                // on-the-fly blur radius
                writer.WriteElementString("BlurRadius", XmlConvert.ToString(skinSettings.transBlurRadius));
                // shadow weight
                writer.WriteElementString("ShadowWeight", XmlConvert.ToString(skinSettings.transShadowWeight));
                // falloff
                writer.WriteElementString("FallOff", XmlConvert.ToString(skinSettings.transFalloff));
            }
            writer.WriteEndElement();
            // shadow
            writer.WriteStartElement("SoftShadow");
            {
                // pcf state
                writer.WriteAttributeString("State", Convert.ToString(shadowSettings.pcfState));
                // soft shadow for directional lights
                writer.WriteElementString("DirPCF", XmlConvert.ToString(shadowSettings.dirPcfEnabled));
                // pcss soft shadow
                writer.WriteElementString("PCSS", XmlConvert.ToString(shadowSettings.pcssEnabled));
                // directional light
                writer.WriteStartElement("Directional");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(shadowSettings.dirLightPenumbra.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(shadowSettings.dirLightPenumbra.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(shadowSettings.dirLightPenumbra.z));
                }
                writer.WriteEndElement();
                // spot light
                writer.WriteStartElement("Spot");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(shadowSettings.spotLightPenumbra.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(shadowSettings.spotLightPenumbra.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(shadowSettings.spotLightPenumbra.z));
                }
                writer.WriteEndElement();
                // spot light
                writer.WriteStartElement("Point");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(shadowSettings.pointLightPenumbra.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(shadowSettings.pointLightPenumbra.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(shadowSettings.pointLightPenumbra.z));
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            // miscellaneous
            writer.WriteStartElement("Miscellaneous");
            {
                writer.WriteElementString("WetSpecGloss", XmlConvert.ToString(skinSettings.useWetSpecGloss));
            }
            writer.WriteEndElement();
        }

        private static void LoadConfig(XmlNode node)
        {
            foreach (XmlNode child0 in node.ChildNodes)
            {
                switch (child0.Name)
                {
                    // skin scattering
                    case "SkinScattering":
                        // enabled?
                        skinSettings.sssEnabled = XmlConvert.ToBoolean(child0.Attributes["Enabled"].Value);

                        foreach (XmlNode child1 in child0.ChildNodes)
                        {
                            switch (child1.Name)
                            {
                                // scattering weight
                                case "Weight":
                                    skinSettings.sssWeight = XmlConvert.ToSingle(child1.InnerText);
                                    break;
                                // pre-integrated brdf
                                case "BRDF":
                                    skinSettings.lutProfile = (LUTProfile)Enum.Parse(typeof(LUTProfile), child1.InnerText);
                                    break;
                                // skin lookup
                                case "Diffusion":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Scale":
                                                skinSettings.skinLutScale = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Bias":
                                                skinSettings.skinLutBias = XmlConvert.ToSingle(child2.InnerText);
                                                break;
                                        }
                                    }
                                    break;
                                // shadow lookup
                                case "Shadow":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Scale":
                                                skinSettings.shadowLutScale = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Bias":
                                                skinSettings.shadowLutBias = XmlConvert.ToSingle(child2.InnerText);
                                                break;
                                        }
                                    }
                                    break;
                                // screen-space normal blur
                                case "NormalBlur":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Weight":
                                                skinSettings.normalBlurWeight = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Radius":
                                                skinSettings.normalBlurRadius = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "CorrectionDepth":
                                                skinSettings.normalBlurDepthRange = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Iterations":
                                                skinSettings.normalBlurIter = XmlConvert.ToInt32(child2.InnerText);
                                                break;
                                        }
                                    }
                                    break;
                                // ao color bleeding
                                case "AOBleeding":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Red":
                                                skinSettings.colorBleedWeights.x = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Green":
                                                skinSettings.colorBleedWeights.y = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Blue":
                                                skinSettings.colorBleedWeights.z = XmlConvert.ToSingle(child2.InnerText);
                                                break;
                                        }
                                    }
                                    break;
                                // skin transmission absorption
                                case "Absorption":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Red":
                                                skinSettings.transAbsorption.x = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Green":
                                                skinSettings.transAbsorption.y = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Blue":
                                                skinSettings.transAbsorption.z = XmlConvert.ToSingle(child2.InnerText);
                                                break;
                                        }
                                    }
                                    break;

                            }
                        }
                        break;

                    case "Transmission":
                        // enabled?
                        skinSettings.transEnabled = XmlConvert.ToBoolean(child0.Attributes["Enabled"].Value);

                        foreach (XmlNode child1 in child0.ChildNodes)
                        {
                            switch (child1.Name)
                            {
                                case "BakedThickness":
                                    skinSettings.bakedThickness = XmlConvert.ToBoolean(child1.InnerText);
                                    break;

                                case "Weight":
                                    skinSettings.transWeight = XmlConvert.ToSingle(child1.InnerText);
                                    break;

                                case "NormalDistortion":
                                    skinSettings.transDistortion = XmlConvert.ToSingle(child1.InnerText);
                                    break;

                                case "BlurRadius":
                                    skinSettings.transBlurRadius = XmlConvert.ToSingle(child1.InnerText);
                                    break;

                                case "ShadowWeight":
                                    skinSettings.transShadowWeight = XmlConvert.ToSingle(child1.InnerText);
                                    break;

                                case "Falloff":
                                    skinSettings.transFalloff = XmlConvert.ToSingle(child1.InnerText);
                                    break;
                            }
                        }
                        break;

                    case "SoftShadow":
                        // pcf kernel size
                        shadowSettings.pcfState = (PCFState)Enum.Parse(typeof(PCFState), child0.Attributes["State"].Value);
                        // soft shadow for directional lights
                        foreach (XmlNode child1 in child0.ChildNodes)
                        {
                            switch (child1.Name)
                            {
                                case "DirPCF":
                                    shadowSettings.dirPcfEnabled = XmlConvert.ToBoolean(child1.InnerText);
                                    break;

                                case "PCSS":
                                    shadowSettings.pcssEnabled = XmlConvert.ToBoolean(child1.InnerText);
                                    break;

                                case "Directional":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "SearchRadius":
                                                shadowSettings.dirLightPenumbra.x = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "LightRadius":
                                                shadowSettings.dirLightPenumbra.y = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "MinPenumbra":
                                                shadowSettings.dirLightPenumbra.z = XmlConvert.ToSingle(child2.InnerText);
                                                break;
                                        }
                                    }
                                    break;

                                case "Spot":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "SearchRadius":
                                                shadowSettings.spotLightPenumbra.x = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "LightRadius":
                                                shadowSettings.spotLightPenumbra.y = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "MinPenumbra":
                                                shadowSettings.spotLightPenumbra.z = XmlConvert.ToSingle(child2.InnerText);
                                                break;
                                        }
                                    }
                                    break;

                                case "Point":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "SearchRadius":
                                                shadowSettings.pointLightPenumbra.x = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "LightRadius":
                                                shadowSettings.pointLightPenumbra.y = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "MinPenumbra":
                                                shadowSettings.pointLightPenumbra.z = XmlConvert.ToSingle(child2.InnerText);
                                                break;
                                        }
                                    }
                                    break;
                            }
                        }
                        break;

                    case "Miscellaneous":
                        foreach (XmlNode child1 in child0.ChildNodes)
                        {
                            switch (child1.Name)
                            {
                                case "WetSpecGloss":
                                    skinSettings.useWetSpecGloss = XmlConvert.ToBoolean(child1.InnerText);
                                    break;
                            }
                        }
                        break;
                }
            }
        }
        #endregion

        #region Harmony Patches
        private static void SpotLightPatcher(OCILight __instance)
        {
            if (LightType.Spot == __instance.lightType)
            {
                if (null == __instance.light.cookie)
                {
                    __instance.light.cookie = spotCookie;
                }
            }
        }

        private static void ShadowMapPatcher(OCILight __instance)
        {
            if (__instance.light != null)
            {
                if (__instance.light.gameObject.GetComponent<ShadowMapDispatcher>() == null)
                {
                    __instance.light.gameObject.AddComponent<ShadowMapDispatcher>();
                }
            }
        }

        private class SkinReplacer
        {
            private static readonly Dictionary<string, string> colorProps = new Dictionary<string, string>()
            {
                { "_Color", "_Color" },
                { "_SpecColor", "_SpecColor"},
                { "_EmissionColor", "_EmissionColor" },
            };

            private static readonly Dictionary<string, string> floatProps = new Dictionary<string, string>()
            {
                { "_Metallic", "_Metallic" },
                { "_Smoothness", "_Smoothness" },
                { "_Glossiness", "_Smoothness" },
                { "_OcclusionStrength", "_OcclusionStrength" },
                { "_BumpScale", "_BumpScale" },
                { "_NormalStrength", "_BumpScale" },
                { "_BlendNormalMapScale", "_BlendNormalMapScale" },
                { "_DetailNormalMapScale", "_DetailNormalMapScale" },
            };

            private static readonly Dictionary<string, string> textureProps = new Dictionary<string, string>()
            {
                { "_MainTex", "_MainTex" },
                { "_SpecGlossMap", "_SpecGlossMap" },
                { "_OcclusionMap", "_OcclusionMap" },
                { "_BumpMap", "_BumpMap" },
                { "_NormalMap", "_BumpMap" },
                { "_BlendNormalMap", "_BlendNormalMap" },
                { "_DetailNormalMap", "_DetailNormalMap" }
            };

            private static void ObjectParser(Material mat, CharReference.TagObjKey key)
            {
                switch (key)
                {
                    case CharReference.TagObjKey.ObjUnderHair:
                        ShaderReplacer(overlayMaterial, mat);
                        mat.EnableKeyword("_METALLIC_OFF");
                        mat.renderQueue = 2001;
                        break;

                    case CharReference.TagObjKey.ObjEyelashes:
                        ShaderReplacer(eyeLashMaterial, mat);
                        mat.EnableKeyword("_METALLIC_OFF");
                        mat.renderQueue = 2001;
                        break;

                    case CharReference.TagObjKey.ObjEyebrow:
                        ShaderReplacer(eyeBrowMaterial, mat);
                        mat.EnableKeyword("_METALLIC_OFF");
                        mat.renderQueue = 2002;
                        break;

                    case CharReference.TagObjKey.ObjEyeHi:
                        ShaderReplacer(eyeOverlayMaterial, mat);
                        mat.EnableKeyword("_METALLIC_OFF");
                        mat.renderQueue = 2003;
                        break;

                    case CharReference.TagObjKey.ObjEyeW:
                        ShaderReplacer(eyeScleraMaterial, mat);
                        mat.EnableKeyword("_METALLIC_OFF");
                        break;

                    case CharReference.TagObjKey.ObjEyeL:
                        ShaderReplacer(eyeCorneaMaterial, mat);
                        mat.renderQueue = 2001;
                        if (!useEyePOMShader)
                        {
                            mat.EnableKeyword("_METALLIC_OFF");
                        }
                        break;

                    case CharReference.TagObjKey.ObjEyeR:
                        ShaderReplacer(eyeCorneaMaterial, mat);
                        mat.renderQueue = 2001;
                        if (!useEyePOMShader)
                        {
                            mat.EnableKeyword("_METALLIC_OFF");
                        }
                        break;

                    case CharReference.TagObjKey.ObjNip:
                        ShaderReplacer(overlayMaterial, mat);
                        mat.EnableKeyword("_METALLIC_OFF");
                        mat.renderQueue = 2001;
                        break;

                    default:
                        break;
                }
            }

            private static void ShaderReplacer(Material sourceMaterial, Material targetMaterial)
            {
                Material cacheMat = new Material(source: targetMaterial);

                targetMaterial.shader = sourceMaterial.shader;
                targetMaterial.CopyPropertiesFromMaterial(sourceMaterial);

                foreach (KeyValuePair<string, string> entry in textureProps)
                {
                    if (cacheMat.HasProperty(entry.Key))
                    {
                        targetMaterial.SetTexture(entry.Value, cacheMat.GetTexture(entry.Key));
                        targetMaterial.SetTextureScale(entry.Value, cacheMat.GetTextureScale(entry.Key));
                        targetMaterial.SetTextureOffset(entry.Value, cacheMat.GetTextureOffset(entry.Key));
                    }
                }

                foreach (KeyValuePair<string, string> entry in colorProps)
                {
                    if (cacheMat.HasProperty(entry.Key))
                    {
                        targetMaterial.SetColor(entry.Value, cacheMat.GetColor(entry.Key));
                    }
                }

                foreach (KeyValuePair<string, string> entry in floatProps)
                {
                    if (cacheMat.HasProperty(entry.Key))
                    {
                        targetMaterial.SetFloat(entry.Value, cacheMat.GetFloat(entry.Key));
                    }
                }
            }

            public static void BodyReplacer(CharInfo ___chaInfo)
            {
                Material bodyMat = ___chaInfo.chaBody.customMatBody;

                if (bodyMat != null)
                {
                    if (WillReplaceShader(bodyMat.shader))
                    {
                        ShaderReplacer(skinMaterial, bodyMat);

                        if (___chaInfo.Sex == 0)
                        {
                            bodyMat.SetTexture("_Thickness", maleBodyThickness);
                        }

                        else if (___chaInfo.Sex == 1)
                        {
                            bodyMat.SetTexture("_Thickness", femaleBodyThickness);
                        }
                        
                        Console.WriteLine("#### HSSSS Replaced " + bodyMat);
                    }
                }
            }

            public static void FaceReplacer(CharInfo ___chaInfo)
            {
                Material faceMat = ___chaInfo.chaBody.customMatFace;

                if (faceMat != null)
                {
                    if (WillReplaceShader(faceMat.shader))
                    {
                        ShaderReplacer(skinMaterial, faceMat);

                        if (___chaInfo.Sex == 0)
                        {
                            faceMat.SetTexture("_Thickness", maleHeadThickness);
                        }

                        else if (___chaInfo.Sex == 1)
                        {
                            faceMat.SetTexture("_Thickness", femaleHeadThickness);
                        }

                        Console.WriteLine("#### HSSSS Replaced " + faceMat);
                    }
                }
            }

            public static void CommonPartsReplacer(CharInfo ___chaInfo, CharReference.TagObjKey key)
            {
                if (fixAlphaShadow)
                {
                    foreach (GameObject obj in ___chaInfo.GetTagInfo(key))
                    {
                        if (obj != null)
                        {
                            foreach (Material mat in obj.GetComponent<Renderer>().materials)
                            {
                                if (mat != null)
                                {
                                    if (WillReplaceShader(mat.shader))
                                    {
                                        ObjectParser(mat, key);
                                        Console.WriteLine("#### HSSSS Replaced " + mat.name);
                                    }
                                }
                            }

                            // turn on receive shadows if disabled
                            if (!obj.GetComponent<Renderer>().receiveShadows)
                            {
                                obj.GetComponent<Renderer>().receiveShadows = true;
                            }
                        }
                    }
                }
            }

            public static void ScleraReplacer(CharInfo ___chaInfo)
            {
                if (fixAlphaShadow)
                {
                    CharReference.TagObjKey key = CharReference.TagObjKey.ObjEyeW;

                    foreach (GameObject obj in ___chaInfo.GetTagInfo(key))
                    {
                        if (obj != null)
                        {
                            foreach (Material mat in obj.GetComponent<Renderer>().materials)
                            {
                                if (mat != null)
                                {
                                    if (WillReplaceShader(mat.shader))
                                    {
                                        ObjectParser(mat, key);
                                        Console.WriteLine("#### HSSSS Replaced " + mat.name);
                                    }
                                }
                            }

                            // turn on receive shadows if disabled
                            if (!obj.GetComponent<Renderer>().receiveShadows)
                            {
                                obj.GetComponent<Renderer>().receiveShadows = true;
                            }
                        }
                    }
                }
            }

            public static void JuicesReplacer(Manager.Character __instance)
            {
                Dictionary<string, Material> juices = __instance.dictSiruMaterial;

                foreach (KeyValuePair<string, Material> entry in juices)
                {
                    Material juiceMat = entry.Value;

                    if (juiceMat != null)
                    {
                        if (WillReplaceShader(juiceMat.shader))
                        {
                            ShaderReplacer(milkMaterial, juiceMat);
                            juiceMat.SetFloat("_Metallic", 0.65f);
                            juiceMat.renderQueue = 2002;
                            Console.WriteLine("#### HSSSS Replaced " + juiceMat.name);
                        }
                    }
                }
            }

            public static void MiscReplacer(CharFemaleBody __instance)
            {
                // face blush
                if (null != __instance.matHohoAka)
                {
                    if (WillReplaceShader(__instance.matHohoAka.shader))
                    {
                        ShaderReplacer(overlayMaterial, __instance.matHohoAka);
                        __instance.matHohoAka.EnableKeyword("_METALLIC_OFF");
                        __instance.matHohoAka.renderQueue = 2001;
                        Console.WriteLine("#### HSSSS Replaced " + __instance.matHohoAka.name);
                    }
                }

                List<GameObject> faceObjs = __instance.chaInfo.GetTagInfo(CharReference.TagObjKey.ObjSkinFace);

                if (0 != faceObjs.Count)
                {
                    Renderer renderer = faceObjs[0].GetComponent<Renderer>();

                    if (1 < renderer.materials.Length)
                    {
                        Material blushMat = renderer.materials[1];

                        if (null != blushMat)
                        {
                            if (WillReplaceShader(blushMat.shader))
                            {
                                ShaderReplacer(overlayMaterial, blushMat);
                                blushMat.EnableKeyword("_METALLIC_OFF");
                                blushMat.renderQueue = 2001;
                                Console.WriteLine("#### HSSSS Replaced " + blushMat.name);
                            }
                        }
                    }
                }

                if (fixAlphaShadow)
                {
                    GameObject objHead = __instance.objHead;

                    if (objHead != null)
                    {
                        // eye occlusion
                        GameObject objShade = null;

                        if (null != objHead.transform.Find("cf_N_head/cf_O_eyekage"))
                        {
                            objShade = objHead.transform.Find("cf_N_head/cf_O_eyekage").gameObject;
                        }

                        else if (null != objHead.transform.Find("cf_N_head/cf_O_eyekage1"))
                        {
                            objShade = objHead.transform.Find("cf_N_head/cf_O_eyekage1").gameObject;
                        }

                        if (null != objShade)
                        {
                            Renderer renderer = objShade.GetComponent<Renderer>();
                            Material matShade = renderer.sharedMaterial;

                            if (!renderer.receiveShadows)
                            {
                                renderer.receiveShadows = true;
                            }

                            if (null != matShade)
                            {
                                if (WillReplaceShader(matShade.shader))
                                {
                                    ShaderReplacer(eyeOverlayMaterial, matShade);
                                    matShade.EnableKeyword("_METALLIC_OFF");
                                    matShade.renderQueue = 2002;
                                    Console.WriteLine("#### HSSSS Replaced " + matShade.name);
                                }
                            }
                        }

                        // tears
                        for (int i = 1; i < 4; i++)
                        {
                            GameObject objTears = objHead.transform.Find("cf_N_head/N_namida/cf_O_namida" + i.ToString("00")).gameObject;

                            if (null != objTears)
                            {
                                Renderer renderer = objTears.GetComponent<Renderer>();
                                Material matTears = renderer.sharedMaterial;

                                if (!renderer.receiveShadows)
                                {
                                    renderer.receiveShadows = true;
                                }

                                if (null != matTears)
                                {
                                    if (WillReplaceShader(matTears.shader))
                                    {
                                        ShaderReplacer(eyeOverlayMaterial, matTears);
                                        matTears.SetFloat("_Metallic", 0.80f);
                                        matTears.renderQueue = 2004;
                                        Console.WriteLine("#### HSSSS Replaced " + matTears.name);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            public static bool WillReplaceShader(Shader shader)
            {
                return shader.name.Contains("Shader Forge/") || shader.name.Contains("HSStandard/") || shader.name.Contains("Unlit/") || shader.name.Equals("Standard");
            }
        }
        #endregion
    }

    public class ConfigWindow : MonoBehaviour
    {
        internal static int uiScale;
        private int singleSpace;
        private int doubleSpace;
        private int tetraSpace;
        private int octaSpace;

        private Vector2 windowSize;
        private static Vector2 windowPosition;

        private Rect configWindow;
        private SkinSettings skinSettings;
        private ShadowSettings shadowSettings;

        private enum TabState
        {
            skinScattering,
            skinTransmission,
            lightShadow,
            miscellaneous
        };

        private TabState tabState;

        private readonly string[] tabLabels = new string[] { "Scattering", "Transmission", "Lights & Shadows", "Miscellaneous" };
        private readonly string[] lutLabels = new string[] { "Penner (Default)", "FaceWorks Type 1", "FaceWorks Type 2" };
        private readonly string[] pcfLabels = new string[] { "Off", "8x", "16x", "32x", "64x" };
        
        public void Awake()
        {
            windowSize = new Vector2(192.0f * uiScale, 192.0f);

            singleSpace = uiScale;
            doubleSpace = uiScale * 2;
            tetraSpace = uiScale * 4;
            octaSpace = uiScale * 8;

            this.configWindow = new Rect(windowPosition, windowSize);
            this.tabState = TabState.skinScattering;
            this.skinSettings = HSSSS.skinSettings;
            this.shadowSettings = HSSSS.shadowSettings;
        }

        public void OnGUI()
        {
            GUI.skin = HSSSS.windowSkin;
            // button
            GUI.skin.button.margin.left = doubleSpace;
            GUI.skin.button.margin.right = doubleSpace;
            GUI.skin.button.fontSize = tetraSpace;
            // label
            GUI.skin.label.fixedHeight = octaSpace;
            GUI.skin.label.fontSize = tetraSpace;
            // text field
            GUI.skin.textField.margin.left = doubleSpace;
            GUI.skin.textField.margin.right = doubleSpace;
            GUI.skin.textField.fontSize = tetraSpace;
            // window
            GUI.skin.window.padding.top = octaSpace;
            GUI.skin.window.padding.left = tetraSpace;
            GUI.skin.window.padding.right = tetraSpace;
            GUI.skin.window.padding.bottom = tetraSpace;
            GUI.skin.window.fontSize = doubleSpace + tetraSpace;
            // slider
            GUI.skin.horizontalSlider.margin.left = doubleSpace;
            GUI.skin.horizontalSlider.margin.right = doubleSpace;
            GUI.skin.horizontalSlider.padding.top = singleSpace;
            GUI.skin.horizontalSlider.padding.left = singleSpace;
            GUI.skin.horizontalSlider.padding.right = singleSpace;
            GUI.skin.horizontalSlider.padding.bottom = singleSpace;
            GUI.skin.horizontalSlider.fontSize = tetraSpace;
            // slider thumb
            GUI.skin.horizontalSliderThumb.fixedWidth = tetraSpace;

            this.configWindow = GUILayout.Window(0, this.configWindow, this.WindowFunction, "HSSSS Configurations");
            Studio.Studio.Instance.cameraCtrl.enabled = !this.configWindow.Contains(Event.current.mousePosition);
        }

        private void WindowFunction(int WindowID)
        {
            GUILayout.Space(octaSpace);
            this.TabsControl();
            GUILayout.Space(tetraSpace);

            switch (this.tabState)
            {
                case TabState.skinScattering:
                    this.ScatteringSettings();
                    break;

                case TabState.skinTransmission:
                    this.TransmissionSettings();
                    break;

                case TabState.lightShadow:
                    this.LightShadowSettings();
                    break;

                case TabState.miscellaneous:
                    this.OtherSettings();
                    break;
            }

            GUILayout.Space(tetraSpace);

            // save and load
            GUILayout.Label("Save/Load Preset");
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            if (GUILayout.Button("Load Preset"))
            {
                if (HSSSS.LoadExternalConfig())
                {
                    this.skinSettings = HSSSS.skinSettings;
                    this.shadowSettings = HSSSS.shadowSettings;
                    Console.WriteLine("#### HSSSS: Loaded Configurations");
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Configuration");
                }

                this.UpdateWindowSize();
            }

            if (GUILayout.Button("Save Preset"))
            {
                HSSSS.skinSettings = this.skinSettings;
                HSSSS.shadowSettings = this.shadowSettings;

                if (HSSSS.SaveExternalConfig())
                {
                    Console.WriteLine("#### HSSSS: Saved Configurations");
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Failed to Save Configurations");
                }

                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(tetraSpace);

            // version
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Version " + HSSSS.pluginVersion);
            GUILayout.EndHorizontal();

            this.UpdateSettings();
            GUI.DragWindow();

            windowPosition = this.configWindow.position;
        }

        private void UpdateWindowSize()
        {
            this.configWindow.size = windowSize;
        }

        private void TabsControl()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            TabState tmpState = (TabState)GUILayout.Toolbar((int)this.tabState, tabLabels);

            if (this.tabState != tmpState)
            {
                this.tabState = tmpState;
                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();
        }

        private void ScatteringSettings()
        {
            GUILayout.Label("Skin Scattering Weight");
            skinSettings.sssWeight = this.SliderControls(skinSettings.sssWeight, 0.0f, 1.0f);

            GUILayout.Space(tetraSpace);

            // profiles
            GUILayout.Label("Pre-integrated BRDF");

            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            LUTProfile lutProfile = (LUTProfile)GUILayout.Toolbar((int)skinSettings.lutProfile, lutLabels);

            if (skinSettings.lutProfile != lutProfile)
            {
                skinSettings.lutProfile = lutProfile;
                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(tetraSpace);

            // skin diffusion brdf
            GUILayout.Label("Skin BRDF Lookup Scale");
            skinSettings.skinLutScale = this.SliderControls(skinSettings.skinLutScale, 0.0f, 1.0f);

            GUILayout.Label("Skin BRDF Lookup Bias");
            skinSettings.skinLutBias = this.SliderControls(skinSettings.skinLutBias, 0.0f, 1.0f);

            GUILayout.Space(tetraSpace);

            // shadow penumbra brdf
            if (skinSettings.lutProfile == LUTProfile.nvidia2)
            {
                GUILayout.Label("Shadow BRDF Lookup Scale");
                skinSettings.shadowLutScale = this.SliderControls(skinSettings.shadowLutScale, 0.0f, 1.0f);

                GUILayout.Label("Shadow BRDF Lookup Bias");
                skinSettings.shadowLutBias = this.SliderControls(skinSettings.shadowLutBias, 0.0f, 1.0f);
            }

            GUILayout.Space(tetraSpace);

            // normal blurs
            GUILayout.Label("Normal Blur Weight");
            skinSettings.normalBlurWeight = this.SliderControls(skinSettings.normalBlurWeight, 0.0f, 1.0f);

            GUILayout.Label("Normal Blur Radius");
            skinSettings.normalBlurRadius = this.SliderControls(skinSettings.normalBlurRadius, 0.0f, 1.0f);

            GUILayout.Label("Normal Blur Depth Range");
            skinSettings.normalBlurDepthRange = this.SliderControls(skinSettings.normalBlurDepthRange, 0.0f, 20.0f);

            GUILayout.Label("Normal Blur Iterations");
            skinSettings.normalBlurIter = this.SliderControls(skinSettings.normalBlurIter, 1, 10);

            GUILayout.Space(tetraSpace);

            // ambient occlusion
            GUILayout.Label("Ambient Occlusion Color Bleeding");
            skinSettings.colorBleedWeights = this.RGBControls(skinSettings.colorBleedWeights);
        }

        private void TransmissionSettings()
        {
            GUILayout.Label("Transmission Weight");
            skinSettings.transWeight = this.SliderControls(skinSettings.transWeight, 0.0f, 1.0f);

            GUILayout.Label("Transmission Distortion");
            skinSettings.transDistortion = this.SliderControls(skinSettings.transDistortion, 0.0f, 1.0f);

            GUILayout.Label("Transmission Shadow Weight");
            skinSettings.transShadowWeight = this.SliderControls(skinSettings.transShadowWeight, 0.0f, 1.0f);

            GUILayout.Label("Transmission Falloff");
            skinSettings.transFalloff = this.SliderControls(skinSettings.transFalloff, 1.0f, 20.0f);

            GUILayout.Label("Transmission Absorption");
            skinSettings.transAbsorption = this.RGBControls(skinSettings.transAbsorption);
        }

        private void LightShadowSettings()
        {
            // pcf iterations count
            GUILayout.Label("PCF Taps Count");
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            PCFState pcfState = (PCFState)GUILayout.Toolbar((int)this.shadowSettings.pcfState, pcfLabels);

            if (this.shadowSettings.pcfState != pcfState)
            {
                this.shadowSettings.pcfState = pcfState;
                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(tetraSpace);

            // pcf soft shadow for directional lights
            GUILayout.Label("Soft Shadows for Directional Lights");
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            bool dirPcfEnabled = GUILayout.Toolbar(Convert.ToUInt16(this.shadowSettings.dirPcfEnabled), new string[] { "Disable", "Enable" }) == 1;

            if (this.shadowSettings.dirPcfEnabled != dirPcfEnabled)
            {
                this.shadowSettings.dirPcfEnabled = dirPcfEnabled;
                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(tetraSpace);

            // pcss soft shadow toggle
            GUILayout.Label("PCSS Soft Shadows");
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            bool pcssEnabled = GUILayout.Toolbar(Convert.ToUInt16(this.shadowSettings.pcssEnabled), new string[] { "Disable", "Enable"} ) == 1;

            if (this.shadowSettings.pcssEnabled != pcssEnabled)
            {
                this.shadowSettings.pcssEnabled = pcssEnabled;
                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(tetraSpace);

            if (this.shadowSettings.pcfState != PCFState.disable)
            {
                // directional lights
                if (this.shadowSettings.dirPcfEnabled)
                {
                    if(this.shadowSettings.pcssEnabled)
                    {
                        GUILayout.Label("Blocker Search Radius (Directional Lights)");
                        this.shadowSettings.dirLightPenumbra.x = this.SliderControls(this.shadowSettings.dirLightPenumbra.x, 0.0f, 20.0f);
                        GUILayout.Label("Light Radius (Directional Lights)");
                        this.shadowSettings.dirLightPenumbra.y = this.SliderControls(this.shadowSettings.dirLightPenumbra.y, 0.0f, 20.0f);
                        GUILayout.Label("Minimum Penumbra (Directional Lights)");
                    }

                    else
                    {
                        GUILayout.Label("Penumbra Scale (Directional Lights)");
                    }

                    this.shadowSettings.dirLightPenumbra.z = this.SliderControls(this.shadowSettings.dirLightPenumbra.z, 0.0f, 20.0f);
                }

                GUILayout.Space(tetraSpace);

                // spot lights
                if (this.shadowSettings.pcssEnabled)
                {
                    GUILayout.Label("Blocker Search Radius (Spot Lights)");
                    this.shadowSettings.spotLightPenumbra.x = this.SliderControls(this.shadowSettings.spotLightPenumbra.x, 0.0f, 20.0f);
                    GUILayout.Label("Light Radius (Spot Lights)");
                    this.shadowSettings.spotLightPenumbra.y = this.SliderControls(this.shadowSettings.spotLightPenumbra.y, 0.0f, 20.0f);
                    GUILayout.Label("Minimum Penumbra (Spot Lights)");
                }

                else
                {
                    GUILayout.Label("Penumbra Scale (Spot Lights)");
                }

                this.shadowSettings.spotLightPenumbra.z = this.SliderControls(this.shadowSettings.spotLightPenumbra.z, 0.0f, 20.0f);

                GUILayout.Space(tetraSpace);

                // point lights
                if (this.shadowSettings.pcssEnabled)
                {
                    GUILayout.Label("Blocker Search Radius (Point Lights)");
                    this.shadowSettings.pointLightPenumbra.x = this.SliderControls(this.shadowSettings.pointLightPenumbra.x, 0.0f, 20.0f);
                    GUILayout.Label("Light Radius (Point Lights)");
                    this.shadowSettings.pointLightPenumbra.y = this.SliderControls(this.shadowSettings.pointLightPenumbra.y, 0.0f, 20.0f);
                    GUILayout.Label("Minimum Penumbra (Point Lights)");
                }

                else
                {
                    GUILayout.Label("Penumbra Scale (Point Lights)");
                }

                this.shadowSettings.pointLightPenumbra.z = this.SliderControls(this.shadowSettings.pointLightPenumbra.z, 0.0f, 20.0f);
            }

            GUILayout.Space(tetraSpace);
            GUILayout.Label($"<size={doubleSpace + tetraSpace}>Requires SoftShadow=1 option in modprefs.ini!</size>");
        }

        private void OtherSettings()
        {
            // wet specgloss
            GUILayout.Label("Wet Skin SpecGloss");
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            bool useWetSpecGloss = GUILayout.Toolbar(Convert.ToUInt16(this.skinSettings.useWetSpecGloss), new string[] { "Disable", "Enable" }) == 1;

            if (this.skinSettings.useWetSpecGloss != useWetSpecGloss)
            {
                this.skinSettings.useWetSpecGloss = useWetSpecGloss;

                if (this.skinSettings.useWetSpecGloss)
                {
                    Shader.EnableKeyword("_WET_SPECGLOSS");
                }

                else
                {
                    Shader.DisableKeyword("_WET_SPECGLOSS");
                }
            }

            GUILayout.EndHorizontal();
        }

        private float SliderControls(float sliderValue, float minValue, float maxValue)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            sliderValue = GUILayout.HorizontalSlider(sliderValue, minValue, maxValue);

            if (float.TryParse(GUILayout.TextField(sliderValue.ToString("0.00"), GUILayout.Width(2 * octaSpace)), out float fieldValue))
            {
                sliderValue = fieldValue;
            }

            GUILayout.EndHorizontal();

            return sliderValue;
        }

        private int SliderControls(int sliderValue, int minValue, int maxValue)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            sliderValue = (int) GUILayout.HorizontalSlider(sliderValue, minValue, maxValue);

            if (int.TryParse(GUILayout.TextField(sliderValue.ToString(), GUILayout.Width(2 * octaSpace)), out int fieldValue))
            {
                sliderValue = fieldValue;
            }

            GUILayout.EndHorizontal();

            return sliderValue;
        }

        private Vector3 RGBControls(Vector3 rgbValue)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            GUILayout.Label("Red");
            
            if (float.TryParse(GUILayout.TextField(rgbValue.x.ToString("0.00"), GUILayout.Width(3 * octaSpace)), out float r))
            {
                rgbValue.x = r;
            }

            GUILayout.Label("Green");

            if (float.TryParse(GUILayout.TextField(rgbValue.y.ToString("0.00"), GUILayout.Width(3 * octaSpace)), out float g))
            {
                rgbValue.y = g;
            }

            GUILayout.Label("Blue");

            if (float.TryParse(GUILayout.TextField(rgbValue.z.ToString("0.00"), GUILayout.Width(3 * octaSpace)), out float b))
            {
                rgbValue.z = b;
            }

            GUILayout.EndHorizontal();

            return rgbValue;
        }
        private void UpdateSettings()
        {
            HSSSS.skinSettings = this.skinSettings;
            HSSSS.shadowSettings = this.shadowSettings;
            HSSSS.UpdateShadowConfig();
            HSSSS.UpdateMiscConfig();

            HSSSS.SSS.ImportSettings();

            if (HSSSS.skinSettings.normalBlurIter != this.skinSettings.normalBlurIter)
            {
                HSSSS.SSS.ForceRefresh();
            }

            else
            {
                HSSSS.SSS.Refresh();
            }
        }
    }
}
