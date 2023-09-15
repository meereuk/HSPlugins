using System;
using UnityEngine;

namespace HSSSS
{
    public class ConfigWindow : MonoBehaviour
    {
        #region Global Fields
        public static bool fixAlphaShadow;
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
        private Properties.TESSProperties tess;

        private enum TabState
        {
            skinScattering,
            skinTransmission,
            lightShadow,
            ssao,
            ssgi,
            miscellaneous
        };

        private TabState tabState;

        private readonly string[] tabLabels = new string[] { "Skin Scattering", "Transmission", "Soft Shadow", "Ambient Occlusion", "Global Illumination", "Miscellaneous" };
        private readonly string[] lutLabels = new string[] { "Penner", "FaceWorks #1", "FaceWorks #2", "Jimenez" };
        private readonly string[] pcfLabels = new string[] { "Off", "Low", "Medium", "High", "Ultra" };
        private readonly string[] thkLabels = new string[] { "Pre-Baked", "On-the-Fly" };
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

            this.skin = Properties.skin;
            this.ssao = Properties.ssao;
            this.ssgi = Properties.ssgi;
            this.sscs = Properties.sscs;
            this.pcss = Properties.pcss;
            this.tess = Properties.tess;
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

                            case TabState.miscellaneous:
                                this.Miscellaneous();
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
            GUILayout.Label("HSSSS version " + HSSSS.pluginVersion);
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
            GUI.skin.button.margin.top = singleSpace;
            GUI.skin.button.margin.left = singleSpace;
            GUI.skin.button.margin.right = singleSpace;
            GUI.skin.button.margin.bottom = singleSpace;
            GUI.skin.button.fontSize = tetraSpace;
            GUI.skin.button.fixedHeight = octaSpace;
            // label
            GUI.skin.label.fixedHeight = hexaSpace;
            GUI.skin.label.fontSize = tetraSpace;
            // text field
            GUI.skin.textField.margin.top = singleSpace;
            GUI.skin.textField.margin.left = singleSpace;
            GUI.skin.textField.margin.right = singleSpace;
            GUI.skin.textField.margin.bottom = singleSpace;
            GUI.skin.textField.fontSize = tetraSpace;
            GUI.skin.textField.fixedHeight = octaSpace;
            // window
            GUI.skin.window.padding.top = tetraSpace;
            GUI.skin.window.padding.left = tetraSpace;
            GUI.skin.window.padding.right = tetraSpace;
            GUI.skin.window.padding.bottom = tetraSpace;
            GUI.skin.window.fontSize = octaSpace;
            // slider
            GUI.skin.horizontalSlider.margin.top = singleSpace;
            GUI.skin.horizontalSlider.margin.left = singleSpace;
            GUI.skin.horizontalSlider.margin.right = singleSpace;
            GUI.skin.horizontalSlider.margin.bottom = singleSpace;
            GUI.skin.horizontalSlider.padding.top = singleSpace;
            GUI.skin.horizontalSlider.padding.left = singleSpace;
            GUI.skin.horizontalSlider.padding.right = singleSpace;
            GUI.skin.horizontalSlider.padding.bottom = singleSpace;
            GUI.skin.horizontalSlider.fontSize = tetraSpace;
            GUI.skin.horizontalSlider.fixedHeight = octaSpace;
            // slider thumb
            GUI.skin.horizontalSliderThumb.fixedWidth = tetraSpace;
        }

