using UnityEngine;
using UnityEngine.Rendering;
using static HMapInfo;

namespace HSSSS
{
    public class DeferredRenderer : MonoBehaviour
    {
        private Camera mCamera;

        private CommandBuffer copyBuffer;
        private CommandBuffer normalBlurBuffer;
        private CommandBuffer diffuseBlurBuffer;

        private readonly string copyBufferName = "HSSSS.ThicknessBlit";
        private readonly string blurBufferName = "HSSSS.ScreenSpaceBlur";

        private static Material copyMaterial;
        private static Material normalBlurMaterial;
        private static Material diffuseBlurMaterial;

        public void Awake()
        {
            copyMaterial = new Material(HSSSS.transmissionBlitShader);
            normalBlurMaterial = new Material(HSSSS.normalBlurShader);
            diffuseBlurMaterial = new Material(HSSSS.diffuseBlurShader);
        }

        public void OnEnable()
        {
            this.mCamera = GetComponent<Camera>();
            this.RefreshProperties();
            this.InitializeBuffers();
        }

        public void OnDisable()
        {
            this.DestroyBuffers();
        }

        #region Properties Control
        private void RefreshSkinProperties()
        {
            Shader.SetGlobalVector("_DeferredSkinParams",
                new Vector4(
                    Properties.skin.sssWeight,
                    Properties.skin.skinLutBias,
                    Properties.skin.skinLutScale,
                    Properties.skin.normalBlurWeight
                    ));
            Shader.SetGlobalVector("_DeferredShadowParams",
                new Vector2(
                    Properties.skin.shadowLutBias,
                    Properties.skin.shadowLutScale
                    ));
            Shader.SetGlobalVector("_DeferredSkinColorBleedAoWeights", Properties.skin.colorBleedWeights);
            Shader.SetGlobalVector("_DeferredSkinTransmissionAbsorption", Properties.skin.transAbsorption);
        }

        private void RefreshBlurProperties()
        {
            if (Properties.skin.lutProfile == Properties.LUTProfile.jimenez)
            {
                diffuseBlurMaterial.SetTexture("_SkinJitter", HSSSS.skinJitter);
                diffuseBlurMaterial.SetVector("_DeferredBlurredNormalsParams",
                    new Vector2(
                        Properties.skin.normalBlurRadius,
                        Properties.skin.normalBlurDepthRange * 100.0f
                        ));
            }

            else
            {
                normalBlurMaterial.SetTexture("_SkinJitter", HSSSS.skinJitter);
                normalBlurMaterial.SetVector("_DeferredBlurredNormalsParams",
                    new Vector2(
                        Properties.skin.normalBlurRadius,
                        Properties.skin.normalBlurDepthRange * 25.0f
                        ));
            }
        }

        private void RefreshLookupProperties()
        {
            Shader.DisableKeyword("_FACEWORKS_TYPE1");
            Shader.DisableKeyword("_FACEWORKS_TYPE2");
            Shader.DisableKeyword("_SCREENSPACE_SSS");

            // lookup texture replacement
            switch (Properties.skin.lutProfile)
            {
                case Properties.LUTProfile.penner:
                    
                    Shader.SetGlobalTexture("_DeferredSkinLut", HSSSS.pennerSkinLUT);
                    break;

                case Properties.LUTProfile.nvidia1:
                    Shader.EnableKeyword("_FACEWORKS_TYPE1");
                    Shader.SetGlobalTexture("_DeferredSkinLut", HSSSS.faceWorksSkinLUT);
                    break;

                case Properties.LUTProfile.nvidia2:
                    Shader.EnableKeyword("_FACEWORKS_TYPE2");
                    Shader.SetGlobalTexture("_DeferredSkinLut", HSSSS.faceWorksSkinLUT);
                    Shader.SetGlobalTexture("_DeferredShadowLut", HSSSS.faceWorksShadowLUT);
                    break;

                case Properties.LUTProfile.jimenez:
                    Shader.EnableKeyword("_SCREENSPACE_SSS");
                    break;
            }
        }

