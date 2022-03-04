using System;
using System.Collections;
using System.Collections.Generic;
using IllusionPlugin;
using UnityEngine;
using UnityEngine.Rendering;

namespace HSSSS
{
    public class HSSSS : IEnhancedPlugin
    {
        #region Plugin Info
        public string Name { get { return "HSSSS";  } }
        public string Version { get { return "0.2.2"; } }
        public string[] Filter { get { return new[] { "StudioNEO_32", "StudioNEO_64" }; } }
        #endregion

        #region Global Variables
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
        internal static Material overMaterial;
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
        private static KeyCode hotKey;
        #endregion

        #region Unity Methods
        public void OnApplicationStart()
        {
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
            useTessellation = ModPrefs.GetBool("HSSSS", "Tessellation", true, true);
            useWetSpecGloss = ModPrefs.GetBool("HSSSS", "WetSpecGloss", false, true);

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
        }

        private static void DeferredAssetLoader()
        {
            skinLUT = bundle.LoadAsset<Texture2D>("DeferredLUT");

            if (useTessellation)
            {
                skinMaterial = bundle.LoadAsset<Material>("DeferredTessellationSkin");
                overMaterial = bundle.LoadAsset<Material>("TessellationSkinOverlay");
            }

            else
            {
                skinMaterial = bundle.LoadAsset<Material>("DeferredSkin");
                overMaterial = bundle.LoadAsset<Material>("SkinOverlay");
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

            if (null != skinMaterial && null != overMaterial)
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
            skinMaterial = bundle.LoadAsset<Material>("ForwardSkin");
            overMaterial = bundle.LoadAsset<Material>("SkinOverlay");

            if (useWetSpecGloss)
            {
                skinMaterial.EnableKeyword("_WET_SPECGLOSS");
            }

            if (null != skinMaterial && null != overMaterial)
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
        #endregion
    }

    public class SkinReplacer : MonoBehaviour
    {
        private enum materialType
        {
            femaleHead,
            femaleBody,
            femaleOver,
            maleHead,
            maleBody,
        };

        private Dictionary<Material, materialType> materialsToReplace;

        private static string[] colorProps = { "_Color" };
        private static string[] floatProps = { "_Metallic", "_Smoothness", "_OcclusionStrength", "_BlendNormalMapScale", "_DetailNormalMapScale" };
        private static string[] textureProps = { "_MainTex", "_SpecGlossMap", "_OcclusionMap", "_BumpMap", "_BlendNormalMap", "_DetailNormalMap" };

        public void Awake()
        {
            materialsToReplace = new Dictionary<Material, materialType>();
        }

        public void Start()
        {
            this.StartCoroutine(this.FindAndReplace());
        }

        public void OnDestroy()
        {
            this.StopAllCoroutines();
        }

        private static void SearchSkinMaterials(Dictionary<Material, materialType> toReplace)
        {
            foreach (CharBody charBody in UnityEngine.Resources.FindObjectsOfTypeAll(typeof(CharBody)))
            {
                Material bodyMat = charBody.customMatBody;
                Material faceMat = charBody.customMatFace;
                byte sex = charBody.chaInfo.Sex;

                if ("Shader Forge/PBRsp" == bodyMat.shader.name)
                {
                    if (0 == sex)
                    {
                        try
                        {
                            toReplace.Add(bodyMat, materialType.maleBody);
                        }
                        catch
                        {
                        }
                    }
                    else if (1 == sex)
                    {
                        try
                        {
                            toReplace.Add(bodyMat, materialType.femaleBody);
                        }
                        catch
                        {
                        }
                    }
                }

                if ("Shader Forge/PBRsp" == faceMat.shader.name)
                {
                    if (1 == sex)
                    {
                        try
                        {
                            toReplace.Add(faceMat, materialType.maleHead);
                        }
                        catch
                        {
                        }
                    }
                    else if (1 == sex)
                    {
                        try
                        {
                            toReplace.Add(faceMat, materialType.femaleHead);
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        private static void SearchMaterials(Dictionary<Material, materialType> toReplace)
        {
            foreach (CharFemaleBody female in UnityEngine.Resources.FindObjectsOfTypeAll(typeof(CharFemaleBody)))
            {
                foreach (GameObject bodyObj in female.chaInfo.GetTagInfo(CharReference.TagObjKey.ObjSkinBody))
                {
                    foreach (Material bodyMat in bodyObj.GetComponent<Renderer>().sharedMaterials)
                    {
                        switch (bodyMat.shader.name)
                        {
                            case "Shader Forge/PBRsp":
                                try
                                {
                                    toReplace.Add(bodyMat, materialType.femaleBody);
                                }
                                catch
                                {
                                }
                                break;
                            case "Standard":
                                try
                                {
                                    toReplace.Add(bodyMat, materialType.femaleOver);
                                }
                                catch
                                {
                                }
                                break;
                            case "HSStandard/Standard Ignore Projector":
                                try
                                {
                                    toReplace.Add(bodyMat, materialType.femaleOver);
                                }
                                catch
                                {
                                }
                                break;
                        }
                    }
                }

                foreach (GameObject faceObj in female.chaInfo.GetTagInfo(CharReference.TagObjKey.ObjSkinFace))
                {
                    foreach (Material faceMat in faceObj.GetComponent<Renderer>().sharedMaterials)
                    {
                        switch (faceMat.shader.name)
                        {
                            case "Shader Forge/PBRsp":
                                try
                                {
                                    toReplace.Add(faceMat, materialType.femaleHead);
                                }
                                catch
                                {
                                }
                                break;
                            case "Standard":
                                try
                                {
                                    toReplace.Add(faceMat, materialType.femaleOver);
                                }
                                catch
                                {
                                }
                                break;
                            case "HSStandard/Standard Ignore Projector":
                                try
                                {
                                    toReplace.Add(faceMat, materialType.femaleOver);
                                }
                                catch
                                {
                                }
                                break;
                        }
                    }
                }
            }

            foreach (CharMaleBody male in UnityEngine.Resources.FindObjectsOfTypeAll(typeof(CharMaleBody)))
            {
                foreach (GameObject bodyObj in male.chaInfo.GetTagInfo(CharReference.TagObjKey.ObjSkinBody))
                {
                    foreach (Material bodyMat in bodyObj.GetComponent<Renderer>().sharedMaterials)
                    {
                        if (bodyMat.shader.name == "Shader Forge/PBRsp")
                        {
                            try
                            {
                                toReplace.Add(bodyMat, materialType.maleBody);
                            }
                            catch
                            {
                            }
                        }
                    }
                }

                foreach (GameObject faceObj in male.chaInfo.GetTagInfo(CharReference.TagObjKey.ObjSkinFace))
                {
                    foreach (Material faceMat in faceObj.GetComponent<Renderer>().sharedMaterials)
                    {
                        if (faceMat.shader.name == "Shader Forge/PBRsp")
                        {
                            try
                            {
                                toReplace.Add(faceMat, materialType.maleHead);
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
        }

        private static void ReplaceMaterials(Dictionary<Material, materialType> toReplace)
        {
            foreach (KeyValuePair<Material, materialType> entry in toReplace)
            {
                Console.WriteLine("#### HSSSS Replaces " + entry.Key.name);

                switch (entry.Value)
                {
                    case materialType.femaleBody:
                        SetSkinMaterialProps(entry.Key);
                        entry.Key.SetTexture("_Thickness", HSSSS.femaleBodyThickness);
                        break;

                    case materialType.femaleHead:
                        SetSkinMaterialProps(entry.Key);
                        entry.Key.SetTexture("_Thickness", HSSSS.famaleHeadThickness);
                        break;

                    case materialType.femaleOver:
                        SetOverMaterialProps(entry.Key);
                        break;

                    case materialType.maleBody:
                        SetSkinMaterialProps(entry.Key);
                        entry.Key.SetTexture("_Thickness", HSSSS.maleBodyThickness);
                        break;

                    case materialType.maleHead:
                        SetSkinMaterialProps(entry.Key);
                        entry.Key.SetTexture("_Thickness", HSSSS.maleHeadThickness);
                        break;
                }
            }

            toReplace.Clear();
        }

        private static void SetSkinMaterialProps(Material targetMaterial)
        {
            Material cacheMat = new Material(source: targetMaterial);

            targetMaterial.shader = HSSSS.skinMaterial.shader;
            targetMaterial.CopyPropertiesFromMaterial(HSSSS.skinMaterial);

            foreach (string prop in textureProps)
            {
                targetMaterial.SetTexture(prop, cacheMat.GetTexture(prop));
                targetMaterial.SetTextureScale(prop, cacheMat.GetTextureScale(prop));
                targetMaterial.SetTextureOffset(prop, cacheMat.GetTextureOffset(prop));
            }

            foreach (string prop in colorProps)
            {
                targetMaterial.SetColor(prop, cacheMat.GetColor(prop));
            }

            foreach (string prop in floatProps)
            {
                targetMaterial.SetFloat(prop, cacheMat.GetFloat(prop));
            }
        }

        private static void SetOverMaterialProps(Material targetMaterial)
        {
            Material cacheMat = new Material(source: targetMaterial);

            targetMaterial.shader = HSSSS.overMaterial.shader;
            targetMaterial.CopyPropertiesFromMaterial(HSSSS.overMaterial);

            foreach (string prop in textureProps)
            {
                targetMaterial.SetTexture(prop, cacheMat.GetTexture(prop));
                targetMaterial.SetTextureScale(prop, cacheMat.GetTextureScale(prop));
                targetMaterial.SetTextureOffset(prop, cacheMat.GetTextureOffset(prop));
            }

            foreach (string prop in colorProps)
            {
                targetMaterial.SetColor(prop, cacheMat.GetColor(prop));
            }

            foreach (string prop in floatProps)
            {
                targetMaterial.SetFloat(prop, cacheMat.GetFloat(prop));
            }

            targetMaterial.SetFloat("_Smoothness", cacheMat.GetFloat("_Glossiness"));
            targetMaterial.SetFloat("_Cutoff", 0.001f);
        }

        private IEnumerator FindAndReplace()
        {
            while (true)
            {
                SearchMaterials(materialsToReplace);
                yield return null;
                ReplaceMaterials(materialsToReplace);
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

        public void Awake()
        {
            this.configWindow = new Rect(256.0f, 0.000f, 480.0f, 640.0f);
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

            this.configWindow = GUI.Window(0, this.configWindow, this.WindowFunction, "HSSSS Configuration");
            Studio.Studio.Instance.cameraCtrl.enabled = !this.configWindow.Contains(Event.current.mousePosition);
        }

        private void WindowFunction(int WindowID)
        {
            GUILayout.Space(16.0f);

            GUILayout.Label("Skin Scattering Weight", this.labelStyle);
            HSSSS.SSS.SkinSettings.Weight = this.SliderControls(HSSSS.SSS.SkinSettings.Weight, 0.0f, 1.0f);

            GUILayout.Label("Skin Scattering Bias", this.labelStyle);
            HSSSS.SSS.SkinSettings.Bias = this.SliderControls(HSSSS.SSS.SkinSettings.Bias, 0.0f, 1.0f);

            GUILayout.Label("Skin Scattering Scale", this.labelStyle);
            HSSSS.SSS.SkinSettings.Scale = this.SliderControls(HSSSS.SSS.SkinSettings.Scale, 0.0f, 1.0f);

            GUILayout.Label("Skin Scattering Bump Blur", this.labelStyle);
            HSSSS.SSS.SkinSettings.BumpBlur = this.SliderControls(HSSSS.SSS.SkinSettings.BumpBlur, 0.0f, 1.0f);

            GUILayout.Label("Skin Scattering Occlusion Color Bleeding", this.labelStyle);
            HSSSS.SSS.SkinSettings.ColorBleedAoWeights = this.RGBControls(HSSSS.SSS.SkinSettings.ColorBleedAoWeights);

            GUILayout.Space(16.0f);

            GUILayout.Label("Transmission Weight", this.labelStyle);
            HSSSS.SSS.TransmissionSettings.Weight = this.SliderControls(HSSSS.SSS.TransmissionSettings.Weight, 0.0f, 1.0f);

            GUILayout.Label("Transmission Distortion", this.labelStyle);
            HSSSS.SSS.TransmissionSettings.BumpDistortion = this.SliderControls(HSSSS.SSS.TransmissionSettings.BumpDistortion, 0.0f, 1.0f);

            GUILayout.Label("Transmission Shadow Weight", this.labelStyle);
            HSSSS.SSS.TransmissionSettings.ShadowWeight = this.SliderControls(HSSSS.SSS.TransmissionSettings.ShadowWeight, 0.0f, 1.0f);

            GUILayout.Label("Transmission Falloff", this.labelStyle);
            HSSSS.SSS.TransmissionSettings.Falloff = this.SliderControls(HSSSS.SSS.TransmissionSettings.Falloff, 1.0f, 20.0f);

            GUILayout.Label("Transmission Absorption", this.labelStyle);
            HSSSS.SSS.SkinSettings.TransmissionAbsorption = this.RGBControls(HSSSS.SSS.SkinSettings.TransmissionAbsorption);

            GUI.DragWindow();

            HSSSS.SSS.Refresh();
        }

        private float SliderControls(float sliderValue, float minValue, float maxValue)
        {
            GUILayout.BeginHorizontal();

            sliderValue = GUILayout.HorizontalSlider(sliderValue, minValue, maxValue, this.sliderStyle, this.thumbStyle);

            if (float.TryParse(GUILayout.TextField(sliderValue.ToString(), this.fieldStyle, GUILayout.Width(64.0f)), out float fieldValue))
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
            
            if (float.TryParse(GUILayout.TextField(rgbValue.x.ToString(), this.fieldStyle), out float r))
            {
                rgbValue.x = r;
            }

            GUILayout.Label("G", labelStyle);

            if (float.TryParse(GUILayout.TextField(rgbValue.y.ToString(), this.fieldStyle), out float g))
            {
                rgbValue.y = g;
            }

            GUILayout.Label("B", labelStyle);

            if (float.TryParse(GUILayout.TextField(rgbValue.z.ToString(), this.fieldStyle), out float b))
            {
                rgbValue.z = b;
            }

            GUILayout.EndHorizontal();

            return rgbValue;
        }
    }
}
