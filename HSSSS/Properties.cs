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

        public enum ResolveResolution
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
            public ResolveResolution resolution;

            public float intensity;
            public float lightBias;
            public float rayRadius;
            public float meanDepth;
            public float fadeDepth;
            public float mixWeight;
            public int rayStride;
        }

        public struct SSGIProperties
        {
            public bool enabled;
            public bool denoise;

            public QualityPreset quality;
            public ResolveResolution resolution;

            public float intensity;
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
            resolution = ResolveResolution.half,

            intensity = 1.0f,
            lightBias = 0.0f,
            rayRadius = 0.1f,
            meanDepth = 0.5f,
            fadeDepth = 100.0f,
            mixWeight = 0.5f,
            rayStride = 2
        };

        public static SSGIProperties ssgi = new SSGIProperties()
        {
            enabled = false,
            denoise = false,

            quality = QualityPreset.medium,
            resolution = ResolveResolution.half,

            intensity = 1.0f,
            rayRadius = 1.0f,
            fadeDepth = 100.0f,
            mixWeight = 0.5f,
            rayStride = 2
        };

        public static void UpdateSSAO(SSAOProperties update)
        {
            bool soft = ssao.quality == update.quality
                && ssao.usegtao == update.usegtao
                && ssao.denoise == update.denoise;

            ssao = update;

            HSSSS.SSAORenderer.UpdateSSAOSettings(soft);
            HSSSS.SSAORenderer.enabled = ssao.enabled;
        }

        public static void UpdateSSGI(SSGIProperties update)
        {
            bool soft = ssgi.quality == update.quality
                && ssgi.denoise == update.denoise
                && ssgi.resolution == update.resolution;

            ssgi = update;

            HSSSS.SSGIRenderer.UpdateSSGISettings(soft);
            HSSSS.SSGIRenderer.enabled = ssgi.enabled;
        }
    }
}
