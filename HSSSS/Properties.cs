using System.Collections.Generic;
using UnityEngine;

namespace HSSSS
{
    public class KeyValue<TKey, TValue>
    {
        public readonly TKey Key;
        public TValue Value;

        public KeyValue(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }
    
    public static class Properties
    {
        public enum LUTProfile
        {
            penner,
            nvidia1,
            nvidia2,
            jimenez
        }

        public enum PCFState
        {
            disable,
            poisson8x,
            poisson16x,
            poisson32x,
            poisson64x
        }

        public enum QualityPreset
        {
            low,
            medium,
            high,
            ultra
        }

        public enum RenderScale
        {
            full,
            half,
            quarter
        }

        public enum MilkPart
        {
            chest,
            spine,
            belly,
            butt
        }

        public struct SkinProperties
        {
            public LUTProfile lutProfile;

            public float skinLutBias;
            public float skinLutScale;
            public float shadowLutBias;
            public float shadowLutScale;

            public float sssBlurWeight;
            public float sssBlurRadius;
            public float sssBlurDepthRange;

            public int sssBlurIter;

            public bool sssBlurAlbedo;

            public Vector3 colorBleedWeights;
            public Vector3 transAbsorption;
            public Vector3 scatteringColor;

            public bool bakedThickness;

            public float transWeight;
            public float transShadowWeight;
            public float transDistortion;
            public float transFalloff;

            public float thicknessBias;

            public bool microDetails;

            public KeyValue<string, float> microDetailWeight_1;
            public KeyValue<string, float> microDetailWeight_2;
            public KeyValue<string, float> microDetailOcclusion;
            public float microDetailTiling;
        }

        public struct PCSSProperties
        {
            public PCFState pcfState;
            public bool pcssEnabled;

            public KeyValue<string, Vector3> pointLightPenumbra;
            public KeyValue<string, Vector3> spotLightPenumbra;
            public KeyValue<string, Vector3> dirLightPenumbra;
        }

        public struct SSAOProperties
        {
            public bool enabled;
            public bool denoise;

            public QualityPreset quality;
            public RenderScale subsample;

            public KeyValue<string, float> intensity;
            public KeyValue<string, float> lightBias;
            public KeyValue<string, float> rayRadius;
            public KeyValue<string, float> meanDepth;
            public KeyValue<string, float> fadeDepth;
            public KeyValue<string, int> rayStride;
            
            public KeyValue<string, bool> usessdo;
            public KeyValue<string, float> doApature;
        }

        public struct SSGIProperties
        {
            public bool enabled;
            public bool denoise;

            public QualityPreset quality;

            public KeyValue<string, float> intensity;
            public KeyValue<string, float> secondary;
            public KeyValue<string, float> roughness;
            public KeyValue<string, float> rayRadius;
            public KeyValue<string, float> meanDepth;
            public KeyValue<string, float> fadeDepth;
            public KeyValue<string, float> mixWeight;
            public KeyValue<string, int> rayStride;
        }

        public struct TAAUProperties
        {
            public bool enabled;
            public bool upscale;
            public KeyValue<string, float> mixWeight;
        }

        public struct SSCSProperties
        {
            public bool enabled;
            public QualityPreset quality;
            public KeyValue<string, float> rayRadius;
            public KeyValue<string, float> depthBias;
            public KeyValue<string, float> meanDepth;
        }

        public struct MiscProperties
        {
            public bool fixEyeball;
            public bool fixOverlay;
            public bool wetOverlay;
            public KeyValue<string, float> wrapOffset;
        }

        public struct TESSProperties
        {
            public bool enabled;
            public KeyValue<string, float> phong;
            public KeyValue<string, float> edge;
        }
        
        public struct AgXProperties
        {
            public bool enabled;
            public KeyValue<string, float> gamma;
            public KeyValue<string, float> saturation;
            public KeyValue<string, Vector3> offset;
            public KeyValue<string, Vector3> slope;
            public KeyValue<string, Vector3> power;
        }

