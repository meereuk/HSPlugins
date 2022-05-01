using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Studio;
using IllusionPlugin;
using UnityEngine;
using UnityEngine.Rendering;
using Harmony;


namespace HSSSS
{
    public class HSSSS : IEnhancedPlugin
    {
        #region Plugin Info
        public string Name { get { return "HSSSS";  } }
        public string Version { get { return "0.8.2"; } }
        public string[] Filter { get { return new[] { "HoneySelect_32", "HoneySelect_64", "StudioNEO_32", "StudioNEO_64" }; } }
        #endregion

        #region Global Variables
        // info
        private static string pluginName;
        private static string pluginVersion;
        private static string pluginLocation;
        private static string configLocation;
        private static string configPath;

        // assets and resources
        private static AssetBundle assetBundle;
        private static Shader deferredSkin;
        private static Shader deferredReflections;

        internal static AlloyDeferredRendererPlus SSS = null;
        internal static Shader deferredTransmissionBlit;
        internal static Shader deferredBlurredNormals;
        internal static Shader shadowMapSampler;
        internal static Texture2D skinLUT;
        internal static Texture2D jitter2D;

        public static Material skinMaterial;
        public static Material overlayMaterial;
        public static Material eyeBrowMaterial;
        public static Material eyeLashMaterial;
        public static Material eyeAlphaMaterial;
        public static Material eyePupilMaterial;
        public static Material eyeWhiteMaterial;
        public static Texture2D femaleBodyThickness;
        public static Texture2D femaleHeadThickness;
        public static Texture2D maleBodyThickness;
        public static Texture2D maleHeadThickness;
        public static Texture2D spotCookie;      

        // modprefs.ini options
        private static bool isStudio;
        private static bool isEnabled;
        private static bool useDeferred;
        private static bool useTessellation;
        private static bool useWetSpecGloss;
        private static bool fixAlphaShadow;
        private static bool useSoftShadow;
        private static bool useCustomThickness;

        private static string femaleBodyCustom;
        private static string femaleHeadCustom;
        private static string maleBodyCustom;
        private static string maleHeadCustom;

        private static KeyCode[] hotKey;

        // ui window
        internal static GUISkin skinUI;
        private GameObject configUI;
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

            this.ConfigParser();

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

