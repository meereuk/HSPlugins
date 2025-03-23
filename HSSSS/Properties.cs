using System.Collections.Generic;
using UnityEngine;

namespace HSSSS
{
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

            public float microDetailWeight_1;
            public float microDetailWeight_2;
            public float microDetailOcclusion;
            public float microDetailTiling;
        }

        public struct PCSSProperties
        {
            public PCFState pcfState;
            public bool pcssEnabled;

            public Vector3 pointLightPenumbra;
            public Vector3 spotLightPenumbra;
            public Vector3 dirLightPenumbra;
        }

        public struct SSAOProperties
        {
            public bool enabled;
            public bool usessdo;
            public bool denoise;

            public QualityPreset quality;
            public RenderScale subsample;

            public float intensity;
            public float lightBias;
            public float rayRadius;
            public float meanDepth;
            public float fadeDepth;
            public float doApature;
            public int rayStride;
        }

        public struct SSGIProperties
        {
            public bool enabled;
            public bool denoise;

            public QualityPreset quality;

            public float intensity;
            public float secondary;
            public float roughness;
            public float rayRadius;
            public float meanDepth;
            public float fadeDepth;
            public float mixWeight;
            public int rayStride;
        }

        public struct TAAUProperties
        {
            public bool enabled;
            public bool upscale;
            public float mixWeight;
        }

        public struct SSCSProperties
        {
            public bool enabled;
            public QualityPreset quality;
            public float rayRadius;
            public float depthBias;
            public float meanDepth;
        }

        public struct MiscProperties
        {
            public bool fixEyeball;
            public bool fixOverlay;
            public bool wetOverlay;
            public float wrapOffset;
        }

        public struct TESSProperties
        {
            public bool enabled;
            public float phong;
            public float edge;
        }
        
        public struct AgXProperties
        {
            public bool enabled;
            public float gamma;
            public Vector3 saturation;
            public Vector3 offset;
            public Vector3 slope;
            public Vector3 power;
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

            microDetailWeight_1 = 0.5f,
            microDetailWeight_2 = 0.5f,
            microDetailOcclusion = 0.5f,

            microDetailTiling = 64.0f
        };

        public static PCSSProperties pcss = new PCSSProperties()
        {
            pcfState = PCFState.disable,
            pcssEnabled = false,

            dirLightPenumbra = new Vector3(1.0f, 1.0f, 1.0f),
            spotLightPenumbra = new Vector3(1.0f, 1.0f, 1.0f),
            pointLightPenumbra = new Vector3(1.0f, 1.0f, 1.0f),
        };

        public static SSAOProperties ssao = new SSAOProperties()
        {
            enabled = false,
            usessdo = false,
            denoise = false,

            quality = QualityPreset.medium,
            subsample = RenderScale.full,

            intensity = 1.0f,
            lightBias = 0.0f,
            rayRadius = 0.1f,
            meanDepth = 0.5f,
            fadeDepth = 100.0f,
            doApature = 0.5f,
            rayStride = 2
        };

        public static SSGIProperties ssgi = new SSGIProperties()
        {
            enabled = false,
            denoise = false,

            quality = QualityPreset.medium,

            intensity = 1.0f,
            secondary = 1.0f,
            roughness = 0.3f,
            rayRadius = 1.0f,
            meanDepth = 1.0f,
            fadeDepth = 100.0f,
            mixWeight = 0.5f,
            rayStride = 2
        };

        public static TAAUProperties taau = new TAAUProperties()
        {
            enabled = false,
            upscale = false,
            mixWeight = 0.5f
        };

        public static SSCSProperties sscs = new SSCSProperties()
        {
            enabled = false,
            quality = QualityPreset.medium,
            rayRadius = 10.0f,
            depthBias = 0.2f,
            meanDepth = 1.0f
        };

        public static TESSProperties tess = new TESSProperties()
        {
            enabled = false,
            phong = 0.5f,
            edge = 2.0f
        };

        public static MiscProperties misc = new MiscProperties()
        {
            fixEyeball = false,
            fixOverlay = false,
            wetOverlay = false,
            wrapOffset = 0.1f
        };

        public static AgXProperties agx = new AgXProperties()
        {
            enabled = false,
            gamma = 2.2f,
            saturation = new Vector3(1.0f, 1.0f, 1.0f),
            offset = new Vector3(0.0f, 0.0f, 0.0f),
            slope = new Vector3(1.0f, 1.0f, 1.0f),
            power = new Vector3(1.0f, 1.0f, 1.0f)
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

                        if (mat.HasProperty("_PoreOcclusionStrength"))
                        {
                            mat.SetFloat("_PoreOcclusionStrength", Properties.skin.microDetailOcclusion);
                        }

                        if (mat.HasProperty("_Phong"))
                        {
                            mat.SetFloat("_Phong", Properties.tess.phong);
                        }

                        if (mat.HasProperty("_EdgeLength"))
                        {
                            mat.SetFloat("_EdgeLength", Properties.tess.edge);
                        }

                        if (mat.HasProperty("_VertexWrapOffset"))
                        {
                            mat.SetFloat("_VertexWrapOffset", Properties.misc.wrapOffset);
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
