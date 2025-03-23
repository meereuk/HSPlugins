using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace HSSSS
{
    public static class AssetLoader
    {
        private static AssetBundle assetBundle = null;

        // internal shaders
        public static Shader deferredLighting = null;
        public static Shader deferredReflection = null;

        // subsurface scattering
        public static Shader sssPrePass = null;
        public static Shader sssMainPass = null;

        // soft shadows
        public static Shader softShadows = null;

        // ssao & ssgi & taau
        public static Shader ssao = null;
        public static Shader ssgi = null;
        public static Shader taau = null;
        public static Shader agx = null;

        public static GUISkin gui = null;

        #region Textures
        public static Texture2D pennerDiffuse = null;
        public static Texture2D nvidiaDiffuse = null;
        public static Texture2D nvidiaShadow = null;
        public static Texture2D deepScatter = null;

        public static Texture2D skinJitter = null;
        public static Texture3D blueNoise = null;

        public static Texture2D femaleBody = null;
        public static Texture2D femaleHead = null;
        public static Texture2D maleBody = null;
        public static Texture2D maleHead = null;

        public static Texture2D spotCookie = null;
        #endregion

        #region Materials
        public static Material skin = null;
        public static Material nail = null;

        public static Material milk = null;
        public static Material liquid = null;

        public static Material bodywet = null;
        public static Material headwet = null;
        public static Material headtears = null;

        public static Material overlay = null;
        public static Material eyebrow = null;
        public static Material eyelash = null;
        public static Material eyeshade = null;
        public static Material eyeoverlay = null;
        public static Material cornea = null;
        public static Material sclera = null;
        #endregion

        private static void LoadAssetBundle()
        {
            assetBundle = AssetBundle.LoadFromMemory(Resources.hssssresources);
        }

        private static void LoadTextures()
        {
            // body thickness
            if (HSSSS.useCustomThickness)
            {
                string[] path =
                {
                    Path.Combine(HSSSS.pluginLocation, HSSSS.femaleBodyCustom),
                    Path.Combine(HSSSS.pluginLocation, HSSSS.femaleHeadCustom),
                    Path.Combine(HSSSS.pluginLocation, HSSSS.maleBodyCustom),
                    Path.Combine(HSSSS.pluginLocation, HSSSS.maleBodyCustom)
                };

                femaleBody = new Texture2D(4, 4, TextureFormat.ARGB32, true, true);
                femaleHead = new Texture2D(4, 4, TextureFormat.ARGB32, true, true);
                maleBody = new Texture2D(4, 4, TextureFormat.ARGB32, true, true);
                maleHead = new Texture2D(4, 4, TextureFormat.ARGB32, true, true);

                if (femaleBody.LoadImage(File.ReadAllBytes(path[0])))
                {
                    femaleBody.Apply();
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Couldn't load custom texture from " + path[0]);
                    Console.WriteLine("#### HSSSS: Trying built-in texture instead...");
                    ReadAsset<Texture2D>(ref femaleBody, "FemaleBodyThickness", "female body thickness");
                }

                if (femaleHead.LoadImage(File.ReadAllBytes(path[1])))
                {
                    femaleHead.Apply();
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Couldn't load custom texture from " + path[1]);
                    Console.WriteLine("#### HSSSS: Trying built-in texture instead...");
                    ReadAsset<Texture2D>(ref femaleHead, "FemaleHeadThickness", "female head thickness");
                }

                if (maleBody.LoadImage(File.ReadAllBytes(path[2])))
                {
                    maleBody.Apply();
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Couldn't load custom texture from " + path[2]);
                    Console.WriteLine("#### HSSSS: Trying built-in texture instead...");
                    ReadAsset<Texture2D>(ref maleBody, "MaleBodyThickness", "Male body thickness");
                }

                if (maleHead.LoadImage(File.ReadAllBytes(path[3])))
                {
                    maleHead.Apply();
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Couldn't load custom texture from " + path[3]);
                    Console.WriteLine("#### HSSSS: Trying built-in texture instead...");
                    ReadAsset<Texture2D>(ref maleHead, "MaleHeadThickness", "Male head thickness");
                }
            }

            else
            {
                ReadAsset<Texture2D>(ref femaleBody, "FemaleBodyThickness", "female body thickness");
                ReadAsset<Texture2D>(ref femaleHead, "FemaleHeadThickness", "female head thickness");
                ReadAsset<Texture2D>(ref maleBody, "MaleBodyThickness", "Male body thickness");
                ReadAsset<Texture2D>(ref maleHead, "MaleHeadThickness", "Male head thickness");
            }

            // pre-integrate lookup textures
            ReadAsset<Texture2D>(ref pennerDiffuse, "DefaultSkinLUT", "penner diffuse lut");
            ReadAsset<Texture2D>(ref nvidiaDiffuse, "FaceWorksSkinLUT", "nvidia diffuse lut");
            ReadAsset<Texture2D>(ref nvidiaShadow, "FaceWorksShadowLUT", "nvidia shadow lut");
            ReadAsset<Texture2D>(ref deepScatter, "DeepScatterLUT", "deep scattering lut");

            // stochastic textures
            ReadAsset<Texture2D>(ref skinJitter, "SkinJitter", "SSS blur jittering texture");
            ReadAsset<Texture3D>(ref blueNoise, "BlueNoise", "blue noise texture");

            // spotlight cookie
            ReadAsset<Texture2D>(ref spotCookie, "DefaultSpotCookie", "spotlight cookie");
        }

        private static void LoadMaterials()
        {
            // skin
            ReadAsset<Material>(ref skin, "Skin", "skin material");
            ReadAsset<Material>(ref nail, "Nail", "nail material");
            ReadAsset<Material>(ref overlay, "Overlay", "skin overlay material");
            ReadAsset<Material>(ref liquid, "Liquid", "liquid material");
            ReadAsset<Material>(ref milk, "OverlayForward", "milk material");

            ReadAsset<Material>(ref eyebrow, "Eyebrow", "eyebrow material");
            ReadAsset<Material>(ref eyelash, "Eyelash", "eyelash material");
            ReadAsset<Material>(ref sclera, "Sclera", "sclera material");
            ReadAsset<Material>(ref cornea, "Cornea", "cornea material");

            // eye shade
            ReadAsset<Material>(ref eyeshade, "Eyeshade", "eye shade material");

            // eye overlay
            ReadAsset<Material>(ref eyeoverlay, "OverlayForward", "eye overlay material");

            // wet materials
            ReadAsset<Material>(ref bodywet, "Bodywet", "body wet material");
            ReadAsset<Material>(ref headwet, "Headwet", "head wet material");
            ReadAsset<Material>(ref headtears, "Headtears", "head tears material");
        }

        private static void LoadMiscellaneous()
        {
            ReadAsset<Shader>(ref deferredLighting, "InternalDeferredShading", "deferred lighting shader");
            ReadAsset<Shader>(ref deferredReflection, "InternalDeferredReflections", "deferred reflection shader");
            ReadAsset<Shader>(ref sssPrePass, "SSSPrePass", "SSS prepass shader");
            ReadAsset<Shader>(ref sssMainPass, "SSSMainPass", "SSS mainpass shader");
            ReadAsset<Shader>(ref softShadows, "SoftShadows", "Soft shadow shader");
            ReadAsset<Shader>(ref ssao, "SSAO", "SSAO shader");
            ReadAsset<Shader>(ref ssgi, "SSGI", "SSGI shader");
            ReadAsset<Shader>(ref taau, "TemporalAntiAliasing", "Temporal Anti Aliasing");
            ReadAsset<Shader>(ref agx, "AgX", "AgX Tone Mapper");
            ReadAsset<GUISkin>(ref gui, "GUISkin", "GUI skin");
        }

        public static void LoadEverything()
        {
            LoadAssetBundle();
            LoadTextures();
            LoadMaterials();
            LoadMiscellaneous();
        }

        public static void RefreshMaterials()
        {
            List<Material> materialList = new List<Material>()
            {
                skin, nail, milk, liquid, overlay, eyelash, cornea, sclera, eyeshade, eyeoverlay, bodywet, headwet
            };

            foreach (Material material in materialList)
            {
                if (material)
                {
                    if (Properties.tess.enabled)
                    {
                        material.shader.maximumLOD = Properties.skin.microDetails ? 450 : 400;
                        material.SetFloat("_Phong", Properties.tess.phong);
                        material.SetFloat("_EdgeLength", Properties.tess.edge);
                    }

                    else
                    {
                        material.shader.maximumLOD = Properties.skin.microDetails ? 350 : 300;
                    }
                }
            }

            if (Properties.misc.fixOverlay)
            {
                eyebrow.SetFloat("_Phong", Properties.tess.phong);
                eyebrow.SetFloat("_EdgeLength", Properties.tess.edge);
                eyebrow.SetFloat("_VertexWrapOffset", Properties.misc.wrapOffset);
            }

            skin.SetFloat("_DetailNormalMapScale_2", Properties.skin.microDetailWeight_1);
            skin.SetFloat("_DetailNormalMapScale_3", Properties.skin.microDetailWeight_2);

            Vector2 tiling = new Vector2(Math.Max(Properties.skin.microDetailTiling, 0.01f), Math.Max(Properties.skin.microDetailTiling, 0.01f));

            skin.SetTextureScale("_DetailNormalMap_2", tiling);
            skin.SetTextureScale("_DetailNormalMap_3", tiling);
            skin.SetTextureScale("_DetailSkinPoreMap", tiling);
        }

        private static void ReadAsset<T>(ref T asset, string name, string desc) where T : UnityEngine.Object
        {
            asset = assetBundle.LoadAsset<T>(name);

            if (asset == null)
            {
                Console.WriteLine("#### HSSSS: Couldn't load " + desc);
            }
        }
    }
}
