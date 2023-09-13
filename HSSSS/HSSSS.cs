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

        // assetbundle file
        private static AssetBundle assetBundle;

        // internal deferred shaders
        private static Shader deferredShading;
        private static Shader deferredReflections;

        // camera effects
        public static CameraProjector CameraProjector = null;
        public static DeferredRenderer DeferredRenderer = null;
        public static SSAORenderer SSAORenderer = null;
        public static SSGIRenderer SSGIRenderer = null;

        private static GameObject mainCamera = null;

        // shaders
        public static Shader normalBlurShader;
        public static Shader diffuseBlurShader;
        public static Shader initSpecularShader;
        public static Shader transmissionBlitShader;
        public static Shader backFaceDepthShader;
        public static Shader temporalBlendShader;
        public static Shader ssaoShader;
        public static Shader ssgiShader;

        // textures
        public static Texture2D areaLightLUT;
        public static Texture2D pennerSkinLUT;
        public static Texture2D faceWorksSkinLUT;
        public static Texture2D faceWorksShadowLUT;
        public static Texture2D deepScatterLUT;
        public static Texture2D skinJitter;
        public static Texture3D blueNoise;

        // body materials
        private static Material skinMaterial;
        private static Material milkMaterial;
        private static Material liquidMaterial;
        private static Material overlayMaterial;
        private static Material eyeBrowMaterial;
        private static Material eyeLashMaterial;
        private static Material eyeCorneaMaterial;
        private static Material eyeScleraMaterial;
        private static Material eyeOverlayMaterial;

        private static Material headWetMaterial;
        private static Material bodyWetMaterial;

        // thickness textures
        private static Texture2D femaleBodyThickness;
        private static Texture2D femaleHeadThickness;
        private static Texture2D maleBodyThickness;
        private static Texture2D maleHeadThickness;

        // cookie for spotlight
        public static Texture2D spotCookie;

        // modprefs.ini options
        public static bool isStudio;
        public static bool isEnabled;
        public static bool hsrCompatible;
        public static bool fixAlphaShadow;
        public static bool useTessellation;
        public static bool useEyePOMShader;
        public static bool useCustomThickness;

        public static string femaleBodyCustom;
        public static string femaleHeadCustom;
        public static string maleBodyCustom;
        public static string maleHeadCustom;

        private static KeyCode[] hotKey;
        private static int uiScale;

        // ui window
        public static GUISkin windowSkin;
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
                Console.WriteLine("#### HSSSS: Could not initialize the singleton pattern");
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
                this.BaseAssetLoader();
                this.DeferredAssetLoader();
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
                    if (XmlParser.LoadExternalFile())
                    {
                        Properties.UpdateSkin();
                        Properties.UpdateSSAO();
                        Properties.UpdateSSGI();
                        Properties.UpdateShadow();
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

                        }
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
            if (isEnabled && isStudio && this.GetHotKeyPressed())
            {
                if (this.windowObj == null)
                {
                    ConfigWindow.useTessellation = useTessellation;
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
                    Properties.UpdateShadow();
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
            // tesellation skin shader
            useTessellation = ModPrefs.GetBool("HSSSS", "Tessellation", false, true);
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
                useTessellation = false;
                useEyePOMShader = false;
                useCustomThickness = false;
            }
        }

        private void BaseAssetLoader()
        {
            // hssssresources.unity3d
            assetBundle = AssetBundle.LoadFromMemory(Resources.hssssresources);

            // custom thickness texture
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

            // build-in thickness texture
            else
            {
                femaleBodyThickness = assetBundle.LoadAsset<Texture2D>("FemaleBodyThickness");
                femaleHeadThickness = assetBundle.LoadAsset<Texture2D>("FemaleHeadThickness");
                maleBodyThickness = assetBundle.LoadAsset<Texture2D>("MaleBodyThickness");
                maleHeadThickness = assetBundle.LoadAsset<Texture2D>("MaleHeadThickness");
            }

            // area light lookup texture
            areaLightLUT = assetBundle.LoadAsset<Texture2D>("AreaLightLUT");

            // sss lookup textures
            pennerSkinLUT = assetBundle.LoadAsset<Texture2D>("DefaultSkinLUT");
            faceWorksSkinLUT = assetBundle.LoadAsset<Texture2D>("FaceWorksSkinLUT");
            faceWorksShadowLUT = assetBundle.LoadAsset<Texture2D>("FaceWorksShadowLUT");
            deepScatterLUT = assetBundle.LoadAsset<Texture2D>("DeepScatterLUT");

            // jitter texture
            skinJitter = assetBundle.LoadAsset<Texture2D>("SkinJitter");
            blueNoise = assetBundle.LoadAsset<Texture3D>("BlueNoise");
            
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

            if (null == areaLightLUT)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Area Light Lookup Texture");
            }

            if (null == pennerSkinLUT || null == faceWorksSkinLUT || null == faceWorksShadowLUT || null == deepScatterLUT)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Skin Lookup Textures");
            }

            if (null == skinJitter)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Blue Noise Texture");
            }

            if (null == skinJitter)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Skin Jitter Texture");
            }

            if (null == spotCookie)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Spotlight Cookie");
            }
            #endregion
        }

        private void DeferredAssetLoader()
        {
            liquidMaterial = assetBundle.LoadAsset<Material>("Liquid");

            headWetMaterial = assetBundle.LoadAsset<Material>("CondensationHead");
            bodyWetMaterial = assetBundle.LoadAsset<Material>("CondensationBody");

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
                skinMaterial = assetBundle.LoadAsset<Material>("Skin");
                milkMaterial = assetBundle.LoadAsset<Material>("OverlayForward");
                overlayMaterial = assetBundle.LoadAsset<Material>("Overlay");
            }

            // post fx shaders
            normalBlurShader = assetBundle.LoadAsset<Shader>("ScreenSpaceNormalBlur");
            diffuseBlurShader = assetBundle.LoadAsset<Shader>("ScreenSpaceDiffuseBlur");
            initSpecularShader = assetBundle.LoadAsset<Shader>("InitSpecularBuffer");
            transmissionBlitShader = assetBundle.LoadAsset<Shader>("TransmissionBlit");
            backFaceDepthShader = assetBundle.LoadAsset<Shader>("BackFaceDepth");
            temporalBlendShader = assetBundle.LoadAsset<Shader>("TemporalBlend");
            ssaoShader = assetBundle.LoadAsset<Shader>("SSAO");
            ssgiShader = assetBundle.LoadAsset<Shader>("SSGI");

            // internal deferred & reflection shaders
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

            if (null == transmissionBlitShader || null == normalBlurShader)
            {
                Console.WriteLine("#### HSSSS: Failed to Load PostFX Shaders");
            }

            if (null == deferredShading || null == deferredReflections)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Deferred Internal Shaders");
            }

            if (null == ssaoShader)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Ambient Occlusion Shader");
            }

            if (null == ssgiShader)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Global Illumination Shader");
            }

            if (null == windowSkin)
            {
                Console.WriteLine("#### HSSSS: Failed to Load UI Skin");
            }
            #endregion

            // materials for additional replacement
            if (fixAlphaShadow)
            {
                eyeBrowMaterial = assetBundle.LoadAsset<Material>("EyeBrow");
                eyeLashMaterial = assetBundle.LoadAsset<Material>("EyeLash");
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
                    eyeCorneaMaterial = assetBundle.LoadAsset<Material>("OverlayForward");
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
                        ShaderReplacer(overlayMaterial, mat);
                        break;

                    case CharReference.TagObjKey.ObjEyelashes:
                        ShaderReplacer(eyeLashMaterial, mat);
                        break;

                    case CharReference.TagObjKey.ObjEyebrow:
                        ShaderReplacer(eyeBrowMaterial, mat);
                        if (fixAlphaShadow)
                        {
                            mat.SetFloat("_VertexWrapOffset", Properties.skin.eyebrowoffset);
                        }
                        break;

                    case CharReference.TagObjKey.ObjEyeHi:
                        ShaderReplacer(eyeOverlayMaterial, mat);
                        mat.renderQueue = 2451;
                        break;

                    case CharReference.TagObjKey.ObjEyeW:
                        ShaderReplacer(eyeScleraMaterial, mat);
                        break;

                    case CharReference.TagObjKey.ObjEyeL:
                        ShaderReplacer(eyeCorneaMaterial, mat);
                        if (useEyePOMShader)
                        {
                            mat.SetTexture("_SpecGlossMap", null);
                            mat.SetTexture("_EmissionMap", mat.GetTexture("_MainTex"));
                        }
                        break;

                    case CharReference.TagObjKey.ObjEyeR:
                        ShaderReplacer(eyeCorneaMaterial, mat);
                        if (useEyePOMShader)
                        {
                            mat.SetTexture("_SpecGlossMap", null);
                            mat.SetTexture("_EmissionMap", mat.GetTexture("_MainTex"));
                        }
                        break;

                    case CharReference.TagObjKey.ObjNip:
                        ShaderReplacer(overlayMaterial, mat);
                        break;

                    case CharReference.TagObjKey.ObjNail:
                        ShaderReplacer(skinMaterial, mat);
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
                    targetMaterial.SetFloat("_Phong", Properties.skin.phongStrength);
                }

                if (targetMaterial.HasProperty("_EdgeLength"))
                {
                    targetMaterial.SetFloat("_EdgeLength", Properties.skin.edgeLength);
                }
            }

            public static void SkinPartsReplacer(CharInfo ___chaInfo, CharReference.TagObjKey tagKey, Material mat)
            {
                if (mat != null)
                {
                    if (WillReplaceShader(mat.shader))
                    {
                        ShaderReplacer(skinMaterial, mat);

                        if (tagKey == CharReference.TagObjKey.ObjSkinBody)
                        {
                            if (___chaInfo.Sex == 0)
                            {
                                mat.SetTexture("_Thickness", maleBodyThickness);
                            }

                            else if (___chaInfo.Sex == 1)
                            {
                                mat.SetTexture("_Thickness", femaleBodyThickness);
                            }
                        }

                        else if (tagKey == CharReference.TagObjKey.ObjSkinFace)
                        {
                            if (___chaInfo.Sex == 0)
                            {
                                mat.SetTexture("_Thickness", maleHeadThickness);
                            }

                            else if (___chaInfo.Sex == 1)
                            {
                                mat.SetTexture("_Thickness", femaleHeadThickness);
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

                            /*
                            else
                            {
                                ShaderReplacer(milkMaterial, juiceMat);
                                juiceMat.SetColor("_Color", new Color(0.8f, 0.8f, 0.8f, 0.2f));
                                Console.WriteLine("#### HSSSS Replaced " + juiceMat.name);
                            }
                            */
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
                                ShaderReplacer(overlayMaterial, material);

                                if (useTessellation)
                                {
                                    material.SetFloat("_Phong", Properties.skin.phongStrength);
                                    material.SetFloat("_EdgeLength", Properties.skin.edgeLength);
                                }
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
                                ShaderReplacer(skinMaterial, material);
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
                                        ShaderReplacer(eyeOverlayMaterial, material);
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
                                            material.shader = liquidMaterial.shader;
                                            material.CopyPropertiesFromMaterial(liquidMaterial);
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
