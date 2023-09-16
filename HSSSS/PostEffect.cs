using UnityEngine;
using UnityEngine.Rendering;

namespace HSSSS
{
    public class DeferredRenderer : MonoBehaviour
    {
        private Camera mCamera;

        private CommandBuffer copyBuffer;
        private CommandBuffer blurBuffer;

        private readonly string copyBufferName = "HSSSS.SSSPrePass";
        private readonly string blurBufferName = "HSSSS.SSSMainPass";

        private static Material prePass;
        private static Material mainPass;

        public void Awake()
        {
            prePass = new Material(AssetLoader.sssPrePass);
            mainPass = new Material(AssetLoader.sssMainPass);
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

        private void OnPreRender()
        {
            Shader.SetGlobalInt("_FrameCount", Time.frameCount);
        }

        #region Properties Control
        private void RefreshSkinProperties()
        {
            Shader.SetGlobalVector("_DeferredSkinParams",
                new Vector4(
                    1.0f,
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
            mainPass.SetTexture("_SkinJitter", AssetLoader.skinJitter);
            mainPass.SetVector("_DeferredBlurredNormalsParams",
                new Vector2(
                    Properties.skin.normalBlurRadius,
                    Properties.skin.normalBlurDepthRange * 100.0f
                    ));
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
                    
                    Shader.SetGlobalTexture("_DeferredSkinLut", AssetLoader.pennerDiffuse);
                    break;

                case Properties.LUTProfile.nvidia1:
                    Shader.EnableKeyword("_FACEWORKS_TYPE1");
                    Shader.SetGlobalTexture("_DeferredSkinLut", AssetLoader.nvidiaDiffuse);
                    break;

                case Properties.LUTProfile.nvidia2:
                    Shader.EnableKeyword("_FACEWORKS_TYPE2");
                    Shader.SetGlobalTexture("_DeferredSkinLut", AssetLoader.nvidiaDiffuse);
                    Shader.SetGlobalTexture("_DeferredShadowLut", AssetLoader.nvidiaShadow);
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
                Shader.SetGlobalTexture("_DeferredTransmissionLut", AssetLoader.deepScatter);
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
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, copyRT, prePass, 0);

            // extract ambient diffuse
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, ambiRT, prePass, 1);

            // spare gbuffer 3's alpha channel (for specular)
            this.copyBuffer.Blit(ambiRT, BuiltinRenderTextureType.CameraTarget);

            // add commandbuffer
            this.mCamera.AddCommandBuffer(CameraEvent.BeforeLighting, this.copyBuffer);

            ///////////////////////////////////
            ///////////////////////////////////
            //// screen space diffuse blur ////
            ///////////////////////////////////
            ///////////////////////////////////
            
            this.blurBuffer = new CommandBuffer() { name = this.blurBufferName };

            // get temporary rendertextures
            this.blurBuffer.GetTemporaryRT(flipRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.blurBuffer.GetTemporaryRT(flopRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

            this.blurBuffer.Blit(BuiltinRenderTextureType.CurrentActive, flipRT, mainPass, 0);

            // separable blur
            for (int i = 0; i < Properties.skin.normalBlurIter; i ++)
            {
                this.blurBuffer.Blit(flipRT, flopRT, mainPass, 1);
                this.blurBuffer.Blit(flopRT, flipRT, mainPass, 2);
            }

            // collect all lightings
            this.blurBuffer.Blit(flipRT, flopRT, mainPass, 3);

            // to camera target
            this.blurBuffer.Blit(flopRT, BuiltinRenderTextureType.CameraTarget);

            // release rendertextures
            this.blurBuffer.ReleaseTemporaryRT(flipRT);
            this.blurBuffer.ReleaseTemporaryRT(flopRT);
            this.blurBuffer.ReleaseTemporaryRT(copyRT);
            this.blurBuffer.ReleaseTemporaryRT(ambiRT);

            // add commandbuffer
            this.mCamera.AddCommandBuffer(CameraEvent.AfterLighting, this.blurBuffer);
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
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, copyRT, prePass, 0);

            // ambient reflections
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, specRT, prePass, 2);
            this.copyBuffer.Blit(specRT, BuiltinRenderTextureType.CameraTarget);

            // add commandbuffer
            this.mCamera.AddCommandBuffer(CameraEvent.BeforeLighting, this.copyBuffer);

            //////////////////////////////////
            //////////////////////////////////
            //// screen space normal blur ////
            //////////////////////////////////
            //////////////////////////////////

            this.blurBuffer = new CommandBuffer() { name = this.blurBufferName };

            this.blurBuffer.GetTemporaryRT(flipRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
            this.blurBuffer.GetTemporaryRT(flopRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
            this.blurBuffer.GetTemporaryRT(buffRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);

            if (Properties.skin.normalBlurIter > 0)
            {
                this.blurBuffer.Blit(BuiltinRenderTextureType.GBuffer2, flipRT, mainPass, 4);
                this.blurBuffer.Blit(flipRT, flopRT, mainPass, 5);

                for (int i = 1; i < Properties.skin.normalBlurIter; i++)
                {
                    this.blurBuffer.Blit(flopRT, flipRT, mainPass, 4);
                    this.blurBuffer.Blit(flipRT, flopRT, mainPass, 5);
                }

                this.blurBuffer.Blit(flopRT, buffRT);
            }

            else
            {
                this.blurBuffer.Blit(BuiltinRenderTextureType.GBuffer2, buffRT);
            }

            this.blurBuffer.ReleaseTemporaryRT(flipRT);
            this.blurBuffer.ReleaseTemporaryRT(flopRT);
            this.blurBuffer.ReleaseTemporaryRT(buffRT);
            this.blurBuffer.ReleaseTemporaryRT(copyRT);
            this.blurBuffer.ReleaseTemporaryRT(specRT);

            this.mCamera.AddCommandBuffer(CameraEvent.BeforeLighting, this.blurBuffer);
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
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, copyRT, prePass, 0);

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
            foreach (CommandBuffer buffer in this.mCamera.GetCommandBuffers(CameraEvent.BeforeLighting))
            {
                if (buffer.name == this.copyBufferName || buffer.name == this.blurBufferName)
                {
                    this.mCamera.RemoveCommandBuffer(CameraEvent.BeforeLighting, buffer);
                }
            }

            foreach (CommandBuffer buffer in this.mCamera.GetCommandBuffers(CameraEvent.AfterLighting))
            {
                if (buffer.name == this.blurBufferName)
                {
                    this.mCamera.RemoveCommandBuffer(CameraEvent.AfterLighting, buffer);
                }
            }

            this.copyBuffer = null;
            this.blurBuffer = null;
        }
        #endregion

        #region Interfaces
        public void UpdateSkinSettings()
        {
            this.RefreshSkinProperties();
            this.RefreshBlurProperties();
            this.RefreshLookupProperties();
            this.RefreshTransmissionProperties();

            this.DestroyBuffers();
            this.InitializeBuffers();
        }
        #endregion
    }

    public class SSAORenderer : MonoBehaviour
    {
        private Camera mCamera;
        private Material mMaterial;
        private CommandBuffer mBuffer;
        private readonly string bufferName = "HSSSS.SSAO";

        private void Awake()
        {
            this.mCamera = GetComponent<Camera>();
            this.mMaterial = new Material(AssetLoader.ssao);
            this.mMaterial.SetTexture("_BlueNoise", AssetLoader.blueNoise);
        }

        private void OnEnable()
        {
            this.UpdateSSAOSettings();

            if (this.mCamera && Properties.ssao.enabled)
            {
                this.SetupCommandBuffer();
            }
        }

        private void OnDisable()
        {
            if (this.mCamera)
            {
                this.RemoveCommandBuffer();
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
            this.mBuffer = new CommandBuffer() { name = this.bufferName };

            int zbf0 = Shader.PropertyToID("_HierachicalZBuffer0");
            int zbf1 = Shader.PropertyToID("_HierachicalZBuffer1");
            int zbf2 = Shader.PropertyToID("_HierachicalZBuffer2");
            int zbf3 = Shader.PropertyToID("_HierachicalZBuffer3");

            int flip = Shader.PropertyToID("_SSAOFlipRenderTexture");
            int flop = Shader.PropertyToID("_SSAOFlopRenderTexture");

            this.mBuffer.GetTemporaryRT(zbf0, -1, -1, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(zbf1, this.mCamera.pixelWidth / 2, this.mCamera.pixelHeight / 2, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(zbf2, this.mCamera.pixelWidth / 4, this.mCamera.pixelHeight / 4, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(zbf3, this.mCamera.pixelWidth / 8, this.mCamera.pixelHeight / 8, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);

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
            foreach (CommandBuffer buffer in this.mCamera.GetCommandBuffers(CameraEvent.AfterReflections))
            {
                if (buffer.name == this.bufferName)
                {
                    this.mCamera.RemoveCommandBuffer(CameraEvent.AfterReflections, buffer);
                }
            }

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

        public void UpdateSSAOSettings()
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
        private static Mesh mrtMesh = null;
        private readonly string bufferName = "HSSSS.SSGI";

        private void Awake()
        {
            this.mCamera = GetComponent<Camera>();
            this.mMaterial = new Material(AssetLoader.ssgi);
            this.mMaterial.SetTexture("_BlueNoise", AssetLoader.blueNoise);
        }

        private void OnEnable()
        {
            this.UpdateSSGISettings(false);
            this.SetupFullScreenMesh();

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

            int[] irad = new int[]
            {
                Shader.PropertyToID("_HierachicalIrradianceBuffer0"),
                Shader.PropertyToID("_HierachicalIrradianceBuffer1"),
                Shader.PropertyToID("_HierachicalIrradianceBuffer2"),
                Shader.PropertyToID("_HierachicalIrradianceBuffer3")
            };

            int[] flip = new int[]
            {
                Shader.PropertyToID("_SSGIFlipDiffuseBuffer"),
                Shader.PropertyToID("_SSGIFlipSpecularBuffer")
            };

            int[] flop = new int[]
            {
                Shader.PropertyToID("_SSGIFlopDiffuseBuffer"),
                Shader.PropertyToID("_SSGIFlopSpecularBuffer")
            };

            RenderTargetIdentifier[] flipMRT = { flip[0], flip[1] };
            RenderTargetIdentifier[] flopMRT = { flop[0], flop[1] };

            this.mBuffer = new CommandBuffer() { name = "HSSSS.SSGI" };

            int div = 1;

            if (Properties.ssgi.samplescale == Properties.RenderScale.half)
            {
                div = 2;
            }

            else if (Properties.ssgi.samplescale == Properties.RenderScale.quarter)
            {
                div = 4;
            }

            for (int i = 0; i < irad.Length; i ++)
            {
                this.mBuffer.GetTemporaryRT(irad[i], this.mCamera.pixelWidth / div, this.mCamera.pixelHeight / div, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                div = div * 2;
            }

            for (int i = 0; i < 2; i ++)
            {
                this.mBuffer.GetTemporaryRT(flip[i], this.mCamera.pixelWidth, this.mCamera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                this.mBuffer.GetTemporaryRT(flop[i], this.mCamera.pixelWidth, this.mCamera.pixelHeight, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            }

            // prepass
            this.mBuffer.Blit(hist, irad[0], this.mMaterial, 0);
            this.mBuffer.Blit(irad[0], irad[1]);
            this.mBuffer.Blit(irad[1], irad[2]);
            this.mBuffer.Blit(irad[2], irad[3]);

            // main pass
            this.mBuffer.SetRenderTarget(flipMRT, BuiltinRenderTextureType.CameraTarget);
            this.mBuffer.DrawMesh(mrtMesh, Matrix4x4.identity, this.mMaterial, 0, (int)Properties.ssgi.quality + 1);

            if (Properties.ssgi.denoise)
            {
                this.mBuffer.SetRenderTarget(flopMRT, BuiltinRenderTextureType.CameraTarget);
                this.mBuffer.DrawMesh(mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 6);
                this.mBuffer.SetRenderTarget(flipMRT, BuiltinRenderTextureType.CameraTarget);
                this.mBuffer.DrawMesh(mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 7);
                /*
                this.mBuffer.SetRenderTarget(flopMRT, BuiltinRenderTextureType.CameraTarget);
                this.mBuffer.DrawMesh(mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 8);
                this.mBuffer.SetRenderTarget(flipMRT, BuiltinRenderTextureType.CameraTarget);
                this.mBuffer.DrawMesh(mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 9);
                */
            }

            // temporal filter
            this.mBuffer.Blit(BuiltinRenderTextureType.CurrentActive, flop[0], this.mMaterial, 5);

            // store
            this.mBuffer.Blit(flop[0], hist);
            this.mBuffer.Blit(hist, zbuf, this.mMaterial, 12);

            // median filter
            this.mBuffer.Blit(flop[0], flip[0], this.mMaterial, 10);

            // collect
            this.mBuffer.Blit(flip[0], flop[0], this.mMaterial, 11);
            this.mBuffer.Blit(flop[0], BuiltinRenderTextureType.CameraTarget);

            for (int i = 0; i < 4; i ++)
            {
                this.mBuffer.ReleaseTemporaryRT(irad[i]);
            }

            for (int i = 0; i < 2; i ++)
            {
                this.mBuffer.ReleaseTemporaryRT(flip[i]);
                this.mBuffer.ReleaseTemporaryRT(flop[i]);
            }

            this.mCamera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque, this.mBuffer);
        }

        private void RemoveCommandBuffer()
        {
            foreach (CommandBuffer buffer in this.mCamera.GetCommandBuffers(CameraEvent.BeforeForwardOpaque))
            {
                if (buffer.name == this.bufferName)
                {
                    this.mCamera.RemoveCommandBuffer(CameraEvent.BeforeForwardOpaque, buffer);
                }
            }

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

        private void SetupFullScreenMesh()
        {
            if (mrtMesh == null)
            {
                mrtMesh = new Mesh()
                {
                    vertices = new Vector3[]
                    {
                        new Vector3(-1.0f, -1.0f, 0.0f),
                        new Vector3(-1.0f,  3.0f, 0.0f),
                        new Vector3( 3.0f, -1.0f, 0.0f)
                    },

                    triangles = new int[] { 0, 1, 2 }
                };
            }

            else
            {
                mrtMesh.vertices = new Vector3[]
                {
                    new Vector3(-1.0f, -1.0f, 0.0f),
                    new Vector3(-1.0f,  3.0f, 0.0f),
                    new Vector3( 3.0f, -1.0f, 0.0f)
                };

                mrtMesh.triangles = new int[] { 0, 1, 2 };
            }
        }

        public void UpdateSSGISettings(bool hard = true)
        {
            if (this.enabled)
            {
                this.mMaterial.SetFloat("_SSGIMeanDepth", Properties.ssgi.meanDepth);
                this.mMaterial.SetFloat("_SSGIFadeDepth", Properties.ssgi.fadeDepth);
                this.mMaterial.SetFloat("_SSGIMixFactor", Properties.ssgi.mixWeight);
                this.mMaterial.SetFloat("_SSGIRayLength", Properties.ssgi.rayRadius);
                this.mMaterial.SetFloat("_SSGIIntensity", Properties.ssgi.intensity);
                this.mMaterial.SetFloat("_SSGISecondary", Properties.ssgi.secondary);
                this.mMaterial.SetFloat("_SSGIRoughness", Properties.ssgi.roughness);
                this.mMaterial.SetInt("_SSGIStepPower", Properties.ssgi.rayStride);

                this.RemoveCommandBuffer();
                //this.RemoveHistoryBuffer();
                //this.SetupHistoryBuffer();
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