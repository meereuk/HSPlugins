﻿using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Studio;

namespace HSSSS
{
    public class ConfigWindow : MonoBehaviour
    {
        #region Global Fields
        public static bool hsrCompatible;

        public static int uiScale;
        public static int presetNum;

        public static string presetName = "default";

        private static int singleSpace;
        private static int doubleSpace;
        private static int tetraSpace;
        private static int hexaSpace;
        private static int octaSpace;
        private static int decaSpace;

        private static Vector2 windowSize;
        private static Vector2 windowPosition;
        private static Vector2 scrollPosition;

        private Rect configWindow;

        private Properties.SkinProperties skin;
        private Properties.PCSSProperties pcss;
        private Properties.SSAOProperties ssao;
        private Properties.SSGIProperties ssgi;
        private Properties.SSCSProperties sscs;
        private Properties.TAAUProperties taau;
        private Properties.TESSProperties tess;
        private Properties.MiscProperties misc;
        private Properties.AgXProperties agx;

        private enum TabState
        {
            skinScattering,
            skinTransmission,
            lightShadow,
            ssao,
            ssgi,
            taau,
            miscellaneous,
            agx,
            replacer,
            preset
        };

        private TabState tabState;

        private static readonly string[] tabLabels = new string[] {
            "SKIN SCATTERING",
            "TRANSMISSION",
            "SOFT SHADOWS",
            "AMBIENT OCCLUSION",
            "GLOBAL ILLUMINATION",
            "FRAME ACCUMULATION",
            "MISCELLANEOUS",
            "TONE MAPPER",
            "SHADER REPLACER",
            "PRESET"
        };

        private static readonly string[] lutLabels = new string[] { "PENNER", "FACEWORKS A", "FACEWORKS B", "JIMENEZ" };
        private static readonly string[] pcfLabels = new string[] { "OFF", "LOW", "MEDIUM", "HIGH", "ULTRA" };
        private static readonly string[] hsmLabels = new string[] { "DEFAULT", "4096x4096", "8192x8192" };

        private static readonly string[] scalelabels = new string[] { "FULL", "HALF", "QUARTER" };
        private static readonly string[] qualitylabels = new string[] { "LOW", "MEDIUM", "HIGH", "ULTRA" };
        #endregion

        public void Awake()
        {
            windowSize = new Vector2(224.0f * uiScale, 192.0f * uiScale);

            singleSpace = uiScale;
            doubleSpace = uiScale * 2;
            tetraSpace = uiScale * 4;
            hexaSpace = uiScale * 6;
            octaSpace = uiScale * 8;
            decaSpace = uiScale * 10;

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
                this.SoftShadows();
            }

