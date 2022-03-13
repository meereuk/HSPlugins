using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using IllusionPlugin;
using UnityEngine;
using UnityEngine.Rendering;

namespace HSSSS
{
    public class HSSSS : IEnhancedPlugin
    {
        #region Plugin Info
        public string Name { get { return "HSSSS";  } }
        public string Version { get { return "0.3.0"; } }
        public string[] Filter { get { return new[] { "StudioNEO_32", "StudioNEO_64" }; } }
        #endregion

        #region Global Variables
        //
        private static string _name;
        private static string _version;

        // 
        private static AssetBundle bundle;
        private static Shader deferredSkin;
        private static Shader deferredReflections;

        //
        internal static AlloyDeferredRendererPlus SSS;
        internal static Shader deferredTransmissionBlit;
        internal static Shader deferredBlurredNormals;
        internal static Texture2D skinLUT;

        // 
        internal static Material skinMaterial;
        internal static Material overlayMaterial;
        internal static Material eyeBrowMaterial;
        internal static Material eyeLashMaterial;
        internal static Material eyeAlphaMaterial;
        internal static Material eyePupilMaterial;
        internal static Material eyeWhiteMaterial;
        internal static Texture2D femaleBodyThickness;
        internal static Texture2D famaleHeadThickness;
        internal static Texture2D maleBodyThickness;
        internal static Texture2D maleHeadThickness;
        internal static Texture2D spotCookie;
        
        //
        private GameObject configUI;
        private GameObject skinReplacer;

        //
        private static bool isEnabled;
        private static bool useDeferred;
        private static bool useTessellation;
        private static bool useWetSpecGloss;
        internal static bool fixAlphaShadow;
        internal static KeyCode hotKey;

        //
        private static string pluginLocation;
        private static string configLocation;
        private static string configPath;
        #endregion

