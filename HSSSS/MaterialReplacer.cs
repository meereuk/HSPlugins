using System;
using System.Collections.Generic;
using UnityEngine;

namespace HSSSS
{
    public static class MaterialReplacer
    {
        private static readonly Dictionary<string, string> colorProps = new Dictionary<string, string>()
        {
            { "_Color", "_Color" },
            { "_SpecColor", "_SpecColor"},
            { "_EmissionColor", "_EmissionColor" },
        };

        private static readonly Dictionary<string, string> floatProps = new Dictionary<string, string>()
        {
            { "_Metallic", "_Metallic" },
            { "_Smoothness", "_Smoothness" },
            { "_Glossiness", "_Smoothness" },
            { "_OcclusionStrength", "_OcclusionStrength" },
            { "_BumpScale", "_BumpScale" },
            { "_NormalStrength", "_BumpScale" },
            { "_BlendNormalMapScale", "_BlendNormalMapScale" },
            { "_DetailNormalMapScale", "_DetailNormalMapScale" },
        };

        private static readonly Dictionary<string, string> textureProps = new Dictionary<string, string>()
        {
            { "_MainTex", "_MainTex" },
            { "_SpecGlossMap", "_SpecGlossMap" },
            { "_OcclusionMap", "_OcclusionMap" },
            { "_BumpMap", "_BumpMap" },
            { "_NormalMap", "_BumpMap" },
            { "_BlendNormalMap", "_BlendNormalMap" },
            { "_DetailNormalMap", "_DetailNormalMap" }
        };

        public static void SkinReplacer(CharInfo ___chaInfo, CharReference.TagObjKey tagKey, Material mat)
        {
            if (mat)
            {
                if (WillReplaceShader(mat.shader))
                {
                    ShaderReplacer(AssetLoader.skin, mat);

                    // body thickness texture
                    if (tagKey == CharReference.TagObjKey.ObjSkinBody)
                    {
                        if (___chaInfo.Sex == 0)
                        {
                            mat.SetTexture("_Thickness", AssetLoader.maleBody);
                        }

                        else if (___chaInfo.Sex == 1)
                        {
                            mat.SetTexture("_Thickness", AssetLoader.femaleBody);
                        }
                    }

                    // face thickness texture
                    else if (tagKey == CharReference.TagObjKey.ObjSkinFace)
                    {
                        if (___chaInfo.Sex == 0)
                        {
                            mat.SetTexture("_Thickness", AssetLoader.maleHead);
                        }

                        else if (___chaInfo.Sex == 1)
                        {
                            mat.SetTexture("_Thickness", AssetLoader.femaleHead);
                        }
                    }

                    Console.WriteLine("#### HSSSS Replaced " + mat);
                }
            }
        }

        public static void ScleraReplacer(CharInfo ___chaInfo)
        {
            CommonReplacer(___chaInfo, CharReference.TagObjKey.ObjEyeW);
        }

        public static void NailReplacer(CharInfo ___chaInfo)
        {
            CommonReplacer(___chaInfo, CharReference.TagObjKey.ObjNail);
        }

