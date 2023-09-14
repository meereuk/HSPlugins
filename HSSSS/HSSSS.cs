using System;
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
    public class HSSSS : IEnhancedPlugin
    {
        #region Plugin Info
        public string Name { get { return "HSSSS";  } }
        public string Version { get { return "1.7.0"; } }
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
        private static GameObject mainCamera = null;

        // modprefs.ini options
        public static bool isStudio;
        public static bool isEnabled;
        public static bool hsrCompatible;
        public static bool fixAlphaShadow;
        public static bool useEyePOMShader;
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
                Console.WriteLine("#### HSSSS: It seems HSSSS is totally fucked up :P");
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

                #region Harmony
                HarmonyInstance harmony = HarmonyInstance.Create("com.kkul.hssss");

                if (!hsrCompatible)
                {
                    harmony.Patch(
                        AccessTools.Method(typeof(CharCustom), nameof(CharCustom.SetBaseMaterial)), null,
                        new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.SkinPartsReplacer))
                        );

                    harmony.Patch(
                        AccessTools.Method(typeof(CharFemaleCustom), nameof(CharFemaleCustom.ChangeNailColor)), null,
                        new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.NailReplacer))
                        );

                    if (fixAlphaShadow)
                    {
                        harmony.Patch(
                            AccessTools.Method(typeof(CharFemaleBody), nameof(CharFemaleBody.Reload)), null,
                            new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.MiscReplacer))
                            );

                        harmony.Patch(
                            AccessTools.Method(typeof(Manager.Character), nameof(Manager.Character.Awake)), null,
                            new HarmonyMethod(typeof(SkinReplacer), nameof(SkinReplacer.JuicesReplacer))
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
                    }
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
                #endregion
            }
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnLevelWasLoaded(int level)
        {
            if (isEnabled && level >= 3)
            {
                if (this.PostFxInitializer())
                {
                    Console.WriteLine("#### HSSSS: Successfully initialized the camera effects");

                    Properties.UpdateSkin();
                    Properties.UpdateSSAO();
                    Properties.UpdateSSGI();
                    Properties.UpdatePCSS();
                    Properties.UpdateMaterials();
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Couldn't initialize the camera effects");
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
                    ConfigWindow.fixAlphaShadow = fixAlphaShadow;
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
            if (node != null)
            {
                try
                {
                    XmlParser.LoadXml(node);
                    Properties.UpdateSkin();
                    Properties.UpdateSSAO();
                    Properties.UpdateSSGI();
                    Properties.UpdatePCSS();
                    Properties.UpdateMaterials();
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
            // shadow fix for overlay materials
            fixAlphaShadow = ModPrefs.GetBool("HSSSS", "FixShadow", true, true);
            // dedicated eye shader which supports pom/sss
            useEyePOMShader = ModPrefs.GetBool("HSSSS", "EyePOMShader", true, true);
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

            if (hsrCompatible)
            {
                fixAlphaShadow = false;
                useEyePOMShader = false;
                useCustomThickness = false;
            }
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

        private bool PostFxInitializer()
        {
            if (isStudio)
            {
                mainCamera = GameObject.Find("StudioScene/Camera/Main Camera");
                mainCamera.GetComponent<Camera>();
            }

            else
            {
                if (Camera.main != null)
                {
                    mainCamera = Camera.main.gameObject;
                    mainCamera.GetComponent<Camera>();
                }
            }

            if (null != mainCamera)
            {
                if (CameraProjector == null)
                {
                    CameraProjector = mainCamera.gameObject.AddComponent<CameraProjector>();
                }

                if (DeferredRenderer == null)
                {
                    DeferredRenderer = mainCamera.gameObject.AddComponent<DeferredRenderer>();
                }

                if (SSAORenderer == null)
                {
                    SSAORenderer = mainCamera.gameObject.AddComponent<SSAORenderer>();
                }

                if (SSGIRenderer == null)
                {
                    SSGIRenderer = mainCamera.gameObject.AddComponent<SSGIRenderer>();
                }

                if (CameraProjector == null)
                {
                    Console.WriteLine("#### HSSSS: Failed to Initialize Camera Projector");
                }

                if (DeferredRenderer == null || SSAORenderer == null || SSGIRenderer == null)
                {
                    Console.WriteLine("#### HSSSS: Failed to Initialize Post FX");
                }

                else
                {
                    return true;
                }
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
            if (__instance.lightType == LightType.Directional || __instance.lightType == LightType.Spot)
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
                        ShaderReplacer(AssetLoader.overlay, mat);
                        break;

                    case CharReference.TagObjKey.ObjEyelashes:
                        ShaderReplacer(AssetLoader.eyelash, mat);
                        break;

                    case CharReference.TagObjKey.ObjEyebrow:
                        ShaderReplacer(AssetLoader.eyebrow, mat);
                        break;

                    case CharReference.TagObjKey.ObjEyeHi:
                        ShaderReplacer(AssetLoader.eyeOverlay, mat);
                        mat.renderQueue = 2451;
                        break;

                    case CharReference.TagObjKey.ObjEyeW:
                        ShaderReplacer(AssetLoader.sclera, mat);
                        break;

                    case CharReference.TagObjKey.ObjEyeL:
                        ShaderReplacer(AssetLoader.cornea, mat);
                        if (useEyePOMShader)
                        {
                            mat.SetTexture("_SpecGlossMap", null);
                            mat.SetTexture("_EmissionMap", mat.GetTexture("_MainTex"));
                        }
                        break;

                    case CharReference.TagObjKey.ObjEyeR:
                        ShaderReplacer(AssetLoader.cornea, mat);
                        if (useEyePOMShader)
                        {
                            mat.SetTexture("_SpecGlossMap", null);
                            mat.SetTexture("_EmissionMap", mat.GetTexture("_MainTex"));
                        }
                        break;

                    case CharReference.TagObjKey.ObjNip:
                        ShaderReplacer(AssetLoader.overlay, mat);
                        break;

                    case CharReference.TagObjKey.ObjNail:
                        ShaderReplacer(AssetLoader.skin, mat);
                        mat.SetTexture("_DetailNormalMap_2", null);
                        mat.SetTexture("_DetailNormalMap_3", null);
                        mat.SetTexture("_DetailSkinPoreMap", null);
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
                        if (targetMaterial.GetTexture(entry.Key) == null)
                        {
                            targetMaterial.SetTexture(entry.Value, cacheMat.GetTexture(entry.Key));
                            targetMaterial.SetTextureScale(entry.Value, cacheMat.GetTextureScale(entry.Key));
                            targetMaterial.SetTextureOffset(entry.Value, cacheMat.GetTextureOffset(entry.Key));
                        }
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

                // microdetails intensity
                if (targetMaterial.HasProperty("_DetailNormalMapScale_2"))
                {
                    targetMaterial.SetFloat("_DetailNormalMapScale_2", Properties.skin.microDetailWeight_1);
                }

                if (targetMaterial.HasProperty("_DetailNormalMapScale_3"))
                {
                    targetMaterial.SetFloat("_DetailNormalMapScale_3", Properties.skin.microDetailWeight_2);
                }

                // microdetails tiling
                Vector2 tiling = new Vector2(Math.Max(Properties.skin.microDetailTiling, 0.01f), Math.Max(Properties.skin.microDetailTiling, 0.01f));

                if (targetMaterial.HasProperty("_DetailNormalMap_2"))
                {
                    targetMaterial.SetTextureScale("_DetailNormalMap_2", tiling);
                }

                if (targetMaterial.HasProperty("_DetailNormalMap_3"))
                {
                    targetMaterial.SetTextureScale("_DetailNormalMap_3", tiling);
                }

                if (targetMaterial.HasProperty("_DetailSkinPoreMap"))
                {
                    targetMaterial.SetTextureScale("_DetailSkinPoreMap", tiling);
                }

                // tessellation
                if (targetMaterial.HasProperty("_Phong"))
                {
                    targetMaterial.SetFloat("_Phong", Properties.tess.phong);
                }

                if (targetMaterial.HasProperty("_EdgeLength"))
                {
                    targetMaterial.SetFloat("_EdgeLength", Properties.tess.edge);
                }
            }

            public static void SkinPartsReplacer(CharInfo ___chaInfo, CharReference.TagObjKey tagKey, Material mat)
            {
                if (mat != null)
                {
                    if (WillReplaceShader(mat.shader))
                    {
                        ShaderReplacer(AssetLoader.skin, mat);

                        if (tagKey == CharReference.TagObjKey.ObjSkinBody)
                        {
                            if (___chaInfo.Sex == 0)
                            {
                                mat.SetTexture("_Thickness", AssetLoader.maleBody);
                            }

                            else if (___chaInfo.Sex == 1)
                            {
                                mat.SetTexture("_Thickness", AssetLoader.femaleBody);
                            }
                        }

                        else if (tagKey == CharReference.TagObjKey.ObjSkinFace)
                        {
                            if (___chaInfo.Sex == 0)
                            {
                                mat.SetTexture("_Thickness", AssetLoader.maleHead);
                            }

                            else if (___chaInfo.Sex == 1)
                            {
                                mat.SetTexture("_Thickness", AssetLoader.femaleHead);
                            }
                        }

                        Console.WriteLine("#### HSSSS Replaced " + mat);
                    }
                }
            }

            public static void CommonPartsReplacer(CharInfo ___chaInfo, CharReference.TagObjKey key)
            {
                foreach (GameObject obj in ___chaInfo.GetTagInfo(key))
                {
                    foreach (Renderer rend in obj.GetComponents<Renderer>())
                    {
                        foreach (Material mat in rend.sharedMaterials)
                        {
                            ObjectParser(mat, key);
                            Console.WriteLine("#### HSSSS Replaced " + mat.name);
                        }

                        if (!rend.receiveShadows)
                        {
                            rend.receiveShadows = true;
                        }
                    }
                }
            }

            public static void ScleraReplacer(CharInfo ___chaInfo)
            {
                CommonPartsReplacer(___chaInfo, CharReference.TagObjKey.ObjEyeW);
            }

            public static void NailReplacer(CharInfo ___chaInfo)
            {
                CommonPartsReplacer(___chaInfo, CharReference.TagObjKey.ObjNail);
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
                            /*
                            switch (juiceMat.name)
                            {
                                case "cf_M_k_kaosiru01":
                                    juiceMat.shader = headWetMaterial.shader;
                                    juiceMat.CopyPropertiesFromMaterial(headWetMaterial);
                                    break;

                                case "cf_M_k_munesiru01":
                                    juiceMat.shader = bodyWetMaterial.shader;
                                    juiceMat.CopyPropertiesFromMaterial(bodyWetMaterial);
                                    break;

                                default:
                                    ShaderReplacer(milkMaterial, juiceMat);
                                    juiceMat.SetColor("_Color", new Color(0.8f, 0.8f, 0.8f, 0.2f));
                                    Console.WriteLine("#### HSSSS Replaced " + juiceMat.name);
                                    break;
                            }
                            */
                            ShaderReplacer(AssetLoader.milk, juiceMat);
                            juiceMat.SetColor("_Color", new Color(0.8f, 0.8f, 0.8f, 0.2f));
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
                        ShaderReplacer(AssetLoader.overlay, __instance.matHohoAka);
                        Console.WriteLine("#### HSSSS Replaced " + __instance.matHohoAka.name);
                    }
                }

                List<GameObject> faceObjs = __instance.chaInfo.GetTagInfo(CharReference.TagObjKey.ObjSkinFace);

                if (faceObjs.Count > 0)
                {
                    Renderer renderer = faceObjs[0].GetComponent<Renderer>();

                    if (renderer && renderer.materials.Length > 1)
                    {
                        Material material = renderer.materials[1];

                        if (material)
                        {
                            if (WillReplaceShader(material.shader))
                            {
                                ShaderReplacer(AssetLoader.overlay, material);
                                Console.WriteLine("#### HSSSS Replaced " + material.name);
                            }
                        }
                    }
                }

                // tongue
                GameObject objHead = __instance.objHead;

                if (objHead)
                {
                    GameObject objTongue = null;

                    if (objHead.transform.Find("cf_N_head/cf_O_sita"))
                    {
                        objTongue = objHead.transform.Find("cf_N_head/cf_O_sita").gameObject;
                    }

                    if (objTongue)
                    {
                        Material material = objTongue.GetComponent<Renderer>().material;

                        if (material)
                        {
                            if (WillReplaceShader(material.shader))
                            {
                                ShaderReplacer(AssetLoader.skin, material);
                                Console.WriteLine("#### HSSSS Replaced " + material.name);
                            }
                        }
                    }
                }

                if (fixAlphaShadow)
                {
                    if (objHead)
                    {
                        // eye shade
                        GameObject objShade = null;

                        if (objHead.transform.Find("cf_N_head/cf_O_eyekage"))
                        {
                            objShade = objHead.transform.Find("cf_N_head/cf_O_eyekage").gameObject;
                        }

                        else if (objHead.transform.Find("cf_N_head/cf_O_eyekage1"))
                        {
                            objShade = objHead.transform.Find("cf_N_head/cf_O_eyekage1").gameObject;
                        }

                        if (objShade)
                        {
                            Renderer renderer = objShade.GetComponent<Renderer>();

                            if (renderer)
                            {
                                if (!renderer.receiveShadows)
                                {
                                    renderer.receiveShadows = true;
                                }

                                Material material = renderer.sharedMaterial;

                                if (material)
                                {
                                    if (WillReplaceShader(material.shader))
                                    {
                                        ShaderReplacer(AssetLoader.eyeOverlay, material);
                                        material.renderQueue = 2452;
                                        Console.WriteLine("#### HSSSS Replaced " + material.name);
                                    }
                                }
                            }
                        }

                        // tears
                        for (int i = 1; i < 4; i++)
                        {
                            GameObject objTears = objHead.transform.Find("cf_N_head/N_namida/cf_O_namida" + i.ToString("00")).gameObject;

                            if (objTears)
                            {
                                Renderer renderer = objTears.GetComponent<Renderer>();

                                if (renderer)
                                {
                                    if (!renderer.receiveShadows)
                                    {
                                        renderer.receiveShadows = true;
                                    }

                                    Material material = renderer.sharedMaterial;

                                    if (material)
                                    {
                                        if (WillReplaceShader(material.shader))
                                        {
                                            material.shader = AssetLoader.liquid.shader;
                                            material.CopyPropertiesFromMaterial(AssetLoader.liquid);
                                            /*
                                            ShaderReplacer(liquidMaterial, material);
                                            material.SetColor("_Color", new Color(0.6f, 0.6f, 0.6f, 0.6f));
                                            material.SetColor("_EmissionColor", new Color(0.0f, 0.0f, 0.0f, 1.0f));
                                            material.renderQueue = 2453;
                                            Console.WriteLine("#### HSSSS Replaced " + material.name);
                                            */
                                        }
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
}