                        Shader.SetGlobalFloat("_DirLightPenumbra", 1.0f);
                        Shader.SetGlobalFloat("_SpotLightPenumbra", 1.0f);
                        Shader.SetGlobalFloat("_PointLightPenumbra", 1.0f);
                    }
                }
            }
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnLevelWasLoaded(int level)
        {
            if (isStudio && level == 3)
            {
                if (isEnabled && useDeferred)
                {
                    this.PostFxInitializer();

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
                        Console.WriteLine("#### HSSSS: Successfully loaded config.xml");
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
                    if (this.configUI == null)
                    {
                        this.configUI = new GameObject("HSSSS.ConfigUI", typeof(ConfigUI));
                    }

                    else
                    {
                        UnityEngine.Object.DestroyImmediate(this.configUI);
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
        private void ConfigParser()
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

            // jitter texture
            jitter2D = assetBundle.LoadAsset<Texture2D>("Jitter2D");
            
            // spotlight cookie
            spotCookie = assetBundle.LoadAsset<Texture2D>("DefaultSpotCookie");

            //
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

            if (null == jitter2D)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Jitter Texture");
            }

            if (null == spotCookie)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Spotlight Cookie");
            }

            // materials for additional replacement
            if (fixAlphaShadow)
            {
                eyeBrowMaterial = assetBundle.LoadAsset<Material>("EyeBrow");
                eyeLashMaterial = assetBundle.LoadAsset<Material>("EyeLash");
                eyeAlphaMaterial = assetBundle.LoadAsset<Material>("EyeAlpha");
                eyePupilMaterial = assetBundle.LoadAsset<Material>("EyePupil");
                eyeWhiteMaterial = assetBundle.LoadAsset<Material>("EyeWhite");

                //
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
            }
        }

        private void DeferredAssetLoader()
        {
            // skin-lookup table for skin scattering
            skinLUT = assetBundle.LoadAsset<Texture2D>("DeferredLUT");

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
            if (useSoftShadow)
            {
                deferredSkin = assetBundle.LoadAsset<Shader>("InternalDeferredShadingSoftShadow");
                deferredReflections = assetBundle.LoadAsset<Shader>("InternalDeferredReflectionsSoftShadow");
                shadowMapSampler = assetBundle.LoadAsset<Shader>("VSMShadowSampler");
            }

            // default soft shadows
            else
            {
                deferredSkin = assetBundle.LoadAsset<Shader>("InternalDeferredShading");
                deferredReflections = assetBundle.LoadAsset<Shader>("InternalDeferredReflections");
            }

            // semi-pbr wetness option
            if (useWetSpecGloss)
            {
                skinMaterial.EnableKeyword("_WET_SPECGLOSS");
            }

            // configuration window skin
            skinUI = assetBundle.LoadAsset<GUISkin>("GUISkin");

            //
            if (null == skinLUT)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Deferred Skin LUT");
            }

            if (null == skinMaterial || null == overlayMaterial)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Skin Material");
            }

            if (null == deferredTransmissionBlit || null == deferredBlurredNormals)
            {
                Console.WriteLine("#### HSSSS: Failed to Load PostFX Shaders");
            }

            if (null == deferredSkin || null == deferredReflections)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Deferred Internal Shaders");
            }

            if (null == skinUI)
            {
                Console.WriteLine("#### HSSSS: Failed to Load UI Skin");
            }
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

            //
            if (null == skinMaterial || null == overlayMaterial)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Skin Material");
            }
        }

        private void InternalShaderReplacer()
        {
            GraphicsSettings.SetShaderMode(BuiltinShaderType.DeferredShading, BuiltinShaderMode.UseCustom);
            GraphicsSettings.SetShaderMode(BuiltinShaderType.DeferredReflections, BuiltinShaderMode.UseCustom);
            GraphicsSettings.SetCustomShader(BuiltinShaderType.DeferredShading, deferredSkin);
            GraphicsSettings.SetCustomShader(BuiltinShaderType.DeferredReflections, deferredReflections);

            if (GraphicsSettings.GetCustomShader(BuiltinShaderType.DeferredShading) != deferredSkin)
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
                SSS = mainCamera.gameObject.AddComponent<AlloyDeferredRendererPlus>();

                if (null == SSS)
                {
                    Console.WriteLine("#### HSSSS: Failed to Initialize Post FX");
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
                
                SSS.SkinSettings.Enabled = bool.Parse(scattering.Attribute("enabled").Value);
                SSS.SkinSettings.Weight = float.Parse(scattering.Element("Weight").Value);
                SSS.SkinSettings.Scale = float.Parse(scattering.Element("Scale").Value);
                SSS.SkinSettings.Bias = float.Parse(scattering.Element("Bias").Value);
                SSS.SkinSettings.BumpBlur = float.Parse(scattering.Element("BumpBlur").Value);
                SSS.SkinSettings.BlurWidth = float.Parse(scattering.Element("BlurWidth").Value);
                SSS.SkinSettings.BlurDepthRange = float.Parse(scattering.Element("BlurDepth").Value);

                SSS.SkinSettings.ColorBleedAoWeights.x = float.Parse(scattering.Element("BleedingColor").Element("Red").Value);
                SSS.SkinSettings.ColorBleedAoWeights.y = float.Parse(scattering.Element("BleedingColor").Element("Green").Value);
                SSS.SkinSettings.ColorBleedAoWeights.z = float.Parse(scattering.Element("BleedingColor").Element("Blue").Value);

                SSS.SkinSettings.TransmissionAbsorption.x = float.Parse(scattering.Element("AbsorptionColor").Element("Red").Value);
                SSS.SkinSettings.TransmissionAbsorption.y = float.Parse(scattering.Element("AbsorptionColor").Element("Green").Value);
                SSS.SkinSettings.TransmissionAbsorption.z = float.Parse(scattering.Element("AbsorptionColor").Element("Blue").Value);
                
                SSS.TransmissionSettings.Enabled = bool.Parse(transmission.Attribute("enabled").Value);

                SSS.TransmissionSettings.Weight = float.Parse(transmission.Element("Weight").Value);
                SSS.TransmissionSettings.BumpDistortion = float.Parse(transmission.Element("Distortion").Value);
                SSS.TransmissionSettings.ShadowWeight = float.Parse(transmission.Element("ShadowWeight").Value);
                SSS.TransmissionSettings.Falloff = float.Parse(transmission.Element("Falloff").Value);

                SSS.Refresh();

                return true;
            }
            
            catch
            {
                return false;
            }
        }

        internal static bool SaveConfig()
        {
            XDocument config = new XDocument();

            XElement root = new XElement(pluginName, new XAttribute("version", pluginVersion));
            XElement scattering = new XElement("SkinScattering",
                new XAttribute("enabled", SSS.SkinSettings.Enabled),
                new XElement("Weight", SSS.SkinSettings.Weight),
                new XElement("Scale", SSS.SkinSettings.Scale),
                new XElement("Bias", SSS.SkinSettings.Bias),
                new XElement("BumpBlur", SSS.SkinSettings.BumpBlur),
                new XElement("BlurWidth", SSS.SkinSettings.BlurWidth),
                new XElement("BlurDepth", SSS.SkinSettings.BlurDepthRange),
                new XElement("BleedingColor",
                    new XElement("Red", SSS.SkinSettings.ColorBleedAoWeights.x),
                    new XElement("Green", SSS.SkinSettings.ColorBleedAoWeights.y),
                    new XElement("Blue", SSS.SkinSettings.ColorBleedAoWeights.z)
                    ),
                new XElement("AbsorptionColor",
                    new XElement("Red", SSS.SkinSettings.TransmissionAbsorption.x),
                    new XElement("Green", SSS.SkinSettings.TransmissionAbsorption.y),
                    new XElement("Blue", SSS.SkinSettings.TransmissionAbsorption.z)
                    )
                );
            XElement transmission = new XElement("Transmission",
                new XAttribute("enabled", SSS.TransmissionSettings.Enabled),
                new XElement("Weight", SSS.TransmissionSettings.Weight),
                new XElement("Distortion", SSS.TransmissionSettings.BumpDistortion),
                new XElement("ShadowWeight", SSS.TransmissionSettings.ShadowWeight),
                new XElement("Falloff", SSS.TransmissionSettings.Falloff)
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
                        mat.EnableKeyword("_METALLIC_OFF");
                        mat.renderQueue = 2001;
                        break;

                    case CharReference.TagObjKey.ObjEyeR:
                        ShaderReplacer(eyePupilMaterial, mat);
                        mat.EnableKeyword("_METALLIC_OFF");
                        mat.renderQueue = 2001;
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
                    if (!bodyMat.shader.name.Contains("HSSSS"))
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
                    if (!faceMat.shader.name.Contains("HSSSS"))
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
                                    if (!mat.shader.name.Contains("HSSSS"))
                                    {
                                        ObjectParser(mat, key);
                                        Console.WriteLine("#### HSSSS Replaced " + mat.name);
                                    }
                                }
                            }

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
                        if (!juiceMat.shader.name.Contains("HSSSS"))
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
                if (null != __instance.matHohoAka)
                {
                    if (!__instance.matHohoAka.shader.name.Contains("HSSSS"))
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
                            if (!blushMat.shader.name.Contains("HSSSS"))
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
                        // EyeShade
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
                                if (!matShade.shader.name.Contains("HSSSS"))
                                {
                                    ShaderReplacer(eyeAlphaMaterial, matShade);
                                    matShade.EnableKeyword("_METALLIC_OFF");
                                    matShade.renderQueue = 2002;
                                    Console.WriteLine("#### HSSSS Replaced " + matShade.name);
                                }
                            }
                        }

                        // Tears
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
                                    if (!matTears.shader.name.Contains("HSSSS"))
                                    {
                                        ShaderReplacer(eyeAlphaMaterial, matTears);
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
        }
        #endregion
    }

    public class ConfigUI : MonoBehaviour
    {
        private static Vector2 windowPosition = new Vector2(250.0f, 0.000f);
        private static Vector2 windowSize = new Vector2(768.0f, 640.0f);

        private Rect configWindow;

        private static float lightAlpha = 1.0f;
        private static Vector3 penumbraScale = new Vector3(1.0f, 1.0f, 1.0f);

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
        }

        public void LateUpdate()
        {
        }

        public void OnGUI()
        {
            GUI.skin = HSSSS.skinUI;

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

            GUI.DragWindow();
            HSSSS.SSS.Refresh();

            windowPosition = this.configWindow.position;
        }

        private void TabsControl()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(32.0f));

            if (GUILayout.Button("Scattering"))
            {
                state = UIState.skinScattering;
                this.configWindow.size = new Vector2(768.0f, 640.0f);
            }

            if (GUILayout.Button("Transmission"))
            {
                state = UIState.skinTransmission;
                this.configWindow.size = new Vector2(768.0f, 512.0f);
            }

            if (GUILayout.Button("Lights & Shadows"))
            {
                state = UIState.lightShadow;
                this.configWindow.size = new Vector2(768.0f, 400.0f);
            }

            if (GUILayout.Button("Presets"))
            {
                state = UIState.presets;
                this.configWindow.size = new Vector2(768.0f, 416.0f);
            }

            GUILayout.EndHorizontal();
        }

        private void ScatteringSettings()
        {
            GUILayout.Label("Skin Scattering Weight");
            HSSSS.SSS.SkinSettings.Weight = this.SliderControls(HSSSS.SSS.SkinSettings.Weight, 0.0f, 1.0f);

            GUILayout.Label("Skin Scattering Scale");
            HSSSS.SSS.SkinSettings.Scale = this.SliderControls(HSSSS.SSS.SkinSettings.Scale, 0.0f, 1.0f);

            GUILayout.Label("Skin Scattering Bias");
            HSSSS.SSS.SkinSettings.Bias = this.SliderControls(HSSSS.SSS.SkinSettings.Bias, 0.0f, 1.0f);

            GUILayout.Label("Skin Scattering Bump Blur");
            HSSSS.SSS.SkinSettings.BumpBlur = this.SliderControls(HSSSS.SSS.SkinSettings.BumpBlur, 0.0f, 1.0f);

            GUILayout.Label("Skin Scattering Blur Width");
            HSSSS.SSS.SkinSettings.BlurWidth = this.SliderControls(HSSSS.SSS.SkinSettings.BlurWidth, 0.0f, 1.0f);

            GUILayout.Label("Skin Scattering Blur Depth Range");
            HSSSS.SSS.SkinSettings.BlurDepthRange = this.SliderControls(HSSSS.SSS.SkinSettings.BlurDepthRange, 0.0f, 20.0f);

            GUILayout.Label("Skin Scattering Occlusion Color Bleeding");
            HSSSS.SSS.SkinSettings.ColorBleedAoWeights = this.RGBControls(HSSSS.SSS.SkinSettings.ColorBleedAoWeights);

            GUILayout.Label("Save/Load Preset");
            GUILayout.BeginHorizontal(GUILayout.Height(32.0f));

            if (GUILayout.Button("Load Preset"))
            {
                if (HSSSS.LoadConfig())
                {
                    Console.WriteLine("#### HSSSS: Loaded Configurations");
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Configuration");
                }
            }

            if (GUILayout.Button("Save Preset"))
            {
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

        private void TransmissionSettings()
        {
            GUILayout.Label("Transmission Weight");
            HSSSS.SSS.TransmissionSettings.Weight = this.SliderControls(HSSSS.SSS.TransmissionSettings.Weight, 0.0f, 1.0f);

            GUILayout.Label("Transmission Distortion");
            HSSSS.SSS.TransmissionSettings.BumpDistortion = this.SliderControls(HSSSS.SSS.TransmissionSettings.BumpDistortion, 0.0f, 1.0f);

            GUILayout.Label("Transmission Shadow Weight");
            HSSSS.SSS.TransmissionSettings.ShadowWeight = this.SliderControls(HSSSS.SSS.TransmissionSettings.ShadowWeight, 0.0f, 1.0f);

            GUILayout.Label("Transmission Falloff");
            HSSSS.SSS.TransmissionSettings.Falloff = this.SliderControls(HSSSS.SSS.TransmissionSettings.Falloff, 1.0f, 20.0f);

            GUILayout.Label("Transmission Absorption");
            HSSSS.SSS.SkinSettings.TransmissionAbsorption = this.RGBControls(HSSSS.SSS.SkinSettings.TransmissionAbsorption);

            GUILayout.Label("Save/Load Preset");
            GUILayout.BeginHorizontal(GUILayout.Height(32.0f));

            if (GUILayout.Button("Load Preset"))
            {
                if (HSSSS.LoadConfig())
                {
                    Console.WriteLine("#### HSSSS: Loaded Configurations");
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Configuration");
                }
            }

            if (GUILayout.Button("Save Preset"))
            {
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
            GUILayout.Label("Light Alpha");

            lightAlpha = this.SliderControls(lightAlpha, 0.0f, 100.0f);

            GUILayout.Space(16.0f);

            GUILayout.BeginHorizontal(GUILayout.Height(32.0f));

            if (GUILayout.Button("Directional"))
            {
                this.SetLightAlpha(LightType.Directional);
            }

            if (GUILayout.Button("Spot"))
            {
                this.SetLightAlpha(LightType.Spot);
            }

            if (GUILayout.Button("Point"))
            {
                this.SetLightAlpha(LightType.Point);
            }

            GUILayout.EndHorizontal();

            GUILayout.Label("Penumbra Scale (Directional Lights)");
            penumbraScale.x = this.SliderControls(penumbraScale.x, 0.0f, 10.0f);

            GUILayout.Label("Penumbra Scale (Spot Lights)");
            penumbraScale.y = this.SliderControls(penumbraScale.y, 0.0f, 10.0f);

            GUILayout.Label("Penumbra Scale (Point Lights");
            penumbraScale.z = this.SliderControls(penumbraScale.z, 0.0f, 10.0f);

            Shader.SetGlobalFloat("_DirLightPenumbra", penumbraScale.x);
            Shader.SetGlobalFloat("_SpotLightPenumbra", penumbraScale.y);
            Shader.SetGlobalFloat("_PointLightPenumbra", penumbraScale.z);
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

        private void SetLightAlpha(LightType type)
        {
            Dictionary<int, ObjectCtrlInfo> ociDict = Singleton<Studio.Studio>.Instance.dicObjectCtrl;

            foreach(KeyValuePair<int, ObjectCtrlInfo> entry in ociDict)
            {
                if (entry.Value is OCILight)
                {
                    var light = (OCILight) entry.Value;

                    if (type == light.lightType)
                    {
                        Color lightColor = light.lightInfo.color;
                        lightColor.a = (float)lightAlpha * 0.01f;
                        light.SetColor(lightColor);
                    }
                }
            }
        }
    }
}