        #region Unity Methods
        public void OnApplicationStart()
        {
            _name = this.Name;
            _version = this.Version;

            pluginLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            configLocation = Path.Combine(pluginLocation, this.Name);
            configPath = Path.Combine(configLocation, "config.xml");

            if (!Directory.Exists(configLocation))
            {
                Directory.CreateDirectory(configLocation);
            }

            ConfigParser();

            if (isEnabled)
            {
                BaseAssetLoader();

                if (useDeferred)
                {
                    DeferredAssetLoader();
                    ReplaceInternalShader();
                }

                else
                {
                    ForwardAssetLoader();
                }
            }
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnLevelWasLoaded(int level)
        {
            if (level == 3)
            {
                if (isEnabled)
                {
                    if (useDeferred)
                    {
                        InitPostFX();
                        
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

                    this.skinReplacer = new GameObject("HSSSS.SkinReplacer", typeof(SkinReplacer));
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
            if(isEnabled)
            {
                FixSpotLights();

                if (useDeferred && Input.GetKeyDown(hotKey))
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
        private static void ConfigParser()
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

        private static void BaseAssetLoader()
        {
            bundle = AssetBundle.LoadFromMemory(Resources.hssssresources);
            femaleBodyThickness = bundle.LoadAsset<Texture2D>("FemaleBodyThickness");
            famaleHeadThickness = bundle.LoadAsset<Texture2D>("FemaleHeadThickness");
            maleBodyThickness = bundle.LoadAsset<Texture2D>("MaleBodyThickness");
            maleHeadThickness = bundle.LoadAsset<Texture2D>("MaleHeadThickness");
            spotCookie = bundle.LoadAsset<Texture2D>("DefaultSpotCookie");

            if (null != bundle)
            {
                Console.WriteLine("#### HSSSS: Assetbundle Loaded");
            }

            if (null != femaleBodyThickness && null != famaleHeadThickness)
            {
                Console.WriteLine("#### HSSSS: Built-In Thickness Map Loaded");
            }

            if (null != spotCookie)
            {
                Console.WriteLine("#### HSSSS: Spotlight Cookie Loaded");
            }

            if (fixAlphaShadow)
            {
                eyeBrowMaterial = bundle.LoadAsset<Material>("EyeBrow");
                eyeLashMaterial = bundle.LoadAsset<Material>("EyeLash");
                eyeAlphaMaterial = bundle.LoadAsset<Material>("EyeAlpha");
                eyePupilMaterial = bundle.LoadAsset<Material>("EyePupil");
                eyeWhiteMaterial = bundle.LoadAsset<Material>("EyeWhite");

                if (null != eyeBrowMaterial && null != eyeLashMaterial && null != eyeAlphaMaterial && null != eyePupilMaterial && null != eyeWhiteMaterial)
                {
                    Console.WriteLine("#### HSSSS: FixShadow Materials Loaded");
                }
            }
        }

        private static void DeferredAssetLoader()
        {
            skinLUT = bundle.LoadAsset<Texture2D>("DeferredLUT");

            if (useTessellation)
            {
                skinMaterial = bundle.LoadAsset<Material>("SkinDeferredTessellation");
                overlayMaterial = bundle.LoadAsset<Material>("OverlayTessellation");
            }

            else
            {
                skinMaterial = bundle.LoadAsset<Material>("SkinDeferred");
                overlayMaterial = bundle.LoadAsset<Material>("Overlay");
            }

            deferredTransmissionBlit = bundle.LoadAsset<Shader>("DeferredTransmissionBlit");
            deferredBlurredNormals = bundle.LoadAsset<Shader>("DeferredBlurredNormals");
            deferredSkin = bundle.LoadAsset<Shader>("Alloy Deferred Skin");
            deferredReflections = bundle.LoadAsset<Shader>("Alloy Deferred Reflections");

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

        private static void ForwardAssetLoader()
        {
            skinMaterial = bundle.LoadAsset<Material>("SkinForward");
            overlayMaterial = bundle.LoadAsset<Material>("Overlay");

            if (useWetSpecGloss)
            {
                skinMaterial.EnableKeyword("_WET_SPECGLOSS");
            }

            if (null != skinMaterial && null != overlayMaterial)
            {
                Console.WriteLine("#### HSSSS: Forward Skin Replacer Loaded");
            }
        }

        private static void ReplaceInternalShader()
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

        private static void InitPostFX()
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

        private static void FixSpotLights()
        {
            foreach (Light light in UnityEngine.Resources.FindObjectsOfTypeAll(typeof(Light)))
            {
                if (light.type == LightType.Spot)
                {
                    if (light.cookie == null)
                    {
                        light.cookie = spotCookie;
                    }
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

            XElement root = new XElement(_name, new XAttribute("version", _version));
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
    }

    public class SkinReplacer : MonoBehaviour
    {
        private enum materialType
        {
            skinOverlay,
            femaleHead,
            femaleBody,
            maleHead,
            maleBody,
            eyePupil,
            eyeShade,
            eyeWhite,
            eyeReflex,
            eyeBrow,
            eyeLash,
        };

        private Dictionary<Material, materialType> materialsToReplace;

        private static Dictionary<string, string> colorProps = new Dictionary<string, string>()
        {
            { "_Color", "_Color" },
            { "_SpecColor", "_SpecColor"},
            { "_EmissionColor", "_EmissionColor" },
        };

        private static Dictionary<string, string> floatProps = new Dictionary<string, string>()
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

        private static Dictionary<string, string> textureProps = new Dictionary<string, string>()
        {
            { "_MainTex", "_MainTex" },
            { "_SpecGlossMap", "_SpecGlossMap" },
            { "_OcclusionMap", "_OcclusionMap" },
            { "_BumpMap", "_BumpMap" },
            { "_NormalMap", "_BumpMap" },
            { "_BlendNormalMap", "_BlendNormalMap" },
            { "_DetailNormalMap", "_DetailNormalMap" }
        };

        public void Awake()
        {
            this.materialsToReplace = new Dictionary<Material, materialType>();
        }

        public void Start()
        {
            this.StartCoroutine(this.FindAndReplace());
        }

        public void OnDestroy()
        {
            this.StopAllCoroutines();
        }

        private void GetObjects()
        {
            foreach (CharBody body in UnityEngine.Resources.FindObjectsOfTypeAll(typeof(CharBody)))
            {
                CharInfo info = body.chaInfo;

                // Body Iteration
                foreach (GameObject bodyObj in info.GetTagInfo(CharReference.TagObjKey.ObjSkinBody))
                {
                    foreach (Material bodyMat in bodyObj.GetComponent<Renderer>().sharedMaterials)
                    {
                        // Skin Materials
                        if (bodyMat.shader.name.Contains("PBRsp"))
                        {
                            if (0 == info.Sex)
                            {
                                this.materialsToReplace[bodyMat] = materialType.maleBody;
                            }

                            else if (1 == info.Sex)
                            {
                                this.materialsToReplace[bodyMat] = materialType.femaleBody;
                            }
                        }

                        // Overlay Materials
                        else if (!bodyMat.shader.name.Contains("HSSSS"))
                        {
                            this.materialsToReplace[bodyMat] = materialType.skinOverlay;
                        }
                    }
                }

                // Head Iteration
                foreach (GameObject faceObj in info.GetTagInfo(CharReference.TagObjKey.ObjSkinFace))
                {
                    foreach (Material faceMat in faceObj.GetComponent<Renderer>().sharedMaterials)
                    {
                        // Skin Materials
                        if (faceMat.shader.name.Contains("PBRsp"))
                        {
                            if (0 == info.Sex)
                            {
                                this.materialsToReplace[faceMat] = materialType.maleHead;
                            }

                            else if (1 == info.Sex)
                            {
                                this.materialsToReplace[faceMat] = materialType.femaleHead;
                            }
                        }

                        // Overlay Materials
                        else if (!faceMat.shader.name.Contains("HSSSS"))
                        {
                            this.materialsToReplace[faceMat] = materialType.skinOverlay;
                        }
                    }
                }

                if (HSSSS.fixAlphaShadow)
                {
                    this.GetObjectByTag(materialType.skinOverlay, info.GetTagInfo(CharReference.TagObjKey.ObjNip));
                    this.GetObjectByTag(materialType.eyePupil, info.GetTagInfo(CharReference.TagObjKey.ObjEyeL));
                    this.GetObjectByTag(materialType.eyePupil, info.GetTagInfo(CharReference.TagObjKey.ObjEyeR));
                    this.GetObjectByTag(materialType.eyeReflex, info.GetTagInfo(CharReference.TagObjKey.ObjEyeHi));
                    this.GetObjectByTag(materialType.eyeBrow, info.GetTagInfo(CharReference.TagObjKey.ObjEyebrow));
                    this.GetObjectByTag(materialType.eyeLash, info.GetTagInfo(CharReference.TagObjKey.ObjEyelashes));

                    this.GetEyeShade(body.objHead);
                }
            }
        }

        private void GetEyeShade(GameObject objHead)
        {
            if (null != objHead)
            {
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

                    if (!matShade.shader.name.Contains("HSSSS"))
                    {
                        this.materialsToReplace[matShade] = materialType.eyeShade;
                    }

                    if (!renderer.receiveShadows)
                    {
                        renderer.receiveShadows = true;
                    }
                }
            }
        }

        private void GetObjectByTag(materialType key, List<GameObject> objList)
        {
            foreach (GameObject obj in objList)
            {
                Renderer renderer = obj.GetComponent<Renderer>();

                foreach (Material mat in renderer.sharedMaterials)
                {
                    if (!mat.shader.name.Contains("HSSSS"))
                    {
                        this.materialsToReplace[mat] = key;
                    }
                }

                if (!renderer.receiveShadows)
                {
                    renderer.receiveShadows = true;
                }
            }
        }

        private void ReplaceMaterials()
        {
            foreach (KeyValuePair<Material, materialType> entry in this.materialsToReplace)
            {
                Console.WriteLine("#### HSSSS Replaces " + entry.Key.name);

                switch (entry.Value)
                {
                    case materialType.femaleBody:
                        this.SetMaterialProps(HSSSS.skinMaterial, entry.Key, entry.Value);
                        entry.Key.SetTexture("_Thickness", HSSSS.femaleBodyThickness);
                        break;

                    case materialType.femaleHead:
                        this.SetMaterialProps(HSSSS.skinMaterial, entry.Key, entry.Value);
                        entry.Key.SetTexture("_Thickness", HSSSS.famaleHeadThickness);
                        break;

                    case materialType.maleBody:
                        this.SetMaterialProps(HSSSS.skinMaterial, entry.Key, entry.Value);
                        entry.Key.SetTexture("_Thickness", HSSSS.maleBodyThickness);
                        break;

                    case materialType.maleHead:
                        this.SetMaterialProps(HSSSS.skinMaterial, entry.Key, entry.Value);
                        entry.Key.SetTexture("_Thickness", HSSSS.maleHeadThickness);
                        break;

                    case materialType.skinOverlay:
                        this.SetMaterialProps(HSSSS.overlayMaterial, entry.Key, entry.Value);
                        entry.Key.renderQueue = 2000;
                        break;

                    case materialType.eyePupil:
                        this.SetMaterialProps(HSSSS.eyePupilMaterial, entry.Key, entry.Value);
                        entry.Key.renderQueue = 2000;
                        break;

                    case materialType.eyeShade:
                        this.SetMaterialProps(HSSSS.eyeAlphaMaterial, entry.Key, entry.Value);
                        entry.Key.renderQueue = 2001;
                        break;

                    case materialType.eyeReflex:
                        this.SetMaterialProps(HSSSS.eyeAlphaMaterial, entry.Key, entry.Value);
                        entry.Key.renderQueue = 2002;
                        break;

                    case materialType.eyeWhite:
                        this.SetMaterialProps(HSSSS.eyeWhiteMaterial, entry.Key, entry.Value);
                        break;

                    case materialType.eyeBrow:
                        this.SetMaterialProps(HSSSS.eyeBrowMaterial, entry.Key, entry.Value);
                        entry.Key.renderQueue = 2001;
                        break;

                    case materialType.eyeLash:
                        this.SetMaterialProps(HSSSS.eyeLashMaterial, entry.Key, entry.Value);
                        entry.Key.renderQueue = 2000;
                        break;
                }
            }

            this.materialsToReplace.Clear();
        }

        private void SetMaterialProps(Material sourceMaterial, Material targetMaterial, materialType key)
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

        private IEnumerator FindAndReplace()
        {
            while (true)
            {
                this.GetObjects();
                yield return null;
                this.ReplaceMaterials();
                yield return null;
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
        
        public void Awake()
        {
            this.configWindow = new Rect(256.0f, 0.000f, 480.0f, 840.0f);
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
            GUILayout.Space(16.0f);

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

            GUILayout.Space(16.0f);

            GUILayout.Label("Transmission Weight", this.labelStyle);
            HSSSS.SSS.TransmissionSettings.Weight = this.SliderControls(HSSSS.SSS.TransmissionSettings.Weight, 0.0f, 1.0f);

            GUILayout.Label("Transmission Distortion", this.labelStyle);
            HSSSS.SSS.TransmissionSettings.BumpDistortion = this.SliderControls(HSSSS.SSS.TransmissionSettings.BumpDistortion, 0.0f, 1.0f);

            GUILayout.Label("Transmission Shadow Weight", this.labelStyle);
            HSSSS.SSS.TransmissionSettings.ShadowWeight = this.SliderControls(HSSSS.SSS.TransmissionSettings.ShadowWeight, 0.0f, 1.0f);

            GUILayout.Label("Transmission Falloff", this.labelStyle);
            HSSSS.SSS.TransmissionSettings.Falloff = this.SliderControls(HSSSS.SSS.TransmissionSettings.Falloff, 1.0f, 20.0f);

            GUILayout.Space(16.0f);

            if (GUILayout.Button("Reset Configuration", this.buttonStyle))
            {
                HSSSS.LoadConfig();
            }

            GUILayout.Space(16.0f);

            if (GUILayout.Button("Save Configuration", this.buttonStyle))
            {
                HSSSS.SaveConfig();
            }

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
    }
}
