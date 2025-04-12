using System;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace HSSSS
{
    public static class XmlParser
    {
        private static XDocument doc;
        private static string prefix;

        public static void SaveXml(XmlWriter writer)
        {
            writer.WriteAttributeString("version", HSSSS.pluginVersion);
            
            // skin scattering
            #region scattering
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
            #endregion
            // transmission
            #region translucency
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
            #endregion
            // shadow
            #region shadow
            writer.WriteStartElement("SoftShadow");
            {
                // pcf state
                writer.WriteAttributeString("State", Convert.ToString(Properties.pcss.pcfState));
                // pcss soft shadow
                writer.WriteElementString("PCSS", XmlConvert.ToString(Properties.pcss.pcssEnabled));
                // directional light
                writer.WriteStartElement("Directional");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(Properties.pcss.dirLightPenumbra.Value.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(Properties.pcss.dirLightPenumbra.Value.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(Properties.pcss.dirLightPenumbra.Value.z));
                }
                writer.WriteEndElement();
                // spot light
                writer.WriteStartElement("Spot");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(Properties.pcss.spotLightPenumbra.Value.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(Properties.pcss.spotLightPenumbra.Value.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(Properties.pcss.spotLightPenumbra.Value.z));
                }
                writer.WriteEndElement();
                // spot light
                writer.WriteStartElement("Point");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(Properties.pcss.pointLightPenumbra.Value.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(Properties.pcss.pointLightPenumbra.Value.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(Properties.pcss.pointLightPenumbra.Value.z));
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            #endregion
            // ambient occlusion
            #region ssao
            writer.WriteStartElement("AmbientOcclusion");
            {
                writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.ssao.enabled));
                // direct occlusion
                writer.WriteElementString("UseSSDO", XmlConvert.ToString(Properties.ssao.usessdo.Value));
                // quality
                writer.WriteElementString("Quality", Convert.ToString(Properties.ssao.quality));
                // multibounce
                writer.WriteElementString("Multibounce", Convert.ToString(Properties.ssao.mbounce));
                // subsample
                writer.WriteElementString("Subsample", Convert.ToString(Properties.ssao.subsample.Value));
                // intensity
                writer.WriteElementString("Intensity", XmlConvert.ToString(Properties.ssao.intensity.Value));
                // light bias
                writer.WriteElementString("LightBias", XmlConvert.ToString(Properties.ssao.lightBias.Value));
                // ray radius
                writer.WriteElementString("RayRadius", XmlConvert.ToString(Properties.ssao.rayRadius.Value));
                // ray stride
                writer.WriteElementString("RayStride", XmlConvert.ToString(Properties.ssao.rayStride.Value));
                // mean depth
                writer.WriteElementString("MeanDepth", XmlConvert.ToString(Properties.ssao.meanDepth.Value));
                // fade depth
                writer.WriteElementString("FadeDepth", XmlConvert.ToString(Properties.ssao.fadeDepth.Value));
                // ssdo apature
                writer.WriteElementString("DOApature", XmlConvert.ToString(Properties.ssao.doApature.Value));
            }
            writer.WriteEndElement();
            #endregion
            // global illumination
            #region ssgi
            writer.WriteStartElement("GlobalIllumination");
            {
                writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.ssgi.enabled));
                // quality
                writer.WriteElementString("Quality", Convert.ToString(Properties.ssgi.quality));
                // primary gain
                writer.WriteElementString("Intensity", XmlConvert.ToString(Properties.ssgi.intensity.Value));
                // secondary gain
                writer.WriteElementString("Secondary", XmlConvert.ToString(Properties.ssgi.secondary.Value));
                // ambient occlusion
                writer.WriteElementString("Occlusion", XmlConvert.ToString(Properties.ssgi.occlusion.Value));
                // ray radius
                writer.WriteElementString("RayRadius", XmlConvert.ToString(Properties.ssgi.rayRadius.Value));
                // ray stride
                writer.WriteElementString("RayStride", XmlConvert.ToString(Properties.ssgi.rayStride.Value));
                // mean depth
                writer.WriteElementString("MeanDepth", XmlConvert.ToString(Properties.ssgi.meanDepth.Value));
                // fade depth
                writer.WriteElementString("FadeDepth", XmlConvert.ToString(Properties.ssgi.fadeDepth.Value));
                // spatial denoiser
                writer.WriteElementString("Denoiser", XmlConvert.ToString(Properties.ssgi.denoise));
                // temporal denoiser
                writer.WriteElementString("MixWeight", XmlConvert.ToString(Properties.ssgi.mixWeight.Value));
            }
            writer.WriteEndElement();
            #endregion
            // contact shadow
            #region contactshadow
            writer.WriteStartElement("ContactShadow");
            {
                writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.sscs.enabled));
                // quality
                writer.WriteElementString("Quality", Convert.ToString(Properties.sscs.quality));
                // ray radius
                writer.WriteElementString("RayRadius", XmlConvert.ToString(Properties.sscs.rayRadius.Value));
                // depth bias
                writer.WriteElementString("DepthBias", XmlConvert.ToString(Properties.sscs.depthBias.Value));
                // mean depth
                writer.WriteElementString("MeanDepth", XmlConvert.ToString(Properties.sscs.meanDepth.Value));
            }
            writer.WriteEndElement();
            #endregion
            // tone mapper
            #region tonemapper
            writer.WriteStartElement("ToneMapper");
            {
                writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.agx.enabled));
                // gamma
                writer.WriteElementString("Gamma", XmlConvert.ToString(Properties.agx.gamma.Value));
                // saturation
                writer.WriteElementString("Saturation", XmlConvert.ToString(Properties.agx.saturation.Value));
                // offset
                writer.WriteStartElement("Offset");
                writer.WriteElementString("Red", XmlConvert.ToString(Properties.agx.offset.Value.x));
                writer.WriteElementString("Green", XmlConvert.ToString(Properties.agx.offset.Value.y));
                writer.WriteElementString("Blue", XmlConvert.ToString(Properties.agx.offset.Value.z));
                writer.WriteEndElement();
                // slope
                writer.WriteStartElement("Slope");
                writer.WriteElementString("Red", XmlConvert.ToString(Properties.agx.slope.Value.x));
                writer.WriteElementString("Green", XmlConvert.ToString(Properties.agx.slope.Value.y));
                writer.WriteElementString("Blue", XmlConvert.ToString(Properties.agx.slope.Value.z));
                writer.WriteEndElement();
                // power
                writer.WriteStartElement("Power");
                writer.WriteElementString("Red", XmlConvert.ToString(Properties.agx.power.Value.x));
                writer.WriteElementString("Green", XmlConvert.ToString(Properties.agx.power.Value.y));
                writer.WriteElementString("Blue", XmlConvert.ToString(Properties.agx.power.Value.z));
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            #endregion
            // miscellaneous
            #region misc
            writer.WriteStartElement("Miscellaneous");
            {
                // skin microdetails
                writer.WriteStartElement("MicroDetails");
                {
                    writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.skin.microDetails));
                    writer.WriteElementString("Weight_1", XmlConvert.ToString(Properties.skin.microDetailWeight_1.Value));
                    writer.WriteElementString("Weight_2", XmlConvert.ToString(Properties.skin.microDetailWeight_2.Value));
                    writer.WriteElementString("Occlusion", XmlConvert.ToString(Properties.skin.microDetailOcclusion.Value));
                    writer.WriteElementString("Tiling", XmlConvert.ToString(Properties.skin.microDetailTiling));
                }
                writer.WriteEndElement();
                // tessellation
                writer.WriteStartElement("Tessellation");
                {
                    writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.tess.enabled));
                    writer.WriteElementString("Phong", XmlConvert.ToString(Properties.tess.phong.Value));
                    writer.WriteElementString("EdgeLength", XmlConvert.ToString(Properties.tess.edge.Value));
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
                    writer.WriteElementString("WrapOffset", XmlConvert.ToString(Properties.misc.wrapOffset.Value));
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
            #endregion
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
            #region scattering
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
            #endregion
            /////////////////////
            // deep scattering //
            /////////////////////
            #region translucency
            prefix = "HSSSS/Transmission";

            XmlQuery<bool>("/BakedThickness", ref Properties.skin.bakedThickness);
            XmlQuery<float>("/Weight", ref Properties.skin.transWeight);
            XmlQuery<float>("/NormalDistortion", ref Properties.skin.transDistortion);
            XmlQuery<float>("/ShadowWeight", ref Properties.skin.transShadowWeight);
            XmlQuery<float>("/FallOff", ref Properties.skin.transFalloff);
            XmlQuery<float>("/ThicknessBias", ref Properties.skin.thicknessBias);
            #endregion
            /////////////////
            // soft shadow //
            /////////////////
            #region softshadow
            prefix = "HSSSS/SoftShadow";

            XmlQuery<Properties.PCFState>(null, "State", ref Properties.pcss.pcfState);
            XmlQuery<bool>("/PCSS", ref Properties.pcss.pcssEnabled);

            XmlQuery<float>("/Directional/SearchRadius", ref Properties.pcss.dirLightPenumbra.Value.x);
            XmlQuery<float>("/Directional/LightRadius", ref Properties.pcss.dirLightPenumbra.Value.y);
            XmlQuery<float>("/Directional/MinPenumbra", ref Properties.pcss.dirLightPenumbra.Value.z);

            XmlQuery<float>("/Spot/SearchRadius", ref Properties.pcss.spotLightPenumbra.Value.x);
            XmlQuery<float>("/Spot/LightRadius", ref Properties.pcss.spotLightPenumbra.Value.y);
            XmlQuery<float>("/Spot/MinPenumbra", ref Properties.pcss.spotLightPenumbra.Value.z);

            XmlQuery<float>("/Point/SearchRadius", ref Properties.pcss.pointLightPenumbra.Value.x);
            XmlQuery<float>("/Point/LightRadius", ref Properties.pcss.pointLightPenumbra.Value.y);
            XmlQuery<float>("/Point/MinPenumbra", ref Properties.pcss.pointLightPenumbra.Value.z);
            #endregion
            ///////////////////////
            // ambient occlusion //
            ///////////////////////
            #region ssao
            prefix = "HSSSS/AmbientOcclusion";

            XmlQuery<bool>("", "Enabled", ref Properties.ssao.enabled);

            XmlQuery<Properties.QualityPreset>("/Quality", ref Properties.ssao.quality);
            XmlQuery<Properties.RenderScale>("/Subsample", ref Properties.ssao.subsample.Value);
            XmlQuery<bool>("/Multibounce", ref Properties.ssao.mbounce);

            XmlQuery<float>("/Intensity", ref Properties.ssao.intensity.Value);
            XmlQuery<float>("/LightBias", ref Properties.ssao.lightBias.Value);

            XmlQuery<float>("/RayRadius", ref Properties.ssao.rayRadius.Value);
            XmlQuery<int>("/RayStride", ref Properties.ssao.rayStride.Value);

            XmlQuery<float>("/MeanDepth", ref Properties.ssao.meanDepth.Value);
            XmlQuery<float>("/FadeDepth", ref Properties.ssao.fadeDepth.Value);

            XmlQuery<bool>("/UseSSDO", ref Properties.ssao.usessdo.Value);
            XmlQuery<float>("/DOApature", ref Properties.ssao.doApature.Value);
            #endregion
            /////////////////////////
            // global illumination //
            /////////////////////////
            #region ssgi
            prefix = "HSSSS/GlobalIllumination";

            XmlQuery<bool>("", "Enabled", ref Properties.ssgi.enabled);
            XmlQuery<Properties.QualityPreset>("/Quality", ref Properties.ssgi.quality);

            XmlQuery<float>("/Intensity", ref Properties.ssgi.intensity.Value);
            XmlQuery<float>("/Secondary", ref Properties.ssgi.secondary.Value);
            
            XmlQuery<float>("/Occlusion", ref Properties.ssgi.occlusion.Value);

            XmlQuery<float>("/RayRadius", ref Properties.ssgi.rayRadius.Value);
            XmlQuery<int>("/RayStride", ref Properties.ssgi.rayStride.Value);

            XmlQuery<float>("/MeanDepth", ref Properties.ssgi.meanDepth.Value);
            XmlQuery<float>("/FadeDepth", ref Properties.ssgi.fadeDepth.Value);

            XmlQuery<bool>("/Denoiser", ref Properties.ssgi.denoise);
            XmlQuery<float>("/MixWeight", ref Properties.ssgi.mixWeight.Value);
            #endregion
            ////////////////////
            // contact shadow //
            ////////////////////
            #region sscs
            prefix = "HSSSS/ContactShadow";

            XmlQuery<bool>("", "Enabled", ref Properties.sscs.enabled);
            XmlQuery<Properties.QualityPreset>("/Quality", ref Properties.sscs.quality);
            XmlQuery<float>("/RayRadius", ref Properties.sscs.rayRadius.Value);
            XmlQuery<float>("/DepthBias", ref Properties.sscs.depthBias.Value);
            XmlQuery<float>("/MeanDepth", ref Properties.sscs.meanDepth.Value);
            #endregion
            /////////////////
            // tone mapper //
            /////////////////
            #region tonemapper
            prefix = "HSSSS/ToneMapper";
            XmlQuery<bool>("", "Enabled", ref Properties.agx.enabled);
            XmlQuery<float>("/Gamma", ref Properties.agx.gamma.Value);
            XmlQuery<float>("/Saturation", ref Properties.agx.saturation.Value);
            
            XmlQuery<float>("/Offset/Red", ref Properties.agx.offset.Value.x);
            XmlQuery<float>("/Offset/Green", ref Properties.agx.offset.Value.y);
            XmlQuery<float>("/Offset/Blue", ref Properties.agx.offset.Value.z);
            
            XmlQuery<float>("/Slope/Red", ref Properties.agx.slope.Value.x);
            XmlQuery<float>("/Slope/Green", ref Properties.agx.slope.Value.y);
            XmlQuery<float>("/Slope/Blue", ref Properties.agx.slope.Value.z);
            
            XmlQuery<float>("/Power/Red", ref Properties.agx.power.Value.x);
            XmlQuery<float>("/Power/Green", ref Properties.agx.power.Value.y);
            XmlQuery<float>("/Power/Blue", ref Properties.agx.power.Value.z);
            #endregion
            ///////////////////
            // miscellaneous //
            ///////////////////
            #region misc
            prefix = "HSSSS/Miscellaneous";

            // microdetails
            XmlQuery<bool>("/MicroDetails", "Enabled", ref Properties.skin.microDetails);
            XmlQuery<float>("/MicroDetails/Weight_1", ref Properties.skin.microDetailWeight_1.Value);
            XmlQuery<float>("/MicroDetails/Weight_2", ref Properties.skin.microDetailWeight_2.Value);
            XmlQuery<float>("/MicroDetails/Occlusion", ref Properties.skin.microDetailOcclusion.Value);
            XmlQuery<float>("/MicroDetails/Tiling", ref Properties.skin.microDetailTiling);
            // tessellation
            XmlQuery<bool>("/Tessellation", "Enabled", ref Properties.tess.enabled);
            XmlQuery<float>("/Tessellation/Phong", ref Properties.tess.phong.Value);
            XmlQuery<float>("/Tessellation/EdgeLength", ref Properties.tess.edge.Value);
            // dedicated eye shader
            XmlQuery<bool>("/POMEyeShader", "Enabled", ref Properties.misc.fixEyeball);
            // fix overlay shadow
            XmlQuery<bool>("/OverlayShader", "Enabled", ref Properties.misc.fixOverlay);
            XmlQuery<float>("/OverlayShader/WrapOffset", ref Properties.misc.wrapOffset.Value);
            // wet skin overlay
            XmlQuery<bool>("/WetSkinOverlay", "Enabled", ref Properties.misc.wetOverlay);
            #endregion
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
