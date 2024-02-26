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

        private static Dictionary<string, Material> milks = null;

        public static void ReplaceSkin(CharInfo ___chaInfo, CharReference.TagObjKey tagKey, Material mat)
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

        public static void ReplaceSclera(CharInfo ___chaInfo)
        {
            ReplaceCommon(___chaInfo, CharReference.TagObjKey.ObjEyeW);
        }

        public static void ReplaceNail(CharInfo ___chaInfo)
        {
            ReplaceCommon(___chaInfo, CharReference.TagObjKey.ObjNail);
        }

        public static void ReplaceMiscFemale(CharFemaleBody __instance)
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

            GameObject objHead = __instance.objHead;

            if (objHead)
            {
                // tongue
                if (objHead.transform.Find("cf_N_head/cf_O_sita"))
                {
                    ObjectReplacer(objHead.transform.Find("cf_N_head/cf_O_sita").gameObject, AssetLoader.skin);
                }

                if (Properties.misc.fixOverlay)
                {
                    // eye shade
                    if (objHead.transform.Find("cf_N_head/cf_O_eyekage"))
                    {
                        ObjectReplacer(objHead.transform.Find("cf_N_head/cf_O_eyekage").gameObject, AssetLoader.eyeshade, false);
                    }

                    else if (objHead.transform.Find("cf_N_head/cf_O_eyekage1"))
                    {
                        ObjectReplacer(objHead.transform.Find("cf_N_head/cf_O_eyekage1").gameObject, AssetLoader.eyeshade, false);
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
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void ReplaceMiscMale(CharMaleBody __instance)
        {
            GameObject objHead = __instance.objHead;

            if (objHead)
            {
                // tongue
                if (objHead.transform.Find("cm_N_O_head/cm_O_sita"))
                {
                    ObjectReplacer(objHead.transform.Find("cm_N_O_head/cm_O_sita").gameObject, AssetLoader.skin);
                }

                // eye highlights
                if (Properties.misc.fixOverlay)
                {
                    if (objHead.transform.Find("cm_N_O_head/cm_O_eyeHi_R"))
                    {
                        ObjectReplacer(objHead.transform.Find("cm_N_O_head/cm_O_eyeHi_R").gameObject, AssetLoader.eyeoverlay);
                    }

                    if (objHead.transform.Find("cm_N_O_head/cm_O_eyeHi_L"))
                    {
                        ObjectReplacer(objHead.transform.Find("cm_N_O_head/cm_O_eyeHi_L").gameObject, AssetLoader.eyeoverlay);
                    }
                }
            }

            // public hair
            if (Properties.misc.fixOverlay)
            {
                foreach (GameObject objBody in __instance.chaInfo.GetTagInfo(CharReference.TagObjKey.ObjSkinBody))
                {
                    if (objBody.transform.parent.FindChild("O_mnpk"))
                    {
                        ObjectReplacer(objBody.transform.parent.FindChild("O_mnpk").gameObject, AssetLoader.overlay);
                    }

                    if (objBody.transform.FindChild("O_mnpk"))
                    {
                        ObjectReplacer(objBody.transform.FindChild("O_mnpk").gameObject, AssetLoader.overlay);
                    }
                }
            }
        }

        public static void ReplaceCommon(CharInfo ___chaInfo, CharReference.TagObjKey key)
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

        public static void StoreMilk()
        {
            milks = new Dictionary<string, Material>();
            var CharacterManager = Singleton<Manager.Character>.Instance;

            foreach (KeyValuePair<string, Material> entry in CharacterManager.dictSiruMaterial)
            {
                milks.Add(entry.Key, new Material(source: entry.Value));
            }
        }

        public static void RestoreMilk()
        {
            var CharacterManager = Singleton<Manager.Character>.Instance;

            foreach (KeyValuePair<string, Material> entry in CharacterManager.dictSiruMaterial)
            {
                entry.Value.shader = milks[entry.Key].shader;
                entry.Value.CopyPropertiesFromMaterial(milks[entry.Key]);
            }
        }

        public static void ReplaceMilk()
        {
            var CharacterManager = Singleton<Manager.Character>.Instance;

            foreach (KeyValuePair<string, Material> entry in CharacterManager.dictSiruMaterial)
            {
                Material material = entry.Value;

                if (material)
                {
                    if (Properties.misc.wetOverlay && material.name == "cf_M_k_kaosiru01")
                    {
                        material.shader = AssetLoader.headwet.shader;
                        material.CopyPropertiesFromMaterial(AssetLoader.headwet);
                    }

                    else if (Properties.misc.wetOverlay && material.name == "cf_M_k_munesiru01")
                    {
                        material.shader = AssetLoader.bodywet.shader;
                        material.CopyPropertiesFromMaterial(AssetLoader.bodywet);
                    }

                    else
                    {
                        ShaderReplacer(AssetLoader.milk, material);
                        material.SetColor("_Color", new Color(0.8f, 0.8f, 0.8f, 0.2f));
                    }

                    if (!Properties.misc.fixOverlay)
                    {
                        material.renderQueue = 3000;
                    }

                }
            }
        }

        private static void ObjectReplacer(GameObject gameObject, Material replace, bool receiveShadow = true)
        {
            if (gameObject)
            {
                Renderer renderer = gameObject.GetComponent<Renderer>();

                if (renderer)
                {
                    Material material = renderer.sharedMaterial;

                    if (WillReplaceShader(material.shader))
                    {
                        ShaderReplacer(replace, material);
                        Console.WriteLine("#### HSSSS Replaced " + material.name);
                    }

                    renderer.receiveShadows = receiveShadow;
                }
            }
        }

        private static void ObjectParser(Material material, CharReference.TagObjKey key)
        {
            if (key == CharReference.TagObjKey.ObjNip)
            {
                ShaderReplacer(AssetLoader.overlay, material);
            }

            else if (key == CharReference.TagObjKey.ObjNail)
            {
                ShaderReplacer(AssetLoader.nail, material);
            }

            else if (key == CharReference.TagObjKey.ObjEyeW)
            {
                ShaderReplacer(AssetLoader.sclera, material);
            }

            else if (key == CharReference.TagObjKey.ObjEyeL || key == CharReference.TagObjKey.ObjEyeR)
            {
                if (Properties.misc.fixEyeball)
                {
                    ShaderReplacer(AssetLoader.cornea, material);
                    material.SetTexture("_SpecGlossMap", null);
                    material.SetTexture("_EmissionMap", material.GetTexture("_MainTex"));
                }

                else
                {
                    ShaderReplacer(AssetLoader.eyeoverlay, material);
                }
            }

            else if (Properties.misc.fixOverlay)
            {
                switch (key)
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

                    default:
                        break;
                }
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
