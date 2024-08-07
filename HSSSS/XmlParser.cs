﻿using System;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace HSSSS
{
    public static class XmlParser
    {
        private static XDocument doc = null;
        private static string prefix = null;

        public static void SaveXml(XmlWriter writer)
        {
            writer.WriteAttributeString("version", HSSSS.pluginVersion);
            // skin scattering
            writer.WriteStartElement("SkinScattering");
            {
                // scattering profile
                writer.WriteElementString("BRDF", Convert.ToString(Properties.skin.lutProfile));

                // diffusion brdf control
                writer.WriteStartElement("Diffusion");
                {
                    writer.WriteElementString("Scale", XmlConvert.ToString(Properties.skin.skinLutScale));
                    writer.WriteElementString("Bias", XmlConvert.ToString(Properties.skin.skinLutBias));
                }
                writer.WriteEndElement();

                // shadow brdf control
                writer.WriteStartElement("Shadow");
                {
                    writer.WriteElementString("Scale", XmlConvert.ToString(Properties.skin.shadowLutScale));
                    writer.WriteElementString("Bias", XmlConvert.ToString(Properties.skin.shadowLutBias));
                }
                writer.WriteEndElement();

                // normal blur
                writer.WriteStartElement("NormalBlur");
                {
                    writer.WriteElementString("Weight", XmlConvert.ToString(Properties.skin.sssBlurWeight));
                    writer.WriteElementString("Radius", XmlConvert.ToString(Properties.skin.sssBlurRadius));
                    writer.WriteElementString("CorrectionDepth", XmlConvert.ToString(Properties.skin.sssBlurDepthRange));
                    writer.WriteElementString("Iterations", XmlConvert.ToString(Properties.skin.sssBlurIter));
                    writer.WriteElementString("BlurAlbedo", XmlConvert.ToString(Properties.skin.sssBlurAlbedo));
                }
                writer.WriteEndElement();

                // ao bleeding
                writer.WriteStartElement("AOBleeding");
                {
                    writer.WriteElementString("Red", XmlConvert.ToString(Properties.skin.colorBleedWeights.x));
                    writer.WriteElementString("Green", XmlConvert.ToString(Properties.skin.colorBleedWeights.y));
                    writer.WriteElementString("Blue", XmlConvert.ToString(Properties.skin.colorBleedWeights.z));
                }
                writer.WriteEndElement();

                // light absorption
                writer.WriteStartElement("Absorption");
                {
                    writer.WriteElementString("Red", XmlConvert.ToString(Properties.skin.transAbsorption.x));
                    writer.WriteElementString("Green", XmlConvert.ToString(Properties.skin.transAbsorption.y));
                    writer.WriteElementString("Blue", XmlConvert.ToString(Properties.skin.transAbsorption.z));
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            // transmission
            writer.WriteStartElement("Transmission");
            {
                // baked thickness
                writer.WriteElementString("BakedThickness", XmlConvert.ToString(Properties.skin.bakedThickness));
                // transmission weight
                writer.WriteElementString("Weight", XmlConvert.ToString(Properties.skin.transWeight));
                // normal distortion
                writer.WriteElementString("NormalDistortion", XmlConvert.ToString(Properties.skin.transDistortion));
                // shadow weight
                writer.WriteElementString("ShadowWeight", XmlConvert.ToString(Properties.skin.transShadowWeight));
                // falloff
                writer.WriteElementString("FallOff", XmlConvert.ToString(Properties.skin.transFalloff));
                // thickness bias
                writer.WriteElementString("ThicknessBias", XmlConvert.ToString(Properties.skin.thicknessBias));
            }
            writer.WriteEndElement();
            // shadow
            writer.WriteStartElement("SoftShadow");
            {
                // pcf state
                writer.WriteAttributeString("State", Convert.ToString(Properties.pcss.pcfState));
                // pcss soft shadow
                writer.WriteElementString("PCSS", XmlConvert.ToString(Properties.pcss.pcssEnabled));
                // directional light
                writer.WriteStartElement("Directional");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(Properties.pcss.dirLightPenumbra.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(Properties.pcss.dirLightPenumbra.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(Properties.pcss.dirLightPenumbra.z));
                }
                writer.WriteEndElement();
                // spot light
                writer.WriteStartElement("Spot");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(Properties.pcss.spotLightPenumbra.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(Properties.pcss.spotLightPenumbra.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(Properties.pcss.spotLightPenumbra.z));
                }
                writer.WriteEndElement();
                // spot light
                writer.WriteStartElement("Point");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(Properties.pcss.pointLightPenumbra.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(Properties.pcss.pointLightPenumbra.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(Properties.pcss.pointLightPenumbra.z));
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            // ambient occlusion
            writer.WriteStartElement("AmbientOcclusion");
            {
                writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.ssao.enabled));
                // direct occlusion
                writer.WriteElementString("UseSSDO", XmlConvert.ToString(Properties.ssao.usessdo));
                // quality
                writer.WriteElementString("Quality", Convert.ToString(Properties.ssao.quality));
                // intensity
                writer.WriteElementString("Intensity", XmlConvert.ToString(Properties.ssao.intensity));
                // light bias
                writer.WriteElementString("LightBias", XmlConvert.ToString(Properties.ssao.lightBias));
                // ray radius
                writer.WriteElementString("RayRadius", XmlConvert.ToString(Properties.ssao.rayRadius));
                // ray stride
                writer.WriteElementString("RayStride", XmlConvert.ToString(Properties.ssao.rayStride));
                // mean depth
                writer.WriteElementString("MeanDepth", XmlConvert.ToString(Properties.ssao.meanDepth));
                // fade depth
                writer.WriteElementString("FadeDepth", XmlConvert.ToString(Properties.ssao.fadeDepth));
                // ssdo apature
                writer.WriteElementString("DOApature", XmlConvert.ToString(Properties.ssao.doApature));
                // spatial denoiser
                writer.WriteElementString("Denoiser", XmlConvert.ToString(Properties.ssao.denoise));
            }
            writer.WriteEndElement();
            // global illumination
            writer.WriteStartElement("GlobalIllumination");
            {
                writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.ssgi.enabled));
                // quality
                writer.WriteElementString("Quality", Convert.ToString(Properties.ssgi.quality));
                // primary gain
                writer.WriteElementString("Intensity", XmlConvert.ToString(Properties.ssgi.intensity));
                // secondary gain
                writer.WriteElementString("Secondary", XmlConvert.ToString(Properties.ssgi.secondary));
                // ray radius
                writer.WriteElementString("RayRadius", XmlConvert.ToString(Properties.ssgi.rayRadius));
                // ray stride
                writer.WriteElementString("RayStride", XmlConvert.ToString(Properties.ssgi.rayStride));
                // mean depth
                writer.WriteElementString("MeanDepth", XmlConvert.ToString(Properties.ssgi.meanDepth));
                // fade depth
                writer.WriteElementString("FadeDepth", XmlConvert.ToString(Properties.ssgi.fadeDepth));
                // spatial denoiser
                writer.WriteElementString("Denoiser", XmlConvert.ToString(Properties.ssgi.denoise));
                // temporal denoiser
                writer.WriteElementString("MixWeight", XmlConvert.ToString(Properties.ssgi.mixWeight));
            }
            writer.WriteEndElement();
            // contact shadow
            writer.WriteStartElement("ContactShadow");
            {
                writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.sscs.enabled));
                // quality
                writer.WriteElementString("Quality", Convert.ToString(Properties.sscs.quality));
                // ray radius
                writer.WriteElementString("RayRadius", XmlConvert.ToString(Properties.sscs.rayRadius));
                // depth bias
                writer.WriteElementString("DepthBias", XmlConvert.ToString(Properties.sscs.depthBias));
                // mean depth
                writer.WriteElementString("MeanDepth", XmlConvert.ToString(Properties.sscs.meanDepth));
            }
            writer.WriteEndElement();
            // miscellaneous
            writer.WriteStartElement("Miscellaneous");
            {
                // skin microdetails
                writer.WriteStartElement("MicroDetails");
                {
                    writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.skin.microDetails));
                    writer.WriteElementString("Weight_1", XmlConvert.ToString(Properties.skin.microDetailWeight_1));
                    writer.WriteElementString("Weight_2", XmlConvert.ToString(Properties.skin.microDetailWeight_2));
                    writer.WriteElementString("Occlusion", XmlConvert.ToString(Properties.skin.microDetailOcclusion));
                    writer.WriteElementString("Tiling", XmlConvert.ToString(Properties.skin.microDetailTiling));
                }
                writer.WriteEndElement();
                // tessellation
                writer.WriteStartElement("Tessellation");
                {
                    writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.tess.enabled));
                    writer.WriteElementString("Phong", XmlConvert.ToString(Properties.tess.phong));
                    writer.WriteElementString("EdgeLength", XmlConvert.ToString(Properties.tess.edge));
                }
                writer.WriteEndElement();
                // dedicated eye shader
                writer.WriteStartElement("POMEyeShader");
                {
                    writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.misc.fixEyeball));
                }
                writer.WriteEndElement();
                // overlay shader
                writer.WriteStartElement("OverlayShader");
                {
                    writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.misc.fixOverlay));
                    writer.WriteElementString("WrapOffset", XmlConvert.ToString(Properties.misc.wrapOffset));
                }
                writer.WriteEndElement();
                // wet skin overlay
                writer.WriteStartElement("WetSkinOverlay");
                {
                    writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.misc.wetOverlay));
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }

        public static void LoadXml(XmlNode node)
        {
            if (node == null)
            {
                return;
            }

            doc = XDocument.Load(new XmlNodeReader(node));

            // version check
            prefix = null;
            string version = "UNKNOWN";
            XmlQuery<string>("HSSSS", "version", ref version);

            Console.WriteLine("#### HSSSS: Loaded configurations from version... " + version);

            // mismatch warning
            if (version != HSSSS.pluginVersion)
            {
                Console.WriteLine("#### HSSSS: Loaded configurations do not match the current version (this message may be harmless)");
            }

            ///////////////////////////
            // subsurface scattering //
            ///////////////////////////
            
            prefix = "HSSSS/SkinScattering";

            XmlQuery<Properties.LUTProfile>("/BRDF", ref Properties.skin.lutProfile);
            // pre-integrated skin lookup
            XmlQuery<float>("/Diffusion/Scale", ref Properties.skin.skinLutScale);
            XmlQuery<float>("/Diffusion/Bias", ref Properties.skin.skinLutBias);
            // pre-integrated shadow lookup
            XmlQuery<float>("/Shadow/Scale", ref Properties.skin.shadowLutScale);
            XmlQuery<float>("/Shadow/Bias", ref Properties.skin.shadowLutBias);
            // normal/diffuse blur
            XmlQuery<float>("/NormalBlur/Weight", ref Properties.skin.sssBlurWeight);
            XmlQuery<float>("/NormalBlur/Radius", ref Properties.skin.sssBlurRadius);
            XmlQuery<float>("/NormalBlur/CorrectionDepth", ref Properties.skin.sssBlurDepthRange);
            XmlQuery<int>("/NormalBlur/Iterations", ref Properties.skin.sssBlurIter);
            XmlQuery<bool>("/NormalBlur/BlurAlbedo", ref Properties.skin.sssBlurAlbedo);
            // color bleeding ao
            XmlQuery<float>("/AOBleeding/Red", ref Properties.skin.colorBleedWeights.x);
            XmlQuery<float>("/AOBleeding/Green", ref Properties.skin.colorBleedWeights.y);
            XmlQuery<float>("/AOBleeding/Blue", ref Properties.skin.colorBleedWeights.z);
            // absorption
            XmlQuery<float>("/Absorption/Red", ref Properties.skin.transAbsorption.x);
            XmlQuery<float>("/Absorption/Green", ref Properties.skin.transAbsorption.y);
            XmlQuery<float>("/Absorption/Blue", ref Properties.skin.transAbsorption.z);

            /////////////////////
            // deep scattering //
            /////////////////////
            
            prefix = "HSSSS/Transmission";

            XmlQuery<bool>("/BakedThickness", ref Properties.skin.bakedThickness);
            XmlQuery<float>("/Weight", ref Properties.skin.transWeight);
            XmlQuery<float>("/NormalDistortion", ref Properties.skin.transDistortion);
            XmlQuery<float>("/ShadowWeight", ref Properties.skin.transShadowWeight);
            XmlQuery<float>("/FallOff", ref Properties.skin.transFalloff);
            XmlQuery<float>("/ThicknessBias", ref Properties.skin.thicknessBias);

            /////////////////
            // soft shadow //
            /////////////////
            
            prefix = "HSSSS/SoftShadow";

            XmlQuery<Properties.PCFState>(null, "State", ref Properties.pcss.pcfState);
            XmlQuery<bool>("/PCSS", ref Properties.pcss.pcssEnabled);

            XmlQuery<float>("/Directional/SearchRadius", ref Properties.pcss.dirLightPenumbra.x);
            XmlQuery<float>("/Directional/LightRadius", ref Properties.pcss.dirLightPenumbra.y);
            XmlQuery<float>("/Directional/MinPenumbra", ref Properties.pcss.dirLightPenumbra.z);

            XmlQuery<float>("/Spot/SearchRadius", ref Properties.pcss.spotLightPenumbra.x);
            XmlQuery<float>("/Spot/LightRadius", ref Properties.pcss.spotLightPenumbra.y);
            XmlQuery<float>("/Spot/MinPenumbra", ref Properties.pcss.spotLightPenumbra.z);

            XmlQuery<float>("/Point/SearchRadius", ref Properties.pcss.pointLightPenumbra.x);
            XmlQuery<float>("/Point/LightRadius", ref Properties.pcss.pointLightPenumbra.y);
            XmlQuery<float>("/Point/MinPenumbra", ref Properties.pcss.pointLightPenumbra.z);

            ///////////////////////
            // ambient occlusion //
            ///////////////////////
            
            prefix = "HSSSS/AmbientOcclusion";

            XmlQuery<bool>("", "Enabled", ref Properties.ssao.enabled);

            XmlQuery<Properties.QualityPreset>("/Quality", ref Properties.ssao.quality);

            XmlQuery<float>("/Intensity", ref Properties.ssao.intensity);
            XmlQuery<float>("/LightBias", ref Properties.ssao.lightBias);

            XmlQuery<float>("/RayRadius", ref Properties.ssao.rayRadius);
            XmlQuery<int>("/RayStride", ref Properties.ssao.rayStride);

            XmlQuery<float>("/MeanDepth", ref Properties.ssao.meanDepth);
            XmlQuery<float>("/FadeDepth", ref Properties.ssao.fadeDepth);

            XmlQuery<bool>("/UseSSDO", ref Properties.ssao.usessdo);
            XmlQuery<float>("/DOApature", ref Properties.ssao.doApature);

            XmlQuery<bool>("/Denoiser", ref Properties.ssao.denoise);

            /////////////////////////
            // global illumination //
            /////////////////////////
            
            prefix = "HSSSS/GlobalIllumination";

            XmlQuery<bool>("", "Enabled", ref Properties.ssgi.enabled);
            XmlQuery<Properties.QualityPreset>("/Quality", ref Properties.ssgi.quality);

            XmlQuery<float>("/Intensity", ref Properties.ssgi.intensity);
            XmlQuery<float>("/Secondary", ref Properties.ssgi.secondary);

            XmlQuery<float>("/RayRadius", ref Properties.ssgi.rayRadius);
            XmlQuery<int>("/RayStride", ref Properties.ssgi.rayStride);

            XmlQuery<float>("/MeanDepth", ref Properties.ssgi.meanDepth);
            XmlQuery<float>("/FadeDepth", ref Properties.ssgi.fadeDepth);

            XmlQuery<bool>("/Denoiser", ref Properties.ssgi.denoise);
            XmlQuery<float>("/MixWeight", ref Properties.ssgi.mixWeight);

            ////////////////////
            // contact shadow //
            ////////////////////
            
            prefix = "HSSSS/ContactShadow";

            XmlQuery<bool>("", "Enabled", ref Properties.sscs.enabled);
            XmlQuery<Properties.QualityPreset>("/Quality", ref Properties.sscs.quality);
            XmlQuery<float>("/RayRadius", ref Properties.sscs.rayRadius);
            XmlQuery<float>("/DepthBias", ref Properties.sscs.depthBias);
            XmlQuery<float>("/MeanDepth", ref Properties.sscs.meanDepth);

            ///////////////////
            // miscellaneous //
            ///////////////////
            
            prefix = "HSSSS/Miscellaneous";

            // microdetails
            XmlQuery<bool>("/MicroDetails", "Enabled", ref Properties.skin.microDetails);
            XmlQuery<float>("/MicroDetails/Weight_1", ref Properties.skin.microDetailWeight_1);
            XmlQuery<float>("/MicroDetails/Weight_2", ref Properties.skin.microDetailWeight_2);
            XmlQuery<float>("/MicroDetails/Occlusion", ref Properties.skin.microDetailOcclusion);
            XmlQuery<float>("/MicroDetails/Tiling", ref Properties.skin.microDetailTiling);
            // tessellation
            XmlQuery<bool>("/Tessellation", "Enabled", ref Properties.tess.enabled);
            XmlQuery<float>("/Tessellation/Phong", ref Properties.tess.phong);
            XmlQuery<float>("/Tessellation/EdgeLength", ref Properties.tess.edge);
            // dedicated eye shader
            XmlQuery<bool>("/POMEyeShader", "Enabled", ref Properties.misc.fixEyeball);
            // fix overlay shadow
            XmlQuery<bool>("/OverlayShader", "Enabled", ref Properties.misc.fixOverlay);
            XmlQuery<float>("/OverlayShader/WrapOffset", ref Properties.misc.wrapOffset);
            // wet skin overlay
            XmlQuery<bool>("/WetSkinOverlay", "Enabled", ref Properties.misc.wetOverlay);

            prefix = null;

        }

        public static bool SaveExternalFile(string file)
        {
            try
            {
                XmlWriterSettings settings = new XmlWriterSettings() { Indent = true };
                XmlWriter writer = XmlWriter.Create(file, settings);
                writer.WriteStartElement("HSSSS");
                SaveXml(writer);
                writer.WriteEndElement();
                writer.Close();

                return true;
            }

            catch
            {
                return false;
            }
        }

        public static bool LoadExternalFile(string file)
        {
            try
            {
                XmlDocument config = new XmlDocument();
                config.Load(file);
                XmlParser.LoadXml(config.LastChild);
                return true;
            }

            catch
            {
                return false;
            }
        }

        private static void XmlQuery<T>(string path, ref T value)
        {
            XElement element = doc.XPathSelectElement(prefix + path);

            if (element == null)
            {
                Console.WriteLine("#### HSSSS: Could not find xpath " + prefix + path);
                return;
            }

            else
            {
                try
                {
                    if (typeof(T).IsEnum)
                    {
                        value = (T)Enum.Parse(typeof(T), element.Value);
                    }

                    else
                    {
                        value = (T)Convert.ChangeType(element.Value, typeof(T));
                    }
                }

                catch (InvalidCastException)
                {
                    Console.WriteLine("#### HSSSS: Could not cast from " + prefix + path);
                }
            }
        }

        private static void XmlQuery<T>(string path, string attr, ref T value)
        {
            XElement element = doc.XPathSelectElement(prefix + path);

            if (element == null)
            {
                Console.WriteLine("#### HSSSS: Could not find xpath " + prefix + path);
                return;
            }

            else
            {
                XAttribute attribute = element.Attribute(attr);

                if (attribute == null)
                {
                    Console.WriteLine("#### HSSSS: Could not find attribute " + attr + " at xpath " + prefix + path);
                    return;
                }

                else
                {
                    try
                    {
                        if (typeof(T).IsEnum)
                        {
                            value = (T)Enum.Parse(typeof(T), attribute.Value);
                        }

                        else
                        {
                            value = (T)Convert.ChangeType(attribute.Value, typeof(T));
                        }
                    }

                    catch (InvalidCastException)
                    {
                        Console.WriteLine("#### HSSSS: Coult not cast from " + attr + " at " + prefix + path);
                    }
                }
            }
        }
    }
}