        private void SkinScattering()
        {
            GUILayout.Label("<b>Skin Scattering</b>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            // weight
            SliderControls("Scattering Weight", ref skin.sssWeight, 0.0f, 1.0f);
            // profiles
            GUILayout.Label("Scattering Profile");
            Properties.LUTProfile lutProfile = (Properties.LUTProfile)GUILayout.Toolbar((int)skin.lutProfile, lutLabels);

            if (skin.lutProfile != lutProfile)
            {
                skin.lutProfile = lutProfile;
                this.RefreshWindowSize();
            }

            Separator();

            if (skin.lutProfile != Properties.LUTProfile.jimenez)
            {
                // skin diffusion brdf
                SliderControls("Skin BRDF Lookup Scale", ref skin.skinLutScale, 0.0f, 1.0f);
                SliderControls("Skin BRDF Lookup Bias", ref skin.skinLutBias, 0.0f, 1.0f);

                // shadow penumbra brdf
                if (skin.lutProfile == Properties.LUTProfile.nvidia2)
                {
                    Separator();

                    SliderControls("Shadow BRDF Lookup Scale", ref skin.shadowLutScale, 0.0f, 1.0f);
                    SliderControls("Shadow BRDF Lookup Bias", ref skin.shadowLutBias, 0.0f, 1.0f);
                }

                Separator();

                SliderControls("Blur Weight", ref skin.normalBlurWeight, 0.0f, 1.0f);
            }

            SliderControls("Blur Radius", ref skin.normalBlurRadius, 0.0f, 4.0f);
            SliderControls("Blur Depth Correction", ref skin.normalBlurDepthRange, 0.0f, 20.0f);
            SliderControls("Blur Iterations Count", ref skin.normalBlurIter, 0, 10);

            Separator();

            // ambient occlusion
            RGBControls("AO Color Bleeding", ref this.skin.colorBleedWeights);
        }

        private void Transmission()
        {
            GUILayout.Label("<b>Transmission</b>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            GUILayout.Label("Thickness Sampling Method");

            bool bakedThickness = GUILayout.Toolbar(Convert.ToUInt16(!this.skin.bakedThickness), thkLabels) == 0;

            if (this.skin.bakedThickness != bakedThickness)
            {
                this.skin.bakedThickness = bakedThickness;
                this.RefreshWindowSize();
            }

            if (!this.skin.bakedThickness && !this.pcss.pcssEnabled)
            {
                GUILayout.Label("<color=red>TURN ON PCSS SOFT SHADOW TO USE THIS OPTION!</color>");
            }

            SliderControls("Transmission Weight", ref skin.transWeight, 0.0f, 1.0f);

            if (this.skin.bakedThickness)
            {
                SliderControls("Transmission Distortion", ref skin.transDistortion, 0.0f, 1.0f);
                SliderControls("Transmission Shadow Weight", ref skin.transShadowWeight, 0.0f, 1.0f);
            }

            else
            {
                SliderControls("Transmission Thickness Bias", ref skin.thicknessBias, 0.0f, 5.0f);
            }

            SliderControls("Transmission Falloff", ref skin.transFalloff, 1.0f, 20.0f);

            if (this.skin.bakedThickness)
            {
                RGBControls("Transmission Absorption", ref this.skin.transAbsorption);
            }
        }

        private void SoftShadow()
        {
            GUILayout.Label("<b>Soft Shadows</b>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            #region Soft Shadow
            // pcf iterations count
            GUILayout.Label("PCF Shadow Quality");
            Properties.PCFState pcfState = (Properties.PCFState)GUILayout.Toolbar((int)this.pcss.pcfState, pcfLabels);

            if (this.pcss.pcfState != pcfState)
            {
                this.pcss.pcfState = pcfState;
                this.RefreshWindowSize();
            }

            if (this.pcss.pcfState != Properties.PCFState.disable)
            {
                // pcss soft shadow toggle
                GUILayout.Label("Percentage Closer Soft Shadow");
                bool pcssEnabled = GUILayout.Toolbar(Convert.ToUInt16(this.pcss.pcssEnabled), new string[] { "Disable", "Enable" }) == 1;

                if (this.pcss.pcssEnabled != pcssEnabled)
                {
                    this.pcss.pcssEnabled = pcssEnabled;
                    this.RefreshWindowSize();
                }

                Separator();

                // directional lights
                if (this.pcss.pcssEnabled)
                {
                    SliderControls("Directional Light / Blocker Search Radius (cm)", ref this.pcss.dirLightPenumbra.x, 0.0f, 20.0f);
                    SliderControls("Directional Light / Light Radius (cm)", ref this.pcss.dirLightPenumbra.y, 0.0f, 20.0f);
                    SliderControls("Directional Light / Minimum Penumbra (cm)", ref this.pcss.dirLightPenumbra.z, 0.0f, 20.0f);
                }

                else
                {
                    SliderControls("Directional Light / Penumbra Scale (cm)", ref this.pcss.dirLightPenumbra.z, 0.0f, 20.0f);
                }

                Separator();

                // spot lights
                if (this.pcss.pcssEnabled)
                {
                    SliderControls("Spot Light / Blocker Search Radius (cm)", ref this.pcss.spotLightPenumbra.x, 0.0f, 20.0f);
                    SliderControls("Spot Light / Light Radius (cm)", ref this.pcss.spotLightPenumbra.y, 0.0f, 20.0f);
                    SliderControls("Spot Light / Minimum Penumbra (cm)", ref this.pcss.spotLightPenumbra.z, 0.0f, 20.0f);
                }

                else
                {
                    SliderControls("Spot Light / Penumbra Scale (cm)", ref this.pcss.spotLightPenumbra.z, 0.0f, 20.0f);
                }

                Separator();

                // point lights
                if (this.pcss.pcssEnabled)
                {
                    SliderControls("Point Light / Blocker Search Radius (cm)", ref this.pcss.pointLightPenumbra.x, 0.0f, 20.0f);
                    SliderControls("Point Light / Light Radius (cm)", ref this.pcss.pointLightPenumbra.y, 0.0f, 20.0f);
                    SliderControls("Point Light / Minimum Penumbra (cm)", ref this.pcss.pointLightPenumbra.z, 0.0f, 20.0f);
                }

                else
                {
                    SliderControls("Point Light / Penumbra Scale (cm)", ref this.pcss.pointLightPenumbra.z, 0.0f, 20.0f);
                }
            }

            else
            {
                this.pcss.pcssEnabled = false;
            }
            #endregion

            #region SSCS
            Separator();
            GUILayout.Label("<b>Contact Shadow</b>");
            bool sscsEnabled = GUILayout.Toolbar(Convert.ToUInt16(this.sscs.enabled), new string[] { "Disable", "Enable" }) == 1;

            if (this.sscs.enabled != sscsEnabled)
            {
                this.sscs.enabled = sscsEnabled;
                this.RefreshWindowSize();
            }

            if (this.sscs.enabled)
            {
                GUILayout.Label("Contact Shadow Quality");
                this.sscs.quality = (Properties.QualityPreset)GUILayout.Toolbar((int)this.sscs.quality, new string[] { "Low", "Medium", "High", "Ultra" });
                Separator();

                SliderControls("Raytrace Radius (cm)", ref this.sscs.rayRadius, 0.0f, 50.0f);
                SliderControls("Raytrace Depth Bias (cm)", ref this.sscs.depthBias, 0.0f, 1.0f);
                SliderControls("Mean Thickness (m)", ref this.sscs.meanDepth, 0.0f, 2.0f);
            }
            #endregion
        }

        private void AmbientOcclusion()
        {
            GUILayout.Label("<b>Ambient Occlusion</b>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            bool ssaoEnabled = GUILayout.Toolbar(Convert.ToUInt16(this.ssao.enabled), new string[] { "Disable", "Enable" }) == 1;

            if (this.ssao.enabled != ssaoEnabled)
            {
                this.ssao.enabled = ssaoEnabled;
                this.RefreshWindowSize();
            }

            if (this.ssao.enabled)
            {
                Separator();

                GUILayout.Label("Visibility Function");
                this.ssao.usegtao = GUILayout.Toolbar(Convert.ToUInt16(this.ssao.usegtao), new string[] { "HBAO", "GTAO" }) == 1;

                GUILayout.Label("AO Quality");
                this.ssao.quality = (Properties.QualityPreset)GUILayout.Toolbar((int)this.ssao.quality, new string[] { "Low", "Medium", "High", "Ultra" });

                GUILayout.Label("Deinterleaved Sampling");
                this.ssao.screenDiv = GUILayout.Toolbar(this.ssao.screenDiv, new string[] { "Off", "2x2", "4x4", "8x8" });

                Separator();

                SliderControls("Occlusion Intensity", ref this.ssao.intensity, 0.1f, 10.0f);
                SliderControls("Occlusion Bias", ref this.ssao.lightBias, 0.0f, 1.0f);

                Separator();

                SliderControls("Raytrace Radius (m)", ref this.ssao.rayRadius, 0.0f, 1.0f);
                SliderControls("Raytrace Stride", ref this.ssao.rayStride, 1, 4);

                Separator();

                SliderControls("Mean Thickness (m)", ref this.ssao.meanDepth, 0.0f, 2.00f);
                SliderControls("Fade Depth (m)", ref this.ssao.fadeDepth, 1.0f, 1000.0f);

                Separator();
                GUILayout.Label("SSDO");
                this.ssao.usessdo = GUILayout.Toolbar(Convert.ToUInt16(this.ssao.usessdo), new string[] { "Off", "On" }) == 1;
                SliderControls("SSDO Light Apature", ref this.ssao.doApature, 0.0f, 1.0f);

                Separator();

                GUILayout.Label("Spatial Denoiser");
                this.ssao.denoise = GUILayout.Toolbar(Convert.ToUInt16(this.ssao.denoise), new string[] { "Off", "On" }) == 1;
            }
        }

        private void GlobalIllumination()
        {
            GUILayout.Label("<b>Global Illumination</b>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            bool ssgiEnabled = GUILayout.Toolbar(Convert.ToUInt16(this.ssgi.enabled), new string[] { "Disable", "Enable" }) == 1;

            if (this.ssgi.enabled != ssgiEnabled)
            {
                this.ssgi.enabled = ssgiEnabled;
                this.RefreshWindowSize();
            }

            if (this.ssgi.enabled)
            {
                Separator();

                GUILayout.Label("GI Quality");
                this.ssgi.quality = (Properties.QualityPreset)GUILayout.Toolbar((int)this.ssgi.quality, new string[] { "Low", "Medium", "High", "Ultra" });

                GUILayout.Label("Sampling Resolution");
                this.ssgi.samplescale = (Properties.RenderScale)GUILayout.Toolbar((int)this.ssgi.samplescale, new string[] { "Quarter", "Half", "Full" });

                Separator();

                SliderControls("First Bounce Gain", ref this.ssgi.intensity, 0.1f, 100.0f);
                SliderControls("Second Bounce Gain", ref this.ssgi.secondary, 0.1f, 100.0f);
                SliderControls("Minimum Roughness", ref this.ssgi.roughness, 0.1f, 0.5f);

                Separator();

                SliderControls("Raytrace Radius (m)", ref this.ssgi.rayRadius, 0.0f, 4.0f);
                SliderControls("Raytrace Stride", ref this.ssgi.rayStride, 1, 4);

                Separator();

                SliderControls("Mean Thickness (m)", ref this.ssgi.meanDepth, 0.0f, 2.0f);
                SliderControls("Fade Depth (m)", ref this.ssgi.fadeDepth, 1.0f, 1000.0f);

                Separator();

                GUILayout.Label("Spatial Denoiser");
                this.ssgi.denoise = GUILayout.Toolbar(Convert.ToUInt16(this.ssgi.denoise), new string[] { "Off", "On" }) == 1;
                SliderControls("Temporal Denoiser", ref this.ssgi.mixWeight, 0.0f, 1.0f);
            }
        }

        private void Miscellaneous()
        {
            GUILayout.Label("<b>Miscellaneous</b>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            // skin microdetails
            SliderControls("MicroDetail #1 Strength", ref this.skin.microDetailWeight_1, 0.0f, 1.0f);
            SliderControls("MicroDetail #2 Strength", ref this.skin.microDetailWeight_2, 0.0f, 1.0f);
            SliderControls("MicroDetail Tiling", ref this.skin.microDetailTiling, 0.1f, 100.0f);

            // tessellation
            Separator();
            GUILayout.Label("Tessellation");
            bool tessEnabled = GUILayout.Toolbar(Convert.ToUInt16(this.tess.enabled), new string[] { "Disable", "Enable" }) == 1;

            if (this.tess.enabled != tessEnabled)
            {
                this.tess.enabled = tessEnabled;
                this.RefreshWindowSize();
            }

            if (this.tess.enabled)
            {

                SliderControls("Phong Strength", ref this.tess.phong, 0.0f, 1.0f);
                SliderControls("Edge Length", ref this.tess.edge, 2.0f, 50.0f);
            }

            if (fixAlphaShadow)
            {
                Separator();

                SliderControls("Eyebrow Wrap Offset", ref this.skin.eyebrowoffset, 0.0f, 0.5f);
            }
        }

        private void Presets()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Load Preset"))
            {
                if (XmlParser.LoadExternalFile())
                {
                    this.skin = Properties.skin;
                    this.ssao = Properties.ssao;
                    this.ssgi = Properties.ssgi;
                    this.sscs = Properties.sscs;
                    this.pcss = Properties.pcss;
                    this.tess = Properties.tess;

                    Properties.UpdateSkin();
                    Properties.UpdatePCSS();
                    Properties.UpdateSSAO();
                    Properties.UpdateSSGI();

                    Console.WriteLine("#### HSSSS: Loaded Configurations");
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Configuration");
                }

                this.RefreshWindowSize();
            }

            if (GUILayout.Button("Save Preset"))
            {
                Properties.skin = this.skin;
                Properties.ssao = this.ssao;
                Properties.ssgi = this.ssgi;
                Properties.sscs = this.sscs;
                Properties.pcss = this.pcss;
                Properties.tess = this.tess;

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

        private static void SliderControls(string label, ref float value, float min, float max)
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

        private static void SliderControls(string label, ref int value, int min, int max)
        {
            GUILayout.Label(label);

            GUILayout.BeginHorizontal();

            value = (int)GUILayout.HorizontalSlider(value, min, max);

            if (int.TryParse(GUILayout.TextField(value.ToString("0.00"), GUILayout.Width(2 * octaSpace)), out int field))
            {
                value = field;
            }

            GUILayout.EndHorizontal();
        }

        private static void RGBControls(string label, ref Vector3 rgb)
        {
            GUIStyle style = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = octaSpace,
                fontSize = tetraSpace
            };

            GUILayout.Label(label);

            GUILayout.BeginHorizontal();

            GUILayout.Label("<color=red>Red</color>", style);

            if (float.TryParse(GUILayout.TextField(rgb.x.ToString("0.00"), GUILayout.Width(3 * octaSpace)), out float r))
            {
                rgb.x = r;
            }

            GUILayout.Label("<color=green>Green</color>", style);

            if (float.TryParse(GUILayout.TextField(rgb.y.ToString("0.00"), GUILayout.Width(3 * octaSpace)), out float g))
            {
                rgb.y = g;
            }

            GUILayout.Label("<color=blue>Blue</color>", style);

            if (float.TryParse(GUILayout.TextField(rgb.z.ToString("0.00"), GUILayout.Width(3 * octaSpace)), out float b))
            {
                rgb.z = b;
            }

            GUILayout.EndHorizontal();
        }

        private static void Separator(bool vertical = false)
        {
            GUILayout.Space(doubleSpace);

            if (vertical)
            {
                GUILayout.Box("", GUILayout.Width(1));
            }

            else
            {
                GUILayout.Box("", GUILayout.Height(1));
            }

            GUILayout.Space(singleSpace);
        }

        private void UpdateSettings(bool force = false)
        {
            switch (this.tabState)
            {
                case TabState.skinScattering:
                    Properties.skin = this.skin;
                    Properties.UpdateSkin();
                    break;

                case TabState.skinTransmission:
                    Properties.skin = this.skin;
                    Properties.UpdateSkin();
                    break;

                case TabState.lightShadow:
                    Properties.pcss = this.pcss;
                    Properties.sscs = this.sscs;
                    Properties.UpdatePCSS();
                    break;

                case TabState.ssao:
                    Properties.ssao = this.ssao;
                    Properties.UpdateSSAO();
                    break;

                case TabState.ssgi:
                    Properties.ssgi = this.ssgi;
                    Properties.UpdateSSGI();
                    break;

                case TabState.miscellaneous:
                    Properties.skin = this.skin;
                    Properties.tess = this.tess;
                    Properties.UpdateSkin();
                    break;
            }
        }
    }
}