        public static SkinProperties skin = new SkinProperties()
        {
            lutProfile = LUTProfile.penner,

            skinLutBias = 0.0f,
            skinLutScale = 0.5f,

            shadowLutBias = 0.0f,
            shadowLutScale = 1.0f,

            sssBlurWeight = 1.0f,
            sssBlurRadius = 0.2f,
            sssBlurDepthRange = 1.0f,

            sssBlurIter = 1,

            sssBlurAlbedo = true,

            colorBleedWeights = new Vector3(0.40f, 0.15f, 0.20f),
            transAbsorption = new Vector3(-8.00f, -48.0f, -64.0f),

            bakedThickness = true,

            transWeight = 1.0f,
            transShadowWeight = 0.5f,
            transDistortion = 0.5f,
            transFalloff = 2.0f,
            thicknessBias = 0.5f,

            microDetails = false,

            microDetailWeight_1 = new KeyValue<string, float>("_DetailNormalMapScale_2", 0.5f),
            microDetailWeight_2 = new KeyValue<string, float>("_DetailNormalMapScale_3", 0.5f),
            microDetailOcclusion = new KeyValue<string, float>("_PoreOcclusionStrength", 0.5f),

            microDetailTiling = 64.0f
        };

        public static PCSSProperties pcss = new PCSSProperties()
        {
            pcfState = PCFState.disable,
            pcssEnabled = false,

            dirLightPenumbra = new KeyValue<string, Vector3>("_DirLightPenumbra", new Vector3(1.0f, 1.0f, 1.0f)),
            spotLightPenumbra = new KeyValue<string, Vector3>("_SpotLightPenumbra", new Vector3(1.0f, 1.0f, 1.0f)),
            pointLightPenumbra = new KeyValue<string, Vector3>("_PointLightPenumbra", new Vector3(1.0f, 1.0f, 1.0f))
        };

        public static SSAOProperties ssao = new SSAOProperties()
        {
            enabled = false,
            denoise = false,

            quality = QualityPreset.medium,
            subsample = RenderScale.full,

            intensity = new KeyValue<string, float>("_SSAOIntensity", 1.0f),
            lightBias = new KeyValue<string, float>("_SSAOLightBias", 0.0f),
            rayRadius = new KeyValue<string, float>("_SSAORayLength", 0.1f),
            meanDepth = new KeyValue<string, float>("_SSAOMeanDepth", 0.5f),
            fadeDepth = new KeyValue<string, float>("_SSAOFadeDepth", 100.0f),
            rayStride = new KeyValue<string, int>("_SSAORayStride", 2),
            
            usessdo = new KeyValue<string, bool>("_UseDirectOcclusion", true),
            doApature = new KeyValue<string, float>("_SSDOLightApatureScale", 0.5f),
        };

        public static SSGIProperties ssgi = new SSGIProperties()
        {
            enabled = false,
            denoise = false,

            quality = QualityPreset.medium,

            intensity = new KeyValue<string, float>("_SSGIIntensity", 1.0f),
            secondary = new KeyValue<string, float>("_SSGISecondary", 1.0f),
            roughness = new KeyValue<string, float>("_SSGIRoughness", 0.3f),
            rayRadius = new KeyValue<string, float>("_SSGIRayLength", 1.0f),
            meanDepth = new KeyValue<string, float>("_SSGIMeanDepth", 1.0f),
            fadeDepth = new KeyValue<string, float>("_SSGIFadeDepth", 100.0f),
            mixWeight = new KeyValue<string, float>("_SSGIMixFactor", 0.5f),
            rayStride = new KeyValue<string, int>("_SSGIStepPower", 2)
        };

        public static TAAUProperties taau = new TAAUProperties()
        {
            enabled = false,
            upscale = false,
            mixWeight = new KeyValue<string, float>("_TemporalMixFactor", 0.5f)
        };

        public static SSCSProperties sscs = new SSCSProperties()
        {
            enabled = false,
            quality = QualityPreset.medium,
            rayRadius = new KeyValue<string, float>("_SSCSRayLength", 10.0f),
            depthBias = new KeyValue<string, float>("_SSCSDepthBias", 0.2f),
            meanDepth = new KeyValue<string, float>("_SSCSMeanDepth", 1.0f)
        };

        public static TESSProperties tess = new TESSProperties()
        {
            enabled = false,
            phong = new KeyValue<string, float>("_Phong", 0.5f),
            edge = new KeyValue<string, float>("_EdgeLength", 2.0f)
        };

        public static MiscProperties misc = new MiscProperties()
        {
            fixEyeball = false,
            fixOverlay = false,
            wetOverlay = false,
            wrapOffset = new KeyValue<string, float>("_VertexWrapOffset", 0.0f),
        };

