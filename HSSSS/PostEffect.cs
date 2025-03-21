using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HSSSS
{
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

        private void LateUpdate()
        {
            Shader.SetGlobalInt("_FrameCount", count);
            count = (count + 3) % 64;
        }

        private void OnPreRender()
        {
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
            this.mCamera.AddCommandBuffer(CameraEvent.AfterLighting, this.blurBuffer);
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

            foreach (CommandBuffer buffer in this.mCamera.GetCommandBuffers(CameraEvent.AfterLighting))
            {
                if (buffer.name == BlurBufferName)
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

            this.RemoveCommandBuffers();
            this.SetupCommandBuffers();

            if (Properties.skin.lutProfile != Properties.LUTProfile.jimenez)
            {
                this.RemoveSpecularRT();
            }
        }
        #endregion
    }

    public class SSAORenderer : MonoBehaviour
    {
        private Camera mCamera;
        private Material mMaterial;
        private CommandBuffer mBuffer;
        private static Mesh mrtMesh = null;
        private const string BufferName = "HSSSS.SSAO";

        private void Awake()
        {
            this.mCamera = GetComponent<Camera>();
            this.mMaterial = new Material(AssetLoader.ssao);
            this.mMaterial.SetTexture("_BlueNoise", AssetLoader.blueNoise);
        }

        private void OnEnable()
        {
            this.SetupFullScreenMesh();
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

        private void SetupCommandBuffer()
        {
            // SSAO command buffer
            this.mBuffer = new CommandBuffer() { name = BufferName };
            
            int[] zbf = new int[5]
            {
                Shader.PropertyToID("_HierarchicalZBuffer0"),
                Shader.PropertyToID("_HierarchicalZBuffer1"),
                Shader.PropertyToID("_HierarchicalZBuffer2"),
                Shader.PropertyToID("_HierarchicalZBuffer3"),
                Shader.PropertyToID("_HierarchicalZBuffer4"),
            };

            int[] aoRT = new int[2]
            {
                Shader.PropertyToID("_SSAODiffuseBuffer"),
                Shader.PropertyToID("_SSAOSpecularBuffer")
            };
            
            int mask = Shader.PropertyToID("_SSAOMaskRenderTexture");
            
            RenderTargetIdentifier[] mrt = { aoRT[0], aoRT[1] };

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
            this.mBuffer.SetRenderTarget(mrt, BuiltinRenderTextureType.CameraTarget);
            this.mBuffer.DrawMesh(mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 5);
            
            // diffuse occlusion
            this.mBuffer.Blit(mrt[0], BuiltinRenderTextureType.CameraTarget);
            // specular occlusion
            this.mBuffer.Blit(mrt[1], BuiltinRenderTextureType.Reflections);
            
            this.mBuffer.ReleaseTemporaryRT(mask);
            
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

        public void UpdateSettings()
        {
            if (this.enabled)
            {
                this.mMaterial.SetFloat("_SSAOFadeDepth", Properties.ssao.fadeDepth);
                this.mMaterial.SetFloat("_SSAORayLength", Properties.ssao.rayRadius);
                this.mMaterial.SetFloat("_SSAOIntensity", Properties.ssao.intensity);
                this.mMaterial.SetFloat("_SSAOLightBias", Properties.ssao.lightBias);
                this.mMaterial.SetFloat("_SSAOMeanDepth", Properties.ssao.meanDepth);
                this.mMaterial.SetInt(  "_SSAORayStride", Properties.ssao.rayStride);
                this.mMaterial.SetInt(  "_SSAOSubSample", Convert.ToUInt16(Properties.ssao.subsample));

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

        public struct HistoryBuffer
        {
            public RenderTexture diffuse;
            public RenderTexture specular;
            public RenderTexture normal;
            public RenderTexture depth;
        };

        private HistoryBuffer history;

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
            this.SetupFullScreenMesh();

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

        private void SetupCommandBuffer()
        {
            RenderTargetIdentifier[] hist =
            {
                new RenderTargetIdentifier(this.history.diffuse),
                new RenderTargetIdentifier(this.history.specular),
                new RenderTargetIdentifier(this.history.normal),
                new RenderTargetIdentifier(this.history.depth)
            };

            int[] irad = new int[]
            {
                Shader.PropertyToID("_HierachicalIrradianceBuffer0"),
                Shader.PropertyToID("_HierachicalIrradianceBuffer1"),
                Shader.PropertyToID("_HierachicalIrradianceBuffer2"),
                Shader.PropertyToID("_HierachicalIrradianceBuffer3"),
                Shader.PropertyToID("_HierachicalIrradianceBuffer4")
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
            this.mBuffer.Blit(irad[0], irad[1], this.mMaterial, 1);
            this.mBuffer.Blit(irad[1], irad[2], this.mMaterial, 1);
            this.mBuffer.Blit(irad[2], irad[3], this.mMaterial, 1);
            this.mBuffer.Blit(irad[3], irad[4], this.mMaterial, 1);

            // main pass
            this.mBuffer.SetRenderTarget(flipMRT, BuiltinRenderTextureType.CameraTarget);
            this.mBuffer.DrawMesh(mrtMesh, Matrix4x4.identity, this.mMaterial, 0, Convert.ToInt32(Properties.ssgi.quality) + 2);
            // temporal filter
            this.mBuffer.SetRenderTarget(flopMRT, BuiltinRenderTextureType.CameraTarget);
            this.mBuffer.DrawMesh(mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 6);
            // main blur & post blur
            if (Properties.ssgi.denoise)
            {
                this.mBuffer.SetRenderTarget(flipMRT, BuiltinRenderTextureType.CameraTarget);
                this.mBuffer.DrawMesh(mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 7);
                this.mBuffer.SetRenderTarget(flopMRT, BuiltinRenderTextureType.CameraTarget);
                this.mBuffer.DrawMesh(mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 8);
                this.mBuffer.SetRenderTarget(flipMRT, BuiltinRenderTextureType.CameraTarget);
                this.mBuffer.DrawMesh(mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 9);
                this.mBuffer.SetRenderTarget(flopMRT, BuiltinRenderTextureType.CameraTarget);
                this.mBuffer.DrawMesh(mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 12);
            }
            // store
            this.mBuffer.SetRenderTarget(hist, BuiltinRenderTextureType.CameraTarget);
            this.mBuffer.DrawMesh(mrtMesh, Matrix4x4.identity, this.mMaterial, 0, 10);
            // collect
            this.mBuffer.Blit(flip[0], flip[1], this.mMaterial, 11);
            this.mBuffer.Blit(flip[1], BuiltinRenderTextureType.CameraTarget);

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
                if (buffer.name == this.bufferName)
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

        public void UpdateSettings()
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
                this.SetupCommandBuffer();
            }
        }
    }

    public class TAAURenderer : MonoBehaviour
    {
        private Camera mCamera;
        private Material mMaterial;
        private CommandBuffer mBuffer;
        private RenderTexture history;

        private readonly string bufferName = "HSSSS.TAAU";
        private static CameraEvent cameraEvent = CameraEvent.BeforeImageEffects;

        private void Awake()
        {
            this.mCamera = GetComponent<Camera>();
            this.mMaterial = new Material(AssetLoader.taau);
        }

        private void OnEnable()
        {
            if (this.mCamera && Properties.taau.enabled)
            {
                this.history = new RenderTexture(this.mCamera.pixelWidth, this.mCamera.pixelHeight, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                this.history.SetGlobalShaderProperty("_FrameBufferHistory");
                this.history.Create();

                RenderTexture rt = RenderTexture.active;
                RenderTexture.active = this.history;
                GL.Clear(true, true, Color.black);
                RenderTexture.active = rt;

                this.SetupCommandBuffer();
            }
        }

        private void OnDisable()
        {
            if (this.mCamera && this.mBuffer != null)
            {
                this.RemoveCommandBuffer();

                this.history.Release();
                this.history = null;
            }
        }

        private void SetupCommandBuffer()
        {
            RenderTargetIdentifier hist = new RenderTargetIdentifier(this.history);
            int buff = Shader.PropertyToID("_TAAUTemporaryBuffer");

            this.mBuffer = new CommandBuffer() { name = this.bufferName };
            this.mBuffer.GetTemporaryRT(buff, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

            this.mBuffer.Blit(BuiltinRenderTextureType.CameraTarget, buff, mMaterial, Convert.ToUInt16(Properties.taau.upscale));
            this.mBuffer.Blit(buff, BuiltinRenderTextureType.CameraTarget);
            this.mBuffer.Blit(BuiltinRenderTextureType.CameraTarget, hist);

            this.mCamera.AddCommandBuffer(cameraEvent, this.mBuffer);
        }

        private void RemoveCommandBuffer()
        {
            foreach (CommandBuffer buffer in this.mCamera.GetCommandBuffers(cameraEvent))
            {
                if (buffer.name == this.bufferName)
                {
                    this.mCamera.RemoveCommandBuffer(cameraEvent, buffer);
                }
            }

            this.mBuffer = null;
        }

        public void UpdateSettings()
        {
            if (this.enabled && this.mMaterial)
            {
                this.mMaterial.SetFloat("_TemporalMixFactor", Properties.taau.mixWeight);
                this.RemoveCommandBuffer();
                this.SetupCommandBuffer();
            }
        }
    }
    
    public class CameraProjector : MonoBehaviour
    {
        private Camera mCamera;

        public Matrix4x4 currentWorldToView;
        public Matrix4x4 currentViewToWorld;

        public Matrix4x4 currentViewToClip;
        public Matrix4x4 currentClipToView;

        public Matrix4x4 previousWorldToView;
        public Matrix4x4 previousViewToWorld;

        public Matrix4x4 previousViewToClip;
        public Matrix4x4 previousClipToView;

        private void Awake()
        {
            this.currentWorldToView = Matrix4x4.identity;
            this.currentViewToWorld = Matrix4x4.identity;

            this.currentViewToClip = Matrix4x4.identity;
            this.currentClipToView = Matrix4x4.identity;

            this.previousWorldToView = Matrix4x4.identity;
            this.previousViewToWorld = Matrix4x4.identity;

            this.previousViewToClip = Matrix4x4.identity;
            this.previousClipToView = Matrix4x4.identity;
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

            foreach (KeyValuePair<Guid, ScreenSpaceShadows> spot in HSSSS.spotDict)
            {
                spot.Value.UpdateProjectionMatrix();
            }
        }

        private void UpdateMatrices()
        {
            // previous frame
            this.previousWorldToView = this.currentWorldToView;
            this.previousViewToWorld = this.currentViewToWorld;

            this.previousViewToClip = this.currentViewToClip;
            this.previousClipToView = this.currentClipToView;

            // current frame
            this.currentWorldToView = this.mCamera.worldToCameraMatrix;
            this.currentViewToWorld = this.currentWorldToView.inverse;

            this.currentViewToClip = this.mCamera.projectionMatrix;
            this.currentClipToView = this.currentViewToClip.inverse;

            Shader.SetGlobalMatrix("_WorldToViewMatrix", this.currentWorldToView);
            Shader.SetGlobalMatrix("_ViewToWorldMatrix", this.currentViewToWorld);

            Shader.SetGlobalMatrix("_ViewToClipMatrix", this.currentViewToClip);
            Shader.SetGlobalMatrix("_ClipToViewMatrix", this.currentClipToView);

            Shader.SetGlobalMatrix("_PrevWorldToViewMatrix", this.previousWorldToView);
            Shader.SetGlobalMatrix("_PrevViewToWorldMatrix", this.previousViewToWorld);

            Shader.SetGlobalMatrix("_PrevViewToClipMatrix", this.previousViewToClip);
            Shader.SetGlobalMatrix("_PrevClipToViewMatrix", this.previousClipToView);
        }
    }
}