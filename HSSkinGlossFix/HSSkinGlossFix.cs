using UnityEngine;
using IllusionPlugin;
using Harmony;

namespace HSSkinGlossFix
{
    public class HSSkinGlossFix : IEnhancedPlugin
    {
        public string Name { get { return "HSSkinGlossFix"; } }
        public string Version { get { return "1.0.0"; } }
        public string[] Filter { get { return new[] { "StudioNEO_32", "StudioNEO_64" }; } }

        public void OnApplicationStart ()
        {
            HarmonyInstance harmony = HarmonyInstance.Create("com.kkul.hsskinglossfix");

            harmony.Patch(
                AccessTools.Method(typeof(Studio.OCIChar), nameof(Studio.OCIChar.SetCoordinateInfo)),
                null, new HarmonyMethod(typeof(HSSkinGlossFix), nameof(ChangeCostume))
                );

            harmony.Patch(
                AccessTools.Method(typeof(Studio.OCICharMale), nameof(Studio.OCICharMale.SetTuyaRate)),
                null, new HarmonyMethod(typeof(HSSkinGlossFix), nameof(MaleSkinGloss))
                );

            harmony.Patch(
                AccessTools.Method(typeof(Studio.MPCharCtrl.OtherInfo), nameof(Studio.MPCharCtrl.OtherInfo.UpdateInfo)),
                null, new HarmonyMethod(typeof(HSSkinGlossFix), nameof(MaleGlossSlider))
                );

            harmony.Patch(
                AccessTools.Method(typeof(Studio.AddObjectAssist), nameof(Studio.AddObjectAssist.UpdateState)),
                null, new HarmonyMethod(typeof(HSSkinGlossFix), nameof(SceneLoad))
                );
        }

        public void OnApplicationQuit ()
        {
        }

        public void OnLevelWasInitialized (int level)
        {
        }

        public void OnLevelWasLoaded (int level)
        {
        }

        public void OnUpdate ()
        {
        }

        public void OnLateUpdate ()
        {
        }

        public void OnFixedUpdate ()
        {
        }

        private static void ChangeCostume(Studio.OCIChar __instance)
        {
            float skinRate = __instance.oiCharInfo.skinRate;
            __instance.SetTuyaRate(skinRate);
        }

        private static void MaleSkinGloss(float _value, Studio.OCICharMale __instance)
        {
            __instance.oiCharInfo.skinRate = _value;
            SetSkinGloss(__instance.male, _value);
        }   

        private static void MaleGlossSlider(Studio.OCIChar _char, Studio.MPCharCtrl.OtherInfo __instance)
        {
            if (0 == _char.oiCharInfo.sex)
            {
                __instance.skin.active = true;
                __instance.skin.slider.value = _char.oiCharInfo.skinRate;
            }
        }

        private static void SceneLoad(Studio.OCIChar _ociChar)
        {
            if (0 == _ociChar.charInfo.Sex)
            {
                CharMale male = _ociChar.charInfo as CharMale;
                SetSkinGloss(male, _ociChar.oiCharInfo.skinRate);
            }
        }

        private static void SetSkinGloss(CharMale male, float skinRate)
        {
            if (null != male.maleCustomInfo)
            {
                float num = 1f;
                float specularSharpenss = male.maleCustomInfo.skinColor.specularSharpness;

                if (specularSharpenss < 1)
                {
                    HSColorSet colorSet = new HSColorSet();
                    colorSet.Copy(male.maleCustomInfo.skinColor);
                    float a = Mathf.Max(0f, specularSharpenss);
                    colorSet.specularSharpness = Mathf.Lerp(a, num, skinRate);

                    if (male.chaBody.customMatBody)
                    {
                        male.chaBody.customMatBody.SetFloat(Singleton<Manager.Character>.Instance._Smoothness, colorSet.specularSharpness);
                        male.maleCustom.SetBodyBaseMaterial();
                    }

                    if (male.chaBody.customMatFace)
                    {
                        male.chaBody.customMatFace.SetFloat(Singleton<Manager.Character>.Instance._Smoothness, colorSet.specularSharpness);
                        male.maleCustom.SetFaceBaseMaterial();
                    }
                }
            }
        }
    }
}
