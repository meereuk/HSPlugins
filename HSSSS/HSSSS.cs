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
        public string Version { get { return "1.4.1"; } }
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
        public static Shader contactShadowShader;
        public static Shader temporalBlendShader;
        public static Shader ssaoShader;
        public static Shader ssgiShader;

        // textures
        public static Texture2D pennerSkinLUT;
        public static Texture2D faceWorksSkinLUT;
        public static Texture2D faceWorksShadowLUT;
        public static Texture2D deepScatterLUT;
        public static Texture2D skinJitter;
        public static Texture2D shadowJitter;

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

                    this.RefreshConfig(false, true);
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
                    this.LoadConfig(node);
                    this.RefreshConfig(false, true);
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
                this.SaveConfig(writer);
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

            // sss lookup textures
            pennerSkinLUT = assetBundle.LoadAsset<Texture2D>("DefaultSkinLUT");
            faceWorksSkinLUT = assetBundle.LoadAsset<Texture2D>("FaceWorksSkinLUT");
            faceWorksShadowLUT = assetBundle.LoadAsset<Texture2D>("FaceWorksShadowLUT");
            deepScatterLUT = assetBundle.LoadAsset<Texture2D>("DeepScatterLUT");

            // jitter texture
            skinJitter = assetBundle.LoadAsset<Texture2D>("SkinJitter");
            shadowJitter = assetBundle.LoadAsset<Texture2D>("ShadowJitter");
            
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

            if (null == pennerSkinLUT || null == faceWorksSkinLUT || null == faceWorksShadowLUT || null == deepScatterLUT)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Skin Lookup Textures");
            }

            if (null == skinJitter)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Skin Jitter Texture");
            }

            if (null == shadowJitter)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Shadow Jitter Texture");
            }

            if (null == spotCookie)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Spotlight Cookie");
            }
            #endregion
        }

        private void DeferredAssetLoader()
        {
            liquidMaterial = assetBundle.LoadAsset<Material>("OverlayForward");

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
            contactShadowShader = assetBundle.LoadAsset<Shader>("ScreenSpaceShadow");
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

            if (null == contactShadowShader)
            {
                Console.WriteLine("#### HSSSS: Failed to Load Contact Shadow Shader");
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

        public void RefreshConfig(bool softRefresh, bool skinRefresh)
        {
            this.UpdateShadowConfig();

            if (!hsrCompatible)
            {
                this.UpdateOtherConfig(skinRefresh);
                //DeferredRenderer.ImportSettings();

                if (softRefresh)
                {
                    DeferredRenderer.Refresh();
                }

                else
                {
                    DeferredRenderer.ForceRefresh();
                }
            }
        }

        private void UpdateShadowConfig()
        {
            Shader.DisableKeyword("_PCF_TAPS_8");
            Shader.DisableKeyword("_PCF_TAPS_16");
            Shader.DisableKeyword("_PCF_TAPS_32");
            Shader.DisableKeyword("_PCF_TAPS_64");
            Shader.DisableKeyword("_PCSS_ON");

            if (isStudio)
            {
                switch (Properties.shadow.pcfState)
                {
                    case Properties.PCFState.disable:
                        Shader.DisableKeyword("_PCSS_ON");
                        Properties.shadow.pcssEnabled = false;
                        break;

                    case Properties.PCFState.poisson8x:
                        Shader.EnableKeyword("_PCF_TAPS_8");
                        Shader.SetGlobalTexture("_ShadowJitterTexture", shadowJitter);
                        break;

                    case Properties.PCFState.poisson16x:
                        Shader.EnableKeyword("_PCF_TAPS_16");
                        Shader.SetGlobalTexture("_ShadowJitterTexture", shadowJitter);
                        break;

                    case Properties.PCFState.poisson32x:
                        Shader.EnableKeyword("_PCF_TAPS_32");
                        Shader.SetGlobalTexture("_ShadowJitterTexture", shadowJitter);
                        break;

                    case Properties.PCFState.poisson64x:
                        Shader.EnableKeyword("_PCF_TAPS_64");
                        Shader.SetGlobalTexture("_ShadowJitterTexture", shadowJitter);
                        break;
                }

                if (Properties.shadow.pcssEnabled)
                {
                    Shader.EnableKeyword("_PCSS_ON");
                }

                // pcf & pcss
                Shader.SetGlobalVector("_DirLightPenumbra", Properties.shadow.dirLightPenumbra);
                Shader.SetGlobalVector("_SpotLightPenumbra", Properties.shadow.spotLightPenumbra);
                Shader.SetGlobalVector("_PointLightPenumbra", Properties.shadow.pointLightPenumbra);
            }
        }

        private void UpdateSkinLoop(CharInfo chaInfo, CharReference.TagObjKey key)
        {
            foreach (GameObject body in chaInfo.GetTagInfo(key))
            {
                foreach (Renderer rend in body.GetComponents<Renderer>())
                {
                    foreach (Material mat in rend.sharedMaterials)
                    {
                        if (mat.HasProperty("_DetailNormalMapScale_2"))
                        {
                            mat.SetFloat("_DetailNormalMapScale_2", Properties.skin.microDetailWeight_1);
                        }

                        if (mat.HasProperty("_DetailNormalMapScale_3"))
                        {
                            mat.SetFloat("_DetailNormalMapScale_3", Properties.skin.microDetailWeight_2);
                        }

                        if (mat.HasProperty("_DetailNormalMap_2"))
                        {
                            mat.SetTextureScale("_DetailNormalMap_2", new Vector2(Properties.skin.microDetailTiling, Properties.skin.microDetailTiling));
                        }

                        if (mat.HasProperty("_DetailNormalMap_3"))
                        {
                            mat.SetTextureScale("_DetailNormalMap_3", new Vector2(Properties.skin.microDetailTiling, Properties.skin.microDetailTiling));
                        }

                        if (mat.HasProperty("_DetailSkinPoreMap"))
                        {
                            mat.SetTextureScale("_DetailSkinPoreMap", new Vector2(Properties.skin.microDetailTiling, Properties.skin.microDetailTiling));
                        }

                        if (mat.HasProperty("_Phong"))
                        {
                            mat.SetFloat("_Phong", Properties.skin.phongStrength);
                        }

                        if (mat.HasProperty("_EdgeLength"))
                        {
                            mat.SetFloat("_EdgeLength", Properties.skin.edgeLength);
                        }

                        if (mat.HasProperty("_VertexWrapOffset"))
                        {
                            mat.SetFloat("_VertexWrapOffset", Properties.skin.eyebrowoffset);
                        }
                    }
                }
            }
        }

        private void UpdateOtherConfig(bool skinRefresh)
        {
            if (skinRefresh)
            {
                var CharacterManager = Singleton<Manager.Character>.Instance;

                foreach (KeyValuePair<int, CharFemale> female in CharacterManager.dictFemale)
                {
                    UpdateSkinLoop(female.Value, CharReference.TagObjKey.ObjSkinBody);
                    UpdateSkinLoop(female.Value, CharReference.TagObjKey.ObjSkinFace);
                    UpdateSkinLoop(female.Value, CharReference.TagObjKey.ObjUnderHair);
                    UpdateSkinLoop(female.Value, CharReference.TagObjKey.ObjNail);
                    UpdateSkinLoop(female.Value, CharReference.TagObjKey.ObjEyebrow);
                }

                foreach (KeyValuePair<int, CharMale> male in CharacterManager.dictMale)
                {
                    UpdateSkinLoop(male.Value, CharReference.TagObjKey.ObjSkinBody);
                    UpdateSkinLoop(male.Value, CharReference.TagObjKey.ObjSkinFace);
                    UpdateSkinLoop(male.Value, CharReference.TagObjKey.ObjUnderHair);
                    UpdateSkinLoop(male.Value, CharReference.TagObjKey.ObjNail);
                    UpdateSkinLoop(male.Value, CharReference.TagObjKey.ObjEyebrow);
                }
            }
        }
        #endregion

        #region Presets
        public bool LoadExternalConfig()
        {
            try
            {
                XmlDocument config = new XmlDocument();
                config.Load(configFile);
                this.LoadConfig(config.LastChild);
                this.RefreshConfig(false, true);

                return true;
            }
            
            catch
            {
                return false;
            }
        }

        public bool SaveExternalConfig()
        {
            try
            {
                XmlWriterSettings settings = new XmlWriterSettings() { Indent = true };
                XmlWriter writer = XmlWriter.Create(configFile, settings);
                writer.WriteStartElement("HSSSS");
                this.SaveConfig(writer);
                writer.WriteEndElement();
                writer.Close();

                return true;
            }

            catch
            {
                return false;
            }
        }

        private void SaveConfig(XmlWriter writer)
        {
            writer.WriteAttributeString("version", pluginVersion);
            // skin scattering
            writer.WriteStartElement("SkinScattering");
            {
                writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.skin.sssEnabled));
                // scattering weight
                writer.WriteElementString("Weight", XmlConvert.ToString(Properties.skin.sssWeight));
                // scattering profile
                writer.WriteElementString("BRDF", Convert.ToString(Properties.skin.lutProfile));

                // diffusion brdf control
                writer.WriteStartElement("Diffusion");
                {
                    writer.WriteElementString("Scale", XmlConvert.ToString(Properties.skin.skinLutScale));
                    writer.WriteElementString("Bias", XmlConvert.ToString(Properties.skin.skinLutBias));
                }
                writer.WriteEndElement();

                // shadow brdf control
                writer.WriteStartElement("Shadow");
                {
                    writer.WriteElementString("Scale", XmlConvert.ToString(Properties.skin.shadowLutScale));
                    writer.WriteElementString("Bias", XmlConvert.ToString(Properties.skin.shadowLutBias));
                }
                writer.WriteEndElement();

                // normal blur
                writer.WriteStartElement("NormalBlur");
                {
                    writer.WriteElementString("Weight", XmlConvert.ToString(Properties.skin.normalBlurWeight));
                    writer.WriteElementString("Radius", XmlConvert.ToString(Properties.skin.normalBlurRadius));
                    writer.WriteElementString("CorrectionDepth", XmlConvert.ToString(Properties.skin.normalBlurDepthRange));
                    writer.WriteElementString("Iterations", XmlConvert.ToString(Properties.skin.normalBlurIter));
                }
                writer.WriteEndElement();

                // ao bleeding
                writer.WriteStartElement("AOBleeding");
                {
                    writer.WriteElementString("Red", XmlConvert.ToString(Properties.skin.colorBleedWeights.x));
                    writer.WriteElementString("Green", XmlConvert.ToString(Properties.skin.colorBleedWeights.y));
                    writer.WriteElementString("Blue", XmlConvert.ToString(Properties.skin.colorBleedWeights.z));
                }
                writer.WriteEndElement();

                // light absorption
                writer.WriteStartElement("Absorption");
                {
                    writer.WriteElementString("Red", XmlConvert.ToString(Properties.skin.transAbsorption.x));
                    writer.WriteElementString("Green", XmlConvert.ToString(Properties.skin.transAbsorption.y));
                    writer.WriteElementString("Blue", XmlConvert.ToString(Properties.skin.transAbsorption.z));
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            // transmission
            writer.WriteStartElement("Transmission");
            {
                writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.skin.transEnabled));
                // baked thickness
                writer.WriteElementString("BakedThickness", XmlConvert.ToString(Properties.skin.bakedThickness));
                // transmission weight
                writer.WriteElementString("Weight", XmlConvert.ToString(Properties.skin.transWeight));
                // normal distortion
                writer.WriteElementString("NormalDistortion", XmlConvert.ToString(Properties.skin.transDistortion));
                // shadow weight
                writer.WriteElementString("ShadowWeight", XmlConvert.ToString(Properties.skin.transShadowWeight));
                // falloff
                writer.WriteElementString("FallOff", XmlConvert.ToString(Properties.skin.transFalloff));
                // thickness bias
                writer.WriteElementString("ThicknessBias", XmlConvert.ToString(Properties.skin.thicknessBias));
            }
            writer.WriteEndElement();
            // shadow
            writer.WriteStartElement("SoftShadow");
            {
                // pcf state
                writer.WriteAttributeString("State", Convert.ToString(Properties.shadow.pcfState));
                // pcss soft shadow
                writer.WriteElementString("PCSS", XmlConvert.ToString(Properties.shadow.pcssEnabled));
                // directional light
                writer.WriteStartElement("Directional");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(Properties.shadow.dirLightPenumbra.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(Properties.shadow.dirLightPenumbra.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(Properties.shadow.dirLightPenumbra.z));
                }
                writer.WriteEndElement();
                // spot light
                writer.WriteStartElement("Spot");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(Properties.shadow.spotLightPenumbra.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(Properties.shadow.spotLightPenumbra.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(Properties.shadow.spotLightPenumbra.z));
                }
                writer.WriteEndElement();
                // spot light
                writer.WriteStartElement("Point");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(Properties.shadow.pointLightPenumbra.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(Properties.shadow.pointLightPenumbra.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(Properties.shadow.pointLightPenumbra.z));
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            // miscellaneous
            writer.WriteStartElement("Miscellaneous");
            {
                // skin microdetails
                writer.WriteStartElement("MicroDetails");
                {
                    writer.WriteElementString("Weight_1", XmlConvert.ToString(Properties.skin.microDetailWeight_1));
                    writer.WriteElementString("Weight_2", XmlConvert.ToString(Properties.skin.microDetailWeight_2));
                    writer.WriteElementString("Tiling", XmlConvert.ToString(Properties.skin.microDetailTiling));
                }
                writer.WriteEndElement();
                // tessellation
                writer.WriteStartElement("Tessellation");
                {
                    writer.WriteElementString("Phong", XmlConvert.ToString(Properties.skin.phongStrength));
                    writer.WriteElementString("EdgeLength", XmlConvert.ToString(Properties.skin.edgeLength));
                }
                writer.WriteEndElement();
                // eyebrow wrap
                writer.WriteElementString("EyebrowOffset", XmlConvert.ToString(Properties.skin.eyebrowoffset));
            }
            writer.WriteEndElement();
        }

        private void LoadConfig(XmlNode node)
        {
            foreach (XmlNode child0 in node.ChildNodes)
            {
                switch (child0.Name)
                {
                    // skin scattering
                    case "SkinScattering":
                        // enabled?
                        Properties.skin.sssEnabled = XmlConvert.ToBoolean(child0.Attributes["Enabled"].Value);

                        foreach (XmlNode child1 in child0.ChildNodes)
                        {
                            switch (child1.Name)
                            {
                                // scattering weight
                                case "Weight":
                                    Properties.skin.sssWeight = XmlConvert.ToSingle(child1.InnerText);
                                    break;
                                // pre-integrated brdf
                                case "BRDF":
                                    Properties.skin.lutProfile = (Properties.LUTProfile)Enum.Parse(typeof(Properties.LUTProfile), child1.InnerText);
                                    break;
                                // skin lookup
                                case "Diffusion":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Scale":
                                                Properties.skin.skinLutScale = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Bias":
                                                Properties.skin.skinLutBias = XmlConvert.ToSingle(child2.InnerText);
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
                                                Properties.skin.shadowLutScale = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Bias":
                                                Properties.skin.shadowLutBias = XmlConvert.ToSingle(child2.InnerText);
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
                                                Properties.skin.normalBlurWeight = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Radius":
                                                Properties.skin.normalBlurRadius = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "CorrectionDepth":
                                                Properties.skin.normalBlurDepthRange = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Iterations":
                                                Properties.skin.normalBlurIter = XmlConvert.ToInt32(child2.InnerText);
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
                                                Properties.skin.colorBleedWeights.x = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Green":
                                                Properties.skin.colorBleedWeights.y = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Blue":
                                                Properties.skin.colorBleedWeights.z = XmlConvert.ToSingle(child2.InnerText);
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
                                                Properties.skin.transAbsorption.x = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Green":
                                                Properties.skin.transAbsorption.y = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Blue":
                                                Properties.skin.transAbsorption.z = XmlConvert.ToSingle(child2.InnerText);
                                                break;
                                        }
                                    }
                                    break;

                            }
                        }
                        break;

                    case "Transmission":
                        // enabled?
                        Properties.skin.transEnabled = XmlConvert.ToBoolean(child0.Attributes["Enabled"].Value);

                        foreach (XmlNode child1 in child0.ChildNodes)
                        {
                            switch (child1.Name)
                            {
                                case "BakedThickness":
                                    Properties.skin.bakedThickness = XmlConvert.ToBoolean(child1.InnerText);
                                    break;

                                case "Weight":
                                    Properties.skin.transWeight = XmlConvert.ToSingle(child1.InnerText);
                                    break;

                                case "NormalDistortion":
                                    Properties.skin.transDistortion = XmlConvert.ToSingle(child1.InnerText);
                                    break;

                                case "ShadowWeight":
                                    Properties.skin.transShadowWeight = XmlConvert.ToSingle(child1.InnerText);
                                    break;

                                case "FallOff":
                                    Properties.skin.transFalloff = XmlConvert.ToSingle(child1.InnerText);
                                    break;

                                case "ThicknessBias":
                                    Properties.skin.thicknessBias = XmlConvert.ToSingle(child1.InnerText);
                                    break;
                            }
                        }
                        break;

                    case "SoftShadow":
                        // pcf kernel size
                        Properties.shadow.pcfState = (Properties.PCFState)Enum.Parse(typeof(Properties.PCFState), child0.Attributes["State"].Value);
                        // soft shadow for directional lights
                        foreach (XmlNode child1 in child0.ChildNodes)
                        {
                            switch (child1.Name)
                            {
                                case "PCSS":
                                    Properties.shadow.pcssEnabled = XmlConvert.ToBoolean(child1.InnerText);
                                    break;

                                case "Directional":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "SearchRadius":
                                                Properties.shadow.dirLightPenumbra.x = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "LightRadius":
                                                Properties.shadow.dirLightPenumbra.y = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "MinPenumbra":
                                                Properties.shadow.dirLightPenumbra.z = XmlConvert.ToSingle(child2.InnerText);
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
                                                Properties.shadow.spotLightPenumbra.x = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "LightRadius":
                                                Properties.shadow.spotLightPenumbra.y = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "MinPenumbra":
                                                Properties.shadow.spotLightPenumbra.z = XmlConvert.ToSingle(child2.InnerText);
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
                                                Properties.shadow.pointLightPenumbra.x = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "LightRadius":
                                                Properties.shadow.pointLightPenumbra.y = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "MinPenumbra":
                                                Properties.shadow.pointLightPenumbra.z = XmlConvert.ToSingle(child2.InnerText);
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
                                case "MicroDetails":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Weight_1":
                                                Properties.skin.microDetailWeight_1 = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Weight_2":
                                                Properties.skin.microDetailWeight_2 = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "Tiling":
                                                Properties.skin.microDetailTiling = XmlConvert.ToSingle(child2.InnerText);
                                                break;
                                        }
                                    }
                                    break;

                                // tessellation
                                case "Tessellation":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Phong":
                                                Properties.skin.phongStrength = XmlConvert.ToSingle(child2.InnerText);
                                                break;

                                            case "EdgeLength":
                                                Properties.skin.edgeLength = XmlConvert.ToSingle(child2.InnerText);
                                                break;
                                        }
                                    }
                                    break;

                                // eyebrow wrap
                                case "EyebrowOffset":
                                    Properties.skin.eyebrowoffset = XmlConvert.ToSingle(child1.InnerText);
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
            if (__instance.lightType == LightType.Spot)
            {
                if (__instance.light.gameObject.GetComponent<CookieUpdater>() == null)
                {
                    __instance.light.gameObject.AddComponent<CookieUpdater>();
                }

                /*
                Renderer rend = __instance.light.gameObject.GetComponent<Renderer>();

                if (null == __instance.light.cookie)
                {
                    __instance.light.cookie = spotCookie;
                }

                Renderer rend = __instance.light.gameObject.GetComponent<Renderer>();

                if (rend == null)
                {
                    rend = __instance.light.gameObject.AddComponent<MeshRenderer>();
                    rend.material = new Material(Shader.Find("Standard"));
                    rend.material.SetTexture("_MainTex", spotCookie);

                    __instance.light.cookie = spotCookie;
                }

                else
                {
                    __instance.light.cookie = rend.material.GetTexture("_MainTex");
                }
                */
            }
        }

        private static void ShadowMapPatcher(OCILight __instance)
        {
            if (__instance.lightType == LightType.Directional)
            {
                if (__instance.light.gameObject.GetComponent<ShadowMapDispatcher>() == null)
                {
                    __instance.light.gameObject.AddComponent<ShadowMapDispatcher>();
                }
            }

            /*
            if (__instance.light.gameObject.GetComponent<ContactShadowSampler>() == null)
            {
                __instance.light.gameObject.AddComponent<ContactShadowSampler>();
            }
            */
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
                if (targetMaterial.HasProperty("_DetailNormalMap_2"))
                {
                    targetMaterial.SetTextureScale("_DetailNormalMap_2", new Vector2(Properties.skin.microDetailTiling, Properties.skin.microDetailTiling));
                }

                if (targetMaterial.HasProperty("_DetailNormalMap_3"))
                {
                    targetMaterial.SetTextureScale("_DetailNormalMap_3", new Vector2(Properties.skin.microDetailTiling, Properties.skin.microDetailTiling));
                }

                if (targetMaterial.HasProperty("_DetailSkinPoreMap"))
                {
                    targetMaterial.SetTextureScale("_DetailSkinPoreMap", new Vector2(Properties.skin.microDetailTiling, Properties.skin.microDetailTiling));
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
                            ShaderReplacer(milkMaterial, juiceMat);
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
                                            ShaderReplacer(liquidMaterial, material);
                                            material.SetColor("_Color", new Color(0.6f, 0.6f, 0.6f, 0.6f));
                                            material.SetColor("_EmissionColor", new Color(0.0f, 0.0f, 0.0f, 1.0f));
                                            material.renderQueue = 2453;
                                            Console.WriteLine("#### HSSSS Replaced " + material.name);
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

    public class ConfigWindow : MonoBehaviour
    {
        #region Global Fields
        public static bool useTessellation;
        public static bool fixAlphaShadow;
        public static bool hsrCompatible;

        public static int uiScale;

        private static int singleSpace;
        private static int doubleSpace;
        private static int tetraSpace;
        private static int octaSpace;

        private static Vector2 windowSize;
        private static Vector2 windowPosition;

        private Rect configWindow;

        private Properties.SkinProperties skin;
        private Properties.ShadowProperties shadow;
        private Properties.SSAOProperties ssao;
        private Properties.SSGIProperties ssgi;

        private enum TabState
        {
            skinScattering,
            skinTransmission,
            lightShadow,
            postEffects,
            miscellaneous
        };

        private TabState tabState;

        private readonly string[] tabLabels = new string[] { "Scattering", "Transmission", "Lights & Shadows", "Post Effects", "Miscellaneous" };
        private readonly string[] lutLabels = new string[] { "Penner", "FaceWorks Type 1", "FaceWorks Type 2", "Jimenez" };
        private readonly string[] pcfLabels = new string[] { "Off", "Low", "Medium", "High", "Ultra" };
        private readonly string[] thkLabels = new string[] { "Pre-Baked", "On-the-Fly" };
        #endregion

        public void Awake()
        {
            windowSize = new Vector2(192.0f * uiScale, 192.0f);

            singleSpace = uiScale;
            doubleSpace = uiScale * 2;
            tetraSpace = uiScale * 4;
            octaSpace = uiScale * 8;

            this.configWindow = new Rect(windowPosition, windowSize);
            this.tabState = TabState.skinScattering;

            this.skin = Properties.skin;
            this.shadow = Properties.shadow;
            this.ssao = Properties.ssao;
            this.ssgi = Properties.ssgi;
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

            if (hsrCompatible)
            {
                this.tabState = TabState.lightShadow;
                this.LightShadowSettings();
            }

            else
            {
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

                    case TabState.postEffects:
                        this.PostEffectsSettings();
                        break;

                    case TabState.miscellaneous:
                        this.OtherSettings();
                        break;
                }
            }

            GUILayout.Space(tetraSpace);

            // save and load
            GUILayout.Label("Save/Load Preset");
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            if (GUILayout.Button("Load Preset"))
            {
                if (HSSSS.instance.LoadExternalConfig())
                {
                    this.skin = Properties.skin;
                    this.shadow = Properties.shadow;
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
                Properties.skin = this.skin;
                Properties.shadow = this.shadow;

                if (HSSSS.instance.SaveExternalConfig())
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
            skin.sssWeight = this.SliderControls(skin.sssWeight, 0.0f, 1.0f);

            GUILayout.Space(tetraSpace);

            // profiles
            GUILayout.Label("Skin Scattering Profile");

            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            Properties.LUTProfile lutProfile = (Properties.LUTProfile)GUILayout.Toolbar((int)skin.lutProfile, lutLabels);

            if (skin.lutProfile != lutProfile)
            {
                skin.lutProfile = lutProfile;
                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(tetraSpace);

            if (skin.lutProfile == Properties.LUTProfile.jimenez)
            {
                GUILayout.Label("Diffuse Blur Radius");
                skin.normalBlurRadius = this.SliderControls(skin.normalBlurRadius, 0.0f, 4.0f);

                GUILayout.Label("Diffuse Blur Depth Range");
                skin.normalBlurDepthRange = this.SliderControls(skin.normalBlurDepthRange, 0.0f, 20.0f);

                GUILayout.Label("Diffuse Blur Iterations");
                skin.normalBlurIter = this.SliderControls(skin.normalBlurIter, 0, 10);
            }

            else
            {
                // skin diffusion brdf
                GUILayout.Label("Skin BRDF Lookup Scale");
                skin.skinLutScale = this.SliderControls(skin.skinLutScale, 0.0f, 1.0f);

                GUILayout.Label("Skin BRDF Lookup Bias");
                skin.skinLutBias = this.SliderControls(skin.skinLutBias, 0.0f, 1.0f);

                GUILayout.Space(tetraSpace);

                // shadow penumbra brdf
                if (skin.lutProfile == Properties.LUTProfile.nvidia2)
                {
                    GUILayout.Label("Shadow BRDF Lookup Scale");
                    skin.shadowLutScale = this.SliderControls(skin.shadowLutScale, 0.0f, 1.0f);

                    GUILayout.Label("Shadow BRDF Lookup Bias");
                    skin.shadowLutBias = this.SliderControls(skin.shadowLutBias, 0.0f, 1.0f);
                }

                GUILayout.Space(tetraSpace);

                // normal blurs
                GUILayout.Label("Normal Blur Weight");
                skin.normalBlurWeight = this.SliderControls(skin.normalBlurWeight, 0.0f, 1.0f);

                GUILayout.Label("Normal Blur Radius");
                skin.normalBlurRadius = this.SliderControls(skin.normalBlurRadius, 0.0f, 4.0f);

                GUILayout.Label("Normal Blur Depth Range");
                skin.normalBlurDepthRange = this.SliderControls(skin.normalBlurDepthRange, 0.0f, 20.0f);

                GUILayout.Label("Normal Blur Iterations");
                skin.normalBlurIter = this.SliderControls(skin.normalBlurIter, 0, 10);
            }

            GUILayout.Space(tetraSpace);

            // ambient occlusion
            GUILayout.Label("Ambient Occlusion Color Bleeding");
            skin.colorBleedWeights = this.RGBControls(skin.colorBleedWeights);
        }

        private void TransmissionSettings()
        {
            GUILayout.Label("Thickness Sampling Method");

            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));
            bool bakedThickness = GUILayout.Toolbar(Convert.ToUInt16(!this.skin.bakedThickness), thkLabels) == 0;
            GUILayout.EndHorizontal();

            if (this.skin.bakedThickness != bakedThickness)
            {
                this.skin.bakedThickness = bakedThickness;
                this.UpdateWindowSize();
            }

            if (!this.skin.bakedThickness && !this.shadow.pcssEnabled)
            {
                GUILayout.Label("<color=red>TURN ON PCSS SOFT SHADOW TO USE THIS OPTION!</color>");
            }

            GUILayout.Label("Transmission Weight");
            skin.transWeight = this.SliderControls(skin.transWeight, 0.0f, 1.0f);

            if (this.skin.bakedThickness)
            {
                GUILayout.Label("Transmission Distortion");
                skin.transDistortion = this.SliderControls(skin.transDistortion, 0.0f, 1.0f);

                GUILayout.Label("Transmission Shadow Weight");
                skin.transShadowWeight = this.SliderControls(skin.transShadowWeight, 0.0f, 1.0f);
            }

            else
            {
                GUILayout.Label("Transmission Thickness Bias");
                skin.thicknessBias = this.SliderControls(skin.thicknessBias, 0.0f, 5.0f);
            }

            GUILayout.Label("Transmission Falloff");
            skin.transFalloff = this.SliderControls(skin.transFalloff, 1.0f, 20.0f);

            if (this.skin.bakedThickness)
            {
                GUILayout.Label("Transmission Absorption");
                skin.transAbsorption = this.RGBControls(skin.transAbsorption);
            }
        }

        private void LightShadowSettings()
        {
            #region Soft Shadow
            // pcf iterations count
            GUILayout.Label("PCF Shadow Quality");
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            Properties.PCFState pcfState = (Properties.PCFState)GUILayout.Toolbar((int)this.shadow.pcfState, pcfLabels);

            if (this.shadow.pcfState != pcfState)
            {
                this.shadow.pcfState = pcfState;
                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(tetraSpace);

            // pcss soft shadow toggle
            GUILayout.Label("Percentage Closer Soft Shadow");
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            bool pcssEnabled = GUILayout.Toolbar(Convert.ToUInt16(this.shadow.pcssEnabled), new string[] { "Disable", "Enable"} ) == 1;

            if (this.shadow.pcssEnabled != pcssEnabled)
            {
                this.shadow.pcssEnabled = pcssEnabled;
                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(tetraSpace);

            if (this.shadow.pcfState != Properties.PCFState.disable)
            {
                // directional lights
                if (this.shadow.pcssEnabled)
                {
                    GUILayout.Label("Directional Light / Blocker Search Radius");
                    this.shadow.dirLightPenumbra.x = this.SliderControls(this.shadow.dirLightPenumbra.x, 0.0f, 20.0f);
                    GUILayout.Label("Directional Light / Light Radius");
                    this.shadow.dirLightPenumbra.y = this.SliderControls(this.shadow.dirLightPenumbra.y, 0.0f, 20.0f);
                    GUILayout.Label("Directional Light / Minimum Penumbra");
                }

                else
                {
                    GUILayout.Label("Directional Light / Penumbra Scale");
                }

                this.shadow.dirLightPenumbra.z = this.SliderControls(this.shadow.dirLightPenumbra.z, 0.0f, 20.0f);

                GUILayout.Space(tetraSpace);

                // spot lights
                if (this.shadow.pcssEnabled)
                {
                    GUILayout.Label("Spot Light / Blocker Search Radius");
                    this.shadow.spotLightPenumbra.x = this.SliderControls(this.shadow.spotLightPenumbra.x, 0.0f, 20.0f);
                    GUILayout.Label("Spot Light / Light Radius");
                    this.shadow.spotLightPenumbra.y = this.SliderControls(this.shadow.spotLightPenumbra.y, 0.0f, 20.0f);
                    GUILayout.Label("Spot Light / Minimum Penumbra");
                }

                else
                {
                    GUILayout.Label("Spot Light / Penumbra Scale");
                }

                this.shadow.spotLightPenumbra.z = this.SliderControls(this.shadow.spotLightPenumbra.z, 0.0f, 20.0f);

                GUILayout.Space(tetraSpace);

                // point lights
                if (this.shadow.pcssEnabled)
                {
                    GUILayout.Label("Point Light / Blocker Search Radius");
                    this.shadow.pointLightPenumbra.x = this.SliderControls(this.shadow.pointLightPenumbra.x, 0.0f, 20.0f);
                    GUILayout.Label("Point Light / Light Radius");
                    this.shadow.pointLightPenumbra.y = this.SliderControls(this.shadow.pointLightPenumbra.y, 0.0f, 20.0f);
                    GUILayout.Label("Point Light / Minimum Penumbra");
                }

                else
                {
                    GUILayout.Label("Point Light / Penumbra Scale");
                }

                this.shadow.pointLightPenumbra.z = this.SliderControls(this.shadow.pointLightPenumbra.z, 0.0f, 20.0f);
            }

            else
            {
                this.shadow.pcssEnabled = false;
            }

            GUILayout.Space(tetraSpace);
            #endregion
        }

        private void PostEffectsSettings()
        {
            #region SSAO
            GUILayout.Label("Ambient Occlusion");
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            bool ssaoEnabled = GUILayout.Toolbar(Convert.ToUInt16(this.ssao.enabled), new string[] { "Disable", "Enable" }) == 1;

            if (this.ssao.enabled != ssaoEnabled)
            {
                this.ssao.enabled = ssaoEnabled;
                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();

            if (this.ssao.enabled)
            {
                GUILayout.Space(tetraSpace);

                GUILayout.Label("AO Quality");
                GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));
                this.ssao.quality = (Properties.QualityPreset)GUILayout.Toolbar((int)this.ssao.quality, new string[] { "Low", "Medium", "High", "Ultra" });
                GUILayout.EndHorizontal();

                GUILayout.Label("Visibility Function");
                GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));
                this.ssao.usegtao = GUILayout.Toolbar(Convert.ToUInt16(this.ssao.usegtao), new string[] { "HBAO", "GTAO" }) == 1;
                GUILayout.EndHorizontal();

                GUILayout.Label("Occlusion Intensity");
                this.ssao.intensity = this.SliderControls(this.ssao.intensity, 0.1f, 10.0f);
                GUILayout.Label("Occlusion Bias");
                this.ssao.lightBias = this.SliderControls(this.ssao.lightBias, 0.0f, 1.0f);
                GUILayout.Label("Raytrace Radius");
                this.ssao.rayRadius = this.SliderControls(this.ssao.rayRadius, 0.0f, 1.0f);
                GUILayout.Label("Raytrace Stride");
                this.ssao.rayStride = this.SliderControls(this.ssao.rayStride, 1, 4);

                GUILayout.Label("Mean Depth");
                this.ssao.meanDepth = this.SliderControls(this.ssao.meanDepth, 0.0f, 2.00f);
                GUILayout.Label("Fade Depth");
                this.ssao.fadeDepth = this.SliderControls(this.ssao.fadeDepth, 1.0f, 1000.0f);

                GUILayout.Label("Spatial Denoiser");
                GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));
                this.ssao.denoise = GUILayout.Toolbar(Convert.ToUInt16(this.ssao.denoise), new string[] { "Off", "On" }) == 1;
                GUILayout.EndHorizontal();

                GUILayout.Label("Temporal Denoiser");
                this.ssao.mixWeight = this.SliderControls(this.ssao.mixWeight, 0.0f, 1.00f);

                GUILayout.Space(tetraSpace);
            }
            #endregion

            #region SSGI
            GUILayout.Label("Global Illumination");
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));

            bool ssgiEnabled = GUILayout.Toolbar(Convert.ToUInt16(this.ssgi.enabled), new string[] { "Disable", "Enable" }) == 1;

            if (this.ssgi.enabled != ssgiEnabled)
            {
                this.ssgi.enabled = ssgiEnabled;
                this.UpdateWindowSize();
            }

            GUILayout.EndHorizontal();

            if (this.ssgi.enabled)
            {
                GUILayout.Space(tetraSpace);

                GUILayout.Label("GI Quality");
                GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));
                this.ssgi.quality = (Properties.QualityPreset)GUILayout.Toolbar((int)this.ssgi.quality, new string[] { "Low", "Medium", "High", "Ultra" });
                GUILayout.EndHorizontal();

                GUILayout.Label("Sampling Resolution");
                GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));
                this.ssgi.resolution = (Properties.ResolveResolution)GUILayout.Toolbar((int)this.ssgi.resolution, new string[] { "Quarter", "Half", "Full" });
                GUILayout.EndHorizontal();

                GUILayout.Label("GI Intensity");
                this.ssgi.intensity = this.SliderControls(this.ssgi.intensity, 0.1f, 10.0f);
                GUILayout.Label("Raytrace Radius");
                this.ssgi.rayRadius = this.SliderControls(this.ssgi.rayRadius, 0.0f, 4.0f);
                GUILayout.Label("Raytrace Stride");
                this.ssgi.rayStride = this.SliderControls(this.ssgi.rayStride, 1, 4);

                GUILayout.Label("Fade Depth");
                this.ssgi.fadeDepth = this.SliderControls(this.ssgi.fadeDepth, 1.0f, 1000.0f);

                GUILayout.Label("Spatial Denoiser");
                GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));
                this.ssgi.denoise = GUILayout.Toolbar(Convert.ToUInt16(this.ssgi.denoise), new string[] { "Off", "On" }) == 1;
                GUILayout.EndHorizontal();

                GUILayout.Label("Temporal Denoiser");
                this.ssgi.mixWeight = this.SliderControls(this.ssgi.mixWeight, 0.0f, 1.0f);
            }
            #endregion
        }

        private void OtherSettings()
        {
            // skin microdetails
            GUILayout.Label("MicroDetail #1 Strength");
            this.skin.microDetailWeight_1 = this.SliderControls(this.skin.microDetailWeight_1, 0.0f, 1.0f);
            GUILayout.Label("MicroDetail #2 Strength");
            this.skin.microDetailWeight_2 = this.SliderControls(this.skin.microDetailWeight_2, 0.0f, 1.0f);
            GUILayout.Label("MicroDetails Tiling");
            this.skin.microDetailTiling = this.SliderControls(this.skin.microDetailTiling, 0.0f, 100.0f);

            // tessellation
            if (useTessellation)
            {
                GUILayout.Label("Tessellation Phong Strength");
                this.skin.phongStrength = this.SliderControls(this.skin.phongStrength, 0.0f, 1.0f);

                GUILayout.Label("Tessellation Edge Length");
                this.skin.edgeLength = this.SliderControls(this.skin.edgeLength, 2.0f, 50.0f);
            }

            if (fixAlphaShadow)
            {
                GUILayout.Label("Eyebrow Wrap Offset");
                this.skin.eyebrowoffset = this.SliderControls(this.skin.eyebrowoffset, 0.0f, 0.5f);
            }

            GUILayout.Space(octaSpace);

            // force refresh configurations
            GUILayout.Label("Troubleshooting");
            GUILayout.BeginHorizontal(GUILayout.Height(octaSpace));
            if (GUILayout.Button("Force Refresh Configurations"))
            {
                HSSSS.instance.RefreshConfig(false, true);
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
            bool softRefresh = Properties.skin.normalBlurIter == this.skin.normalBlurIter;
            softRefresh = softRefresh && (Properties.skin.lutProfile == this.skin.lutProfile);

            bool skinRefresh = Properties.skin.microDetailWeight_1 != this.skin.microDetailWeight_1;
            skinRefresh = skinRefresh || (Properties.skin.microDetailWeight_2 != this.skin.microDetailWeight_2);
            skinRefresh = skinRefresh || (Properties.skin.phongStrength != this.skin.phongStrength);
            skinRefresh = skinRefresh || (Properties.skin.edgeLength != this.skin.edgeLength);
            skinRefresh = skinRefresh || (Properties.skin.microDetailTiling != this.skin.microDetailTiling);
            skinRefresh = skinRefresh || (Properties.skin.eyebrowoffset != this.skin.eyebrowoffset);

            Properties.skin = this.skin;
            Properties.shadow = this.shadow;

            HSSSS.instance.RefreshConfig(softRefresh, skinRefresh);

            Properties.UpdateSSAO(this.ssao);
            Properties.UpdateSSGI(this.ssgi);
        }
    }
}
