using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HSSSS
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class DeferredRenderer : MonoBehaviour
    {
        private Camera mCamera;

        private CommandBuffer copyBuffer;
        private CommandBuffer blurBuffer;

        private const string CopyBufferName = "HSSSS.SSSPrePass";
        private const string BlurBufferName = "HSSSS.SSSMainPass";

        private static Material prePass;
        private static Material mainPass;

        private static int count;
        private static readonly int frameCount = Shader.PropertyToID("_FrameCount");

        private struct RGBTextures
        {
            public RenderTexture R;
            public RenderTexture G;
            public RenderTexture B;
        }

        private RGBTextures specular;

        public void Awake()
        {
            prePass = new Material(AssetLoader.sssPrePass);
            mainPass = new Material(AssetLoader.sssMainPass);

            count = 1;
        }

        public void OnEnable()
        {
            this.mCamera = GetComponent<Camera>();
            this.RefreshProperties();
            this.SetupCommandBuffers();
        }

        public void OnDisable()
        {
            this.RemoveCommandBuffers();
        }

        private void OnPreRender()
        {
            Shader.SetGlobalInt(frameCount, count);
            count = (count + 1) % 128;
            
            if (Properties.skin.lutProfile == Properties.LUTProfile.jimenez)
            {
                this.SetupSpecularRT();
            }
        }

        private void OnPostRender()
        {
            if (Properties.skin.lutProfile == Properties.LUTProfile.jimenez)
            {
                this.specular.R.Release();
                this.specular.G.Release();
                this.specular.B.Release();
            }
        }

        private void SetupSpecularRT()
        {
            int width = this.mCamera.pixelWidth;
            int height = this.mCamera.pixelHeight;

            if (this.mCamera.targetTexture)
            {
                width = this.mCamera.targetTexture.width;
                height = this.mCamera.targetTexture.height;
            }

            this.specular.R = new RenderTexture(width, height, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                generateMips = false
            };

            this.specular.G = new RenderTexture(width, height, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                generateMips = false
            };

            this.specular.B = new RenderTexture(width, height, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                generateMips = false
            };

            this.specular.R.SetGlobalShaderProperty("_SpecularBufferR");
            this.specular.G.SetGlobalShaderProperty("_SpecularBufferG");
            this.specular.B.SetGlobalShaderProperty("_SpecularBufferB");

            this.specular.R.Create();
            this.specular.G.Create();
            this.specular.B.Create();

            RenderTexture rt = RenderTexture.active;
            RenderTexture.active = this.specular.R;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = this.specular.G;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = this.specular.B;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = rt;

            Graphics.ClearRandomWriteTargets();
            Graphics.SetRandomWriteTarget(1, this.specular.R);
            Graphics.SetRandomWriteTarget(2, this.specular.G);
            Graphics.SetRandomWriteTarget(3, this.specular.B);
        }

        private void RemoveSpecularRT()
        {
            if (this.specular.R)
            {
                this.specular.R.Release();
                DestroyImmediate(this.specular.R);
                this.specular.R = null;
            }

            if (this.specular.G)
            {
                this.specular.G.Release();
                DestroyImmediate(this.specular.G);
                this.specular.G = null;
            }

            if (this.specular.B)
            {
                this.specular.B.Release();
                DestroyImmediate(this.specular.B);
                this.specular.B = null;
            }
        }

        #region Properties Control
        private void RefreshSkinProperties()
        {
            Shader.SetGlobalVector("_DeferredSkinParams",
                new Vector4(
                    1.0f,
                    Properties.skin.skinLutBias,
                    Properties.skin.skinLutScale,
                    Properties.skin.sssBlurWeight
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
                    Properties.skin.sssBlurRadius,
                    Properties.skin.sssBlurDepthRange * 100.0f
                    ));
            mainPass.SetInt("_BlurAlbedoTexture", Properties.skin.sssBlurAlbedo ? 1 : 0);
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
            int copyRT = Shader.PropertyToID("_DeferredTransmissionBuffer");
            int flipRT = Shader.PropertyToID("_TemporaryFlipRenderTexture");
            int flopRT = Shader.PropertyToID("_TemporaryFlopRenderTexture");

            ///////////////////////////////////
            ///////////////////////////////////
            // transmission & ambient lights //
            ///////////////////////////////////
            ///////////////////////////////////

            this.copyBuffer = new CommandBuffer() { name = CopyBufferName };

            // get temporary render textures
            this.copyBuffer.GetTemporaryRT(copyRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
            // extract thickness map from g-buffer 3
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, copyRT, prePass, 0);
            // add command buffer
            this.mCamera.AddCommandBuffer(CameraEvent.BeforeLighting, this.copyBuffer);

            ///////////////////////////////////
            ///////////////////////////////////
            //// screen space diffuse blur ////
            ///////////////////////////////////
            ///////////////////////////////////
            
            this.blurBuffer = new CommandBuffer() { name = BlurBufferName };

            // get temporary render textures
            this.blurBuffer.GetTemporaryRT(flipRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.blurBuffer.GetTemporaryRT(flopRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

            this.blurBuffer.Blit(BuiltinRenderTextureType.CurrentActive, flipRT, mainPass, 0);

            // separable blur
            for (int i = 0; i < Properties.skin.sssBlurIter; i ++)
            {
                this.blurBuffer.Blit(flipRT, flopRT, mainPass, 1);
                this.blurBuffer.Blit(flopRT, flipRT, mainPass, 2);
            }

            // collect all lighting
            this.blurBuffer.Blit(flipRT, flopRT, mainPass, 3);

            // to camera target
            this.blurBuffer.Blit(flopRT, BuiltinRenderTextureType.CameraTarget);

            // release render textures
            this.blurBuffer.ReleaseTemporaryRT(flipRT);
            this.blurBuffer.ReleaseTemporaryRT(flopRT);
            this.blurBuffer.ReleaseTemporaryRT(copyRT);

            // add command buffer
            this.mCamera.AddCommandBuffer(CameraEvent.AfterFinalPass, this.blurBuffer);
        }

        private void SetupNormalBlurBuffer()
        {
            ///////////////////////////////////////
            ///////////////////////////////////////
            //// transmission & ambient lights ////
            ///////////////////////////////////////
            ///////////////////////////////////////
            
            int copyRT = Shader.PropertyToID("_DeferredTransmissionBuffer");

            this.copyBuffer = new CommandBuffer() { name = CopyBufferName };
            // get temporary render textures
            this.copyBuffer.GetTemporaryRT(copyRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            // extract thickness map from g-buffer 3
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, copyRT, prePass, 0);
            // add command buffer
            this.mCamera.AddCommandBuffer(CameraEvent.BeforeLighting, this.copyBuffer);

            //////////////////////////////////
            //////////////////////////////////
            //// screen space normal blur ////
            //////////////////////////////////
            //////////////////////////////////

            this.blurBuffer = new CommandBuffer() { name = BlurBufferName };

            if (Properties.skin.sssBlurIter > 0)
            {
                int flipRT = Shader.PropertyToID("_TemporaryFlipRenderTexture");
                int flopRT = Shader.PropertyToID("_TemporaryFlopRenderTexture");

                this.blurBuffer.GetTemporaryRT(flipRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
                this.blurBuffer.GetTemporaryRT(flopRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);

                this.blurBuffer.Blit(BuiltinRenderTextureType.GBuffer2, flipRT, mainPass, 4);
                this.blurBuffer.Blit(flipRT, flopRT, mainPass, 5);

                for (int i = 1; i < Properties.skin.sssBlurIter; i++)
                {
                    this.blurBuffer.Blit(flopRT, flipRT, mainPass, 4);
                    this.blurBuffer.Blit(flipRT, flopRT, mainPass, 5);
                }

                this.blurBuffer.SetGlobalTexture("_DeferredBlurredNormalBuffer", flopRT);

                this.blurBuffer.ReleaseTemporaryRT(flipRT);
                this.blurBuffer.ReleaseTemporaryRT(flopRT);
            }

            else
            {
                this.blurBuffer.SetGlobalTexture("_DeferredBlurredNormalBuffer", BuiltinRenderTextureType.GBuffer2);
            }

            this.mCamera.AddCommandBuffer(CameraEvent.BeforeLighting, this.blurBuffer);
        }

        private void SetupDummyBuffer()
        {
            int copyRT = Shader.PropertyToID("_DeferredTransmissionBuffer");

            ///////////////////////////////////
            // transmission & ambient lights //
            ///////////////////////////////////
            this.copyBuffer = new CommandBuffer() { name = CopyBufferName };
            // get temporary rendertextures
            this.copyBuffer.GetTemporaryRT(copyRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
            // extract thickness map from gbuffer 3
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, copyRT, prePass, 0);
            // release rendertexture
            this.copyBuffer.ReleaseTemporaryRT(copyRT);
            // add commandbuffer
            this.mCamera.AddCommandBuffer(CameraEvent.AfterGBuffer, this.copyBuffer);
        }

        private void SetupCommandBuffers()
        {
            // buffer 0: hsr compatible buffer
            if (HSSSS.hsrCompatible)
            {
                this.SetupDummyBuffer();
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

        private void RemoveCommandBuffers()
        {
            foreach (CommandBuffer buffer in this.mCamera.GetCommandBuffers(CameraEvent.BeforeLighting))
            {
                if (buffer.name == CopyBufferName || buffer.name == BlurBufferName)
                {
                    this.mCamera.RemoveCommandBuffer(CameraEvent.BeforeLighting, buffer);
                }
            }

            foreach (CommandBuffer buffer in this.mCamera.GetCommandBuffers(CameraEvent.AfterFinalPass))
            {
                if (buffer.name == BlurBufferName)
                {
                    this.mCamera.RemoveCommandBuffer(CameraEvent.AfterFinalPass, buffer);
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

            this.RemoveCommandBuffers();
            this.SetupCommandBuffers();

            if (Properties.skin.lutProfile != Properties.LUTProfile.jimenez)
            {
                this.RemoveSpecularRT();
            }
        }
        #endregion
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class SSAORenderer : MonoBehaviour
    {
        private Camera mCamera;
        private Material mMaterial;
        private CommandBuffer mBuffer;
        private const string BufferName = "HSSSS.SSAO";
        
        private void Awake()
        {
            this.mCamera = GetComponent<Camera>();
            this.mMaterial = new Material(AssetLoader.ssao);
            this.mMaterial.SetTexture("_BlueNoise", AssetLoader.blueNoise);
        }

        private void OnEnable()
        {
            this.UpdateSettings();

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
        
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (this.mMaterial == null || Properties.ssao.debugView == 0)
            {
                Graphics.Blit(source, destination);
                return;
            }
            
            Graphics.Blit(source, destination, this.mMaterial, Properties.ssao.debugView == 1 ? Properties.ssao.mbounce ? 8 : 7 : 9);
        }

        private void SetupCommandBuffer()
        {
            // SSAO command buffer
            this.mBuffer = new CommandBuffer() { name = BufferName };
            
            int[] zbf =
            {
                Shader.PropertyToID("_HierarchicalZBuffer0"),
                Shader.PropertyToID("_HierarchicalZBuffer1"),
                Shader.PropertyToID("_HierarchicalZBuffer2"),
                Shader.PropertyToID("_HierarchicalZBuffer3"),
                Shader.PropertyToID("_HierarchicalZBuffer4"),
            };

            int[] aoRT =
            {
                Shader.PropertyToID("_SSAODiffuseBuffer"),
                Shader.PropertyToID("_SSAOSpecularBuffer")
            };
            
            int mask = Shader.PropertyToID("_SSAOMaskRenderTexture");
            
            RenderTargetIdentifier[] aoMRT = { aoRT[0], aoRT[1] };

            this.mBuffer.GetTemporaryRT(zbf[0], -1, -1, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);

            for (int i = 1; i < 5; i ++)
            {
                this.mBuffer.GetTemporaryRT(zbf[i], this.mCamera.pixelWidth / (int)Math.Pow(2, i), this.mCamera.pixelHeight / (int)Math.Pow(2, i),
                        0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            }
            
            this.mBuffer.GetTemporaryRT(aoRT[0], -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(aoRT[1], -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(mask, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

            // z-buffer prepass
            this.mBuffer.Blit(BuiltinRenderTextureType.CurrentActive, zbf[0], this.mMaterial, 0);

            // hierarchical z buffers
            this.mBuffer.Blit(zbf[0], zbf[1]);
            this.mBuffer.Blit(zbf[1], zbf[2]);
            this.mBuffer.Blit(zbf[2], zbf[3]);
            this.mBuffer.Blit(zbf[3], zbf[4]);

            // GTAO pass
            this.mBuffer.Blit(BuiltinRenderTextureType.CurrentActive, mask, this.mMaterial, Convert.ToInt32(Properties.ssao.quality) + 1);
            
            // calculate occlusion mrt
            this.mBuffer.SetRenderTarget(aoMRT, BuiltinRenderTextureType.CameraTarget);
            this.mBuffer.DrawMesh(CameraProjector.mrtMesh, Matrix4x4.identity, this.mMaterial, 0, Properties.ssao.mbounce ? 6 : 5);
            
            // diffuse occlusion
            this.mBuffer.Blit(aoMRT[0], BuiltinRenderTextureType.CameraTarget);
            // specular occlusion
            this.mBuffer.Blit(aoMRT[1], BuiltinRenderTextureType.Reflections);
            
            this.mBuffer.ReleaseTemporaryRT(aoRT[0]);
            this.mBuffer.ReleaseTemporaryRT(aoRT[1]);
            
            this.mBuffer.ReleaseTemporaryRT(zbf[0]);
            this.mBuffer.ReleaseTemporaryRT(zbf[1]);
            this.mBuffer.ReleaseTemporaryRT(zbf[2]);
            this.mBuffer.ReleaseTemporaryRT(zbf[3]);
            this.mBuffer.ReleaseTemporaryRT(zbf[4]);

            this.mCamera.AddCommandBuffer(CameraEvent.BeforeLighting, this.mBuffer);
        }

        private void RemoveCommandBuffer()
        {
            foreach (CommandBuffer buffer in this.mCamera.GetCommandBuffers(CameraEvent.BeforeLighting))
            {
                if (buffer.name == BufferName)
                {
                    this.mCamera.RemoveCommandBuffer(CameraEvent.BeforeLighting, buffer);
                }
            }

            this.mBuffer = null;
        }

        public void UpdateSettings()
        {
            if (this.enabled)
            {
                this.mMaterial.SetFloat(Properties.ssao.fadeDepth.Key, Properties.ssao.fadeDepth.Value);
                this.mMaterial.SetFloat(Properties.ssao.rayRadius.Key, Properties.ssao.rayRadius.Value);
                this.mMaterial.SetFloat(Properties.ssao.intensity.Key, Properties.ssao.intensity.Value);
                this.mMaterial.SetFloat(Properties.ssao.lightBias.Key, Properties.ssao.lightBias.Value);
                this.mMaterial.SetFloat(Properties.ssao.meanDepth.Key, Properties.ssao.meanDepth.Value);
                this.mMaterial.SetInt(Properties.ssao.rayStride.Key, Properties.ssao.rayStride.Value);
                this.mMaterial.SetInt(Properties.ssao.subsample.Key, Convert.ToInt32(Properties.ssao.subsample.Value));

                Shader.SetGlobalInt(Properties.ssao.usessdo.Key, Properties.ssao.usessdo.Value ? 1 : 0);
                Shader.SetGlobalFloat(Properties.ssao.doApature.Key, Properties.ssao.doApature.Value);

                this.RemoveCommandBuffer();
                this.SetupCommandBuffer();
            }
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class SSGIRenderer : MonoBehaviour
    {
        private Camera mCamera;
        private Material mMaterial;
        private CommandBuffer mBuffer;

        private struct HistoryBuffer
        {
            public RenderTexture diffuse;
            public RenderTexture specular;
            public RenderTexture normal;
            public RenderTexture depth;
        };

        private HistoryBuffer history;
        
        private const string bufferName = "HSSSS.SSGI";

        private void Awake()
        {
            this.mCamera = GetComponent<Camera>();
            this.mMaterial = new Material(AssetLoader.ssgi);
            this.mMaterial.SetTexture("_BlueNoise", AssetLoader.blueNoise);
        }

        private void OnEnable()
        {
            if(this.mCamera && Properties.ssgi.enabled)
            {
                this.SetupHistoryBuffer();
                this.SetupCommandBuffer();
                this.UpdateSettings();
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

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (Properties.ssgi.debugView.Value == 0)
            {
                Graphics.Blit(source, destination);
                return;
            }
            
            Graphics.Blit(source, destination, this.mMaterial, 13);
        }

        private void SetupCommandBuffer()
        {
            int[] irad =
            {
                Shader.PropertyToID("_HierachicalIrradianceBuffer0"),
                Shader.PropertyToID("_HierachicalIrradianceBuffer1"),
                Shader.PropertyToID("_HierachicalIrradianceBuffer2"),
                Shader.PropertyToID("_HierachicalIrradianceBuffer3"),
                Shader.PropertyToID("_HierachicalIrradianceBuffer4")
            };

            int[] flip =
            {
                Shader.PropertyToID("_SSGIFlipDiffuseBuffer"),
                Shader.PropertyToID("_SSGIFlipSpecularBuffer")
            };

            int[] flop =
            {
                Shader.PropertyToID("_SSGIFlopDiffuseBuffer"),
                Shader.PropertyToID("_SSGIFlopSpecularBuffer")
            };

            RenderTargetIdentifier[] flipMRT = { flip[0], flip[1] };
            RenderTargetIdentifier[] flopMRT = { flop[0], flop[1] };
            RenderTargetIdentifier[] histMRT = { this.history.diffuse, this.history.specular, this.history.normal, this.history.depth };

            this.mBuffer = new CommandBuffer() { name = "HSSSS.SSGI" };
            
            this.mBuffer.GetTemporaryRT(irad[0], -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(irad[1], this.mCamera.pixelWidth / 2, this.mCamera.pixelHeight / 2, 0, FilterMode.Point, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(irad[2], this.mCamera.pixelWidth / 4, this.mCamera.pixelHeight / 4, 0, FilterMode.Point, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(irad[3], this.mCamera.pixelWidth / 8, this.mCamera.pixelHeight / 8, 0, FilterMode.Point, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(irad[4], this.mCamera.pixelWidth / 16, this.mCamera.pixelHeight / 16, 0, FilterMode.Point, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

            this.mBuffer.GetTemporaryRT(flip[0], -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(flip[1], -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(flop[0], -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(flop[1], -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

            // prepass
            this.mBuffer.Blit(BuiltinRenderTextureType.CurrentActive, irad[0], this.mMaterial, 0);
            this.mBuffer.Blit(irad[0], irad[1]);
            this.mBuffer.Blit(irad[1], irad[2]);
            this.mBuffer.Blit(irad[2], irad[3]);
            this.mBuffer.Blit(irad[3], irad[4]);

            // main pass
            this.mBuffer.SetRenderTarget(flipMRT, BuiltinRenderTextureType.CameraTarget);
            this.mBuffer.DrawMesh(CameraProjector.mrtMesh, Matrix4x4.identity, this.mMaterial, 0, Convert.ToInt32(Properties.ssgi.quality) + 1);
            // temporal filter
            this.mBuffer.SetRenderTarget(flopMRT, BuiltinRenderTextureType.CameraTarget);
            this.mBuffer.DrawMesh(CameraProjector.mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 5);
            // main blur & post blur
            if (Properties.ssgi.denoise)
            {
                this.mBuffer.SetRenderTarget(flipMRT, BuiltinRenderTextureType.CameraTarget);
                this.mBuffer.DrawMesh(CameraProjector.mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 6);
                this.mBuffer.SetRenderTarget(flopMRT, BuiltinRenderTextureType.CameraTarget);
                this.mBuffer.DrawMesh(CameraProjector.mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 7);
                this.mBuffer.SetRenderTarget(flipMRT, BuiltinRenderTextureType.CameraTarget);
                this.mBuffer.DrawMesh(CameraProjector.mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 8);
                this.mBuffer.SetRenderTarget(flopMRT, BuiltinRenderTextureType.CameraTarget);
                this.mBuffer.DrawMesh(CameraProjector.mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 11);
            }
            // store
            this.mBuffer.SetRenderTarget(histMRT, BuiltinRenderTextureType.CameraTarget);
            this.mBuffer.DrawMesh(CameraProjector.mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 9);
            // collect
            this.mBuffer.Blit(BuiltinRenderTextureType.CameraTarget, flip[0], this.mMaterial, 10);
            this.mBuffer.Blit(flip[0], BuiltinRenderTextureType.CameraTarget);

            for (int i = 0; i < 4; i ++)
            {
                this.mBuffer.ReleaseTemporaryRT(irad[i]);
            }

            for (int i = 0; i < 2; i ++)
            {
                this.mBuffer.ReleaseTemporaryRT(flip[i]);
                this.mBuffer.ReleaseTemporaryRT(flop[i]);
            }

            this.mCamera.AddCommandBuffer(CameraEvent.AfterLighting, this.mBuffer);
        }

        private void RemoveCommandBuffer()
        {
            foreach (CommandBuffer buffer in this.mCamera.GetCommandBuffers(CameraEvent.AfterLighting))
            {
                if (buffer.name == bufferName)
                {
                    this.mCamera.RemoveCommandBuffer(CameraEvent.AfterLighting, buffer);
                }
            }

            this.mBuffer = null;
        }

        private void SetupHistoryBuffer()
        {
            this.history.diffuse = new RenderTexture(this.mCamera.pixelWidth, this.mCamera.pixelHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.history.diffuse.SetGlobalShaderProperty("_SSGITemporalDiffuseBuffer");
            this.history.diffuse.Create();

            this.history.specular = new RenderTexture(this.mCamera.pixelWidth, this.mCamera.pixelHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.history.specular.SetGlobalShaderProperty("_SSGITemporalSpecularBuffer");
            this.history.specular.Create();

            this.history.normal = new RenderTexture(this.mCamera.pixelWidth, this.mCamera.pixelHeight, 0, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
            this.history.normal.SetGlobalShaderProperty("_CameraNormalHistory");
            this.history.normal.Create();

            this.history.depth = new RenderTexture(this.mCamera.pixelWidth, this.mCamera.pixelHeight, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            this.history.depth.SetGlobalShaderProperty("_CameraDepthHistory");
            this.history.depth.Create();

            RenderTexture rt = RenderTexture.active;
            RenderTexture.active = this.history.diffuse;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = this.history.specular;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = rt;
        }

        private void RemoveHistoryBuffer()
        {
            if (this.history.diffuse != null)
            {
                this.history.diffuse.Release();
                this.history.diffuse = null;
            }

            if (this.history.specular != null)
            {
                this.history.specular.Release();
                this.history.specular = null;
            }

            if (this.history.normal != null)
            {
                this.history.normal.Release();
                this.history.normal = null;
            }

            if (this.history.depth != null)
            {
                this.history.depth.Release();
                this.history.depth = null;
            }
        }

        public void UpdateSettings()
        {
            if (this.enabled)
            {
                this.mMaterial.SetFloat(Properties.ssgi.meanDepth.Key, Properties.ssgi.meanDepth.Value);
                this.mMaterial.SetFloat(Properties.ssgi.fadeDepth.Key, Properties.ssgi.fadeDepth.Value);
                this.mMaterial.SetFloat(Properties.ssgi.mixWeight.Key, Properties.ssgi.mixWeight.Value);
                this.mMaterial.SetFloat(Properties.ssgi.rayRadius.Key, Properties.ssgi.rayRadius.Value);
                this.mMaterial.SetFloat(Properties.ssgi.intensity.Key, Properties.ssgi.intensity.Value);
                this.mMaterial.SetFloat(Properties.ssgi.secondary.Key, Properties.ssgi.secondary.Value);
                this.mMaterial.SetFloat(Properties.ssgi.roughness.Key, Properties.ssgi.roughness.Value);
                this.mMaterial.SetFloat(Properties.ssgi.occlusion.Key, Properties.ssgi.occlusion.Value);
                
                this.mMaterial.SetInt(Properties.ssgi.rayStride.Key, Properties.ssgi.rayStride.Value);
                this.mMaterial.SetInt(Properties.ssgi.debugView.Key, Properties.ssgi.debugView.Value);

                this.RemoveCommandBuffer();
                this.SetupCommandBuffer();
            }
        }
    }
    
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class TAAURenderer : MonoBehaviour
    {
        private Camera mCamera;
        private Material mMaterial;
        private RenderTexture history;
        
        private void OnEnable()
        {
            this.mCamera = GetComponent<Camera>();
            this.mMaterial = new Material(AssetLoader.taau);
            
            this.history = new RenderTexture(this.mCamera.pixelWidth, this.mCamera.pixelHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.history.SetGlobalShaderProperty("_FrameBufferHistory");
            this.history.wrapMode = TextureWrapMode.Clamp;
            this.history.filterMode = FilterMode.Point;
            this.history.Create();
            
            /*
            RenderTexture rt = RenderTexture.active;
            RenderTexture.active = this.history;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = rt;
            */
        }

        private void OnDisable()
        {
            this.mCamera = null;
            this.mMaterial = null;
            this.history.Release();
            this.history = null;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            RenderTexture temp = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);

            Graphics.Blit(source, temp, this.mMaterial, Properties.taau.upscale ? 1 : 0);
            Graphics.Blit(temp, this.history);
            Graphics.Blit(temp, destination);
            
            RenderTexture.ReleaseTemporary(temp);
        }

        public void UpdateSettings()
        {
            if (this.enabled && this.mMaterial)
            {
                this.mMaterial.SetFloat(Properties.taau.mixWeight.Key, Properties.taau.mixWeight.Value);
            }
        }
    }
    
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CameraProjector : MonoBehaviour
    {
        private Camera mCamera;

        private KeyValue<int, Matrix4x4> currentWorldToView;
        private KeyValue<int, Matrix4x4> currentViewToWorld;

        private KeyValue<int, Matrix4x4> currentViewToClip;
        private KeyValue<int, Matrix4x4> currentClipToView;

        private KeyValue<int, Matrix4x4> previousWorldToView;
        private KeyValue<int, Matrix4x4> previousViewToWorld;

        private KeyValue<int, Matrix4x4> previousViewToClip;
        private KeyValue<int, Matrix4x4> previousClipToView;

        public static readonly Mesh mrtMesh = new Mesh()
        {
            vertices = new []
            {
                new Vector3(-1.0f, -1.0f, 0.0f),
                new Vector3(-1.0f,  3.0f, 0.0f),
                new Vector3( 3.0f, -1.0f, 0.0f)
            },

            triangles = new [] { 0, 1, 2 }
        };

        private void Awake()
        {
            this.currentWorldToView = new KeyValue<int, Matrix4x4>(Shader.PropertyToID("_WorldToViewMatrix"), Matrix4x4.identity);
            this.currentViewToWorld = new KeyValue<int, Matrix4x4>(Shader.PropertyToID("_ViewToWorldMatrix"), Matrix4x4.identity);

            this.currentViewToClip = new KeyValue<int, Matrix4x4>(Shader.PropertyToID("_ViewToClipMatrix"), Matrix4x4.identity);
            this.currentClipToView = new KeyValue<int, Matrix4x4>(Shader.PropertyToID("_ClipToViewMatrix"), Matrix4x4.identity);

            this.previousWorldToView = new KeyValue<int, Matrix4x4>(Shader.PropertyToID("_PrevWorldToViewMatrix"), Matrix4x4.identity);
            this.previousViewToWorld = new KeyValue<int, Matrix4x4>(Shader.PropertyToID("_PrevViewToWorldMatrix"), Matrix4x4.identity);

            this.previousViewToClip = new KeyValue<int, Matrix4x4>(Shader.PropertyToID("_PrevViewToClipMatrix"), Matrix4x4.identity);
            this.previousClipToView = new KeyValue<int, Matrix4x4>(Shader.PropertyToID("_PrevClipToViewMatrix"), Matrix4x4.identity);
        }

        private void OnEnable()
        {
            this.mCamera = GetComponent<Camera>();
        }

        private void OnDisable()
        {
            this.mCamera = null;
        }

        private void OnPreCull()
        {
            if (this.mCamera)
            {
                this.UpdateMatrices();
            }
        }

        private void UpdateMatrices()
        {
            // previous frame
            this.previousWorldToView.Value = this.currentWorldToView.Value;
            this.previousViewToWorld.Value = this.currentViewToWorld.Value;

            this.previousViewToClip.Value = this.currentViewToClip.Value;
            this.previousClipToView.Value = this.currentClipToView.Value;

            // current frame
            this.currentWorldToView.Value = this.mCamera.worldToCameraMatrix;
            this.currentViewToWorld.Value = this.mCamera.worldToCameraMatrix.inverse;

            this.currentViewToClip.Value = this.mCamera.projectionMatrix;
            this.currentClipToView.Value = this.mCamera.projectionMatrix.inverse;

            Shader.SetGlobalMatrix(this.currentWorldToView.Key, this.currentWorldToView.Value);
            Shader.SetGlobalMatrix(this.currentViewToWorld.Key, this.currentViewToWorld.Value);

            Shader.SetGlobalMatrix(this.currentViewToClip.Key, this.currentViewToClip.Value);
            Shader.SetGlobalMatrix(this.currentClipToView.Key, this.currentClipToView.Value);

            Shader.SetGlobalMatrix(this.previousWorldToView.Key, this.previousWorldToView.Value);
            Shader.SetGlobalMatrix(this.previousViewToWorld.Key, this.previousViewToWorld.Value);

            Shader.SetGlobalMatrix(this.previousViewToClip.Key, this.previousViewToClip.Value);
            Shader.SetGlobalMatrix(this.previousClipToView.Key, this.previousClipToView.Value);
        }
    }
    
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class AgXToneMapper : MonoBehaviour
    {
        private Material mMaterial;

        private void Awake()
        {
            this.mMaterial = new Material(AssetLoader.agx);
        }
        
        [ImageEffectTransformsToLDR]
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if ((Properties.ssao.enabled && Properties.ssao.debugView != 0) || (Properties.ssgi.enabled && Properties.ssgi.debugView.Value == 4))
            {
                Graphics.Blit(source, destination);
                return;
            }
            
            Graphics.Blit(source, destination, this.mMaterial, 0);
        }
        
        public void UpdateSettings()
        {
            if (this.enabled && this.mMaterial)
            {
                this.mMaterial.SetFloat(Properties.agx.gamma.Key, Properties.agx.gamma.Value);
                this.mMaterial.SetFloat(Properties.agx.saturation.Key, Properties.agx.saturation.Value);
                this.mMaterial.SetVector(Properties.agx.offset.Key, Properties.agx.offset.Value);
                this.mMaterial.SetVector(Properties.agx.slope.Key, Properties.agx.slope.Value);
                this.mMaterial.SetVector(Properties.agx.power.Key, Properties.agx.power.Value);
            }
        }
    }

    /*
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class TangentRenderer : MonoBehaviour
    {
        private Camera mCamera;
        private Camera tCamera;
        private Shader mShader;
        private RenderTexture mTexture;
        private GameObject mObject;

        private void OnEnable()
        {
            this.mCamera = GetComponent<Camera>();
            this.mShader = AssetLoader.drawTangent;

            this.mObject = new GameObject("HSSSS.TangentCamera");
            this.tCamera = this.mObject.AddComponent<Camera>();
            
            this.tCamera.name = "HSSSS.TangentCamera";
            this.tCamera.enabled = false;
            this.tCamera.backgroundColor = Color.black;
            this.tCamera.clearFlags = CameraClearFlags.SolidColor;
            this.tCamera.depthTextureMode = DepthTextureMode.Depth;
            this.tCamera.renderingPath = RenderingPath.Forward;
            
            int layer1 = LayerMask.NameToLayer("Chara");
            int layer2 = LayerMask.NameToLayer("Map");
            int layer3 = LayerMask.NameToLayer("MapNoShadow");
            
            int layer1Mask = 1 << layer1;
            int layer2Mask = 1 << layer2;
            int layer3Mask = 1 << layer3;
            
            this.tCamera.cullingMask = layer1Mask | layer2Mask | layer3Mask;

        }

        private void OnDisable()
        {
            DestroyImmediate(this.tCamera);
            DestroyImmediate(this.mObject);
            this.mCamera = null;
        }

        private void Update()
        {
            this.tCamera.transform.position = this.mCamera.transform.position;
            this.tCamera.transform.rotation = this.mCamera.transform.rotation;
            this.tCamera.nearClipPlane = this.mCamera.nearClipPlane;
            this.tCamera.farClipPlane = this.mCamera.farClipPlane;
            this.tCamera.fieldOfView = this.mCamera.fieldOfView;
        }

        private void OnPreCull()
        {
            this.mTexture = RenderTexture.GetTemporary(this.mCamera.pixelWidth, this.mCamera.pixelHeight, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            this.mTexture.SetGlobalShaderProperty("_DeferredTangentBuffer");
            this.tCamera.targetTexture = this.mTexture;
            this.tCamera.RenderWithShader(this.mShader, "");
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Graphics.Blit(source, destination);
            RenderTexture.ReleaseTemporary(this.mTexture);
        }
    }
    */
}