using System;
using UnityEngine;

namespace HSSSS
{
    public class ConfigWindow : MonoBehaviour
    {
        #region Global Fields
        public static bool hsrCompatible;

        public static int uiScale;

        private static int singleSpace;
        private static int doubleSpace;
        private static int tetraSpace;
        private static int hexaSpace;
        private static int octaSpace;

        private static Vector2 windowSize;
        private static Vector2 windowPosition;

        private Rect configWindow;

        private Properties.SkinProperties skin;
        private Properties.PCSSProperties pcss;
        private Properties.SSAOProperties ssao;
        private Properties.SSGIProperties ssgi;
        private Properties.SSCSProperties sscs;
        private Properties.TAAUProperties taau;
        private Properties.TESSProperties tess;
        private Properties.MiscProperties misc;

        private enum TabState
        {
            skinScattering,
            skinTransmission,
            lightShadow,
            ssao,
            ssgi,
            taau,
            miscellaneous
        };

        private TabState tabState;

        private readonly string[] tabLabels = new string[] {
            "SKIN SCATTERING",
            "TRANSMISSION",
            "SOFT SHADOWS",
            "AMBIENT OCCLUSION",
            "GLOBAL ILLUMINATION",
            "TEMPORAL UPSCALING",
            "MISCELLANEOUS"
        };

        private readonly string[] lutLabels = new string[] { "PENNER", "FACEWORKS A", "FACEWORKS B", "JIMENEZ" };
        private readonly string[] pcfLabels = new string[] { "OFF", "LOW", "MEDIUM", "HIGH", "ULTRA" };
        private readonly string[] hsmLabels = new string[] { "DEFAULT", "4096x4096", "8192x8192" };

        private readonly string[] scalelabels = new string[] { "QUARTER", "HALF", "FULL" };
        private readonly string[] qualitylabels = new string[] { "LOW", "MEDIUM", "HIGH", "ULTRA" };
        #endregion

        public void Awake()
        {
            windowSize = new Vector2(192.0f * uiScale, 192.0f);

            singleSpace = uiScale;
            doubleSpace = uiScale * 2;
            tetraSpace = uiScale * 4;
            hexaSpace = uiScale * 6;
            octaSpace = uiScale * 8;

            this.configWindow = new Rect(windowPosition, windowSize);
            this.tabState = TabState.skinScattering;
            this.ReadSettings();
        }

        public void OnGUI()
        {
            this.RefreshGUISkin();
            this.configWindow = GUILayout.Window(0, this.configWindow, this.WindowFunction, "");
            Studio.Studio.Instance.cameraCtrl.enabled = !this.configWindow.Contains(Event.current.mousePosition);
        }

