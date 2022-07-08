using System;
using System.IO;
using System.Xml.Linq;
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
        penner = 0,
        nvidia1 = 1,
        nvidia2 = 2
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

        public float transWeight;
        public float transShadowWeight;
        public float transDistortion;
        public float transFalloff;
    }

    public class HSSSS : IEnhancedPlugin
    {
        #region Plugin Info
        public string Name { get { return "HSSSS";  } }
        public string Version { get { return "1.0.1"; } }
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

            transWeight = 1.0f,
            transShadowWeight = 0.5f,
            transDistortion = 0.5f,
            transFalloff = 2.0f
        };

        internal static Shader deferredTransmissionBlit;
        internal static Shader deferredBlurredNormals;

        internal static Texture2D defaultSkinLUT;
        internal static Texture2D faceWorksSkinLUT;
        internal static Texture2D faceWorksShadowLUT;
        internal static Texture2D skinJitter;

        // skin and body materials
        public static Material skinMaterial;
        public static Material overlayMaterial;
        public static Material eyeBrowMaterial;
        public static Material eyeLashMaterial;
        public static Material eyeAlphaMaterial;
        public static Material eyePupilMaterial;
        public static Material eyeWhiteMaterial;

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
        private static bool useWetSpecGloss;
        private static bool fixAlphaShadow;
        private static bool useEyePOMShader;
        private static bool useSoftShadow;
        private static bool useCustomThickness;

        private static string femaleBodyCustom;
        private static string femaleHeadCustom;
        private static string maleBodyCustom;
        private static string maleHeadCustom;

        private static KeyCode[] hotKey;

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
                        AccessTools.Method(typeof(OCILight), "Update"), null,
                        new HarmonyMethod(typeof(HSSSS), nameof(SpotLightPatcher))
                        );

                    if (useSoftShadow)
                    {
                        harmony.Patch(
                            AccessTools.Method(typeof(OCILight), nameof(OCILight.SetEnable)), null,
                            new HarmonyMethod(typeof(HSSSS), nameof(ShadowMapPatcher))
                            );

                        Shader.EnableKeyword("_PCF_TAPS_16");

                        Shader.SetGlobalVector("_DirLightPenumbra", new Vector3(1.0f, 1.0f, 1.0f));
                        Shader.SetGlobalVector("_SpotLightPenumbra", new Vector3(1.0f, 1.0f, 1.0f));
                        Shader.SetGlobalVector("_PointLightPenumbra", new Vector3(1.0f, 1.0f, 1.0f));
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

                        SSS.ImportSettings();

                        if (!LoadConfig())
                        {
                            Console.WriteLine("#### HSSSS: Could not load config.xml; writing a new one...");

                            if (SaveConfig())
                            {
                                Console.WriteLine("#### HSSSS: Successfully wrote a new configuration file");
                            }
                        }

                        else
                        {
                            SSS.Refresh();
                            Console.WriteLine("#### HSSSS: Successfully loaded config.xml");
                        }
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
                        this.windowObj = new GameObject("HSSSS.ConfigWindow", typeof(ConfigWindow));
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
            useWetSpecGloss = ModPrefs.GetBool("HSSSS", "WetSpecGloss", false, true);
            // additional replacement option for some transparent materials
            fixAlphaShadow = ModPrefs.GetBool("HSSSS", "FixShadow", false, true);
            // dedicated eye shader which supports pom/sss
            useEyePOMShader = ModPrefs.GetBool("HSSSS", "EyePOMShader", false, true);
            // extensive PCF soft shadows for spotlight/pointlight
            useSoftShadow = ModPrefs.GetBool("HSSSS", "SoftShadow", false, true);
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

            // materials for additional replacement
            if (fixAlphaShadow)
            {
                eyeBrowMaterial = assetBundle.LoadAsset<Material>("EyeBrow");
                eyeLashMaterial = assetBundle.LoadAsset<Material>("EyeLash");
                eyeAlphaMaterial = assetBundle.LoadAsset<Material>("EyeAlpha");
                eyeWhiteMaterial = assetBundle.LoadAsset<Material>("EyeWhite");

                if (useEyePOMShader)
                {
                    eyePupilMaterial = assetBundle.LoadAsset<Material>("EyePOM");
                }

                else
                {
                    eyePupilMaterial = assetBundle.LoadAsset<Material>("EyePupil");
                }

                #region Errors
                if (null == eyeBrowMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eyebrow Material");
                }

                if (null == eyeLashMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eyelash Material");
                }

                if (null == eyePupilMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eyepuil Material");
                }

                if (null == eyeWhiteMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eyewhite Material");
                }

                if (null == eyeAlphaMaterial)
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Eye Overlay Material");
                }
                #endregion
            }
        }

        private void DeferredAssetLoader()
        {
            // tesellation materials
            if (useTessellation)
            {
                skinMaterial = assetBundle.LoadAsset<Material>("SkinDeferredTessellation");
                overlayMaterial = assetBundle.LoadAsset<Material>("OverlayTessellation");
            }

            // non-tesellation materials
            else
            {
                skinMaterial = assetBundle.LoadAsset<Material>("SkinDeferred");
                overlayMaterial = assetBundle.LoadAsset<Material>("Overlay");
            }

            // post fx shaders
            deferredTransmissionBlit = assetBundle.LoadAsset<Shader>("TransmissionBlit");
            deferredBlurredNormals = assetBundle.LoadAsset<Shader>("BlurredNormals");

            // internal deferred & reflection shaders
            // additional pcf filters for spot/point lights
            deferredShading = assetBundle.LoadAsset<Shader>("InternalDeferredShading");
            deferredReflections = assetBundle.LoadAsset<Shader>("InternalDeferredReflections");

            // semi-pbr wetness option
            if (useWetSpecGloss)
            {
                skinMaterial.EnableKeyword("_WET_SPECGLOSS");
            }

            // configuration window skin
            windowSkin = assetBundle.LoadAsset<GUISkin>("GUISkin");

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
        }

        private void ForwardAssetLoader()
        {
            // forward skin materials
            skinMaterial = assetBundle.LoadAsset<Material>("SkinForward");
            overlayMaterial = assetBundle.LoadAsset<Material>("Overlay");

            // semi-pbr wetness option
            if (useWetSpecGloss)
            {
                skinMaterial.EnableKeyword("_WET_SPECGLOSS");
            }

            #region Errors
            if (null == skinMaterial || null == overlayMaterial)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Skin Material");
            }
            #endregion
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

        internal static bool LoadConfig()
        {
            try
            {
                XDocument config = XDocument.Load(configPath);
                
                XElement root = config.Root;
                XElement scattering = root.Element("SkinScattering");
                XElement transmission = root.Element("Transmission");
                
                skinSettings.sssEnabled = bool.Parse(scattering.Attribute("Enabled").Value);

                skinSettings.sssWeight = float.Parse(scattering.Element("Weight").Value);

                skinSettings.lutProfile = (LUTProfile)Enum.Parse(typeof(LUTProfile), scattering.Element("BRDF").Value);

                skinSettings.skinLutScale = float.Parse(scattering.Element("Diffusion").Element("Scale").Value);
                skinSettings.skinLutBias = float.Parse(scattering.Element("Diffusion").Element("Bias").Value);

                skinSettings.shadowLutScale = float.Parse(scattering.Element("Shadow").Element("Scale").Value);
                skinSettings.shadowLutBias = float.Parse(scattering.Element("Shadow").Element("Bias").Value);

                skinSettings.normalBlurWeight = float.Parse(scattering.Element("NormalBlur").Element("Weight").Value);
                skinSettings.normalBlurRadius = float.Parse(scattering.Element("NormalBlur").Element("Radius").Value);
                skinSettings.normalBlurDepthRange = float.Parse(scattering.Element("NormalBlur").Element("CorrectionDepth").Value);
                skinSettings.normalBlurIter = int.Parse(scattering.Element("NormalBlur").Element("Iterations").Value);

                skinSettings.colorBleedWeights.x = float.Parse(scattering.Element("AOBleeding").Element("Red").Value);
                skinSettings.colorBleedWeights.y = float.Parse(scattering.Element("AOBleeding").Element("Green").Value);
                skinSettings.colorBleedWeights.z = float.Parse(scattering.Element("AOBleeding").Element("Blue").Value);

                skinSettings.transAbsorption.x = float.Parse(scattering.Element("Absorption").Element("Red").Value);
                skinSettings.transAbsorption.y = float.Parse(scattering.Element("Absorption").Element("Green").Value);
                skinSettings.transAbsorption.z = float.Parse(scattering.Element("Absorption").Element("Blue").Value);
                
                skinSettings.transEnabled = bool.Parse(transmission.Attribute("Enabled").Value);
                skinSettings.transWeight = float.Parse(transmission.Element("Weight").Value);
                skinSettings.transDistortion = float.Parse(transmission.Element("NormalDistortion").Value);
                skinSettings.transShadowWeight = float.Parse(transmission.Element("ShadowWeight").Value);
                skinSettings.transFalloff = float.Parse(transmission.Element("Falloff").Value);

                SSS.ImportSettings();
                SSS.ForceRefresh();

                return true;
            }
            
            catch
            {
                return false;
            }
        }

        internal static bool SaveConfig()
        {
            SSS.ExportSettings();

            XDocument config = new XDocument();

            XElement root = new XElement(pluginName, new XAttribute("version", pluginVersion));
            XElement scattering = new XElement("SkinScattering",
                new XAttribute("Enabled", skinSettings.sssEnabled),
                new XElement("Weight", skinSettings.sssWeight),
                new XElement("BRDF", skinSettings.lutProfile),
                new XElement("Diffusion",
                    new XElement("Scale", skinSettings.skinLutScale),
                    new XElement("Bias", skinSettings.skinLutBias)
                ),
                new XElement("Shadow",
                    new XElement("Scale", skinSettings.shadowLutScale),
                    new XElement("Bias", skinSettings.shadowLutBias)
                ),
                new XElement("NormalBlur",
                    new XElement("Weight", skinSettings.normalBlurWeight),
                    new XElement("Radius", skinSettings.normalBlurRadius),
                    new XElement("CorrectionDepth", skinSettings.normalBlurDepthRange),
                    new XElement("Iterations", skinSettings.normalBlurIter)
                ),
                new XElement("AOBleeding",
                    new XElement("Red", skinSettings.colorBleedWeights.x),
                    new XElement("Green", skinSettings.colorBleedWeights.y),
                    new XElement("Blue", skinSettings.colorBleedWeights.z)
                    ),
                new XElement("Absorption",
                    new XElement("Red", skinSettings.transAbsorption.x),
                    new XElement("Green", skinSettings.transAbsorption.y),
                    new XElement("Blue", skinSettings.transAbsorption.z)
                    )
                );
            XElement transmission = new XElement("Transmission",
                new XAttribute("Enabled", skinSettings.transEnabled),
                new XElement("Weight", skinSettings.transWeight),
                new XElement("NormalDistortion", skinSettings.transDistortion),
                new XElement("ShadowWeight", skinSettings.transShadowWeight),
                new XElement("Falloff", skinSettings.transFalloff)
                );

            config.Add(root);
            root.Add(scattering);
            root.Add(transmission);

            try
            {
                config.Save(configPath);
                return true;
            }

            catch
            {
                return false;
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
                        ShaderReplacer(eyeAlphaMaterial, mat);
                        mat.EnableKeyword("_METALLIC_OFF");
                        mat.renderQueue = 2003;
                        break;

                    case CharReference.TagObjKey.ObjEyeW:
                        ShaderReplacer(eyeWhiteMaterial, mat);
                        mat.EnableKeyword("_METALLIC_OFF");
                        break;

                    case CharReference.TagObjKey.ObjEyeL:
                        ShaderReplacer(eyePupilMaterial, mat);
                        mat.renderQueue = 2001;
                        if (!useEyePOMShader)
                        {
                            mat.EnableKeyword("_METALLIC_OFF");
                        }
                        break;

                    case CharReference.TagObjKey.ObjEyeR:
                        ShaderReplacer(eyePupilMaterial, mat);
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
                            ShaderReplacer(overlayMaterial, juiceMat);
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
                                    ShaderReplacer(eyeAlphaMaterial, matShade);
                                    matShade.EnableKeyword("_METALLIC_OFF");
                                    matShade.renderQueue = 2002;
                                    Console.WriteLine("#### HSSSS Replaced " + matShade.name);
                                }
                            }
                        }

                        // dears
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
                                        ShaderReplacer(eyeAlphaMaterial, matTears);
                                        matTears.SetFloat("_Metallic", 0.80f);
                                        matTears.renderQueue = 2004;
                                        Console.WriteLine("#### HSSSS Replaced " + matTears.name);
                                    }
                                }
                            }
                        }

                        // disable sclera if POMshader enabled
                        if (useEyePOMShader)
                        {
                            Transform[] trfScelra =
                            {
                                objHead.transform.Find("cf_N_head/N_eyeL/cf_O_eyewhite_L"),
                                objHead.transform.Find("cf_N_head/N_eyeR/cf_O_eyewhite_R")
                            };

                            foreach (Transform trf in trfScelra)
                            {
                                if (trf != null)
                                {
                                    trf.gameObject.SetActive(false);
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
        private static Vector2 windowPosition = new Vector2(250.0f, 0.000f);
        private static Vector2 windowSize = new Vector2(768.0f, 128.0f);

        private static bool togglePCF = true;
        private static bool togglePCSS = false;
        private static bool toggleDirPCF = false;

        private static Vector3 dirPenumbra = new Vector3(1.0f, 1.0f, 1.0f);
        private static Vector3 spotPenumbra = new Vector3(1.0f, 1.0f, 1.0f);
        private static Vector3 pointPenumbra = new Vector3(1.0f, 1.0f, 1.0f);

        private Rect configWindow;
        private SkinSettings skinSettings;

        private enum UIState
        {
            skinScattering,
            skinTransmission,
            lightShadow,
            presets
        };

        private UIState state;
        
        public void Awake()
        {
            this.configWindow = new Rect(windowPosition, windowSize);
            this.state = UIState.skinScattering;
            this.skinSettings = HSSSS.skinSettings;
        }

        public void OnGUI()
        {
            GUI.skin = HSSSS.windowSkin;

            this.configWindow = GUILayout.Window(0, this.configWindow, this.WindowFunction, "HSSSS Configurations");
            Studio.Studio.Instance.cameraCtrl.enabled = !this.configWindow.Contains(Event.current.mousePosition);
        }

        private void WindowFunction(int WindowID)
        {
            GUILayout.Space(32.0f);

            this.TabsControl();

            GUILayout.Space(16.0f);

            switch (this.state)
            {
                case UIState.skinScattering:
                    this.ScatteringSettings();
                    break;

                case UIState.skinTransmission:
                    this.TransmissionSettings();
                    break;

                case UIState.lightShadow:
                    this.LightShadowSettings();
                    break;

                case UIState.presets:
                    this.PresetsControls();
                    break;
            }


            GUILayout.Space(16.0f);
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
            this.configWindow.size = new Vector2(768.0f, 256.0f);
        }

        private void TabsControl()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(32.0f));

            if (GUILayout.Button("Scattering"))
            {
                state = UIState.skinScattering;
                this.UpdateWindowSize();
            }

            if (GUILayout.Button("Transmission"))
            {
                state = UIState.skinTransmission;
                this.UpdateWindowSize();
            }

            if (GUILayout.Button("Lights & Shadows"))
            {
                state = UIState.lightShadow;
                this.UpdateWindowSize();
            }

            if (GUILayout.Button("Presets"))
            {
                state = UIState.presets;
                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();
        }

        private void ScatteringSettings()
        {
            GUILayout.Label("Skin Scattering Weight");
            skinSettings.sssWeight = this.SliderControls(skinSettings.sssWeight, 0.0f, 1.0f);

            GUILayout.Space(16.0f);

            // profiles
            GUILayout.Label("Pre-integrated BRDF");

            GUILayout.BeginHorizontal(GUILayout.Height(32.0f));

            if (GUILayout.Button("Penner (Default)"))
            {
                skinSettings.lutProfile = LUTProfile.penner;
                this.UpdateWindowSize();
            }

            if (GUILayout.Button("Faceworks Type 1"))
            {
                skinSettings.lutProfile = LUTProfile.nvidia1;
                this.UpdateWindowSize();
            }

            if (GUILayout.Button("Faceworks Type 2"))
            {
                skinSettings.lutProfile = LUTProfile.nvidia2;
                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(16.0f);

            // skin diffusion brdf
            GUILayout.Label("Skin BRDF Lookup Scale");
            skinSettings.skinLutScale = this.SliderControls(skinSettings.skinLutScale, 0.0f, 1.0f);

            GUILayout.Label("Skin BRDF Lookup Bias");
            skinSettings.skinLutBias = this.SliderControls(skinSettings.skinLutBias, 0.0f, 1.0f);

            GUILayout.Space(16.0f);

            // shadow penumbra brdf
            if (skinSettings.lutProfile == LUTProfile.nvidia2)
            {
                GUILayout.Label("Shadow BRDF Lookup Scale");
                skinSettings.shadowLutScale = this.SliderControls(skinSettings.shadowLutScale, 0.0f, 1.0f);

                GUILayout.Label("Shadow BRDF Lookup Bias");
                skinSettings.shadowLutBias = this.SliderControls(skinSettings.shadowLutBias, 0.0f, 1.0f);
            }

            GUILayout.Space(16.0f);

            // normal blurs
            GUILayout.Label("Normal Blur Weight");
            skinSettings.normalBlurWeight = this.SliderControls(skinSettings.normalBlurWeight, 0.0f, 1.0f);

            GUILayout.Label("Normal Blur Radius");
            skinSettings.normalBlurRadius = this.SliderControls(skinSettings.normalBlurRadius, 0.0f, 1.0f);

            GUILayout.Label("Normal Blur Depth Range");
            skinSettings.normalBlurDepthRange = this.SliderControls(skinSettings.normalBlurDepthRange, 0.0f, 20.0f);

            GUILayout.Label("Normal Blur Iterations");
            skinSettings.normalBlurIter = this.SliderControls(skinSettings.normalBlurIter, 1, 10);

            GUILayout.Space(16.0f);

            // ambient occlusion
            GUILayout.Label("Ambient Occlusion Color Bleeding");
            skinSettings.colorBleedWeights = this.RGBControls(skinSettings.colorBleedWeights);

            GUILayout.Space(16.0f);

            // save and load
            GUILayout.Label("Save/Load Preset");
            GUILayout.BeginHorizontal(GUILayout.Height(32.0f));

            if (GUILayout.Button("Load Preset"))
            {
                if (HSSSS.LoadConfig())
                {
                    this.skinSettings = HSSSS.skinSettings;
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

                if (HSSSS.SaveConfig())
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
        }

        private void TransmissionSettings()
        {
            //
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

            GUILayout.Space(16.0f);

            //
            GUILayout.Label("Save/Load Preset");
            GUILayout.BeginHorizontal(GUILayout.Height(32.0f));

            if (GUILayout.Button("Load Preset"))
            {
                if (HSSSS.LoadConfig())
                {
                    Console.WriteLine("#### HSSSS: Loaded Configurations");
                    this.skinSettings = HSSSS.skinSettings;
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Configuration");
                }
            }

            if (GUILayout.Button("Save Preset"))
            {
                HSSSS.skinSettings = this.skinSettings;

                if (HSSSS.SaveConfig())
                {
                    Console.WriteLine("#### HSSSS: Saved Configurations");
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Failed to Save Configurations");
                }
            }

            GUILayout.EndHorizontal();
        }

        private void LightShadowSettings()
        {
            // pcf iterations count
            GUILayout.Label("PCF TAPS COUNT");
            GUILayout.BeginHorizontal(GUILayout.Height(32.0f));

            if (GUILayout.Button("Off"))
            {
                Shader.DisableKeyword("_PCF_TAPS_8");
                Shader.DisableKeyword("_PCF_TAPS_16");
                Shader.DisableKeyword("_PCF_TAPS_32");
                Shader.DisableKeyword("_PCF_TAPS_64");
                Shader.DisableKeyword("_DIR_PCF_ON");
                Shader.DisableKeyword("_PCSS_ON");
                togglePCF = false;
                togglePCSS = false;
                toggleDirPCF = false;

                this.UpdateWindowSize();
            }

            if (GUILayout.Button("8"))
            {
                Shader.DisableKeyword("_PCF_TAPS_16");
                Shader.DisableKeyword("_PCF_TAPS_32");
                Shader.DisableKeyword("_PCF_TAPS_64");
                Shader.EnableKeyword("_PCF_TAPS_8");
                togglePCF = true;

                this.UpdateWindowSize();
            }

            if (GUILayout.Button("16"))
            {
                Shader.DisableKeyword("_PCF_TAPS_8");
                Shader.DisableKeyword("_PCF_TAPS_32");
                Shader.DisableKeyword("_PCF_TAPS_64");
                Shader.EnableKeyword("_PCF_TAPS_16");
                togglePCF = true;

                this.UpdateWindowSize();
            }

            if (GUILayout.Button("32"))
            {
                Shader.DisableKeyword("_PCF_TAPS_8");
                Shader.DisableKeyword("_PCF_TAPS_16");
                Shader.DisableKeyword("_PCF_TAPS_64");
                Shader.EnableKeyword("_PCF_TAPS_32");
                togglePCF = true;

                this.UpdateWindowSize();
            }

            if (GUILayout.Button("64"))
            {
                Shader.DisableKeyword("_PCF_TAPS_8");
                Shader.DisableKeyword("_PCF_TAPS_16");
                Shader.DisableKeyword("_PCF_TAPS_32");
                Shader.EnableKeyword("_PCF_TAPS_64");
                togglePCF = true;

                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(16.0f);

            // pcf soft shadow for directional lights
            GUILayout.BeginHorizontal(GUILayout.Height(32.0f));

            if (GUILayout.Button("Directional PCF ON"))
            {
                Shader.EnableKeyword("_DIR_PCF_ON");
                toggleDirPCF = true;

                this.UpdateWindowSize();
            }

            if (GUILayout.Button("Directional PCF OFF"))
            {
                Shader.DisableKeyword("_DIR_PCF_ON");
                toggleDirPCF = false;

                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(16.0f);

            // pcss soft shadow toggle
            GUILayout.BeginHorizontal(GUILayout.Height(32.0f));

            if (GUILayout.Button("PCSS ON"))
            {
                togglePCSS = true;
                Shader.EnableKeyword("_PCSS_ON");

                this.UpdateWindowSize();
            }

            if (GUILayout.Button("PCSS OFF"))
            {
                togglePCSS = false;
                Shader.DisableKeyword("_PCSS_ON");

                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(16.0f);

            #region PCSS sliders
            if (togglePCSS && togglePCF)
            {
                if(toggleDirPCF)
                {
                    GUILayout.Label("PCSS Blocker Search Radius (Directional)");
                    dirPenumbra.x = this.SliderControls(dirPenumbra.x, 0.0f, 10.0f);
                    GUILayout.Label("PCSS Light Radius (Directional)");
                    dirPenumbra.y = this.SliderControls(dirPenumbra.y, 0.0f, 10.0f);
                    GUILayout.Label("PCSS Minimum Penumbra (Directional)");
                    dirPenumbra.z = this.SliderControls(dirPenumbra.z, 0.0f, 10.0f);
                    Shader.SetGlobalVector("_DirLightPenumbra", dirPenumbra);

                    GUILayout.Space(16.0f);
                }

                GUILayout.Label("PCSS Blocker Search Radius (Spot)");
                spotPenumbra.x = this.SliderControls(spotPenumbra.x, 0.0f, 10.0f);
                GUILayout.Label("PCSS Light Radius (Spot)");
                spotPenumbra.y = this.SliderControls(spotPenumbra.y, 0.0f, 10.0f);
                GUILayout.Label("PCSS Minimum Penumbra (Spot)");
                spotPenumbra.z = this.SliderControls(spotPenumbra.z, 0.0f, 10.0f);
                Shader.SetGlobalVector("_SpotLightPenumbra", spotPenumbra);

                GUILayout.Space(16.0f);

                GUILayout.Label("PCSS Blocker Search Radius (Point)");
                pointPenumbra.x = this.SliderControls(pointPenumbra.x, 0.0f, 10.0f);
                GUILayout.Label("PCSS Light Radius (Point)");
                pointPenumbra.y = this.SliderControls(pointPenumbra.y, 0.0f, 10.0f);
                GUILayout.Label("PCSS Minimum Penumbra (Point)");
                pointPenumbra.z = this.SliderControls(pointPenumbra.z, 0.0f, 10.0f);
                Shader.SetGlobalVector("_PointLightPenumbra", pointPenumbra);
            }
            #endregion

            #region PCF sliders
            else if (togglePCF)
            {
                if (toggleDirPCF)
                {
                    GUILayout.Label("Penumbra Scale (Directional Lights)");
                    dirPenumbra.z = this.SliderControls(dirPenumbra.z, 0.0f, 10.0f);
                    Shader.SetGlobalVector("_DirLightPenumbra", dirPenumbra);
                }

                GUILayout.Label("Penumbra Scale (Spot Lights)");
                spotPenumbra.z = this.SliderControls(spotPenumbra.z, 0.0f, 10.0f);
                Shader.SetGlobalVector("_SpotLightPenumbra", spotPenumbra);

                GUILayout.Label("Penumbra Scale (Point Lights)");
                pointPenumbra.z = this.SliderControls(pointPenumbra.z, 0.0f, 10.0f);
                Shader.SetGlobalVector("_PointLightPenumbra", pointPenumbra);
            }
            #endregion

            GUILayout.Space(16.0f);
            GUILayout.Label("<size=32>Requires SoftShadow=1 option in modprefs.ini!</size>");
        }

        private void PresetsControls()
        {
            GUILayout.Space(128.0f);
            GUILayout.Label("Not Implemented Yet; I'm working on it!");
            GUILayout.Space(128.0f);
        }

        private float SliderControls(float sliderValue, float minValue, float maxValue)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(32.0f));

            sliderValue = GUILayout.HorizontalSlider(sliderValue, minValue, maxValue);

            if (float.TryParse(GUILayout.TextField(sliderValue.ToString("0.00"), GUILayout.Width(64.0f)), out float fieldValue))
            {
                sliderValue = fieldValue;
            }

            GUILayout.EndHorizontal();

            return sliderValue;
        }

        private int SliderControls(int sliderValue, int minValue, int maxValue)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(32.0f));

            sliderValue = (int) GUILayout.HorizontalSlider(sliderValue, minValue, maxValue);

            if (int.TryParse(GUILayout.TextField(sliderValue.ToString(), GUILayout.Width(64.0f)), out int fieldValue))
            {
                sliderValue = fieldValue;
            }

            GUILayout.EndHorizontal();

            return sliderValue;
        }

        private Vector3 RGBControls(Vector3 rgbValue)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(32.0f));

            GUILayout.Label("Red");
            
            if (float.TryParse(GUILayout.TextField(rgbValue.x.ToString("0.00"), GUILayout.Width(96.0f)), out float r))
            {
                rgbValue.x = r;
            }

            GUILayout.Label("Green");

            if (float.TryParse(GUILayout.TextField(rgbValue.y.ToString("0.00"), GUILayout.Width(96.0f)), out float g))
            {
                rgbValue.y = g;
            }

            GUILayout.Label("Blue");

            if (float.TryParse(GUILayout.TextField(rgbValue.z.ToString("0.00"), GUILayout.Width(96.0f)), out float b))
            {
                rgbValue.z = b;
            }

            GUILayout.EndHorizontal();

            return rgbValue;
        }
        private void UpdateSettings()
        {
            if (HSSSS.skinSettings.normalBlurIter != this.skinSettings.normalBlurIter)
            {
                HSSSS.skinSettings = this.skinSettings;
                HSSSS.SSS.ImportSettings();
                HSSSS.SSS.ForceRefresh();
            }

            else
            {
                HSSSS.skinSettings = this.skinSettings;
                HSSSS.SSS.ImportSettings();
                HSSSS.SSS.Refresh();
            }
        }
    }
}
