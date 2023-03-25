using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace HSSSS
{
    public class AssetLoader
    {
        internal static AssetBundle assetBundle;

        // body materials
        public static Material skinMaterial;
        public static Material milkMaterial;
        public static Material liquidMaterial;
        public static Material overlayMaterial;
        public static Material eyeBrowMaterial;
        public static Material eyeLashMaterial;
        public static Material eyeCorneaMaterial;
        public static Material eyeScleraMaterial;
        public static Material eyeOverlayMaterial;

        // thickness textures
        public static Texture2D femaleBodyThickness;
        public static Texture2D femaleHeadThickness;
        public static Texture2D maleBodyThickness;
        public static Texture2D maleHeadThickness;

        private static Dictionary<Texture2D, string> textures = new Dictionary<Texture2D, string>()
        {
            { femaleBodyThickness, "FemaleBodyThickness" },
            { femaleHeadThickness, "FemaleHeadThickness" },
            { maleBodyThickness, "MaleBodyThickness" },
            { maleHeadThickness, "MaleHeadThickness" }
        };

        private static Dictionary<Material, string> materials = new Dictionary<Material, string>()
        {
            { skinMaterial, "Skin" },
            { milkMaterial, "Milk" },
            { liquidMaterial, "Liquid" },
            { overlayMaterial, "Overlay" },
            { eyeBrowMaterial, "EyeBrow" },
            { eyeLashMaterial, "EyeLash" }
        };

        private static Dictionary<Shader, string> shaders = new Dictionary<Shader, string>()
        {
        };

        public static void LoadAssetBundle()
        {
            assetBundle = AssetBundle.LoadFromMemory(Resources.hssssresources);
        }

        public static void LoadBodyAssets()
        {
            // custom thickness texture
            if (HSSSS.useCustomThickness)
            {
                LoadCustomThickness();
            }

            // built-in thickness texture
            else
            {
                femaleBodyThickness = assetBundle.LoadAsset<Texture2D>("FemaleBodyThickness");
                femaleHeadThickness = assetBundle.LoadAsset<Texture2D>("FemaleHeadThickness");
                maleBodyThickness = assetBundle.LoadAsset<Texture2D>("MaleBodyThickness");
                maleHeadThickness = assetBundle.LoadAsset<Texture2D>("MaleHeadThickness");
            }
        }

        public static void LoadEffectAssets()
        {

        }

        private static void LoadCustomThickness()
        {
            string femaleBodyCustom = Path.Combine(HSSSS.pluginLocation, HSSSS.femaleBodyCustom);
            string femaleHeadCustom = Path.Combine(HSSSS.pluginLocation, HSSSS.femaleHeadCustom);
            string maleBodyCustom = Path.Combine(HSSSS.pluginLocation, HSSSS.maleBodyCustom);
            string maleHeadCustom = Path.Combine(HSSSS.pluginLocation, HSSSS.maleHeadCustom);

            femaleBodyThickness = new Texture2D(4, 4, TextureFormat.RGBA32, true, true);
            femaleHeadThickness = new Texture2D(4, 4, TextureFormat.RGBA32, true, true);
            maleBodyThickness = new Texture2D(4, 4, TextureFormat.RGBA32, true, true);
            maleHeadThickness = new Texture2D(4, 4, TextureFormat.RGBA32, true, true);

            // female body
            if (femaleBodyThickness.LoadImage(File.ReadAllBytes(femaleBodyCustom)))
            {
                femaleBodyThickness.Apply();
            }

            else
            {
                Console.Write("#### HSSSS: Could not load " + femaleBodyCustom + ", using built-in texture...");
                femaleBodyThickness = assetBundle.LoadAsset<Texture2D>("FemaleBodyThickness");
            }

            // female head
            if (femaleHeadThickness.LoadImage(File.ReadAllBytes(femaleHeadCustom)))
            {
                femaleHeadThickness.Apply();
            }

            else
            {
                Console.Write("#### HSSSS: Could not load " + femaleHeadCustom + ", using built-in texture...");
                femaleHeadThickness = assetBundle.LoadAsset<Texture2D>("FemaleHeadThickness");
            }

            // male body
            if (maleBodyThickness.LoadImage(File.ReadAllBytes(maleBodyCustom)))
            {
                maleBodyThickness.Apply();
            }

            else
            {
                Console.Write("#### HSSSS: Could not load " + maleBodyCustom + ", using built-in texture...");
                maleBodyThickness = assetBundle.LoadAsset<Texture2D>("MaleBodyThickness");
            }

            // male head
            if (maleHeadThickness.LoadImage(File.ReadAllBytes(maleHeadCustom)))
            {
                maleHeadThickness.Apply();
            }

            else
            {
                Console.Write("#### HSSSS: Could not load " + femaleHeadCustom + ", using built-in texture...");
                maleHeadThickness = assetBundle.LoadAsset<Texture2D>("maleHeadThickness");
            }
        }
    }
}