        public static AgXProperties agx = new AgXProperties()
        {
            enabled = false,
            gamma = new KeyValue<string, float>("_AgXGamma", 2.2f),
            saturation = new KeyValue<string, float>("_AgXSaturation", 1.0f),
            offset = new KeyValue<string, Vector3>("_AgXOffset", new Vector3(0.0f, 0.0f, 0.0f)),
            slope = new KeyValue<string, Vector3>("_AgXSlope", new Vector3(1.0f, 1.0f, 1.0f)),
            power = new KeyValue<string, Vector3>("_AgXPower", new Vector3(1.0f, 1.0f, 1.0f))
        };

        public static void UpdateSkin()
        {
            AssetLoader.RefreshMaterials();

            HSSSS.DeferredRenderer.UpdateSkinSettings();

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

        private static void UpdateSkinLoop(CharInfo chaInfo, CharReference.TagObjKey key)
        {
            foreach (GameObject body in chaInfo.GetTagInfo(key))
            {
                foreach (Renderer rend in body.GetComponents<Renderer>())
                {
                    foreach (Material mat in rend.sharedMaterials)
                    {
                        if (mat.HasProperty(Properties.skin.microDetailWeight_1.Key))
                        {
                            mat.SetFloat(Properties.skin.microDetailWeight_1.Key, Properties.skin.microDetailWeight_1.Value);
                        }

                        if (mat.HasProperty(Properties.skin.microDetailWeight_2.Key))
                        {
                            mat.SetFloat(Properties.skin.microDetailWeight_2.Key, Properties.skin.microDetailWeight_2.Value);
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

                        if (mat.HasProperty(Properties.skin.microDetailOcclusion.Key))
                        {
                            mat.SetFloat(Properties.skin.microDetailOcclusion.Key, Properties.skin.microDetailOcclusion.Value);
                        }

                        if (mat.HasProperty(Properties.tess.phong.Key))
                        {
                            mat.SetFloat(Properties.tess.phong.Key, Properties.tess.phong.Value);
                        }

                        if (mat.HasProperty(Properties.tess.edge.Key))
                        {
                            mat.SetFloat(Properties.tess.edge.Key, Properties.tess.edge.Value);
                        }

                        if (mat.HasProperty(Properties.misc.wrapOffset.Key))
                        {
                            mat.SetFloat(Properties.misc.wrapOffset.Key, Properties.misc.wrapOffset.Value);
                        }
                    }
                }
            }
        }

        public static void UpdatePCSS()
        {
            if (HSSSS.isStudio)
            {
                Shader.DisableKeyword("_PCF_ON");

                if (Properties.pcss.pcfState != PCFState.disable)
                {
                    Shader.EnableKeyword("_PCF_ON");
                }

                foreach (Light light in UnityEngine.Resources.FindObjectsOfTypeAll<Light>())
                {
                    if (light)
                    {
                        if (light.GetComponent<ScreenSpaceShadows>())
                        {
                            light.GetComponent<ScreenSpaceShadows>().UpdateSettings();
                        }
                    }
                }
            }
        }

        public static void UpdateSSAO()
        {
            if (HSSSS.SSAORenderer)
            {
                HSSSS.SSAORenderer.enabled = ssao.enabled;

                if (ssao.enabled)
                {
                    HSSSS.SSAORenderer.UpdateSettings();
                    Shader.SetGlobalInt("_UseAmbientOcclusion", 1);
                }

                else
                {
                    Shader.SetGlobalInt("_UseDirectOcclusion", 0);
                    Shader.SetGlobalInt("_UseAmbientOcclusion", 0);
                }
            }
        }

        public static void UpdateSSGI()
        {
            if (HSSSS.SSGIRenderer)
            {
                HSSSS.SSGIRenderer.enabled = ssgi.enabled;

                if (ssgi.enabled)
                {
                    HSSSS.SSGIRenderer.UpdateSettings();
                }
            }
        }

        public static void UpdateTAAU()
        {
            if (HSSSS.TAAURenderer)
            {
                HSSSS.TAAURenderer.enabled = taau.enabled;

                if (taau.enabled)
                {
                    HSSSS.TAAURenderer.UpdateSettings();
                }
            }
        }

        public static void UpdateAgX()
        {
            if (HSSSS.AgXToneMapper)
            {
                HSSSS.AgXToneMapper.enabled = agx.enabled;

                if (agx.enabled)
                {
                    HSSSS.AgXToneMapper.UpdateSettings();
                }
            }
        }

        public static void UpdateAll()
        {
            UpdateSkin();
            UpdatePCSS();
            UpdateSSAO();
            UpdateSSGI();
            UpdateTAAU();
            UpdateAgX();
        }
    }
}
