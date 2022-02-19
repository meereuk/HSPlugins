using System;
using System.Collections.Generic;
using IllusionPlugin;
using UnityEngine;
using UnityEngine.Rendering;

namespace HSSSS
{
    public class HSSSS : IEnhancedPlugin
    {
        #region Info
        public string Name { get { return "HSSSS";  } }
        public string Version { get { return "0.20"; } }
        public string[] Filter { get { return new[] { "StudioNEO_32", "StudioNEO_64" }; } }
        #endregion

        #region Variables
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
        private static Material alloySkin;
        private static Texture2D bodyThickness;
        private static Texture2D faceThickness;
        private static Texture2D spotCookie;
        
        //
        private GameObject configUI;

        //
        private static bool isEnabled;
        private static bool isDeferred;
        private static KeyCode hotKey;

        //
        private static string[] colorAttr = { "_Color" };
        private static string[] floatAttr = { "_Metallic", "_Smoothness", "_OcclusionStrength", "_BlendNormalMapScale", "_DetailNormalMapScale" };
        private static string[] textureAttr = { "_MainTex", "_SpecGlossMap", "_OcclusionMap", "_BumpMap", "_BlendNormalMap", "_DetailNormalMap" };

        //
        private Dictionary<Material, bool> materialsToReplace;
        #endregion

        #region Unity Methods
        public void OnApplicationStart()
        {
            ConfigParser();
            BaseAssetLoader();

            if (isEnabled)
            {
                if (isDeferred)
                {
                    DeferredAssetLoader();
                    ReplaceInternalShader();
                }

                else
                {
                    ForwardAssetLoader();
                }

                materialsToReplace = new Dictionary<Material, bool>();
            }
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnLevelWasLoaded(int level)
        {
            if (isEnabled && isDeferred)
            {
                if (level == 3)
                {
                    InitPostFX();
                }
            }
        }

        public void OnUpdate()
        {
            if(isEnabled)
            {
                SearchMaterials(this.materialsToReplace);
            }
        }

        public void OnFixedUpdate()
        {
        }

        public void OnLateUpdate()
        {
            if(isEnabled)
            {
                ReplaceMaterials(materialsToReplace);
                FixSpotLights();

                if (isDeferred && Input.GetKeyDown(hotKey))
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
            isDeferred = ModPrefs.GetBool("HSSSS", "DeferredSkin", true, true);
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
            bodyThickness = bundle.LoadAsset<Texture2D>("bodyThickness");
            faceThickness = bundle.LoadAsset<Texture2D>("faceThickness");
            spotCookie = bundle.LoadAsset<Texture2D>("DefaultSpotCookie");

            if (null != bundle)
            {
                Console.WriteLine("#### HSSSS: Assetbundle Loaded");
            }

            if (null != bodyThickness && null != faceThickness)
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
            alloySkin = bundle.LoadAsset<Material>("SkinReplaceDeferred");
            deferredTransmissionBlit = bundle.LoadAsset<Shader>("DeferredTransmissionBlit");
            deferredBlurredNormals = bundle.LoadAsset<Shader>("DeferredBlurredNormals");
            deferredSkin = bundle.LoadAsset<Shader>("Alloy Deferred Skin");
            deferredReflections = bundle.LoadAsset<Shader>("Alloy Deferred Reflections");

            alloySkin.EnableKeyword("BUILTIN_THICKNESSMAP");

            if (null != skinLUT)
            {
                Console.WriteLine("#### HSSSS: Deferred Skin LUT Loaded");
            }

            if (null != alloySkin)
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
            alloySkin = bundle.LoadAsset<Material>("SkinReplaceForward");
            alloySkin.EnableKeyword("BUILTIN_THICKNESSMAP");

            if (null != alloySkin)
            {
                Console.WriteLine("#### HSSSS: Deferred Skin Replacer Loaded");
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

        private static void SearchMaterials(Dictionary<Material, bool> toReplace)
        {
            foreach (CharFemaleBody female in UnityEngine.Resources.FindObjectsOfTypeAll(typeof(CharFemaleBody)))
            {
                foreach (GameObject bodyObj in female.chaInfo.GetTagInfo(CharReference.TagObjKey.ObjSkinBody))
                {
                    foreach (Material bodyMat in bodyObj.GetComponent<Renderer>().sharedMaterials)
                    {
                        if (bodyMat.IsKeywordEnabled("_SKIN_EFFECT_ON"))
                        {
                            try
                            {
                                toReplace.Add(bodyMat, true);
                            }
                            catch
                            {
                            }
                        }
                    }
                }

                foreach (GameObject faceObj in female.chaInfo.GetTagInfo(CharReference.TagObjKey.ObjSkinFace))
                {
                    foreach (Material faceMat in faceObj.GetComponent<Renderer>().sharedMaterials)
                    {
                        if (faceMat.IsKeywordEnabled("_SKIN_EFFECT_ON"))
                        {
                            try
                            {
                                toReplace.Add(faceMat, false);
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
        }

        private static void ReplaceMaterials(Dictionary<Material, bool> toReplace)
        {
            foreach(KeyValuePair<Material, bool> entry in toReplace)
            {
                Console.WriteLine("#### HSSSS Replaces " + entry.Key.name);

                SetMaterialProps(entry.Key);

                if(entry.Value)
                {
                    entry.Key.SetTexture("_Thickness", bodyThickness);
                }

                else
                {
                    entry.Key.SetTexture("_Thickness", faceThickness);
                }
            }

            toReplace.Clear();
        }

        private static void SetMaterialProps(Material targetMaterial)
        {
            Material cacheMat = new Material(source: targetMaterial);

            targetMaterial.shader = alloySkin.shader;
            targetMaterial.CopyPropertiesFromMaterial(alloySkin);

            foreach (string attr in textureAttr)
            {
                targetMaterial.SetTexture(attr, cacheMat.GetTexture(attr));
                targetMaterial.SetTextureScale(attr, cacheMat.GetTextureScale(attr));
                targetMaterial.SetTextureOffset(attr, cacheMat.GetTextureOffset(attr));
            }

            foreach (string attr in colorAttr)
            {
                targetMaterial.SetColor(attr, cacheMat.GetColor(attr));
            }

            foreach (string attr in floatAttr)
            {
                targetMaterial.SetFloat(attr, cacheMat.GetFloat(attr));
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
