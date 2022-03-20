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
        public string Version { get { return "0.5.0"; } }
        public string[] Filter { get { return new[] { "HoneySelect_32", "HoneySelect_64", "StudioNEO_32", "StudioNEO_64" }; } }
        #endregion

        #region Global Variables
        private static string pluginName;
        private static string pluginVersion;
        private static string pluginLocation;
        private static string configLocation;
        private static string configPath;

        private static AssetBundle assetBundle;
        private static Shader deferredSkin;
        private static Shader deferredReflections;

        internal static AlloyDeferredRendererPlus SSS = null;
        internal static Shader deferredTransmissionBlit;
        internal static Shader deferredBlurredNormals;
        internal static Texture2D skinLUT;

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
        
        private GameObject configUI;

        private static bool isStudio;
        private static bool isEnabled;
        private static bool useDeferred;
        private static bool useTessellation;
        private static bool useWetSpecGloss;
        private static bool fixAlphaShadow;
        private static KeyCode hotKey;
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
                        AccessTools.Method(typeof(CharFemaleCustom), "CreateBodyTexture"), null,
                        new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.FemaleBodyReplacer))
                        );

                harmony.Patch(
                    AccessTools.Method(typeof(CharFemaleCustom), "CreateFaceTexture"), null,
                    new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.FemaleFaceReplacer))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(CharMaleCustom), "CreateBodyTexture"), null,
                    new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.MaleBodyReplacer))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(CharMaleCustom), "CreateFaceTexture"), null,
                    new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.MaleFaceReplacer))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(CharCustom), "ChangeMaterial"), null,
                    new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.CommonPartsReplacer))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(Manager.Character), "Awake"), null,
                    new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.JuicesReplacer))
                    );

                harmony.Patch(
                    AccessTools.Method(typeof(CharFemaleBody), "Reload"), null,
                    new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.BlushReplacer))
                    );

                if (isStudio)
                {
                    harmony.Patch(
                        AccessTools.Method(typeof(OCILight), "Update"), null,
                        new HarmonyMethod(typeof(HSSSS), nameof(SpotLightPatcher))
                        );
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
                if (isStudio && Input.GetKeyDown(hotKey))
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
            isEnabled = ModPrefs.GetBool("HSSSS", "Enabled", true, true);
            useDeferred = ModPrefs.GetBool("HSSSS", "DeferredSkin", true, true);
            useTessellation = ModPrefs.GetBool("HSSSS", "Tessellation", false, true);
            useWetSpecGloss = ModPrefs.GetBool("HSSSS", "WetSpecGloss", false, true);
            fixAlphaShadow = ModPrefs.GetBool("HSSSS", "FixShadow", false, true);

            try
            {
                string hotKeyString = ModPrefs.GetString("HSSSS", "ShortcutKey", KeyCode.ScrollLock.ToString(), true);
                hotKey = (KeyCode)Enum.Parse(typeof(KeyCode), hotKeyString, true);
            }
            catch (Exception)
            {
                hotKey = KeyCode.ScrollLock;
            }
        }

        private void BaseAssetLoader()
        {
            assetBundle = AssetBundle.LoadFromMemory(Resources.hssssresources);
            femaleBodyThickness = assetBundle.LoadAsset<Texture2D>("FemaleBodyThickness");
            femaleHeadThickness = assetBundle.LoadAsset<Texture2D>("FemaleHeadThickness");
            maleBodyThickness = assetBundle.LoadAsset<Texture2D>("MaleBodyThickness");
            maleHeadThickness = assetBundle.LoadAsset<Texture2D>("MaleHeadThickness");
            spotCookie = assetBundle.LoadAsset<Texture2D>("DefaultSpotCookie");

            if (null != assetBundle)
            {
                Console.WriteLine("#### HSSSS: Assetbundle Loaded");
            }

            if (null != femaleBodyThickness && null != femaleHeadThickness)
            {
                Console.WriteLine("#### HSSSS: Built-In Thickness Map Loaded");
            }

            if (null != spotCookie)
            {
                Console.WriteLine("#### HSSSS: Spotlight Cookie Loaded");
            }

            if (fixAlphaShadow)
            {
                eyeBrowMaterial = assetBundle.LoadAsset<Material>("EyeBrow");
                eyeLashMaterial = assetBundle.LoadAsset<Material>("EyeLash");
                eyeAlphaMaterial = assetBundle.LoadAsset<Material>("EyeAlpha");
                eyePupilMaterial = assetBundle.LoadAsset<Material>("EyePupil");
                eyeWhiteMaterial = assetBundle.LoadAsset<Material>("EyeWhite");

                if (null != eyeBrowMaterial && null != eyeLashMaterial && null != eyeAlphaMaterial && null != eyePupilMaterial && null != eyeWhiteMaterial)
                {
                    Console.WriteLine("#### HSSSS: FixShadow Materials Loaded");
                }
            }
        }

        private void DeferredAssetLoader()
        {
            skinLUT = assetBundle.LoadAsset<Texture2D>("DeferredLUT");

            if (useTessellation)
            {
                skinMaterial = assetBundle.LoadAsset<Material>("SkinDeferredTessellation");
                overlayMaterial = assetBundle.LoadAsset<Material>("OverlayTessellation");
            }

            else
            {
                skinMaterial = assetBundle.LoadAsset<Material>("SkinDeferred");
                overlayMaterial = assetBundle.LoadAsset<Material>("Overlay");
            }

            deferredTransmissionBlit = assetBundle.LoadAsset<Shader>("DeferredTransmissionBlit");
            deferredBlurredNormals = assetBundle.LoadAsset<Shader>("DeferredBlurredNormals");
            deferredSkin = assetBundle.LoadAsset<Shader>("Alloy Deferred Skin");
            deferredReflections = assetBundle.LoadAsset<Shader>("Alloy Deferred Reflections");

            if (useWetSpecGloss)
            {
                skinMaterial.EnableKeyword("_WET_SPECGLOSS");
            }

            if (null != skinLUT)
            {
                Console.WriteLine("#### HSSSS: Deferred Skin LUT Loaded");
            }

            if (null != skinMaterial && null != overlayMaterial)
            {
                Console.WriteLine("#### HSSSS: Deferred Skin Replacer Loaded");
            }

            if (null != deferredTransmissionBlit && null != deferredBlurredNormals)
            {
                Console.WriteLine("#### HSSSS: Deferred PostFX Shader Loaded");
            }

            if (null != deferredSkin && null != deferredReflections)
            {
                Console.WriteLine("#### HSSSS: Deferred Internal Shader Loaded");
            }
        }

        private void ForwardAssetLoader()
        {
            skinMaterial = assetBundle.LoadAsset<Material>("SkinForward");
            overlayMaterial = assetBundle.LoadAsset<Material>("Overlay");

            if (useWetSpecGloss)
            {
                skinMaterial.EnableKeyword("_WET_SPECGLOSS");
            }

            if (null != skinMaterial && null != overlayMaterial)
            {
                Console.WriteLine("#### HSSSS: Forward Skin Replacer Loaded");
            }
        }

        private void InternalShaderReplacer()
        {
            GraphicsSettings.SetShaderMode(BuiltinShaderType.DeferredShading, BuiltinShaderMode.UseCustom);
            GraphicsSettings.SetShaderMode(BuiltinShaderType.DeferredReflections, BuiltinShaderMode.UseCustom);
            GraphicsSettings.SetCustomShader(BuiltinShaderType.DeferredShading, deferredSkin);
            GraphicsSettings.SetCustomShader(BuiltinShaderType.DeferredReflections, deferredReflections);

            if (GraphicsSettings.GetCustomShader(BuiltinShaderType.DeferredShading) == deferredSkin)
            {
                Console.WriteLine("#### HSSSS: Internal Deferred Shading Replaced");
            }

            if (GraphicsSettings.GetCustomShader(BuiltinShaderType.DeferredReflections) == deferredReflections)
            {
                Console.WriteLine("#### HSSSS: Internal Deferred Reflection Replaced");
            }
        }

        private void PostFxInitializer()
        {
            GameObject mainCamera = GameObject.Find("StudioScene/Camera/Main Camera");

            if (null != mainCamera)
            {
                SSS = mainCamera.gameObject.AddComponent<AlloyDeferredRendererPlus>();

                if (null != SSS)
                {
                    Console.WriteLine("#### HSSSS: PostFX Initialized");
                }
            }
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

        #endregion

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

            public static void FemaleBodyReplacer(CharInfo ___chaInfo)
            {
                Material bodyMat = ___chaInfo.chaBody.customMatBody;

                if (bodyMat != null)
                {
                    if (!bodyMat.shader.name.Contains("HSSSS"))
                    {
                        ShaderReplacer(skinMaterial, bodyMat);
                        bodyMat.SetTexture("_Thickness", femaleBodyThickness);
                        Console.WriteLine("#### HSSSS Replaced " + bodyMat);
                    }
                }
            }

            public static void FemaleFaceReplacer(CharInfo ___chaInfo)
            {
                Material faceMat = ___chaInfo.chaBody.customMatFace;

                if (faceMat != null)
                {
                    if (!faceMat.shader.name.Contains("HSSSS"))
                    {
                        ShaderReplacer(skinMaterial, faceMat);
                        faceMat.SetTexture("_Thickness", femaleHeadThickness);
                        Console.WriteLine("#### HSSSS Replaced " + faceMat.name);
                    }
                }

                if (fixAlphaShadow)
                {
                    GameObject objHead = ___chaInfo.chaBody.objHead;

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

            public static void MaleBodyReplacer(CharInfo ___chaInfo)
            {
                Material mat = ___chaInfo.chaBody.customMatBody;

                if (mat != null)
                {
                    if (!mat.shader.name.Contains("HSSSS"))
                    {
                        ShaderReplacer(skinMaterial, mat);
                        mat.SetTexture("_Thickness", maleBodyThickness);
                        Console.WriteLine("#### HSSSS Replaced " + mat);
                    }
                }
            }

            public static void MaleFaceReplacer(CharInfo ___chaInfo)
            {
                Material mat = ___chaInfo.chaBody.customMatFace;

                if (mat != null)
                {
                    if (!mat.shader.name.Contains("HSSSS"))
                    {
                        ShaderReplacer(skinMaterial, mat);
                        mat.SetTexture("_Thickness", maleHeadThickness);
                        Console.WriteLine("#### HSSSS Replaced " + mat);
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

            public static void BlushReplacer(CharFemaleBody __instance)
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
            }
        }
    }

    public class ConfigUI : MonoBehaviour
    {
        private Rect configWindow;

        private GUIStyle labelStyle;
        private GUIStyle fieldStyle;
        private GUIStyle sliderStyle;
        private GUIStyle thumbStyle;
        private GUIStyle buttonStyle;

        private int lightAlpha = 1;
        
        public void Awake()
        {
            this.configWindow = new Rect(250.0f, 0.000f, 500.0f, 850.0f);
        }

        public void LateUpdate()
        {
        }

        public void OnGUI()
        {
            this.labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16
            };

            this.fieldStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 16
            };

            this.sliderStyle = new GUIStyle(GUI.skin.horizontalSlider)
            {
                fixedHeight = 16
            };

            this.thumbStyle = new GUIStyle(GUI.skin.horizontalScrollbarThumb)
            {
                fixedWidth = 16,
                fixedHeight = 16
            };

            this.buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16
            };

            this.configWindow = GUI.Window(0, this.configWindow, this.WindowFunction, "HSSSS Configuration");
            Studio.Studio.Instance.cameraCtrl.enabled = !this.configWindow.Contains(Event.current.mousePosition);
        }

        private void WindowFunction(int WindowID)
        {
            GUILayout.Space(8.0f);

            #region Skin Scattering
            GUILayout.Label("Skin Scattering Weight", this.labelStyle);
            HSSSS.SSS.SkinSettings.Weight = this.SliderControls(HSSSS.SSS.SkinSettings.Weight, 0.0f, 1.0f);

            GUILayout.Label("Skin Scattering Scale", this.labelStyle);
            HSSSS.SSS.SkinSettings.Scale = this.SliderControls(HSSSS.SSS.SkinSettings.Scale, 0.0f, 1.0f);

            GUILayout.Label("Skin Scattering Bias", this.labelStyle);
            HSSSS.SSS.SkinSettings.Bias = this.SliderControls(HSSSS.SSS.SkinSettings.Bias, 0.0f, 1.0f);

            GUILayout.Label("Skin Scattering Bump Blur", this.labelStyle);
            HSSSS.SSS.SkinSettings.BumpBlur = this.SliderControls(HSSSS.SSS.SkinSettings.BumpBlur, 0.0f, 1.0f);

            GUILayout.Label("Skin Scattering Blur Width", this.labelStyle);
            HSSSS.SSS.SkinSettings.BlurWidth = this.SliderControls(HSSSS.SSS.SkinSettings.BlurWidth, 0.0f, 1.0f);

            GUILayout.Label("Skin Scattering Blur Depth Range", this.labelStyle);
            HSSSS.SSS.SkinSettings.BlurDepthRange = this.SliderControls(HSSSS.SSS.SkinSettings.BlurDepthRange, 0.0f, 20.0f);

            GUILayout.Label("Skin Scattering Occlusion Color Bleeding", this.labelStyle);
            HSSSS.SSS.SkinSettings.ColorBleedAoWeights = this.RGBControls(HSSSS.SSS.SkinSettings.ColorBleedAoWeights);

            GUILayout.Label("Transmission Absorption", this.labelStyle);
            HSSSS.SSS.SkinSettings.TransmissionAbsorption = this.RGBControls(HSSSS.SSS.SkinSettings.TransmissionAbsorption);
            #endregion

            GUILayout.Space(8.0f);

            #region Transmission
            GUILayout.Label("Transmission Weight", this.labelStyle);
            HSSSS.SSS.TransmissionSettings.Weight = this.SliderControls(HSSSS.SSS.TransmissionSettings.Weight, 0.0f, 1.0f);

            GUILayout.Label("Transmission Distortion", this.labelStyle);
            HSSSS.SSS.TransmissionSettings.BumpDistortion = this.SliderControls(HSSSS.SSS.TransmissionSettings.BumpDistortion, 0.0f, 1.0f);

            GUILayout.Label("Transmission Shadow Weight", this.labelStyle);
            HSSSS.SSS.TransmissionSettings.ShadowWeight = this.SliderControls(HSSSS.SSS.TransmissionSettings.ShadowWeight, 0.0f, 1.0f);

            GUILayout.Label("Transmission Falloff", this.labelStyle);
            HSSSS.SSS.TransmissionSettings.Falloff = this.SliderControls(HSSSS.SSS.TransmissionSettings.Falloff, 1.0f, 20.0f);
            #endregion

            GUILayout.Space(8.0f);

            if (GUILayout.Button("Reset Configuration", this.buttonStyle))
            {
                HSSSS.LoadConfig();
            }

            GUILayout.Space(8.0f);

            if (GUILayout.Button("Save Configuration", this.buttonStyle))
            {
                HSSSS.SaveConfig();
            }

            GUILayout.Space(8.0f);

            #region Fix Lights Alpha
            GUILayout.Label("Light Alpha", this.labelStyle);

            GUILayout.BeginHorizontal();

            if (int.TryParse(GUILayout.TextField(this.lightAlpha.ToString(), this.fieldStyle), out int alpha))
            {
                this.lightAlpha = alpha;
            }

            if (GUILayout.Button("Directional", this.buttonStyle))
            {
                this.SetLightAlpha(LightType.Directional);
            }

            if (GUILayout.Button("Spot", this.buttonStyle))
            {
                this.SetLightAlpha(LightType.Spot);
            }

            if (GUILayout.Button("Point", this.buttonStyle))
            {
                this.SetLightAlpha(LightType.Point);
            }

            GUILayout.EndHorizontal();
            #endregion

            GUI.DragWindow();

            HSSSS.SSS.Refresh();
        }

        private float SliderControls(float sliderValue, float minValue, float maxValue)
        {
            GUILayout.BeginHorizontal();

            sliderValue = GUILayout.HorizontalSlider(sliderValue, minValue, maxValue, this.sliderStyle, this.thumbStyle);

            if (float.TryParse(GUILayout.TextField(sliderValue.ToString("0.00"), this.fieldStyle, GUILayout.Width(64.0f)), out float fieldValue))
            {
                sliderValue = fieldValue;
            }

            GUILayout.EndHorizontal();

            return sliderValue;
        }

        private Vector3 RGBControls(Vector3 rgbValue)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label("R", labelStyle);
            
            if (float.TryParse(GUILayout.TextField(rgbValue.x.ToString("0.00"), this.fieldStyle), out float r))
            {
                rgbValue.x = r;
            }

            GUILayout.Label("G", labelStyle);

            if (float.TryParse(GUILayout.TextField(rgbValue.y.ToString("0.00"), this.fieldStyle), out float g))
            {
                rgbValue.y = g;
            }

            GUILayout.Label("B", labelStyle);

            if (float.TryParse(GUILayout.TextField(rgbValue.z.ToString("0.00"), this.fieldStyle), out float b))
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
                        lightColor.a = (float)this.lightAlpha * 0.01f;
                        light.SetColor(lightColor);
                    }
                }
            }
        }
    }
}