        public static void MiscReplacer(CharFemaleBody __instance)
        {
            // face blush
            if (null != __instance.matHohoAka)
            {
                if (WillReplaceShader(__instance.matHohoAka.shader))
                {
                    ShaderReplacer(AssetLoader.overlay, __instance.matHohoAka);
                    Console.WriteLine("#### HSSSS Replaced " + __instance.matHohoAka.name);
                }
            }

            List<GameObject> faceObjs = __instance.chaInfo.GetTagInfo(CharReference.TagObjKey.ObjSkinFace);

            if (faceObjs.Count > 0)
            {
                Renderer renderer = faceObjs[0].GetComponent<Renderer>();

                if (renderer && renderer.materials.Length > 1)
                {
                    Material material = renderer.materials[1];

                    if (material)
                    {
                        if (WillReplaceShader(material.shader))
                        {
                            ShaderReplacer(AssetLoader.overlay, material);
                            Console.WriteLine("#### HSSSS Replaced " + material.name);
                        }
                    }
                }
            }

            // tongue
            GameObject objHead = __instance.objHead;

            if (objHead)
            {
                GameObject objTongue = null;

                if (objHead.transform.Find("cf_N_head/cf_O_sita"))
                {
                    objTongue = objHead.transform.Find("cf_N_head/cf_O_sita").gameObject;
                }

                if (objTongue)
                {
                    Material material = objTongue.GetComponent<Renderer>().material;

                    if (material)
                    {
                        if (WillReplaceShader(material.shader))
                        {
                            ShaderReplacer(AssetLoader.skin, material);
                            Console.WriteLine("#### HSSSS Replaced " + material.name);
                        }
                    }
                }
            }

            if (HSSSS.fixAlphaShadow)
            {
                if (objHead)
                {
                    // eye shade
                    GameObject objShade = null;

                    if (objHead.transform.Find("cf_N_head/cf_O_eyekage"))
                    {
                        objShade = objHead.transform.Find("cf_N_head/cf_O_eyekage").gameObject;
                    }

                    else if (objHead.transform.Find("cf_N_head/cf_O_eyekage1"))
                    {
                        objShade = objHead.transform.Find("cf_N_head/cf_O_eyekage1").gameObject;
                    }

                    if (objShade)
                    {
                        Renderer renderer = objShade.GetComponent<Renderer>();

                        if (renderer)
                        {
                            Material material = renderer.sharedMaterial;

                            if (material)
                            {
                                if (WillReplaceShader(material.shader))
                                {
                                    ShaderReplacer(AssetLoader.eyeshade, material);
                                    Console.WriteLine("#### HSSSS Replaced " + material.name);
                                }
                            }
                        }
                    }

                    // tears
                    for (int i = 1; i < 4; i++)
                    {
                        GameObject objTears = objHead.transform.Find("cf_N_head/N_namida/cf_O_namida" + i.ToString("00")).gameObject;

                        if (objTears)
                        {
                            Renderer renderer = objTears.GetComponent<Renderer>();

                            if (renderer)
                            {
                                if (!renderer.receiveShadows)
                                {
                                    renderer.receiveShadows = true;
                                }

                                Material material = renderer.sharedMaterial;

                                if (material)
                                {
                                    if (WillReplaceShader(material.shader))
                                    {
                                        material.shader = AssetLoader.liquid.shader;
                                        material.CopyPropertiesFromMaterial(AssetLoader.liquid);
                                        /*
                                        ShaderReplacer(liquidMaterial, material);
                                        material.SetColor("_Color", new Color(0.6f, 0.6f, 0.6f, 0.6f));
                                        material.SetColor("_EmissionColor", new Color(0.0f, 0.0f, 0.0f, 1.0f));
                                        material.renderQueue = 2453;
                                        Console.WriteLine("#### HSSSS Replaced " + material.name);
                                        */
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void CommonReplacer(CharInfo ___chaInfo, CharReference.TagObjKey key)
        {
            foreach (GameObject obj in ___chaInfo.GetTagInfo(key))
            {
                foreach (Renderer rend in obj.GetComponents<Renderer>())
                {
                    foreach (Material mat in rend.sharedMaterials)
                    {
                        ObjectParser(mat, key);
                        Console.WriteLine("#### HSSSS Replaced " + mat.name);
                    }

                    if (!rend.receiveShadows)
                    {
                        rend.receiveShadows = true;
                    }
                }
            }
        }

        public static void MilkReplacer()
        {
            foreach (KeyValuePair<string, Material> entry in Manager.Character.Instance.dictSiruMaterial)
            {
                Material material = entry.Value;

                if (material)
                {
                    switch (material.name)
                    {
                        case "cf_M_k_kaosiru01":
                            material.shader = AssetLoader.headwet.shader;
                            material.CopyPropertiesFromMaterial(AssetLoader.headwet);
                            Console.WriteLine("#### HSSSS Replaced " + material.name);
                            break;

                        case "cf_M_k_munesiru01":
                            material.shader = AssetLoader.bodywet.shader;
                            material.CopyPropertiesFromMaterial(AssetLoader.bodywet);
                            Console.WriteLine("#### HSSSS Replaced " + material.name);
                            break;

                        default:
                            ShaderReplacer(AssetLoader.milk, material);
                            material.SetColor("_Color", new Color(0.8f, 0.8f, 0.8f, 0.2f));
                            Console.WriteLine("#### HSSSS Replaced " + material.name);
                            break;
                    }
                }
            }
        }

        private static void ObjectParser(Material material, CharReference.TagObjKey tag)
        {
            switch (tag)
            {
                case CharReference.TagObjKey.ObjUnderHair:
                    ShaderReplacer(AssetLoader.overlay, material);
                    break;

                case CharReference.TagObjKey.ObjEyelashes:
                    ShaderReplacer(AssetLoader.eyelash, material);
                    break;

                case CharReference.TagObjKey.ObjEyebrow:
                    ShaderReplacer(AssetLoader.eyebrow, material);
                    break;

                case CharReference.TagObjKey.ObjEyeHi:
                    ShaderReplacer(AssetLoader.eyeoverlay, material);
                    material.renderQueue = 2451;
                    break;

                case CharReference.TagObjKey.ObjEyeW:
                    ShaderReplacer(AssetLoader.sclera, material);
                    break;

                case CharReference.TagObjKey.ObjEyeL:
                    ShaderReplacer(AssetLoader.cornea, material);
                    if (HSSSS.useEyePOMShader)
                    {
                        material.SetTexture("_SpecGlossMap", null);
                        material.SetTexture("_EmissionMap", material.GetTexture("_MainTex"));
                    }
                    break;

                case CharReference.TagObjKey.ObjEyeR:
                    ShaderReplacer(AssetLoader.cornea, material);
                    if (HSSSS.useEyePOMShader)
                    {
                        material.SetTexture("_SpecGlossMap", null);
                        material.SetTexture("_EmissionMap", material.GetTexture("_MainTex"));
                    }
                    break;

                case CharReference.TagObjKey.ObjNip:
                    ShaderReplacer(AssetLoader.overlay, material);
                    break;

                case CharReference.TagObjKey.ObjNail:
                    ShaderReplacer(AssetLoader.nail, material);
                    break;

                default:
                    break;
            }
        }

        private static void ShaderReplacer(Material source, Material target)
        {
            Material cache = new Material(source: target);

            target.shader = source.shader;
            target.CopyPropertiesFromMaterial(source);

            foreach (KeyValuePair<string, string> entry in textureProps)
            {
                if (cache.HasProperty(entry.Key))
                {
                    if (target.GetTexture(entry.Key) == null)
                    {
                        target.SetTexture(entry.Value, cache.GetTexture(entry.Key));
                        target.SetTextureScale(entry.Value, cache.GetTextureScale(entry.Key));
                        target.SetTextureOffset(entry.Value, cache.GetTextureOffset(entry.Key));
                    }
                }
            }

            foreach (KeyValuePair<string, string> entry in colorProps)
            {
                if (cache.HasProperty(entry.Key))
                {
                    target.SetColor(entry.Value, cache.GetColor(entry.Key));
                }
            }

            foreach (KeyValuePair<string, string> entry in floatProps)
            {
                if (cache.HasProperty(entry.Key))
                {
                    target.SetFloat(entry.Value, cache.GetFloat(entry.Key));
                }
            }
        }

        private static bool WillReplaceShader(Shader shader)
        {
            return shader.name.Contains("Shader Forge/")
                || shader.name.Contains("HSStandard/")
                || shader.name.Contains("Unlit/")
                || shader.name.Equals("Standard")
                || shader.name.Equals("Standard (Specular Setup)");
        }
    }
}