        private void WindowFunction(int WindowID)
        {
            if (hsrCompatible)
            {
                this.tabState = TabState.lightShadow;
                this.SoftShadow();
            }

            else
            {
                GUILayout.BeginHorizontal();
                {
                    // left column
                    GUILayout.BeginVertical(GUILayout.Width(octaSpace * 5));
                    {
                        GUILayout.BeginHorizontal(GUILayout.Height(octaSpace * tabLabels.Length));
                        {
                            TabState tmpState = (TabState)GUILayout.SelectionGrid((int)this.tabState, tabLabels, 1);

                            if (this.tabState != tmpState)
                            {
                                this.tabState = tmpState;
                                this.RefreshWindowSize();
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();

                    //
                    GUILayout.Space(tetraSpace);

                    // right column
                    GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                    {
                        switch (this.tabState)
                        {
                            case TabState.skinScattering:
                                this.SkinScattering();
                                break;

                            case TabState.skinTransmission:
                                this.Transmission();
                                break;

                            case TabState.lightShadow:
                                this.SoftShadow();
                                break;

                            case TabState.ssao:
                                this.AmbientOcclusion();
                                break;

                            case TabState.ssgi:
                                this.GlobalIllumination();
                                break;

                            case TabState.taau:
                                this.TemporalAntiAliasing();
                                break;

                            case TabState.miscellaneous:
                                this.Miscellaneous();
                                break;

                            default:
                                break;
                        }
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(octaSpace);
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            // save and load
            this.Presets();

            // version
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("VERSION " + HSSSS.pluginVersion);
            GUILayout.EndHorizontal();

            this.UpdateSettings();
            GUI.DragWindow();

            windowPosition = this.configWindow.position;
        }

        private void RefreshWindowSize()
        {
            this.configWindow.size = windowSize;
        }

        private void RefreshGUISkin()
        {
            GUI.skin = AssetLoader.gui;
            // button
            GUI.skin.button.margin.top = doubleSpace;
            GUI.skin.button.margin.left = doubleSpace;
            GUI.skin.button.margin.right = doubleSpace;
            GUI.skin.button.margin.bottom = doubleSpace;
            GUI.skin.button.fontSize = tetraSpace;
            GUI.skin.button.fixedHeight = octaSpace;
            // label
            GUI.skin.label.fixedHeight = hexaSpace;
            GUI.skin.label.fontSize = tetraSpace;
            // text field
            GUI.skin.textField.margin.top = doubleSpace;
            GUI.skin.textField.margin.left = doubleSpace;
            GUI.skin.textField.margin.right = doubleSpace;
            GUI.skin.textField.margin.bottom = doubleSpace;
            GUI.skin.textField.fontSize = tetraSpace;
            GUI.skin.textField.fixedHeight = tetraSpace + doubleSpace;
            // window
            GUI.skin.window.padding.top = tetraSpace;
            GUI.skin.window.padding.left = tetraSpace;
            GUI.skin.window.padding.right = tetraSpace;
            GUI.skin.window.padding.bottom = tetraSpace;
            GUI.skin.window.fontSize = octaSpace;
            // slider
            GUI.skin.horizontalSlider.margin.top = doubleSpace;
            GUI.skin.horizontalSlider.margin.left = doubleSpace;
            GUI.skin.horizontalSlider.margin.right = doubleSpace;
            GUI.skin.horizontalSlider.margin.bottom = doubleSpace;
            GUI.skin.horizontalSlider.padding.top = singleSpace;
            GUI.skin.horizontalSlider.padding.left = singleSpace;
            GUI.skin.horizontalSlider.padding.right = singleSpace;
            GUI.skin.horizontalSlider.padding.bottom = singleSpace;
            GUI.skin.horizontalSlider.fontSize = tetraSpace;
            GUI.skin.horizontalSlider.fixedHeight = tetraSpace + doubleSpace;
            // slider thumb
            GUI.skin.horizontalSliderThumb.fixedWidth = tetraSpace;
        }

        private void SkinScattering()
        {
            GUILayout.Label("<color=white><b>SKIN SCATTERING</b></color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            // weight
            //SliderControls("Scattering Weight", ref skin.sssWeight, 0.0f, 1.0f);

            // profiles
            EnumToolbar("SCATTERING PROFILE", ref this.skin.lutProfile);

            Separator();

            if (this.skin.lutProfile == Properties.LUTProfile.jimenez)
            {
                OnOffToolbar("ALBEDO BLUR", ref this.skin.sssBlurAlbedo);
            }

            else
            {
                // skin diffusion brdf
                SliderControls("SKIN BRDF LOOKUP SCALE", ref this.skin.skinLutScale, 0.0f, 1.0f);
                SliderControls("SKIN BRDF LOOKUP BIAS", ref this.skin.skinLutBias, 0.0f, 1.0f);

                // shadow penumbra brdf
                if (this.skin.lutProfile == Properties.LUTProfile.nvidia2)
                {
                    Separator();

                    SliderControls("SHADOW BRDF LOOKUP SCALE", ref this.skin.shadowLutScale, 0.0f, 1.0f);
                    SliderControls("SHADOW BRDF LOOKUP BIAS", ref this.skin.shadowLutBias, 0.0f, 1.0f);
                }

                Separator();

                SliderControls("BLUR WEIGHT", ref this.skin.sssBlurWeight, 0.0f, 1.0f);
            }

            SliderControls("BLUR RADIUS", ref this.skin.sssBlurRadius, 0.0f, 4.0f);
            SliderControls("BLUR DEPTH CORRECTION", ref this.skin.sssBlurDepthRange, 0.0f, 20.0f);
            SliderControls("BLUR ITERATIONS COUNT", ref this.skin.sssBlurIter, 0, 10);

            Separator();

            // ambient occlusion
            RGBControls("AO COLOR BLEEDING", ref this.skin.colorBleedWeights);
        }

        private void Transmission()
        {
            GUILayout.Label("<color=white><b>TRANSMISSION</b></color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            OnOffToolbar("THICKNESS SAMPLING METHOD", new string[] { "ON-THE-FLY", "PRE-BAKED" }, ref this.skin.bakedThickness);

            if (!this.skin.bakedThickness && !this.pcss.pcssEnabled)
            {
                GUILayout.Label("<color=red>TURN ON PCSS SOFT SHADOW TO USE THIS OPTION!</color>");
            }

            SliderControls("TRANSMISSION WEIGHT", ref skin.transWeight, 0.0f, 1.0f);

            if (this.skin.bakedThickness)
            {
                SliderControls("TRANSMISSION DISTORTION", ref skin.transDistortion, 0.0f, 1.0f);
                SliderControls("TRANSMISSION SHADOW WEIGHT", ref skin.transShadowWeight, 0.0f, 1.0f);
            }

            else
            {
                SliderControls("TRANSMISSION THICKNESS BIAS", ref skin.thicknessBias, 0.0f, 5.0f);
            }

            SliderControls("TRANSMISSION FALLOFF", ref skin.transFalloff, 1.0f, 20.0f);

            if (this.skin.bakedThickness)
            {
                RGBControls("TRANSMISSION ABSORPTION", ref this.skin.transAbsorption);
            }
        }

        private void SoftShadow()
        {
            GUILayout.Label("<color=white><b>SOFT SHADOWS</b></color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            #region Soft Shadow
            // high-res shadow map
            EnumToolbar("HIGH-RES SHADOWMAP", ref this.pcss.highRes);

            // pcf iterations count
            EnumToolbar("PCF SHADOW QUALITY", ref this.pcss.pcfState);

            // sparse rendering
            OnOffToolbar("SPARSE RENDERING", ref this.pcss.checkerboard);

            if (this.pcss.pcfState != Properties.PCFState.disable)
            {
                // pcss soft shadow toggle
                OnOffToolbar("PERCENTAGE CLOSER SOFT SHADOW", ref this.pcss.pcssEnabled);

                Separator();

                // directional lights
                if (this.pcss.pcssEnabled)
                {
                    SliderControls("SEARCH RADIUS (tangent)", ref this.pcss.dirLightPenumbra.x, 0.0f, 20.0f);
                    SliderControls("LIGHT RADIUS (tangent)", ref this.pcss.dirLightPenumbra.y, 0.0f, 20.0f);
                    SliderControls("MINIMUM PENUMBRA (cm)", ref this.pcss.dirLightPenumbra.z, 0.0f, 20.0f);
                }

                else
                {
                    SliderControls("PENUMBRA SCALE (cm)", ref this.pcss.dirLightPenumbra.z, 0.0f, 20.0f);
                }

                Separator();

                // spot lights
                if (this.pcss.pcssEnabled)
                {
                    SliderControls("SEARCH RADIUS (cm)", ref this.pcss.spotLightPenumbra.x, 0.0f, 20.0f);
                    SliderControls("LIGHT RADIUS (cm)", ref this.pcss.spotLightPenumbra.y, 0.0f, 20.0f);
                    SliderControls("MINIMUM PENUMBRA (cm)", ref this.pcss.spotLightPenumbra.z, 0.0f, 20.0f);
                }

                else
                {
                    SliderControls("PENUMBRA SCALE (cm)", ref this.pcss.spotLightPenumbra.z, 0.0f, 20.0f);
                }

                Separator();

                // point lights
                if (this.pcss.pcssEnabled)
                {
                    SliderControls("SEARCH RADIUS (cm)", ref this.pcss.pointLightPenumbra.x, 0.0f, 20.0f);
                    SliderControls("LIGHT RADIUS (cm)", ref this.pcss.pointLightPenumbra.y, 0.0f, 20.0f);
                    SliderControls("MINIMUM PENUMBRA (cm)", ref this.pcss.pointLightPenumbra.z, 0.0f, 20.0f);
                }

                else
                {
                    SliderControls("PENUMBRA SCALE (cm)", ref this.pcss.pointLightPenumbra.z, 0.0f, 20.0f);
                }
            }

            else
            {
                this.pcss.pcssEnabled = false;
            }
            #endregion

            #region SSCS
            Separator();

            OnOffToolbar("<b>CONTACT SHADOW</b>", ref this.sscs.enabled);

            if (this.sscs.enabled)
            {
                EnumToolbar("CONTACT SHADOW QUALITY", ref this.sscs.quality);

                Separator();

                SliderControls("RAYTRACE RADIUS (cm)", ref this.sscs.rayRadius, 0.0f, 50.0f);
                SliderControls("RAYTRACE DEPTH BIAS (cm)", ref this.sscs.depthBias, 0.0f, 1.0f);
                SliderControls("MEAN THICKNESS (m)", ref this.sscs.meanDepth, 0.0f, 2.0f);
            }
            #endregion
        }

        private void AmbientOcclusion()
        {
            GUILayout.Label("<color=white>AMBIENT OCCLUSION</color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            OnOffToolbar(ref this.ssao.enabled);

            if (this.ssao.enabled)
            {
                Separator();

                OnOffToolbar("VISIBILITY FUNCTION", new string[] { "HBAO", "GTAO" }, ref this.ssao.usegtao);

                EnumToolbar("SSAO QUALITY", ref this.ssao.quality);

                OnOffToolbar("SPARSE RENDERING", ref this.ssao.sparse);

                Separator();

                SliderControls("OCCLUSION INTENSITY", ref this.ssao.intensity, 0.1f, 10.0f);
                SliderControls("OCCLUSION BIAS", ref this.ssao.lightBias, 0.0f, 1.0f);

                Separator();

                SliderControls("RAYTRACE RADIUS (METER)", ref this.ssao.rayRadius, 0.0f, 1.0f);
                SliderControls("RAYTRACE STRIDE", ref this.ssao.rayStride, 1, 4);

                Separator();

                SliderControls("MEAN THICKNESS (METER)", ref this.ssao.meanDepth, 0.0f, 2.00f);
                SliderControls("FADE DEPTH (METER)", ref this.ssao.fadeDepth, 1.0f, 1000.0f);

                Separator();

                OnOffToolbar("DIRECTIONAL OCCLUSION", ref this.ssao.usessdo);

                if (this.ssao.usessdo)
                {
                    SliderControls("LIGHT APATURE", ref this.ssao.doApature, 0.0f, 1.0f);
                }

                Separator();

                OnOffToolbar("SPATIAL DENOISER", ref this.ssao.denoise);
            }
        }

        private void GlobalIllumination()
        {
            GUILayout.Label("<color=white>GLOBAL ILLUMINATION</color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            OnOffToolbar(ref this.ssgi.enabled);

            if (this.ssgi.enabled)
            {
                Separator();

                EnumToolbar("GI QUALITY", ref this.ssgi.quality);

                Separator();

                SliderControls("1ST BOUNCE GAIN", ref this.ssgi.intensity, 0.1f, 100.0f);
                SliderControls("2ND BOUNCE GAIN", ref this.ssgi.secondary, 0.1f, 100.0f);
                SliderControls("MINIMUM ROUGHNESS", ref this.ssgi.roughness, 0.1f, 0.5f);

                Separator();

                SliderControls("RAYTRACE RADIUS (METER)", ref this.ssgi.rayRadius, 0.0f, 50.0f);
                SliderControls("RAYTRACE STRIDE", ref this.ssgi.rayStride, 1, 4);

                Separator();

                SliderControls("MEAN THICKNESS (METER)", ref this.ssgi.meanDepth, 0.0f, 2.0f);
                SliderControls("FADE DEPTH (METER)", ref this.ssgi.fadeDepth, 1.0f, 1000.0f);


                Separator();

                OnOffToolbar("SPATIAL DENOISER", ref this.ssgi.denoise);
                SliderControls("TEMPORAL DENOISER", ref this.ssgi.mixWeight, 0.0f, 1.0f);
            }
        }

        private void TemporalAntiAliasing()
        {
            GUILayout.Label("<color=white>TEMPORAL UPSCALING</color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            OnOffToolbar(ref this.taau.enabled);

            if (this.taau.enabled)
            {
                Separator();

                OnOffToolbar("TAA METHOD", new string[] { "JUST MIX", "UPSCALE" }, ref this.taau.upscale);

                SliderControls("MIX WEIGHT", ref this.taau.mixWeight, 0.0f, 1.0f);
            }
        }

        private void Miscellaneous()
        {
            GUILayout.Label("<color=white>MISCELLANEOUS</color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            // skin microdetails
            OnOffToolbar("MICRODETAILS", ref this.skin.microDetails);

            if (this.skin.microDetails)
            {
                SliderControls("DETAIL NORMAL #1", ref this.skin.microDetailWeight_1, 0.0f, 1.0f);
                SliderControls("DETAIL NORMAL #2", ref this.skin.microDetailWeight_2, 0.0f, 1.0f);
                SliderControls("DETAIL OCCLUSION", ref this.skin.microDetailOcclusion, 0.0f, 1.0f);
                SliderControls("TEXTURE TILING", ref this.skin.microDetailTiling, 0.1f, 100.0f);
            }

            Separator();

            // tessellation
            OnOffToolbar("TESSELLATION", ref this.tess.enabled);

            if (this.tess.enabled)
            {
                SliderControls("SOFTENING", ref this.tess.phong, 0.0f, 1.0f);
                SliderControls("SUBDIVISION", ref this.tess.edge, 2.0f, 50.0f);
            }

            Separator();
            
            // wet skin replacer for some milks
            OnOffToolbar("CONDENSATION OVERLAY", ref this.misc.wetOverlay);

            if (this.misc.wetOverlay != Properties.misc.wetOverlay)
            {
                Properties.misc.wetOverlay = this.misc.wetOverlay;

                MaterialReplacer.RestoreMilk();
                MaterialReplacer.ReplaceMilk();
            }


            // dedicated pom eye shader
            OnOffToolbar("DEDICATED POM EYE SHADER<color=red>*</color>", ref this.misc.fixEyeball);
            // dedicated overlay shader
            OnOffToolbar("DEDICATED OVERLAY SHADER<color=red>*</color>", ref this.misc.fixOverlay);

            if (this.misc.fixOverlay)
            {
                SliderControls("EYEBROW WRAP OFFSET", ref this.misc.wrapOffset, 0.0f, 0.5f);
            }

            GUILayout.Label("<color=red>*Reload the scene or character to apply the changes</color>", new GUIStyle { fontSize = tetraSpace });
        }

        private void Presets()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("LOAD PRESET"))
            {
                if (XmlParser.LoadExternalFile())
                {
                    this.ReadSettings();

                    Properties.UpdateSkin();
                    Properties.UpdatePCSS();
                    Properties.UpdateSSAO();
                    Properties.UpdateSSGI();
                    Properties.UpdateTAAU();

                    Console.WriteLine("#### HSSSS: Loaded Configurations");
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Configuration");
                }

                this.RefreshWindowSize();
            }

            if (GUILayout.Button("SAVE PRESET"))
            {
                this.WriteSettings();

                if (XmlParser.SaveExternalFile())
                {
                    Console.WriteLine("#### HSSSS: Saved Configurations");
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Failed to Save Configurations");
                }

                this.RefreshWindowSize();
            }

            GUILayout.EndHorizontal();
        }

        private void UpdateSettings()
        {
            this.WriteSettings();

            switch (this.tabState)
            {
                case TabState.skinScattering:
                    Properties.UpdateSkin();
                    break;

                case TabState.skinTransmission:
                    Properties.UpdateSkin();
                    break;

                case TabState.lightShadow:
                    Properties.UpdatePCSS();
                    break;

                case TabState.ssao:
                    Properties.UpdateSSAO();
                    break;

                case TabState.ssgi:
                    Properties.UpdateSSGI();
                    Properties.UpdateSkin();
                    break;

                case TabState.taau:
                    Properties.UpdateTAAU();
                    break;

                case TabState.miscellaneous:
                    Properties.UpdateSkin();
                    break;

                default:
                    break;
            }
        }

        private void ReadSettings()
        {
            this.skin = Properties.skin;
            this.ssao = Properties.ssao;
            this.ssgi = Properties.ssgi;
            this.taau = Properties.taau;
            this.sscs = Properties.sscs;
            this.pcss = Properties.pcss;
            this.tess = Properties.tess;
            this.misc = Properties.misc;
        }

        private void WriteSettings()
        {
            Properties.skin = this.skin;
            Properties.ssao = this.ssao;
            Properties.ssgi = this.ssgi;
            Properties.taau = this.taau;
            Properties.sscs = this.sscs;
            Properties.pcss = this.pcss;
            Properties.tess = this.tess;
            Properties.misc = this.misc;
        }

        private void EnumToolbar(string label, ref Properties.HighResShadow value)
        {
            GUILayout.Label(label);

            Properties.HighResShadow temp = (Properties.HighResShadow)GUILayout.Toolbar((int)value, hsmLabels);

            if (value != temp)
            {
                value = temp;
                this.RefreshWindowSize();
            }
        }

        private void EnumToolbar(string label, ref Properties.PCFState value)
        {
            GUILayout.Label(label);

            Properties.PCFState temp = (Properties.PCFState)GUILayout.Toolbar((int)value, pcfLabels);

            if (value != temp)
            {
                value = temp;
                this.RefreshWindowSize();
            }
        }

        private void EnumToolbar(string label, ref Properties.LUTProfile value)
        {
            GUILayout.Label(label);

            Properties.LUTProfile temp = (Properties.LUTProfile)GUILayout.Toolbar((int)value, lutLabels);

            if (value != temp)
            {
                value = temp;
                this.RefreshWindowSize();
            }
        }

        private void EnumToolbar(string label, ref Properties.RenderScale value)
        {
            GUILayout.Label(label);

            Properties.RenderScale temp = (Properties.RenderScale)GUILayout.Toolbar((int)value, scalelabels);

            if (value != temp)
            {
                value = temp;
                this.RefreshWindowSize();
            }
        }

        private void EnumToolbar(string label, ref Properties.QualityPreset value)
        {
            GUILayout.Label(label);

            Properties.QualityPreset temp = (Properties.QualityPreset)GUILayout.Toolbar((int)value, qualitylabels);

            if (value != temp)
            {
                value = temp;
                this.RefreshWindowSize();
            }
        }

        private void OnOffToolbar(ref bool value)
        {
            bool temp = GUILayout.Toolbar(Convert.ToUInt16(value), new string[] { "DISABLE", "ENABLE" }) == 1;

            if (value != temp)
            {
                value = temp;
                this.RefreshWindowSize();
            }
        }

        private void OnOffToolbar(string label, ref bool value)
        {
            GUILayout.Label(label);

            bool temp = GUILayout.Toolbar(Convert.ToUInt16(value), new string[] { "DISABLE", "ENABLE" }) == 1;

            if (value != temp)
            {
                value = temp;
                this.RefreshWindowSize();
            }
        }

        private void OnOffToolbar(string label, string[] text, ref bool value)
        {
            GUILayout.Label(label);

            bool temp = GUILayout.Toolbar(Convert.ToUInt16(value), text) == 1;

            if (value != temp)
            {
                value = temp;
                this.RefreshWindowSize();
            }
        }

        private void SliderControls(string label, ref float value, float min, float max)
        {
            GUILayout.Label(label);

            GUILayout.BeginHorizontal();

            value = GUILayout.HorizontalSlider(value, min, max);

            if (float.TryParse(GUILayout.TextField(value.ToString("0.00"), GUILayout.Width(2 * octaSpace)), out float field))
            {
                value = field;
            }

            GUILayout.EndHorizontal();
        }

        private void SliderControls(string label, ref int value, int min, int max)
        {
            GUILayout.Label(label);

            GUILayout.BeginHorizontal();

            value = (int)GUILayout.HorizontalSlider(value, min, max);

            if (int.TryParse(GUILayout.TextField(value.ToString(), GUILayout.Width(2 * octaSpace)), out int field))
            {
                value = field;
            }

            GUILayout.EndHorizontal();
        }

        private void RGBControls(string label, ref Vector3 rgb)
        {
            GUIStyle style = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = octaSpace,
                fontSize = tetraSpace
            };

            GUILayout.Label(label);

            GUILayout.BeginHorizontal();

            GUILayout.Label("<color=red>RED</color>", style);

            if (float.TryParse(GUILayout.TextField(rgb.x.ToString("0.00"), GUILayout.Width(3 * octaSpace)), out float r))
            {
                rgb.x = r;
            }

            GUILayout.Label("<color=green>GREEN</color>", style);

            if (float.TryParse(GUILayout.TextField(rgb.y.ToString("0.00"), GUILayout.Width(3 * octaSpace)), out float g))
            {
                rgb.y = g;
            }

            GUILayout.Label("<color=blue>BLUE</color>", style);

            if (float.TryParse(GUILayout.TextField(rgb.z.ToString("0.00"), GUILayout.Width(3 * octaSpace)), out float b))
            {
                rgb.z = b;
            }

            GUILayout.EndHorizontal();
        }

        private void Separator(bool vertical = false)
        {
            GUILayout.Space(tetraSpace);

            if (vertical)
            {
                GUILayout.Box("", GUILayout.Width(2));
            }

            else
            {
                GUILayout.Box("", GUILayout.Height(2));
            }

            GUILayout.Space(doubleSpace);
        }
    }
}
