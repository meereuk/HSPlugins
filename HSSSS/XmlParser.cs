using System;
using System.Xml;

namespace HSSSS
{
    public static class XmlParser
    {
        public static void SaveXml(XmlWriter writer)
        {
            writer.WriteAttributeString("version", HSSSS.pluginVersion);
            // skin scattering
            writer.WriteStartElement("SkinScattering");
            {
                writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.skin.sssEnabled));
                // scattering weight
                writer.WriteElementString("Weight", XmlConvert.ToString(Properties.skin.sssWeight));
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
                    writer.WriteElementString("Weight", XmlConvert.ToString(Properties.skin.normalBlurWeight));
                    writer.WriteElementString("Radius", XmlConvert.ToString(Properties.skin.normalBlurRadius));
                    writer.WriteElementString("CorrectionDepth", XmlConvert.ToString(Properties.skin.normalBlurDepthRange));
                    writer.WriteElementString("Iterations", XmlConvert.ToString(Properties.skin.normalBlurIter));
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
                writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.skin.transEnabled));
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
                writer.WriteAttributeString("State", Convert.ToString(Properties.shadow.pcfState));
                // pcss soft shadow
                writer.WriteElementString("PCSS", XmlConvert.ToString(Properties.shadow.pcssEnabled));
                // directional light
                writer.WriteStartElement("Directional");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(Properties.shadow.dirLightPenumbra.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(Properties.shadow.dirLightPenumbra.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(Properties.shadow.dirLightPenumbra.z));
                }
                writer.WriteEndElement();
                // spot light
                writer.WriteStartElement("Spot");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(Properties.shadow.spotLightPenumbra.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(Properties.shadow.spotLightPenumbra.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(Properties.shadow.spotLightPenumbra.z));
                }
                writer.WriteEndElement();
                // spot light
                writer.WriteStartElement("Point");
                {
                    writer.WriteElementString("SearchRadius", XmlConvert.ToString(Properties.shadow.pointLightPenumbra.x));
                    writer.WriteElementString("LightRadius", XmlConvert.ToString(Properties.shadow.pointLightPenumbra.y));
                    writer.WriteElementString("MinPenumbra", XmlConvert.ToString(Properties.shadow.pointLightPenumbra.z));
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            // ambient occlusion
            writer.WriteStartElement("AmbientOcclusion");
            {
                writer.WriteAttributeString("Enabled", XmlConvert.ToString(Properties.ssao.enabled));
                // visibility function
                writer.WriteElementString("UseGTAO", XmlConvert.ToString(Properties.ssao.usegtao));
                // direct occlusion
                writer.WriteElementString("UseSSDO", XmlConvert.ToString(Properties.ssao.usessdo));
                // quality
                writer.WriteElementString("Quality", Convert.ToString(Properties.ssao.quality));
                // deinterleaving
                writer.WriteElementString("ScreenDiv", XmlConvert.ToString(Properties.ssao.screenDiv));
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
                // sampling resolution
                writer.WriteElementString("SampleScale", Convert.ToString(Properties.ssgi.samplescale));
                // rendering resolution
                writer.WriteElementString("RenderScale", Convert.ToString(Properties.ssgi.renderscale));
                // primary gain
                writer.WriteElementString("Intensity", XmlConvert.ToString(Properties.ssgi.intensity));
                // secondary gain
                writer.WriteElementString("Secondary", XmlConvert.ToString(Properties.ssgi.secondary));
                // ray radius
                writer.WriteElementString("RayRadius", XmlConvert.ToString(Properties.ssgi.rayRadius));
                // ray stride
                writer.WriteElementString("RayStride", XmlConvert.ToString(Properties.ssgi.rayStride));
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
                    writer.WriteElementString("Weight_1", XmlConvert.ToString(Properties.skin.microDetailWeight_1));
                    writer.WriteElementString("Weight_2", XmlConvert.ToString(Properties.skin.microDetailWeight_2));
                    writer.WriteElementString("Tiling", XmlConvert.ToString(Properties.skin.microDetailTiling));
                }
                writer.WriteEndElement();
                // tessellation
                writer.WriteStartElement("Tessellation");
                {
                    writer.WriteElementString("Phong", XmlConvert.ToString(Properties.skin.phongStrength));
                    writer.WriteElementString("EdgeLength", XmlConvert.ToString(Properties.skin.edgeLength));
                }
                writer.WriteEndElement();
                // eyebrow wrap
                writer.WriteElementString("EyebrowOffset", XmlConvert.ToString(Properties.skin.eyebrowoffset));
            }
            writer.WriteEndElement();
        }

        public static void LoadXml(XmlNode node)
        {
            string version = node.Attributes["version"].Value;

            Console.WriteLine("#### HSSSS: Saved configurations from version... " + version);

            if (version != HSSSS.pluginVersion)
            {
                Console.WriteLine("#### HSSSS: Saved configurations do not match the version (this message may be harmless)");
            }

            foreach (XmlNode child0 in node.ChildNodes)
            {
                switch (child0.Name)
                {
                    // skin scattering
                    case "SkinScattering":
                        // enabled?
                        Properties.skinUpdate.sssEnabled = XmlConvert.ToBoolean(child0.Attributes["Enabled"].Value);

                        foreach (XmlNode child1 in child0.ChildNodes)
                        {
                            switch (child1.Name)
                            {
                                // scattering weight
                                case "Weight": Properties.skinUpdate.sssWeight = XmlConvert.ToSingle(child1.InnerText); break;
                                // pre-integrated brdf
                                case "BRDF": Properties.skinUpdate.lutProfile = (Properties.LUTProfile)Enum.Parse(typeof(Properties.LUTProfile), child1.InnerText); break;
                                // skin lookup
                                case "Diffusion":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Scale": Properties.skinUpdate.skinLutScale = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "Bias": Properties.skinUpdate.skinLutBias = XmlConvert.ToSingle(child2.InnerText); break;
                                            default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "/" + child1.Name + "/" + child2.Name + "; ignored"); break;
                                        }
                                    }
                                    break;
                                // shadow lookup
                                case "Shadow":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Scale": Properties.skinUpdate.shadowLutScale = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "Bias": Properties.skinUpdate.shadowLutBias = XmlConvert.ToSingle(child2.InnerText); break;
                                            default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "/" + child1.Name + "/" + child2.Name + "; ignored"); break;
                                        }
                                    }
                                    break;
                                // screen-space normal blur
                                case "NormalBlur":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Weight": Properties.skinUpdate.normalBlurWeight = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "Radius": Properties.skinUpdate.normalBlurRadius = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "CorrectionDepth": Properties.skinUpdate.normalBlurDepthRange = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "Iterations": Properties.skinUpdate.normalBlurIter = XmlConvert.ToInt32(child2.InnerText); break;
                                            default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "/" + child1.Name + "/" + child2.Name + "; ignored"); break;
                                        }
                                    }
                                    break;
                                // ao color bleeding
                                case "AOBleeding":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Red": Properties.skinUpdate.colorBleedWeights.x = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "Green": Properties.skinUpdate.colorBleedWeights.y = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "Blue": Properties.skinUpdate.colorBleedWeights.z = XmlConvert.ToSingle(child2.InnerText); break;
                                            default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "/" + child1.Name + "/" + child2.Name + "; ignored"); break;
                                        }
                                    }
                                    break;
                                // skin transmission absorption
                                case "Absorption":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Red": Properties.skinUpdate.transAbsorption.x = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "Green": Properties.skinUpdate.transAbsorption.y = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "Blue": Properties.skinUpdate.transAbsorption.z = XmlConvert.ToSingle(child2.InnerText); break;
                                            default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "/" + child1.Name + "/" + child2.Name + "; ignored"); break;
                                        }
                                    }
                                    break;

                                default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "/" + child1.Name + "; ignored"); break;
                            }
                        }
                        break;

                    case "Transmission":
                        // enabled?
                        Properties.skinUpdate.transEnabled = XmlConvert.ToBoolean(child0.Attributes["Enabled"].Value);

                        foreach (XmlNode child1 in child0.ChildNodes)
                        {
                            switch (child1.Name)
                            {
                                case "BakedThickness": Properties.skinUpdate.bakedThickness = XmlConvert.ToBoolean(child1.InnerText); break;
                                case "Weight": Properties.skinUpdate.transWeight = XmlConvert.ToSingle(child1.InnerText); break;
                                case "NormalDistortion": Properties.skinUpdate.transDistortion = XmlConvert.ToSingle(child1.InnerText); break;
                                case "ShadowWeight": Properties.skinUpdate.transShadowWeight = XmlConvert.ToSingle(child1.InnerText); break;
                                case "FallOff": Properties.skinUpdate.transFalloff = XmlConvert.ToSingle(child1.InnerText); break;
                                case "ThicknessBias": Properties.skinUpdate.thicknessBias = XmlConvert.ToSingle(child1.InnerText); break;
                                default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "/" + child1.Name + "; ignored"); break;
                            }
                        }
                        break;

                    case "SoftShadow":
                        // pcf kernel size
                        Properties.shadowUpdate.pcfState = (Properties.PCFState)Enum.Parse(typeof(Properties.PCFState), child0.Attributes["State"].Value);

                        foreach (XmlNode child1 in child0.ChildNodes)
                        {
                            switch (child1.Name)
                            {
                                case "PCSS": Properties.shadowUpdate.pcssEnabled = XmlConvert.ToBoolean(child1.InnerText); break;
                                case "Directional":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "SearchRadius": Properties.shadowUpdate.dirLightPenumbra.x = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "LightRadius": Properties.shadowUpdate.dirLightPenumbra.y = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "MinPenumbra": Properties.shadowUpdate.dirLightPenumbra.z = XmlConvert.ToSingle(child2.InnerText); break;
                                            default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "/" + child1.Name + "/" + child2.Name + "; ignored"); break;
                                        }
                                    }
                                    break;

                                case "Spot":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "SearchRadius": Properties.shadowUpdate.spotLightPenumbra.x = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "LightRadius": Properties.shadowUpdate.spotLightPenumbra.y = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "MinPenumbra": Properties.shadowUpdate.spotLightPenumbra.z = XmlConvert.ToSingle(child2.InnerText); break;
                                            default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "/" + child1.Name + "/" + child2.Name + "; ignored"); break;
                                        }
                                    }
                                    break;

                                case "Point":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "SearchRadius": Properties.shadowUpdate.pointLightPenumbra.x = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "LightRadius": Properties.shadowUpdate.pointLightPenumbra.y = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "MinPenumbra": Properties.shadowUpdate.pointLightPenumbra.z = XmlConvert.ToSingle(child2.InnerText); break;
                                            default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "/" + child1.Name + "/" + child2.Name + "; ignored"); break;
                                        }
                                    }
                                    break;

                                default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "/" + child1.Name + "; ignored"); break;
                            }
                        }
                        break;

                    case "AmbientOcclusion":
                        Properties.ssaoUpdate.enabled = XmlConvert.ToBoolean(child0.Attributes["Enabled"].Value);
                        foreach (XmlNode child1 in child0.ChildNodes)
                        {
                            switch (child1.Name)
                            {
                                case "UseGTAO": Properties.ssaoUpdate.usegtao = XmlConvert.ToBoolean(child1.InnerText); break;
                                case "UseSSDO": Properties.ssaoUpdate.usessdo = XmlConvert.ToBoolean(child1.InnerText); break;
                                case "Quality": Properties.ssaoUpdate.quality = (Properties.QualityPreset)Enum.Parse(typeof(Properties.QualityPreset), child1.InnerText); break;
                                case "ScreenDiv": Properties.ssaoUpdate.screenDiv = XmlConvert.ToUInt16(child1.InnerText); break;
                                case "Intensity": Properties.ssaoUpdate.intensity = XmlConvert.ToSingle(child1.InnerText); break;
                                case "LightBias": Properties.ssaoUpdate.lightBias = XmlConvert.ToSingle(child1.InnerText); break;
                                case "RayRadius": Properties.ssaoUpdate.rayRadius = XmlConvert.ToSingle(child1.InnerText); break;
                                case "RayStride": Properties.ssaoUpdate.rayStride = XmlConvert.ToUInt16(child1.InnerText); break;
                                case "MeanDepth": Properties.ssaoUpdate.meanDepth = XmlConvert.ToSingle(child1.InnerText); break;
                                case "FadeDepth": Properties.ssaoUpdate.fadeDepth = XmlConvert.ToSingle(child1.InnerText); break;
                                case "DOApature": Properties.ssaoUpdate.doApature = XmlConvert.ToSingle(child1.InnerText); break;
                                case "Denoiser": Properties.ssaoUpdate.denoise = XmlConvert.ToBoolean(child1.InnerText); break;
                                default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "/" + child1.Name + "; ignored"); break;
                            }
                        }
                        break;

                    case "GlobalIllumination":
                        Properties.ssgiUpdate.enabled = XmlConvert.ToBoolean(child0.Attributes["Enabled"].Value);
                        foreach (XmlNode child1 in child0.ChildNodes)
                        {
                            switch (child1.Name)
                            {
                                case "Quality": Properties.ssgiUpdate.quality = (Properties.QualityPreset)Enum.Parse(typeof(Properties.QualityPreset), child1.InnerText); break;
                                case "SampleScale": Properties.ssgiUpdate.samplescale = (Properties.RenderScale)Enum.Parse(typeof(Properties.RenderScale), child1.InnerText); break;
                                case "RenderScale": Properties.ssgiUpdate.renderscale = (Properties.RenderScale)Enum.Parse(typeof(Properties.RenderScale), child1.InnerText); break;
                                case "Intensity": Properties.ssgiUpdate.intensity = XmlConvert.ToSingle(child1.InnerText); break;
                                case "Secondary": Properties.ssgiUpdate.secondary = XmlConvert.ToSingle(child1.InnerText); break;
                                case "RayRadius": Properties.ssgiUpdate.rayRadius = XmlConvert.ToSingle(child1.InnerText); break;
                                case "RayStride": Properties.ssgiUpdate.rayStride = XmlConvert.ToUInt16(child1.InnerText); break;
                                case "FadeDepth": Properties.ssgiUpdate.fadeDepth = XmlConvert.ToSingle(child1.InnerText); break;
                                case "Denoiser": Properties.ssgiUpdate.denoise = XmlConvert.ToBoolean(child1.InnerText); break;
                                case "MixWeight": Properties.ssgiUpdate.mixWeight = XmlConvert.ToSingle(child1.InnerText); break;
                                default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "/" + child1.Name + "; ignored"); break;
                            }
                        }
                        break;

                    case "ContactShadow":
                        Properties.sscsUpdate.enabled = XmlConvert.ToBoolean(child0.Attributes["Enabled"].Value);
                        foreach (XmlNode child1 in child0.ChildNodes)
                        {
                            switch (child1.Name)
                            {
                                case "Quality": Properties.sscsUpdate.quality = (Properties.QualityPreset)Enum.Parse(typeof(Properties.QualityPreset), child1.InnerText); break;
                                case "RayRadius": Properties.sscsUpdate.rayRadius = XmlConvert.ToSingle(child1.InnerText); break;
                                case "DepthBias": Properties.sscsUpdate.depthBias = XmlConvert.ToSingle(child1.InnerText); break;
                                case "MeanDepth": Properties.sscsUpdate.meanDepth = XmlConvert.ToSingle(child1.InnerText); break;
                                default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "/" + child1.Name + "is; ignored"); break;
                            }
                        }
                        break;

                    case "Miscellaneous":
                        foreach (XmlNode child1 in child0.ChildNodes)
                        {
                            switch (child1.Name)
                            {
                                case "MicroDetails":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Weight_1": Properties.skinUpdate.microDetailWeight_1 = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "Weight_2": Properties.skinUpdate.microDetailWeight_2 = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "Tiling": Properties.skinUpdate.microDetailTiling = XmlConvert.ToSingle(child2.InnerText); break;
                                        }
                                    }
                                    break;

                                // tessellation
                                case "Tessellation":
                                    foreach (XmlNode child2 in child1.ChildNodes)
                                    {
                                        switch (child2.Name)
                                        {
                                            case "Phong": Properties.skinUpdate.phongStrength = XmlConvert.ToSingle(child2.InnerText); break;
                                            case "EdgeLength": Properties.skinUpdate.edgeLength = XmlConvert.ToSingle(child2.InnerText); break;
                                        }
                                    }
                                    break;

                                // eyebrow wrap
                                case "EyebrowOffset": Properties.skinUpdate.eyebrowoffset = XmlConvert.ToSingle(child1.InnerText); break;
                            }
                        }
                        break;

                    default: Console.WriteLine("#### HSSSS: Unknown XML entry " + child0.Name + "is; ignored"); break;
                }
            }
        }

        public static bool SaveExternalFile()
        {
            try
            {
                XmlWriterSettings settings = new XmlWriterSettings() { Indent = true };
                XmlWriter writer = XmlWriter.Create(HSSSS.configFile, settings);
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

        public static bool LoadExternalFile()
        {
            try
            {
                XmlDocument config = new XmlDocument();
                config.Load(HSSSS.configFile);
                XmlParser.LoadXml(config.LastChild);
                return true;
            }

            catch
            {
                return false;
            }
        }
    }
}
