using Studio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            quarter,
            half,
            full
        }

        public struct SkinProperties
        {
            public bool sssEnabled;
            public bool transEnabled;

            public LUTProfile lutProfile;

            public float sssWeight;

            public float skinLutBias;
            public float skinLutScale;
            public float shadowLutBias;
            public float shadowLutScale;

            public float normalBlurWeight;
            public float normalBlurRadius;
            public float normalBlurDepthRange;

            public int normalBlurIter;

            public Vector3 colorBleedWeights;
            public Vector3 transAbsorption;
            public Vector3 scatteringColor;

            public bool bakedThickness;

            public float transWeight;
            public float transShadowWeight;
            public float transDistortion;
            public float transFalloff;

            public float thicknessBias;

            public float microDetailWeight_1;
            public float microDetailWeight_2;
            public float microDetailTiling;

            public float phongStrength;
            public float edgeLength;

            public float eyebrowoffset;
        }

        public struct ShadowProperties
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
            public bool usegtao;
            public bool denoise;

            public QualityPreset quality;

            public float intensity;
            public float lightBias;
            public float rayRadius;
            public float meanDepth;
            public float fadeDepth;
            public int rayStride;
            public int screenDiv;
        }

        public struct SSGIProperties
        {
            public bool enabled;
            public bool denoise;

            public QualityPreset quality;
            public RenderScale samplescale;
            public RenderScale renderscale;

            public float intensity;
            public float secondary;
            public float rayRadius;
            public float fadeDepth;
            public float mixWeight;
            public int rayStride;
        }

        public static SkinProperties skin = new SkinProperties()
        {
            sssEnabled = true,
            transEnabled = true,

            lutProfile = LUTProfile.penner,

            sssWeight = 1.0f,

            skinLutBias = 0.0f,
            skinLutScale = 0.5f,

            shadowLutBias = 0.0f,
            shadowLutScale = 1.0f,

            normalBlurWeight = 1.0f,
            normalBlurRadius = 0.2f,
            normalBlurDepthRange = 1.0f,

            normalBlurIter = 1,

            colorBleedWeights = new Vector3(0.40f, 0.15f, 0.20f),
            transAbsorption = new Vector3(-8.00f, -48.0f, -64.0f),

            bakedThickness = true,

            transWeight = 1.0f,
            transShadowWeight = 0.5f,
            transDistortion = 0.5f,
            transFalloff = 2.0f,
            thicknessBias = 0.5f,

            microDetailWeight_1 = 0.32f,
            microDetailWeight_2 = 0.32f,

            microDetailTiling = 64.0f,

            phongStrength = 0.5f,
            edgeLength = 2.0f,
            eyebrowoffset = 0.1f
        };

        public static ShadowProperties shadow = new ShadowProperties()
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
            usegtao = false,
            denoise = false,

            quality = QualityPreset.medium,

            intensity = 1.0f,
            lightBias = 0.0f,
            rayRadius = 0.1f,
            meanDepth = 0.5f,
            fadeDepth = 100.0f,
            rayStride = 2,
            screenDiv = 1
        };

        public static SSGIProperties ssgi = new SSGIProperties()
        {
            enabled = false,
            denoise = false,

            quality = QualityPreset.medium,
            samplescale = RenderScale.half,

            intensity = 1.0f,
            secondary = 1.0f,
            rayRadius = 1.0f,
            fadeDepth = 100.0f,
            mixWeight = 0.5f,
            rayStride = 2
        };

        public static SkinProperties skinUpdate;
        public static ShadowProperties shadowUpdate;
        public static SSAOProperties ssaoUpdate;
        public static SSGIProperties ssgiUpdate;

        public static void UpdateSkin()
        {
            bool soft = skin.lutProfile == skinUpdate.lutProfile
                && skin.normalBlurIter == skinUpdate.normalBlurIter;

            bool detail = skin.microDetailWeight_1 == skinUpdate.microDetailWeight_1
                && skin.microDetailWeight_2 == skinUpdate.microDetailWeight_2
                && skin.microDetailTiling == skinUpdate.microDetailTiling
                && skin.eyebrowoffset == skinUpdate.eyebrowoffset
                && skin.phongStrength == skinUpdate.phongStrength
                && skin.edgeLength == skinUpdate.edgeLength;

            skin = skinUpdate;

            HSSSS.DeferredRenderer.UpdateSkinSettings(soft);

            if (!detail)
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

        public static void UpdateShadow()
        {
            shadow = shadowUpdate;

            Shader.DisableKeyword("_PCSS_ON");
            Shader.DisableKeyword("_PCF_TAPS_8");
            Shader.DisableKeyword("_PCF_TAPS_16");
            Shader.DisableKeyword("_PCF_TAPS_32");
            Shader.DisableKeyword("_PCF_TAPS_64");

            if (HSSSS.isStudio)
            {
                if (shadow.pcfState == PCFState.disable)
                {
                    shadow.pcssEnabled = false;
                }

                else
                {
                    switch (shadow.pcfState)
                    {
                        case PCFState.poisson8x: Shader.EnableKeyword("_PCF_TAPS_8"); break;
                        case PCFState.poisson16x: Shader.EnableKeyword("_PCF_TAPS_16"); break;
                        case PCFState.poisson32x: Shader.EnableKeyword("_PCF_TAPS_32"); break;
                        case PCFState.poisson64x: Shader.EnableKeyword("_PCF_TAPS_64"); break;
                    }

                    Shader.SetGlobalTexture("_ShadowJitterTexture", HSSSS.shadowJitter);
                }

                if (shadow.pcssEnabled)
                {
                    Shader.EnableKeyword("_PCSS_ON");
                }

                // pcf & pcss
                Shader.SetGlobalVector("_DirLightPenumbra", shadow.dirLightPenumbra);
                Shader.SetGlobalVector("_SpotLightPenumbra", shadow.spotLightPenumbra);
                Shader.SetGlobalVector("_PointLightPenumbra", shadow.pointLightPenumbra);
            }
        }

        public static void UpdateSSAO()
        {
            bool soft = ssao.quality == ssaoUpdate.quality
                && ssao.usegtao == ssaoUpdate.usegtao
                && ssao.denoise == ssaoUpdate.denoise;

            ssao = ssaoUpdate;

            HSSSS.SSAORenderer.UpdateSSAOSettings(soft);
            HSSSS.SSAORenderer.enabled = ssao.enabled;
        }

        public static void UpdateSSGI()
        {
            bool soft = ssgi.quality == ssgiUpdate.quality
                && ssgi.denoise == ssgiUpdate.denoise
                && ssgi.samplescale == ssgiUpdate.samplescale
                && ssgi.renderscale == ssgiUpdate.renderscale;

            ssgi = ssgiUpdate;

            HSSSS.SSGIRenderer.UpdateSSGISettings(soft);
            HSSSS.SSGIRenderer.enabled = ssgi.enabled;
        }
    }
}
