using System;
using System.IO;
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

        // ssao & ssgi
        public static Shader ssao;
        public static Shader ssgi;

        public static GUISkin gui;

        #region Textures
        public static Texture2D pennerDiffuse;
        public static Texture2D nvidiaDiffuse;
        public static Texture2D nvidiaShadow;
        public static Texture2D deepScatter;

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
        public static Material milk;
        public static Material liquid;
        public static Material overlay;
        public static Material eyebrow;
        public static Material eyelash;
        public static Material cornea;
        public static Material sclera;
        public static Material eyeOverlay;
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
            ReadAsset<Material>(ref overlay, "Overlay", "skin overlay material");
            ReadAsset<Material>(ref liquid, "Liquid", "liquid material");
            ReadAsset<Material>(ref milk, "OverlayForward", "milk material");

            if (HSSSS.fixAlphaShadow)
            {
                ReadAsset<Material>(ref eyebrow, "Eyebrow", "eyebrow material");
                ReadAsset<Material>(ref eyelash, "Eyelash", "eyelash material");
                ReadAsset<Material>(ref sclera, "Sclera", "sclera material");

                // cornea
                if (HSSSS.useEyePOMShader)
                {
                    ReadAsset<Material>(ref cornea, "Cornea", "cornea material");
                }

                else
                {
                    ReadAsset<Material>(ref cornea, "OverlayForward", "cornea material");
                }

                // eye overlay
                ReadAsset<Material>(ref eyeOverlay, "OverlayForward", "eye overlay material");
            }
        }

        private static void LoadMiscellaneous()
        {
            ReadAsset<Shader>(ref deferredLighting, "InternalDeferredShading", "deferred lighting shader");
            ReadAsset<Shader>(ref deferredReflection, "InternalDeferredReflections", "deferred reflection shader");
            ReadAsset<Shader>(ref sssPrePass, "SSSPrePass", "SSS prepass shader");
            ReadAsset<Shader>(ref sssMainPass, "SSSMainPass", "SSS mainpass shader");
            ReadAsset<Shader>(ref ssao, "SSAO", "SSAO shader");
            ReadAsset<Shader>(ref ssgi, "SSGI", "SSGI shader");
            ReadAsset<GUISkin>(ref gui, "GUISkin", "GUI skin");
        }

        public static void LoadEverything()
        {
            LoadAssetBundle();
            LoadTextures();
            LoadMaterials();
            LoadMiscellaneous();
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
