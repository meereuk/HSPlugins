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

        //public static bool HSSSS.hsrCompatible = false;

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
            this.copyBuffer.GetTemporaryRT(copyRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.R8);
            this.copyBuffer.GetTemporaryRT(ambiRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);

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
            this.diffuseBlurBuffer.GetTemporaryRT(flipRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);
            this.diffuseBlurBuffer.GetTemporaryRT(flopRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);

            this.diffuseBlurBuffer.Blit(BuiltinRenderTextureType.CurrentActive, flipRT);

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
            this.copyBuffer.GetTemporaryRT(copyRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.RHalf);
            this.copyBuffer.GetTemporaryRT(specRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);

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

            this.normalBlurBuffer.GetTemporaryRT(flipRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010);
            this.normalBlurBuffer.GetTemporaryRT(flopRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010);
            this.normalBlurBuffer.GetTemporaryRT(buffRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010);

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
            this.copyBuffer.GetTemporaryRT(copyRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.R8);

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
        public void Refresh()
        {
            this.RefreshProperties();
        }

        public void ForceRefresh()
        {
            this.RefreshProperties();
            this.DestroyBuffers();
            this.InitializeBuffers();
        }

        #endregion
    }

    /*
    public class BackFaceRenderer : MonoBehaviour
    {
        private Camera mainCamera;
        private Camera depthCamera;
        private RenderTexture depthBuffer;
        private GameObject depthCameraObj;

        public void OnEnable()
        {
            this.SetUpDepthCamera();
        }

        public void OnDisable()
        {
            DestroyObject(this.depthCamera);
            DestroyImmediate(this.depthBuffer);

            this.mainCamera = null;
            this.depthCamera = null;
            this.depthBuffer = null;
        }

        public void OnPreCull()
        {
            if (this.depthCamera && this.mainCamera)
            {
                this.UpdateCameraParams();
                this.CaptureDepthBuffer();
            }
        }

        private void SetUpDepthCamera()
        {
            this.mainCamera = GetComponent<Camera>();

            if (this.depthCamera == null)
            {
                this.depthCameraObj = new GameObject("HSSSS.BackFaceDepthCamera");
                this.depthCamera = this.depthCameraObj.AddComponent<Camera>();
            }

            this.depthCamera.name = "HSSSS.BackFaceDepthCamera";
            this.depthCamera.enabled = false;
            this.depthCamera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            this.depthCamera.clearFlags = CameraClearFlags.SolidColor;
            this.depthCamera.renderingPath = RenderingPath.VertexLit;

            this.depthCamera.cullingMask = 0;
            this.depthCamera.cullingMask |= 1 << LayerMask.NameToLayer("Chara");
            this.depthCamera.cullingMask |= 1 << LayerMask.NameToLayer("Map");
        }

        private void UpdateCameraParams()
        {
            this.depthCamera.transform.position = this.mainCamera.transform.position;
            this.depthCamera.transform.rotation = this.mainCamera.transform.rotation;
            this.depthCamera.fieldOfView = this.mainCamera.fieldOfView;
            this.depthCamera.nearClipPlane = this.mainCamera.nearClipPlane;
            this.depthCamera.farClipPlane = this.mainCamera.farClipPlane;
        }

        private void CaptureDepthBuffer()
        {
            this.depthBuffer = RenderTexture.GetTemporary(
                Screen.width, Screen.height, 24,
                RenderTextureFormat.Depth, RenderTextureReadWrite.Linear
                );
            this.depthCamera.targetTexture = this.depthBuffer;
            this.depthCamera.RenderWithShader(HSSSS.backFaceDepthShader, "");
            Shader.SetGlobalTexture("_BackFaceDepthBuffer", this.depthBuffer);
            RenderTexture.ReleaseTemporary(this.depthBuffer);
        }
    }
    */

    public class SSAORenderer : MonoBehaviour
    {
        private Camera mCamera;
        private Material mMaterial;
        private CommandBuffer mBuffer;
        private RenderTexture mTexture;

        private void Awake()
        {
            this.mCamera = GetComponent<Camera>();
            this.mMaterial = new Material(HSSSS.ssaoShader);
        }

        private void OnEnable()
        {
            this.UpdateSSAOSettings(true);

            if (this.mCamera && Properties.ssao.enabled)
            {
                this.SetupCommandBuffer();
            }

            Shader.SetGlobalInt("_SSGIDirectOcclusion", 1);
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

            Shader.SetGlobalInt("_SSGIDirectOcclusion", 0);
        }

        private void OnPreRender()
        {
            this.UpdateMatrices();
            this.mMaterial.SetTexture("_SSGITemporalAOBuffer", this.mTexture);
        }

        private void SetupCommandBuffer()
        {
            this.mTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.mTexture.filterMode = FilterMode.Point;
            this.mTexture.Create();
            ClearTemporalTexture(this.mTexture);

            RenderTargetIdentifier temp = new RenderTargetIdentifier(this.mTexture);

            // SSAO command buffer
            this.mBuffer = new CommandBuffer() { name = "HSSSS.SSAO" };

            int flip = Shader.PropertyToID("_SSAOFlipRenderTexture");
            int flop = Shader.PropertyToID("_SSAOFlopRenderTexture");

            this.mBuffer.GetTemporaryRT(flip, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(flop, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);


            // full resolution ssao
            if (Properties.ssao.usegtao)
            {
                this.mBuffer.Blit(temp, flip, this.mMaterial, (int)Properties.ssao.quality + 5);
            }
            
            else
            {
                this.mBuffer.Blit(temp, flip, this.mMaterial, (int)Properties.ssao.quality + 1);
            }

            // spatio noise filtering
            if (Properties.ssao.denoise)
            {
                this.mBuffer.Blit(flip, flop, this.mMaterial, 9);
                this.mBuffer.Blit(flop, flip, this.mMaterial, 10);
            }

            this.mBuffer.Blit(flip, flop, this.mMaterial, 11);
            this.mBuffer.Blit(flop, temp);

            // diffuse occlusion
            this.mBuffer.Blit(BuiltinRenderTextureType.CameraTarget, flip, this.mMaterial, 13);
            this.mBuffer.Blit(flip, BuiltinRenderTextureType.CameraTarget);
            // specular occlusion
            this.mBuffer.Blit(BuiltinRenderTextureType.Reflections, flip, this.mMaterial, 14);
            this.mBuffer.Blit(flip, BuiltinRenderTextureType.Reflections);

            this.mBuffer.SetGlobalTexture("_SSGITemporalAOBuffer", temp);

            this.mBuffer.ReleaseTemporaryRT(flip);
            this.mBuffer.ReleaseTemporaryRT(flop);

            this.mCamera.AddCommandBuffer(CameraEvent.AfterReflections, this.mBuffer);
        }

        private void RemoveCommandBuffer()
        {
            this.mCamera.RemoveCommandBuffer(CameraEvent.AfterReflections, this.mBuffer);
            this.mTexture.Release();
            this.mTexture = null;
            this.mBuffer = null;
        }

        private void ClearTemporalTexture(RenderTexture tex)
        {
            RenderTexture rt = RenderTexture.active;
            RenderTexture.active = tex;
            GL.Clear(true, true, Color.white);
            RenderTexture.active = rt;
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
                this.mMaterial.SetFloat("_SSAOMixFactor", Properties.ssao.mixWeight);
                this.mMaterial.SetFloat("_SSAORayLength", Properties.ssao.rayRadius);
                this.mMaterial.SetFloat("_SSAOIntensity", Properties.ssao.intensity);
                this.mMaterial.SetFloat("_SSAOLightBias", Properties.ssao.lightBias);
                this.mMaterial.SetFloat("_SSAOMeanDepth", Properties.ssao.meanDepth);
                this.mMaterial.SetInt(  "_SSAOStepPower", Properties.ssao.rayStride);
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

        private void Awake()
        {
            this.mCamera = GetComponent<Camera>();
            this.mMaterial = new Material(HSSSS.ssgiShader);
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
                    this.RemoveCommandBuffer();
                    this.SetupHistoryBuffer();
                }
            }
        }

        private void OnPreRender()
        {
            this.UpdateMatrices();
        }

        private void SetupCommandBuffer()
        {
            RenderTargetIdentifier temp = new RenderTargetIdentifier(this.giHistory);

            this.mBuffer = new CommandBuffer() { name = "HSSSS.SSGI" };

            int ilum = Shader.PropertyToID("_SSGIIlumRenderTexture");
            int flip = Shader.PropertyToID("_SSGIFlipRenderTexture");
            int flop = Shader.PropertyToID("_SSGIFlopRenderTexture");

            switch (Properties.ssgi.resolution)
            {
                case Properties.ResolveResolution.quarter:
                    this.mBuffer.GetTemporaryRT(ilum, Screen.width / 4, Screen.height / 4, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    break;

                case Properties.ResolveResolution.half:
                    this.mBuffer.GetTemporaryRT(ilum, Screen.width / 2, Screen.height / 2, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    break;

                case Properties.ResolveResolution.full:
                    this.mBuffer.GetTemporaryRT(ilum, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    break;
            }

            this.mBuffer.GetTemporaryRT(flip, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.mBuffer.GetTemporaryRT(flop, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

            this.mBuffer.SetGlobalTexture("_SSGITemporalGIBuffer", this.giHistory);

            this.mBuffer.Blit(temp, ilum, this.mMaterial, 0);
            this.mBuffer.Blit(ilum, flip, this.mMaterial, (int)Properties.ssgi.quality + 1);

            if (Properties.ssgi.denoise)
            {
                this.mBuffer.Blit(flip, flop, this.mMaterial, 9);
                this.mBuffer.Blit(flop, flip, this.mMaterial, 10);
                this.mBuffer.Blit(flip, flop, this.mMaterial, 11);
                this.mBuffer.Blit(flop, flip, this.mMaterial, 12);
            }

            this.mBuffer.Blit(flip, flop, this.mMaterial, 13);
            this.mBuffer.Blit(flop, flip, this.mMaterial, 14);

            this.mBuffer.Blit(flop, temp);
            this.mBuffer.Blit(flip, BuiltinRenderTextureType.CameraTarget);

            this.mBuffer.ReleaseTemporaryRT(ilum);
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
            this.giHistory.filterMode = FilterMode.Bilinear;
            this.giHistory.Create();

            RenderTexture rt = RenderTexture.active;
            RenderTexture.active = this.giHistory;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = rt;
        }

        private void RemoveHistoryBuffer()
        {
            this.giHistory.Release();
            this.giHistory = null;
        }

        private void ClearTemporalTexture(RenderTexture tex)
        {
            RenderTexture rt = RenderTexture.active;
            RenderTexture.active = tex;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = rt;
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
                this.mMaterial.SetInt("_SSGIStepPower", Properties.ssgi.rayStride);
            }

            if (!soft)
            {
                this.RemoveCommandBuffer();
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