            else
            {
                GUILayout.BeginHorizontal();
                {
                    // left column
                    GUILayout.BeginVertical(GUILayout.Width(octaSpace * 6));
                    {
                        GUILayout.BeginHorizontal(GUILayout.Height(octaSpace * tabLabels.Length));
                        {
                            TabState tmpState = (TabState)GUILayout.SelectionGrid((int)this.tabState, tabLabels, 1);

                            if (this.tabState != tmpState)
                            {
                                this.tabState = tmpState;
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();

                    // space
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
                                this.SoftShadows();
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
                            
                            case TabState.agx:
                                this.AgXToneMapper();
                                break;
                            
                            case TabState.replacer:
                                this.ManualReplacer();
                                break;

                            case TabState.miscellaneous:
                                this.Miscellaneous();
                                break;

                            case TabState.preset:
                                this.Presets();
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

            // version
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("HSSSS VERSION " + HSSSS.pluginVersion);
            GUILayout.EndHorizontal();

            this.UpdateSettings();
            GUI.DragWindow();

            windowPosition = this.configWindow.position;
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
            // toggle
            GUI.skin.toggle.fontSize = tetraSpace;
            GUI.skin.toggle.fixedHeight = octaSpace;
            // label
            GUI.skin.label.fixedHeight = hexaSpace;
            GUI.skin.label.fontSize = tetraSpace;
            // text field
            GUI.skin.textField.margin.top = doubleSpace;
            GUI.skin.textField.margin.left = doubleSpace;
            GUI.skin.textField.margin.right = doubleSpace;
            GUI.skin.textField.margin.bottom = doubleSpace;
            GUI.skin.textField.fontSize = tetraSpace;
            GUI.skin.textField.fixedHeight = hexaSpace;
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
            GUI.skin.horizontalSlider.padding.top = 1;
            GUI.skin.horizontalSlider.padding.left = 1;
            GUI.skin.horizontalSlider.padding.right = 1;
            GUI.skin.horizontalSlider.padding.bottom = 1;
            GUI.skin.horizontalSlider.fontSize = tetraSpace;
            GUI.skin.horizontalSlider.fixedHeight = hexaSpace;
            // slider thumb
            GUI.skin.horizontalSliderThumb.fixedWidth = tetraSpace;
            // scroll
            GUI.skin.verticalScrollbar.margin.left = tetraSpace;
            GUI.skin.verticalScrollbar.padding.top = 1;
            GUI.skin.verticalScrollbar.padding.left = 1;
            GUI.skin.verticalScrollbar.padding.right = 1;
            GUI.skin.verticalScrollbar.padding.bottom = 1;
            GUI.skin.verticalScrollbar.fixedWidth = tetraSpace;
            GUI.skin.verticalScrollbarThumb.stretchWidth = true;
        }

        private void SkinScattering()
        {
            GUILayout.Label("<color=white><b>SKIN SCATTERING</b></color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)});

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

            Separator();
            GUILayout.EndScrollView();
        }

        private void Transmission()
        {
            GUILayout.Label("<color=white><b>TRANSMISSION</b></color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true) });

            OnOffToolbar("THICKNESS SAMPLING METHOD", new string[] { "ON-THE-FLY", "PRE-BAKED" }, ref this.skin.bakedThickness);

            Separator();

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

            Separator();
            GUILayout.EndScrollView();
        }

        private void SoftShadows()
        {
            GUILayout.Label("<color=white><b>SOFT SHADOWS</b></color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true) });

            #region PCSS
            // pcf iterations count
            EnumToolbar("PCF SHADOW QUALITY", ref this.pcss.pcfState);

            if (this.pcss.pcfState == Properties.PCFState.disable)
            {
                this.pcss.pcssEnabled = false;
                this.sscs.enabled = false;
            }

            else
            {
                // pcss soft shadow toggle
                OnOffToolbar("PERCENTAGE CLOSER SOFT SHADOW", ref this.pcss.pcssEnabled);

                Separator();

                if (this.pcss.pcssEnabled)
                {
                    SliderControls("DIRECTIONAL / SEARCH RADIUS (TANGENT / 100)", ref this.pcss.dirLightPenumbra.Value.x, 0.0f, 20.0f);
                    SliderControls("DIRECTIONAL / LIGHT RADIUS (TANGENT / 100)", ref this.pcss.dirLightPenumbra.Value.y, 0.0f, 20.0f);
                    SliderControls("DIRECTIONAL / MINIMUM PENUMBRA (MILIMETER)", ref this.pcss.dirLightPenumbra.Value.z, 0.0f, 20.0f);
                }

                else
                {
                    SliderControls("DIRECTIONAL / PENUMBRA SCALE (MILIMETER)", ref this.pcss.dirLightPenumbra.Value.z, 0.0f, 20.0f);
                }

                Separator();

                // spot lights
                if (this.pcss.pcssEnabled)
                {
                    SliderControls("SPOT / SEARCH RADIUS (CENTIMETER)", ref this.pcss.spotLightPenumbra.Value.x, 0.0f, 20.0f);
                    SliderControls("SPOT / LIGHT RADIUS (CENTIMETER)", ref this.pcss.spotLightPenumbra.Value.y, 0.0f, 20.0f);
                    SliderControls("SPOT / MINIMUM PENUMBRA (MILIMETER)", ref this.pcss.spotLightPenumbra.Value.z, 0.0f, 20.0f);
                }

                else
                {
                    SliderControls("SPOT / PENUMBRA SCALE (MILIMETER)", ref this.pcss.spotLightPenumbra.Value.z, 0.0f, 20.0f);
                }

                Separator();

                // point lights
                if (this.pcss.pcssEnabled)
                {
                    SliderControls("POINT / SEARCH RADIUS (CENTIMETER)", ref this.pcss.pointLightPenumbra.Value.x, 0.0f, 20.0f);
                    SliderControls("POINT / LIGHT RADIUS (CENTIMETER)", ref this.pcss.pointLightPenumbra.Value.y, 0.0f, 20.0f);
                    SliderControls("POINT / MINIMUM PENUMBRA (MILIMETER)", ref this.pcss.pointLightPenumbra.Value.z, 0.0f, 20.0f);
                }

                else
                {
                    SliderControls("POINT / PENUMBRA SCALE (MILIMETER)", ref this.pcss.pointLightPenumbra.Value.z, 0.0f, 20.0f);
                }
            }
            #endregion

            #region SSCS
            Separator();

            if (this.pcss.pcfState != Properties.PCFState.disable)
            {
                OnOffToolbar("<b>CONTACT SHADOWS</b>", ref this.sscs.enabled);

                if (this.sscs.enabled)
                {
                    EnumToolbar("SHADOW QUALITY", ref this.sscs.quality);

                    Separator();

                    SliderControls("RAYTRACE RADIUS (CENTIMETER)", ref this.sscs.rayRadius.Value, 0.02f, 10.0f);
                    SliderControls("RAYTRACE DEPTH BIAS (MILIMETER)", ref this.sscs.depthBias.Value, 0.0f, 1.0f);
                    SliderControls("MEAN THICKNESS (METER)", ref this.sscs.meanDepth.Value, 0.0f, 2.0f);
                }
            }
            #endregion
            
            Separator();
            GUILayout.EndScrollView();
        }

        private void AmbientOcclusion()
        {
            GUILayout.Label("<color=white>AMBIENT OCCLUSION</color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true) });

            OnOffToolbar(ref this.ssao.enabled);

            if (this.ssao.enabled)
            {
                Separator();

                EnumToolbar("SSAO QUALITY", ref this.ssao.quality);
                EnumToolbar("SSAO RENDERING SCALE", ref this.ssao.subsample.Value);
                OnOffToolbar("MULTIBOUNCE OCCLUSION", ref this.ssao.mbounce);

                Separator();

                SliderControls("OCCLUSION INTENSITY", ref this.ssao.intensity.Value, 0.1f, 10.0f);
                SliderControls("OCCLUSION BIAS", ref this.ssao.lightBias.Value, 0.0f, 1.0f);

                Separator();

                SliderControls("RAYTRACE RADIUS (METER)", ref this.ssao.rayRadius.Value, 0.0f, 1.0f);
                SliderControls("RAYTRACE STRIDE", ref this.ssao.rayStride.Value, 1, 4);

                Separator();

                SliderControls("MEAN THICKNESS (METER)", ref this.ssao.meanDepth.Value, 0.0f, 2.00f);
                SliderControls("FADE DEPTH (METER)", ref this.ssao.fadeDepth.Value, 1.0f, 1000.0f);

                Separator();
                
                GUILayout.Label("DEBUG VIEW");
                this.ssao.debugView = GUILayout.Toolbar(this.ssao.debugView, new string[]{"OFF", "OCCLUSION", "BENT NORMAL"});
                
                Separator();

                OnOffToolbar("DIRECTIONAL OCCLUSION", ref this.ssao.usessdo.Value);

                if (this.ssao.usessdo.Value)
                {
                    SliderControls("LIGHT APATURE", ref this.ssao.doApature.Value, 0.0f, 1.0f);
                }
            }
            
            Separator();
            GUILayout.EndScrollView();
        }

        private void GlobalIllumination()
        {
            GUILayout.Label("<color=white>GLOBAL ILLUMINATION</color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true) });

            OnOffToolbar(ref this.ssgi.enabled);

            if (this.ssgi.enabled)
            {
                Separator();

                EnumToolbar("GI QUALITY", ref this.ssgi.quality);

                Separator();

                SliderControls("1ST BOUNCE GAIN", ref this.ssgi.intensity.Value, 0.1f, 10.0f);
                SliderControls("2ND BOUNCE GAIN", ref this.ssgi.secondary.Value, 0.1f, 10.0f);
                
                Separator();
                
                SliderControls("AMBIENT OCCLUSION", ref this.ssgi.occlusion.Value, 0.1f, 10.0f);
                SliderControls("MINIMUM ROUGHNESS", ref this.ssgi.roughness.Value, 0.1f, 0.5f);

                Separator();

                SliderControls("RAYTRACE RADIUS (METER)", ref this.ssgi.rayRadius.Value, 0.0f, 50.0f);
                SliderControls("RAYTRACE STRIDE", ref this.ssgi.rayStride.Value, 1, 4);

                Separator();

                SliderControls("MEAN THICKNESS (METER)", ref this.ssgi.meanDepth.Value, 0.0f, 2.0f);
                SliderControls("FADE DEPTH (METER)", ref this.ssgi.fadeDepth.Value, 1.0f, 1000.0f);
                
                Separator();
                
                GUILayout.Label("DEBUG VIEW");
                this.ssgi.debugView.Value = GUILayout.Toolbar(this.ssgi.debugView.Value, new string[]{"OFF", "DIFFUSE", "SPECULAR", "COMBINED", "OCCLUSION"});

                Separator();

                OnOffToolbar("SPATIAL DENOISER", ref this.ssgi.denoise);
                SliderControls("TEMPORAL DENOISER", ref this.ssgi.mixWeight.Value, 0.0f, 1.0f);
            }
            
            Separator();
            GUILayout.EndScrollView();
        }

        private void TemporalAntiAliasing()
        {
            GUILayout.Label("<color=white>FRAME ACCUMULATION</color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true) });

            OnOffToolbar(ref this.taau.enabled);

            if (this.taau.enabled)
            {
                Separator();

                OnOffToolbar("TAA METHOD", new string[] { "JUST MIX", "UPSCALE" }, ref this.taau.upscale);

                SliderControls("MIX WEIGHT", ref this.taau.mixWeight.Value, 0.0f, 1.0f);
            }

            Separator();
            GUILayout.EndScrollView();
        }

        private void Miscellaneous()
        {
            GUILayout.Label("<color=white>MISCELLANEOUS</color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true) });

            // skin microdetails
            OnOffToolbar("MICRODETAILS", ref this.skin.microDetails);

            if (this.skin.microDetails)
            {
                SliderControls("DETAIL NORMAL #1", ref this.skin.microDetailWeight_1.Value, 0.0f, 1.0f);
                SliderControls("DETAIL NORMAL #2", ref this.skin.microDetailWeight_2.Value, 0.0f, 1.0f);
                SliderControls("DETAIL OCCLUSION", ref this.skin.microDetailOcclusion.Value, 0.0f, 1.0f);
                SliderControls("TEXTURE TILING", ref this.skin.microDetailTiling, 0.1f, 100.0f);
            }

            Separator();

            // tessellation
            OnOffToolbar("TESSELLATION", ref this.tess.enabled);

            if (this.tess.enabled)
            {
                SliderControls("SOFTENING", ref this.tess.phong.Value, 0.0f, 1.0f);
                SliderControls("SUBDIVISION", ref this.tess.edge.Value, 2.0f, 50.0f);
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
                SliderControls("EYEBROW WRAP OFFSET", ref this.misc.wrapOffset.Value, 0.0f, 0.5f);
            }

            Separator();
            GUILayout.EndScrollView();

            GUILayout.Space(doubleSpace);
            GUILayout.Label("<color=#ff4040>*Save & reload the current scene or waifus to apply the changes</color>", new GUIStyle { fontSize = tetraSpace });
        }

        private void AgXToneMapper()
        {
            GUILayout.Label("<color=white>AgX TONE MAPPER</color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);
            
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true) });
            
            OnOffToolbar(ref this.agx.enabled);

            if (this.agx.enabled)
            {
                Separator();
                SliderControls("GAMMA", ref this.agx.gamma.Value, 0.0f, 4.0f, 2.2f);
                SliderControls("SATURATION", ref this.agx.saturation.Value, 0.0f, 2.0f, 1.0f);
                
                Separator();
                
                SliderControls("POWER <color=#ff8040>RED</color>", ref this.agx.power.Value.x, 0.0f, 2.0f, 1.0f);
                SliderControls("POWER <color=#40fd80>GREEN</color>", ref this.agx.power.Value.y, 0.0f, 2.0f, 1.0f);
                SliderControls("POWER <color=#4080ff>BLUE</color>", ref this.agx.power.Value.z, 0.0f, 2.0f, 1.0f);
                
                Separator();
                
                SliderControls("OFFSET <color=#ff8040>RED</color>", ref this.agx.offset.Value.x, 0.0f, 1.0f, 0.0f);
                SliderControls("OFFSET <color=#40fd80>GREEN</color>", ref this.agx.offset.Value.y, 0.0f, 1.0f, 0.0f);
                SliderControls("OFFSET <color=#4080ff>BLUE</color>", ref this.agx.offset.Value.z, 0.0f, 1.0f, 0.0f);
                
                Separator();
                
                SliderControls("SLOPE <color=#ff8040>RED</color>", ref this.agx.slope.Value.x, 0.0f, 2.0f, 1.0f);
                SliderControls("SLOPE <color=#40fd80>GREEN</color>", ref this.agx.slope.Value.y, 0.0f, 2.0f, 1.0f);
                SliderControls("SLOPE <color=#4080ff>BLUE</color>", ref this.agx.slope.Value.z, 0.0f, 2.0f, 1.0f);
            }
            
            Separator();
            GUILayout.EndScrollView();
            
            GUILayout.Space(doubleSpace);
            GUILayout.Label("<color=#ff4040>Disable LRE Tone Mapping, Color Grading, and Color Correction Curve.</color>", new GUIStyle { fontSize = tetraSpace });
        }

        private void ManualReplacer()
        {
            GUILayout.Label("<color=white>MANUAL SHADER REPLACER</color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);
            
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true) });
            
            TreeNodeObject node = Studio.Studio.Instance.treeNodeCtrl.selectNode;
            
            if (node)
            {
                GUILayout.Label("CURRENT OBJECT: " + node.textName);
                
                Separator();

                if (Studio.Studio.Instance.dicInfo.TryGetValue(node, out ObjectCtrlInfo info))
                {
                    foreach (Renderer renderer in info.guideObject.transformTarget.GetComponentsInChildren<Renderer>())
                    {
                        foreach (Material material in renderer.sharedMaterials)
                        {
                            if (GUILayout.Button(material.name))
                            {
                                MaterialReplacer.ReplaceManual(material);
                            }
                        }
                    }
                }
            }

            else
            {
                GUILayout.Label("NO OBJECT SELECTED");
            }
            
            Separator();
            GUILayout.EndScrollView();
            GUILayout.Space(doubleSpace);
        }

        private void Presets()
        {
            GUILayout.Label("<color=white>PRESETS</color>", new GUIStyle { fontSize = octaSpace });
            GUILayout.Box("", GUILayout.Height(2));
            GUILayout.Space(doubleSpace);

            string[] files = Directory.GetFiles(HSSSS.configLocation, "*.xml", SearchOption.TopDirectoryOnly).Select(file => Path.GetFileNameWithoutExtension(file)).ToArray();

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, new GUILayoutOption[] { GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true) });
            presetNum = GUILayout.SelectionGrid(presetNum, files, 1);
            GUILayout.EndScrollView();

            string path = Path.Combine(HSSSS.configLocation, files[presetNum] + ".xml");

            Separator();

            #region 1
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("LOAD FROM <color=orange>" + files[presetNum] + "</color>"))
            {
                if (XmlParser.LoadExternalFile(path))
                {
                    this.ReadSettings();

                    Properties.UpdateAll();

                    Console.WriteLine("#### HSSSS: Loaded Configurations");
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Failed to Load Configuration");
                }
            }

            if (GUILayout.Button("SAVE TO <color=orange>" + files[presetNum] + "</color>"))
            {
                this.WriteSettings();

                if (XmlParser.SaveExternalFile(path))
                {
                    Console.WriteLine("#### HSSSS: Saved Configurations");
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Failed to Save Configurations");
                }
            }

            if (files.Length > 1 && GUILayout.Button("DELETE <color=orange>" + files[presetNum] + "</color>"))
            {
                if (presetNum == files.Length - 1)
                {
                    presetNum = 0;
                }

                if (File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                    }

                    catch
                    {
                        Console.WriteLine("#### HSSSS: Failed to Delete Preset File");
                    }
                }
            }
            
            GUILayout.EndHorizontal();
            #endregion

            GUILayout.Space(doubleSpace);

            #region 2
            presetName = GUILayout.TextField(presetName, GUILayout.ExpandWidth(true));

            if (GUILayout.Button("SAVE NEW"))
            {
                string newpath = Path.Combine(HSSSS.configLocation, presetName + ".xml");

                this.WriteSettings();

                if (XmlParser.SaveExternalFile(newpath))
                {
                    Console.WriteLine("#### HSSSS: Succesfully Saved New Preset");
                }

                else
                {
                    Console.WriteLine("#### HSSSS: Failed to Save New Preset");
                }

            }
            #endregion
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
                
                case TabState.agx:
                    Properties.UpdateAgX();
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
            this.agx = Properties.agx;
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
            Properties.agx = this.agx;
        }

        #region Controls
        private static void EnumToolbar(string label, ref Properties.PCFState value)
        {
            GUILayout.Label(label);
            value = (Properties.PCFState)GUILayout.Toolbar((int)value, pcfLabels);
        }

        private static void EnumToolbar(string label, ref Properties.LUTProfile value)
        {
            GUILayout.Label(label);
            value = (Properties.LUTProfile)GUILayout.Toolbar((int)value, lutLabels);
        }

        private static void EnumToolbar(string label, ref Properties.RenderScale value)
        {
            GUILayout.Label(label);
            value = (Properties.RenderScale)GUILayout.Toolbar((int)value, scalelabels);
        }

        private static void EnumToolbar(string label, ref Properties.QualityPreset value)
        {
            GUILayout.Label(label);
            value = (Properties.QualityPreset)GUILayout.Toolbar((int)value, qualitylabels);
        }

        private static void OnOffToolbar(ref bool value)
        {
            value = GUILayout.Toolbar(Convert.ToUInt16(value), new string[] { "DISABLE", "ENABLE" }) == 1;
        }

        private static void OnOffToolbar(string label, ref bool value)
        {
            GUILayout.Label(label);
            value = GUILayout.Toolbar(Convert.ToUInt16(value), new string[] { "DISABLE", "ENABLE" }) == 1;
        }

        private static void OnOffToolbar(string label, string[] text, ref bool value)
        {
            GUILayout.Label(label);

            bool temp = GUILayout.Toolbar(Convert.ToUInt16(value), text) == 1;

            if (value != temp)
            {
                value = temp;
            }
        }

        private static void ToggleControls(string label, ref bool value)
        {
            bool temp = GUILayout.Toggle(value, label);

            if (value != temp)
            {
                value = temp;
            }
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
        
        private static void SliderControls(string label, ref float value, float min, float max, float reset)
        {
            GUILayout.Label(label);

            GUILayout.BeginHorizontal();

            value = GUILayout.HorizontalSlider(value, min, max);

            if (float.TryParse(GUILayout.TextField(value.ToString("0.00"), GUILayout.Width(2 * octaSpace)), out float field))
            {
                value = field;
            }

            GUI.skin.button.fixedHeight = hexaSpace;
            
            if (GUILayout.Button("RESET", GUILayout.Width(2 * octaSpace)))
            {
                value = reset;
            }
            
            GUI.skin.button.fixedHeight = octaSpace;

            GUILayout.EndHorizontal();
        }

        private static void SliderControls(string label, ref int value, int min, int max)
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

        private static void RGBControls(string label, ref Vector3 rgb)
        {
            GUIStyle style = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = decaSpace,
                fontSize = tetraSpace,
                padding = new RectOffset { top = 0, left = 0, right = 0, bottom = 0},
                margin = new RectOffset { top = 0, left = 0, right = 0, bottom = 0 },
                border = new RectOffset { top = 0, left = 0, right = 0, bottom = 0 },
                overflow = new RectOffset { top = 0, left = 0, right = 0, bottom = 0 }
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

        private static void Separator(bool vertical = false, int width = 1)
        {
            GUILayout.Space(tetraSpace);
            GUILayout.Box("", vertical ? GUILayout.Width(width) : GUILayout.Height(width));
            GUILayout.Space(doubleSpace);
        }
        #endregion
    }
}