        private void RefreshTransmissionProperties()
        {
            Shader.DisableKeyword("_BAKED_THICKNESS");

            if (Properties.skin.bakedThickness)
            {
                Shader.EnableKeyword("_BAKED_THICKNESS");
            }

            else
            {
                Shader.SetGlobalTexture("_DeferredTransmissionLut", HSSSS.deepScatterLUT);
                Shader.SetGlobalFloat("_DeferredThicknessBias", Properties.skin.thicknessBias * 0.01f);
            }

            Shader.SetGlobalVector("_DeferredTransmissionParams",
                new Vector4(
                    Properties.skin.transWeight,
                    Properties.skin.transFalloff,
                    Properties.skin.transDistortion,
                    Properties.skin.transShadowWeight
                    ));
        }

        private void RefreshProperties()
        {
            this.RefreshSkinProperties();
            this.RefreshBlurProperties();
            this.RefreshLookupProperties();
            this.RefreshTransmissionProperties();
        }
        #endregion

        #region Commandbuffer Control
        private void SetupDiffuseBlurBuffer()
        {
            int ambiRT = Shader.PropertyToID("_AmbientDiffuseBuffer");
            int copyRT = Shader.PropertyToID("_DeferredTransmissionBuffer");
            int flipRT = Shader.PropertyToID("_TemporaryFlipRenderTexture");
            int flopRT = Shader.PropertyToID("_TemporaryFlopRenderTexture");

            ///////////////////////////////////
            ///////////////////////////////////
            // transmission & ambient lights //
            ///////////////////////////////////
            ///////////////////////////////////

            this.copyBuffer = new CommandBuffer() { name = this.copyBufferName };

            // get temporary rendertextures
            this.copyBuffer.GetTemporaryRT(copyRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
            this.copyBuffer.GetTemporaryRT(ambiRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

            // extract thickness map from gbuffer 3
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, copyRT, copyMaterial, 0);

            // extract ambient diffuse
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, ambiRT, copyMaterial, 1);

            // spare gbuffer 3's alpha channel (for specular)
            this.copyBuffer.Blit(ambiRT, BuiltinRenderTextureType.CameraTarget);

            // release rendertexture
            this.copyBuffer.ReleaseTemporaryRT(copyRT);
            this.copyBuffer.ReleaseTemporaryRT(ambiRT);

            // add commandbuffer
            this.mCamera.AddCommandBuffer(CameraEvent.BeforeLighting, this.copyBuffer);

            ///////////////////////////////////
            ///////////////////////////////////
            //// screen space diffuse blur ////
            ///////////////////////////////////
            ///////////////////////////////////
            
            this.diffuseBlurBuffer = new CommandBuffer() { name = this.blurBufferName };

            // get temporary rendertextures
            this.diffuseBlurBuffer.GetTemporaryRT(flipRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.diffuseBlurBuffer.GetTemporaryRT(flopRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

            this.diffuseBlurBuffer.Blit(BuiltinRenderTextureType.CurrentActive, flipRT, diffuseBlurMaterial, 0);

            // separable blur
            for (int i = 0; i < Properties.skin.normalBlurIter; i ++)
            {
                this.diffuseBlurBuffer.Blit(flipRT, flopRT, diffuseBlurMaterial, 1);
                this.diffuseBlurBuffer.Blit(flopRT, flipRT, diffuseBlurMaterial, 2);
            }

            // collect all lightings
            this.diffuseBlurBuffer.Blit(flipRT, flopRT, diffuseBlurMaterial, 3);

            // to camera target
            this.diffuseBlurBuffer.Blit(flopRT, BuiltinRenderTextureType.CameraTarget);

            // release rendertextures
            this.diffuseBlurBuffer.ReleaseTemporaryRT(flipRT);
            this.diffuseBlurBuffer.ReleaseTemporaryRT(flopRT);

            // add commandbuffer
            this.mCamera.AddCommandBuffer(CameraEvent.AfterLighting, this.diffuseBlurBuffer);
        }

        private void SetupNormalBlurBuffer()
        {
            int specRT = Shader.PropertyToID("_AmbientReflectionBuffer");
            int copyRT = Shader.PropertyToID("_DeferredTransmissionBuffer");
            int buffRT = Shader.PropertyToID("_DeferredBlurredNormalBuffer");
            int flipRT = Shader.PropertyToID("_TemporaryFlipRenderTexture");
            int flopRT = Shader.PropertyToID("_TemporaryFlopRenderTexture");

            ///////////////////////////////////////
            ///////////////////////////////////////
            //// transmission & ambient lights ////
            ///////////////////////////////////////
            ///////////////////////////////////////

            this.copyBuffer = new CommandBuffer() { name = this.copyBufferName };

            // get temporary rendertextures
            this.copyBuffer.GetTemporaryRT(copyRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            this.copyBuffer.GetTemporaryRT(specRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

            // extract thickness map from gbuffer 3
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, copyRT, copyMaterial, 0);

            // ambient reflections
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, specRT, copyMaterial, 2);
            this.copyBuffer.Blit(specRT, BuiltinRenderTextureType.CameraTarget);

            // release rendertexture
            this.copyBuffer.ReleaseTemporaryRT(copyRT);
            this.copyBuffer.ReleaseTemporaryRT(specRT);

            // add commandbuffer
            this.mCamera.AddCommandBuffer(CameraEvent.BeforeLighting, this.copyBuffer);

            //////////////////////////////////
            //////////////////////////////////
            //// screen space normal blur ////
            //////////////////////////////////
            //////////////////////////////////

            this.normalBlurBuffer = new CommandBuffer() { name = this.blurBufferName };

            this.normalBlurBuffer.GetTemporaryRT(flipRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
            this.normalBlurBuffer.GetTemporaryRT(flopRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
            this.normalBlurBuffer.GetTemporaryRT(buffRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);

            if (Properties.skin.normalBlurIter > 0)
            {
                this.normalBlurBuffer.Blit(BuiltinRenderTextureType.GBuffer2, flipRT, normalBlurMaterial, 0);
                this.normalBlurBuffer.Blit(flipRT, flopRT, normalBlurMaterial, 1);

                for (int i = 1; i < Properties.skin.normalBlurIter; i++)
                {
                    this.normalBlurBuffer.Blit(flopRT, flipRT, normalBlurMaterial, 0);
                    this.normalBlurBuffer.Blit(flipRT, flopRT, normalBlurMaterial, 1);
                }

                this.normalBlurBuffer.Blit(flopRT, buffRT);
            }

            else
            {
                this.normalBlurBuffer.Blit(BuiltinRenderTextureType.GBuffer2, buffRT);
            }

            this.normalBlurBuffer.ReleaseTemporaryRT(flipRT);
            this.normalBlurBuffer.ReleaseTemporaryRT(flopRT);
            this.normalBlurBuffer.ReleaseTemporaryRT(buffRT);

            this.mCamera.AddCommandBuffer(CameraEvent.BeforeLighting, this.normalBlurBuffer);
        }

        private void InitDummyBuffer()
        {
            int copyRT = Shader.PropertyToID("_DeferredTransmissionBuffer");

            ///////////////////////////////////
            // transmission & ambient lights //
            ///////////////////////////////////
            
            this.copyBuffer = new CommandBuffer() { name = this.copyBufferName };

            // get temporary rendertextures
            this.copyBuffer.GetTemporaryRT(copyRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);

            // extract thickness map from gbuffer 3
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, copyRT, copyMaterial, 0);

            // release rendertexture
            this.copyBuffer.ReleaseTemporaryRT(copyRT);

            // add commandbuffer
            this.mCamera.AddCommandBuffer(CameraEvent.AfterGBuffer, this.copyBuffer);
        }

        private void InitializeBuffers()
        {
            // buffer 0: hsr compatible buffer
            if (HSSSS.hsrCompatible)
            {
                this.InitDummyBuffer();
            }

            else
            {
                // buffer 1: screen space scattering
                if (Properties.skin.lutProfile == Properties.LUTProfile.jimenez)
                {
                    this.SetupDiffuseBlurBuffer();
                }

                // buffer 2: pre-integrated scattering
                else
                {
                    this.SetupNormalBlurBuffer();
                }
            }
        }

        private void DestroyBuffers()
        {
            if (this.copyBuffer != null)
            {
                this.mCamera.RemoveCommandBuffer(CameraEvent.BeforeLighting, this.copyBuffer);
            }

            if (this.normalBlurBuffer != null)
            {
                this.mCamera.RemoveCommandBuffer(CameraEvent.BeforeLighting, this.normalBlurBuffer);
            }

            if (this.diffuseBlurBuffer != null)
            {
                this.mCamera.RemoveCommandBuffer(CameraEvent.AfterLighting, this.diffuseBlurBuffer);
            }
        }
        #endregion

        #region Interfaces
        public void UpdateSkinSettings(bool soft = true)
        {
            this.RefreshSkinProperties();
            this.RefreshBlurProperties();
            this.RefreshLookupProperties();
            this.RefreshTransmissionProperties();

            if (!soft)
            {
                this.DestroyBuffers();
                this.InitializeBuffers();
            }
        }
        #endregion
    }

    public class SSAORenderer : MonoBehaviour
    {
        private Camera mCamera;
        private Material mMaterial;
        private CommandBuffer mBuffer;

        private void Awake()
        {
            this.mCamera = GetComponent<Camera>();
            this.mMaterial = new Material(HSSSS.ssaoShader);
            this.mMaterial.SetTexture("_BlueNoise", HSSSS.blueNoise);
        }

        private void OnEnable()
        {
            this.UpdateSSAOSettings(true);

            if (this.mCamera && Properties.ssao.enabled)
            {
                this.SetupCommandBuffer();
            }
        }

        private void OnDisable()
        {
            if (this.mCamera)
            {
                if (this.mBuffer != null)
                {
                    this.RemoveCommandBuffer();
                }
            }
        }

        private void OnPreRender()
        {
            this.UpdateMatrices();
            this.mMaterial.SetInt("_FrameCount", Time.frameCount);
        }

        private void SetupCommandBuffer()
        {
            // SSAO command buffer
            this.mBuffer = new CommandBuffer() { name = "HSSSS.SSAO" };

            int zbf0 = Shader.PropertyToID("_HierachicalZBuffer0");
            int zbf1 = Shader.PropertyToID("_HierachicalZBuffer1");
            int zbf2 = Shader.PropertyToID("_HierachicalZBuffer2");
            int zbf3 = Shader.PropertyToID("_HierachicalZBuffer3");

            int flip = Shader.PropertyToID("_SSAOFlipRenderTexture");
            int flop = Shader.PropertyToID("_SSAOFlopRenderTexture");

            this.mBuffer.GetTemporaryRT(zbf0, -1, -1, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(zbf1, Screen.width / 2, Screen.height / 2, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(zbf2, Screen.width / 4, Screen.height / 4, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(zbf3, Screen.width / 8, Screen.height / 8, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);

            this.mBuffer.GetTemporaryRT(flip, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(flop, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            
            // z-buffer prepass
            this.mBuffer.Blit(BuiltinRenderTextureType.CurrentActive, zbf0, this.mMaterial, 0);

            // hierachical z buffers
            this.mBuffer.Blit(zbf0, zbf1);
            this.mBuffer.Blit(zbf1, zbf2);
            this.mBuffer.Blit(zbf2, zbf3);

            // gtao
            if (Properties.ssao.usegtao)
            {
                this.mBuffer.Blit(zbf0, flip, this.mMaterial, (int)Properties.ssao.quality + 5);
            }
            
            // hbao
            else
            {
                this.mBuffer.Blit(zbf0, flip, this.mMaterial, (int)Properties.ssao.quality + 1);
            }

            // decode pass
            this.mBuffer.Blit(flip, flop, this.mMaterial, 9);

            // spatio noise filtering
            if (Properties.ssao.denoise)
            {
                this.mBuffer.Blit(flop, flip, this.mMaterial, 10);
                this.mBuffer.Blit(flip, flop, this.mMaterial, 11);
                this.mBuffer.Blit(flop, flip, this.mMaterial, 12);
                this.mBuffer.Blit(flip, flop);
            }

            // diffuse occlusion
            this.mBuffer.Blit(BuiltinRenderTextureType.CameraTarget, flip, this.mMaterial, 15);
            this.mBuffer.Blit(flip, BuiltinRenderTextureType.CameraTarget);
            // specular occlusion
            this.mBuffer.Blit(BuiltinRenderTextureType.Reflections, flip, this.mMaterial, 16);
            this.mBuffer.Blit(flip, BuiltinRenderTextureType.Reflections);
            // direct occlusion
            this.mBuffer.SetGlobalTexture("_SSDOBentNormalTexture", flop);

            this.mBuffer.ReleaseTemporaryRT(flip);
            this.mBuffer.ReleaseTemporaryRT(flop);
            this.mBuffer.ReleaseTemporaryRT(zbf0);
            this.mBuffer.ReleaseTemporaryRT(zbf1);
            this.mBuffer.ReleaseTemporaryRT(zbf2);
            this.mBuffer.ReleaseTemporaryRT(zbf3);

            this.mCamera.AddCommandBuffer(CameraEvent.AfterReflections, this.mBuffer);
        }

        private void RemoveCommandBuffer()
        {
            this.mCamera.RemoveCommandBuffer(CameraEvent.AfterReflections, this.mBuffer);
            this.mBuffer = null;
        }

        private void UpdateMatrices()
        {
            this.mMaterial.SetMatrix("_WorldToViewMatrix", HSSSS.CameraProjector.CurrentWorldToView);
            this.mMaterial.SetMatrix("_ViewToWorldMatrix", HSSSS.CameraProjector.CurrentViewToWorld);

            this.mMaterial.SetMatrix("_ViewToClipMatrix", HSSSS.CameraProjector.CurrentViewToClip);
            this.mMaterial.SetMatrix("_ClipToViewMatrix", HSSSS.CameraProjector.CurrentClipToView);

            this.mMaterial.SetMatrix("_PrevWorldToViewMatrix", HSSSS.CameraProjector.PreviousWorldToView);
            this.mMaterial.SetMatrix("_PrevViewToWorldMatrix", HSSSS.CameraProjector.PreviousViewToWorld);

            this.mMaterial.SetMatrix("_PrevViewToClipMatrix", HSSSS.CameraProjector.PreviousViewToClip);
            this.mMaterial.SetMatrix("_PrevClipToViewMatrix", HSSSS.CameraProjector.PreviousClipToView);
        }

        public void UpdateSSAOSettings(bool soft = true)
        {
            if (this.enabled)
            {
                this.mMaterial.SetFloat("_SSAOFadeDepth", Properties.ssao.fadeDepth);
                this.mMaterial.SetFloat("_SSAORayLength", Properties.ssao.rayRadius);
                this.mMaterial.SetFloat("_SSAOIntensity", Properties.ssao.intensity);
                this.mMaterial.SetFloat("_SSAOLightBias", Properties.ssao.lightBias);
                this.mMaterial.SetFloat("_SSAOMeanDepth", Properties.ssao.meanDepth);
                this.mMaterial.SetInt(  "_SSAORayStride", Properties.ssao.rayStride);
                this.mMaterial.SetInt(  "_SSAOScreenDiv", Properties.ssao.screenDiv);

                Shader.SetGlobalInt("_UseDirectOcclusion", Properties.ssao.usessdo ? 1 : 0);
                Shader.SetGlobalFloat("_SSDOLightApatureScale", Properties.ssao.doApature);
            }

            if (!soft)
            {
                this.RemoveCommandBuffer();
                this.SetupCommandBuffer();
            }
        }
    }

    public class SSGIRenderer : MonoBehaviour
    {
        private Camera mCamera;
        private Material mMaterial;
        private CommandBuffer mBuffer;
        private RenderTexture giHistory;
        private RenderTexture zbHistory;

        private void Awake()
        {
            this.mCamera = GetComponent<Camera>();
            this.mMaterial = new Material(HSSSS.ssgiShader);
            this.mMaterial.SetTexture("_BlueNoise", HSSSS.blueNoise);
        }

        private void OnEnable()
        {
            this.UpdateSSGISettings(true);

            if(this.mCamera && Properties.ssgi.enabled)
            {
                this.SetupHistoryBuffer();
                this.SetupCommandBuffer();
            }
        }

        private void OnDisable()
        {
            if (this.mCamera)
            {
                if (this.mBuffer != null)
                {
                    this.RemoveHistoryBuffer();
                    this.RemoveCommandBuffer();
                }
            }
        }

        private void OnPreRender()
        {
            this.UpdateMatrices();

            this.mMaterial.SetTexture("_SSGITemporalGIBuffer", this.giHistory);
            this.mMaterial.SetTexture("_CameraDepthHistory", this.zbHistory);
            this.mMaterial.SetInt("_FrameCount", Time.frameCount);
        }

        private void SetupCommandBuffer()
        {
            RenderTargetIdentifier hist = new RenderTargetIdentifier(this.giHistory);
            RenderTargetIdentifier zbuf = new RenderTargetIdentifier(this.zbHistory);

            this.mBuffer = new CommandBuffer() { name = "HSSSS.SSGI" };

            int ibf0 = Shader.PropertyToID("_HierachicalIrradianceBuffer0");
            int ibf1 = Shader.PropertyToID("_HierachicalIrradianceBuffer1");
            int ibf2 = Shader.PropertyToID("_HierachicalIrradianceBuffer2");
            int ibf3 = Shader.PropertyToID("_HierachicalIrradianceBuffer3");

            //int ilum = Shader.PropertyToID("_SSGIIrradianceTexture");
            int flip = Shader.PropertyToID("_SSGIFlipRenderTexture");
            int flop = Shader.PropertyToID("_SSGIFlopRenderTexture");

            switch (Properties.ssgi.samplescale)
            {
                case Properties.RenderScale.quarter:
                    this.mBuffer.GetTemporaryRT(ibf0, Screen.width / 4, Screen.height / 4, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    this.mBuffer.GetTemporaryRT(ibf1, Screen.width / 8, Screen.height / 8, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    this.mBuffer.GetTemporaryRT(ibf2, Screen.width / 16, Screen.height / 16, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    this.mBuffer.GetTemporaryRT(ibf3, Screen.width / 32, Screen.height / 32, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    break;

                case Properties.RenderScale.half:
                    this.mBuffer.GetTemporaryRT(ibf0, Screen.width / 2, Screen.height / 2, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    this.mBuffer.GetTemporaryRT(ibf1, Screen.width / 4, Screen.height / 4, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    this.mBuffer.GetTemporaryRT(ibf2, Screen.width / 8, Screen.height / 8, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    this.mBuffer.GetTemporaryRT(ibf3, Screen.width / 16, Screen.height / 16, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    break;

                case Properties.RenderScale.full:
                    this.mBuffer.GetTemporaryRT(ibf0, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    this.mBuffer.GetTemporaryRT(ibf1, Screen.width / 2, Screen.height / 2, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    this.mBuffer.GetTemporaryRT(ibf2, Screen.width / 4, Screen.height / 4, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    this.mBuffer.GetTemporaryRT(ibf3, Screen.width / 8, Screen.height / 8, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    break;
            }

            this.mBuffer.GetTemporaryRT(flip, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(flop, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

            // prepass
            this.mBuffer.Blit(hist, ibf0, this.mMaterial, 0);
            this.mBuffer.Blit(ibf0, ibf1);
            this.mBuffer.Blit(ibf1, ibf2);
            this.mBuffer.Blit(ibf2, ibf3);

            //this.mBuffer.Blit(hist, ilum, this.mMaterial, 0);

            // main pass
            this.mBuffer.Blit(BuiltinRenderTextureType.CurrentActive, flip, this.mMaterial, (int)Properties.ssgi.quality + 1);

            // temporal
            this.mBuffer.Blit(flip, flop, this.mMaterial, 5);

            // store
            this.mBuffer.Blit(flop, hist);
            this.mBuffer.Blit(hist, zbuf, this.mMaterial, 12);

            // median filter
            this.mBuffer.Blit(flop, flip, this.mMaterial, 10);
            this.mBuffer.Blit(flip, flop);

            // spatial
            if (Properties.ssgi.denoise)
            {
                this.mBuffer.Blit(flop, flip, this.mMaterial, 6);
                this.mBuffer.Blit(flip, flop, this.mMaterial, 7);
                this.mBuffer.Blit(flop, flip, this.mMaterial, 8);
                this.mBuffer.Blit(flip, flop, this.mMaterial, 9);
            }

            // collect
            this.mBuffer.Blit(flop, flip, this.mMaterial, 11);
            this.mBuffer.Blit(flip, BuiltinRenderTextureType.CameraTarget);

            this.mBuffer.ReleaseTemporaryRT(ibf0);
            this.mBuffer.ReleaseTemporaryRT(ibf1);
            this.mBuffer.ReleaseTemporaryRT(ibf2);
            this.mBuffer.ReleaseTemporaryRT(ibf3);

            //this.mBuffer.ReleaseTemporaryRT(ilum);
            this.mBuffer.ReleaseTemporaryRT(flip);
            this.mBuffer.ReleaseTemporaryRT(flop);

            this.mCamera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, this.mBuffer);
        }

        private void RemoveCommandBuffer()
        {
            this.mCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, this.mBuffer);
            this.mBuffer = null;
        }

        private void SetupHistoryBuffer()
        {
            this.giHistory = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.giHistory.Create();

            this.zbHistory = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            this.zbHistory.Create();

            RenderTexture rt = RenderTexture.active;
            RenderTexture.active = this.giHistory;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = this.zbHistory;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = rt;
        }

        private void RemoveHistoryBuffer()
        {
            if (this.giHistory)
            {
                this.giHistory.Release();
                this.giHistory = null;
            }

            if (this.zbHistory)
            {
                this.zbHistory.Release();
                this.zbHistory = null;
            }
        }

        private void UpdateMatrices()
        {
            this.mMaterial.SetMatrix("_WorldToViewMatrix", HSSSS.CameraProjector.CurrentWorldToView);
            this.mMaterial.SetMatrix("_ViewToWorldMatrix", HSSSS.CameraProjector.CurrentViewToWorld);

            this.mMaterial.SetMatrix("_ViewToClipMatrix", HSSSS.CameraProjector.CurrentViewToClip);
            this.mMaterial.SetMatrix("_ClipToViewMatrix", HSSSS.CameraProjector.CurrentClipToView);

            this.mMaterial.SetMatrix("_PrevWorldToViewMatrix", HSSSS.CameraProjector.PreviousWorldToView);
            this.mMaterial.SetMatrix("_PrevViewToWorldMatrix", HSSSS.CameraProjector.PreviousViewToWorld);

            this.mMaterial.SetMatrix("_PrevViewToClipMatrix", HSSSS.CameraProjector.PreviousViewToClip);
            this.mMaterial.SetMatrix("_PrevClipToViewMatrix", HSSSS.CameraProjector.PreviousClipToView);
        }

        public void UpdateSSGISettings(bool soft = true)
        {
            if (this.enabled)
            {
                this.mMaterial.SetFloat("_SSGIFadeDepth", Properties.ssgi.fadeDepth);
                this.mMaterial.SetFloat("_SSGIMixFactor", Properties.ssgi.mixWeight);
                this.mMaterial.SetFloat("_SSGIRayLength", Properties.ssgi.rayRadius);
                this.mMaterial.SetFloat("_SSGIIntensity", Properties.ssgi.intensity);
                this.mMaterial.SetFloat("_SSGISecondary", Properties.ssgi.secondary);
                this.mMaterial.SetInt("_SSGIStepPower", Properties.ssgi.rayStride);
            }

            if (!soft)
            {
                this.RemoveCommandBuffer();
                this.RemoveHistoryBuffer();
                this.SetupHistoryBuffer();
                this.SetupCommandBuffer();
            }
        }
    }
    
    public class CameraProjector : MonoBehaviour
    {
        private Camera mCamera;

        public Matrix4x4 CurrentWorldToView;
        public Matrix4x4 CurrentViewToWorld;

        public Matrix4x4 CurrentViewToClip;
        public Matrix4x4 CurrentClipToView;

        public Matrix4x4 PreviousWorldToView;
        public Matrix4x4 PreviousViewToWorld;

        public Matrix4x4 PreviousViewToClip;
        public Matrix4x4 PreviousClipToView;

        private void Awake()
        {
            this.CurrentWorldToView = Matrix4x4.identity;
            this.CurrentViewToWorld = Matrix4x4.identity;

            this.CurrentViewToClip = Matrix4x4.identity;
            this.CurrentClipToView = Matrix4x4.identity;

            this.PreviousWorldToView = Matrix4x4.identity;
            this.PreviousViewToWorld = Matrix4x4.identity;

            this.PreviousViewToClip = Matrix4x4.identity;
            this.PreviousClipToView = Matrix4x4.identity;
        }

        private void OnEnable()
        {
            mCamera = GetComponent<Camera>();
        }

        private void OnDisable()
        {
            mCamera = null;
        }

        private void OnPreRender()
        {
            if (mCamera != null)
            {
                this.UpdateMatrices();
            }
        }

        private void UpdateMatrices()
        {
            // previous frame
            this.PreviousWorldToView = this.CurrentWorldToView;
            this.PreviousViewToWorld = this.CurrentViewToWorld;

            this.PreviousViewToClip = this.CurrentViewToClip;
            this.PreviousClipToView = this.CurrentClipToView;

            // current frame
            this.CurrentWorldToView = this.mCamera.worldToCameraMatrix;
            this.CurrentViewToWorld = this.CurrentWorldToView.inverse;

            this.CurrentViewToClip = this.mCamera.projectionMatrix;
            this.CurrentClipToView = this.CurrentViewToClip.inverse;
        }
    }
}