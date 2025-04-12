using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace HSSSS
{
    public static class AssetLoader
    {
        private static AssetBundle assetBundle;

        // internal shaders
        public static Shader deferredLighting;
        public static Shader deferredReflection;

        // subsurface scattering
        public static Shader sssPrePass;
        public static Shader sssMainPass;
        
        // tangent rendering
        public static Shader drawTangent;

        // soft shadows
        public static Shader softShadows;

        // ssao & ssgi & taau
        public static Shader ssao;
        public static Shader ssgi;
        public static Shader taau;
        public static Shader agx;

        public static GUISkin gui;

        #region Textures
        public static Texture2D pennerDiffuse;
        public static Texture2D nvidiaDiffuse;
        public static Texture2D nvidiaShadow;
        public static Texture2D deepScatter ;

        public static Texture2D skinJitter;
        public static Texture3D blueNoise;

        public static Texture2D femaleBody;
        public static Texture2D femaleHead;
        public static Texture2D maleBody;
        public static Texture2D maleHead;

        public static Texture2D spotCookie;
        #endregion

        #region Materials
        public static Material skin;
        public static Material nail;

        public static Material milk;
        public static Material liquid;

        public static Material bodywet;
        public static Material headwet;
        public static Material headtears;

        public static Material overlay;
        public static Material eyebrow;
        public static Material eyelash;
        public static Material eyeshade;
        public static Material eyeoverlay;
        public static Material cornea;
        public static Material sclera;

        public static Material common;
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
                    femaleBody = ReadAsset<Texture2D>("FemaleBodyThickness", "female body thickness");
                }

                if (femaleHead.LoadImage(File.ReadAllBytes(path[1])))
                {
                    femaleHead.Apply();
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Couldn't load custom texture from " + path[1]);
                    Console.WriteLine("#### HSSSS: Trying built-in texture instead...");
                    femaleHead = ReadAsset<Texture2D>("FemaleHeadThickness", "female head thickness");
                }

                if (maleBody.LoadImage(File.ReadAllBytes(path[2])))
                {
                    maleBody.Apply();
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Couldn't load custom texture from " + path[2]);
                    Console.WriteLine("#### HSSSS: Trying built-in texture instead...");
                    maleBody = ReadAsset<Texture2D>("MaleBodyThickness", "Male body thickness");
                }

                if (maleHead.LoadImage(File.ReadAllBytes(path[3])))
                {
                    maleHead.Apply();
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Couldn't load custom texture from " + path[3]);
                    Console.WriteLine("#### HSSSS: Trying built-in texture instead...");
                    maleHead = ReadAsset<Texture2D>("MaleHeadThickness", "Male head thickness");
                }
            }

            else
            {
                femaleBody = ReadAsset<Texture2D>("FemaleBodyThickness", "female body thickness");
                femaleHead = ReadAsset<Texture2D>("FemaleHeadThickness", "female head thickness");
                maleBody = ReadAsset<Texture2D>("MaleBodyThickness", "Male body thickness");
                maleHead = ReadAsset<Texture2D>("MaleHeadThickness", "Male head thickness");
            }

            // pre-integrate lookup textures
            pennerDiffuse = ReadAsset<Texture2D>("DefaultSkinLUT", "penner diffuse lut");
            nvidiaDiffuse = ReadAsset<Texture2D>("FaceWorksSkinLUT", "nvidia diffuse lut");
            nvidiaShadow = ReadAsset<Texture2D>("FaceWorksShadowLUT", "nvidia shadow lut");
            deepScatter = ReadAsset<Texture2D>("DeepScatterLUT", "deep scattering lut");

            // stochastic textures
            skinJitter = ReadAsset<Texture2D>("SkinJitter", "SSS blur jittering texture");
            blueNoise = ReadAsset<Texture3D>("BlueNoise", "blue noise texture");

            // spotlight cookie
            spotCookie = ReadAsset<Texture2D>("DefaultSpotCookie", "spotlight cookie");
        }

        private static void LoadMaterials()
        {
            // skin
            skin = ReadAsset<Material>("Skin", "skin material");
            nail = ReadAsset<Material>("Nail", "nail material");
            overlay = ReadAsset<Material>("Overlay", "skin overlay material");
            liquid = ReadAsset<Material>("Liquid", "liquid material");
            milk = ReadAsset<Material>("OverlayForward", "milk material");

            eyebrow = ReadAsset<Material>("Eyebrow", "eyebrow material");
            eyelash = ReadAsset<Material>("Eyelash", "eyelash material");
            sclera = ReadAsset<Material>("Sclera", "sclera material");
            cornea = ReadAsset<Material>("Cornea", "cornea material");

            // eye shade
            eyeshade = ReadAsset<Material>("Eyeshade", "eye shade material");

            // eye overlay
            eyeoverlay = ReadAsset<Material>("OverlayForward", "eye overlay material");

            // wet materials
            bodywet = ReadAsset<Material>("Bodywet", "body wet material");
            headwet = ReadAsset<Material>("Headwet", "head wet material");
            headtears = ReadAsset<Material>("Headtears", "head tears material");
            
            // common
            common = ReadAsset<Material>("Common", "common material");
        }

        private static void LoadMiscellaneous()
        {
            deferredLighting = ReadAsset<Shader>("InternalDeferredShading", "deferred lighting shader");
            deferredReflection = ReadAsset<Shader>("InternalDeferredReflections", "deferred reflection shader");
            sssPrePass = ReadAsset<Shader>("SSSPrePass", "SSS prepass shader");
            sssMainPass = ReadAsset<Shader>("SSSMainPass", "SSS mainpass shader");
            softShadows = ReadAsset<Shader>("SoftShadows", "Soft shadow shader");
            drawTangent = ReadAsset<Shader>("DrawTangent", "draw tangent shader");
            ssao = ReadAsset<Shader>("SSAO", "SSAO shader");
            ssgi = ReadAsset<Shader>("SSGI", "SSGI shader");
            taau = ReadAsset<Shader>("TemporalAntiAliasing", "Temporal Anti Aliasing");
            agx = ReadAsset<Shader>("AgX", "AgX Tone Mapper");
            gui = ReadAsset<GUISkin>("GUISkin", "GUI skin");
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
                        material.SetFloat(Properties.tess.phong.Key, Properties.tess.phong.Value);
                        material.SetFloat(Properties.tess.edge.Key, Properties.tess.edge.Value);
                    }

                    else
                    {
                        material.shader.maximumLOD = Properties.skin.microDetails ? 350 : 300;
                    }
                }
            }

            if (Properties.misc.fixOverlay)
            {
                eyebrow.SetFloat(Properties.tess.phong.Key, Properties.tess.phong.Value);
                eyebrow.SetFloat(Properties.tess.edge.Key, Properties.tess.edge.Value);
                eyebrow.SetFloat(Properties.misc.wrapOffset.Key, Properties.misc.wrapOffset.Value);
            }

            skin.SetFloat(Properties.skin.microDetailWeight_1.Key, Properties.skin.microDetailWeight_1.Value);
            skin.SetFloat(Properties.skin.microDetailWeight_2.Key, Properties.skin.microDetailWeight_2.Value);

            Vector2 tiling = new Vector2(Math.Max(Properties.skin.microDetailTiling, 0.01f), Math.Max(Properties.skin.microDetailTiling, 0.01f));

            skin.SetTextureScale("_DetailNormalMap_2", tiling);
            skin.SetTextureScale("_DetailNormalMap_3", tiling);
            skin.SetTextureScale("_DetailSkinPoreMap", tiling);
        }

        private static T ReadAsset<T>(string name, string desc) where T : UnityEngine.Object
        {
            T asset = assetBundle.LoadAsset<T>(name);

            if (asset == null)
            {
                Console.WriteLine("#### HSSSS: Couldn't load " + desc);
            }

            return asset;
        }
    }
}
