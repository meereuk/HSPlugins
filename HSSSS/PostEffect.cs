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

        private static Texture2D skinJitter;
        private static Texture2D defaultSkinLUT;
        private static Texture2D faceWorksSkinLUT;
        private static Texture2D faceWorksShadowLUT;
        private static Texture2D deepScatterLUT;

        //private static SkinSettings skinSettings;

        public static bool hsrCompatible = false;

        public void Awake()
        {
            copyMaterial = new Material(HSSSS.transmissionBlitShader);
            normalBlurMaterial = new Material(HSSSS.normalBlurShader);
            diffuseBlurMaterial = new Material(HSSSS.diffuseBlurShader);

            skinJitter = HSSSS.skinJitter;
            defaultSkinLUT = HSSSS.pennerSkinLUT;
            faceWorksSkinLUT = HSSSS.faceWorksSkinLUT;
            faceWorksShadowLUT = HSSSS.faceWorksShadowLUT;
            deepScatterLUT = HSSSS.deepScatterLUT;

            //skinSettings = HSSSS.skinSettings;
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
                    HSSSS.skinSettings.sssWeight,
                    HSSSS.skinSettings.skinLutBias,
                    HSSSS.skinSettings.skinLutScale,
                    HSSSS.skinSettings.normalBlurWeight
                    ));
            Shader.SetGlobalVector("_DeferredShadowParams",
                new Vector2(
                    HSSSS.skinSettings.shadowLutBias,
                    HSSSS.skinSettings.shadowLutScale
                    ));
            Shader.SetGlobalVector("_DeferredSkinColorBleedAoWeights", HSSSS.skinSettings.colorBleedWeights);
            Shader.SetGlobalVector("_DeferredSkinTransmissionAbsorption", HSSSS.skinSettings.transAbsorption);
        }

        private void RefreshBlurProperties()
        {
            if (HSSSS.skinSettings.lutProfile == LUTProfile.jimenez)
            {
                diffuseBlurMaterial.SetTexture("_SkinJitter", skinJitter);
                diffuseBlurMaterial.SetVector("_DeferredBlurredNormalsParams",
                    new Vector2(
                        HSSSS.skinSettings.normalBlurRadius,
                        HSSSS.skinSettings.normalBlurDepthRange * 100.0f
                        ));
            }

            else
            {
                normalBlurMaterial.SetTexture("_SkinJitter", skinJitter);
                normalBlurMaterial.SetVector("_DeferredBlurredNormalsParams",
                    new Vector2(
                        HSSSS.skinSettings.normalBlurRadius,
                        HSSSS.skinSettings.normalBlurDepthRange * 25.0f
                        ));
            }
        }

        private void RefreshLookupProperties()
        {
            Shader.DisableKeyword("_FACEWORKS_TYPE1");
            Shader.DisableKeyword("_FACEWORKS_TYPE2");
            Shader.DisableKeyword("_SCREENSPACE_SSS");

            // lookup texture replacement
            switch (HSSSS.skinSettings.lutProfile)
            {
                case LUTProfile.penner:
                    
                    Shader.SetGlobalTexture("_DeferredSkinLut", defaultSkinLUT);
                    break;

                case LUTProfile.nvidia1:
                    Shader.EnableKeyword("_FACEWORKS_TYPE1");
                    Shader.SetGlobalTexture("_DeferredSkinLut", faceWorksSkinLUT);
                    break;

                case LUTProfile.nvidia2:
                    Shader.EnableKeyword("_FACEWORKS_TYPE2");
                    Shader.SetGlobalTexture("_DeferredSkinLut", faceWorksSkinLUT);
                    Shader.SetGlobalTexture("_DeferredShadowLut", faceWorksShadowLUT);
                    break;

                case LUTProfile.jimenez:
                    Shader.EnableKeyword("_SCREENSPACE_SSS");
                    break;
            }
        }

        private void RefreshTransmissionProperties()
        {
            Shader.DisableKeyword("_BAKED_THICKNESS");

            if (HSSSS.skinSettings.bakedThickness)
            {
                Shader.EnableKeyword("_BAKED_THICKNESS");
            }

            else
            {
                Shader.SetGlobalTexture("_DeferredTransmissionLut", deepScatterLUT);
                Shader.SetGlobalFloat("_DeferredThicknessBias", HSSSS.skinSettings.thicknessBias * 0.01f);
            }

            Shader.SetGlobalVector("_DeferredTransmissionParams",
                new Vector4(
                    HSSSS.skinSettings.transWeight,
                    HSSSS.skinSettings.transFalloff,
                    HSSSS.skinSettings.transDistortion,
                    HSSSS.skinSettings.transShadowWeight
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
        private void InitDiffuseBlurBuffer()
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
            this.mCamera.AddCommandBuffer(CameraEvent.AfterGBuffer, this.copyBuffer);

            ///////////////////////////////////
            ///////////////////////////////////
            //// screen space diffuse blur ////
            ///////////////////////////////////
            ///////////////////////////////////
            
            this.diffuseBlurBuffer = new CommandBuffer() { name = this.blurBufferName };

            // get temporary rendertextures
            this.diffuseBlurBuffer.GetTemporaryRT(flipRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);
            this.diffuseBlurBuffer.GetTemporaryRT(flopRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);

            // get rid of ambient specular
            this.diffuseBlurBuffer.Blit(BuiltinRenderTextureType.CameraTarget, flipRT, diffuseBlurMaterial, 0);

            // separable blur
            for (int i = 0; i < HSSSS.skinSettings.normalBlurIter; i ++)
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

        private void InitNormalBlurBuffer()
        {
            int copyRT = Shader.PropertyToID("_DeferredTransmissionBuffer");
            int buffRT = Shader.PropertyToID("_DeferredBlurredNormalBuffer");
            int flipRT = Shader.PropertyToID("_TemporaryFlipRenderTexture");
            int flopRT = Shader.PropertyToID("_TemporaryFlopRenderTexture");

            ///////////////////////////////////
            // transmission & ambient lights //
            ///////////////////////////////////

            this.copyBuffer = new CommandBuffer() { name = this.copyBufferName };

            // get temporary rendertextures
            this.copyBuffer.GetTemporaryRT(copyRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.RHalf);

            // extract thickness map from gbuffer 3
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, copyRT, copyMaterial, 0);

            // release rendertexture
            this.copyBuffer.ReleaseTemporaryRT(copyRT);

            // add commandbuffer
            this.mCamera.AddCommandBuffer(CameraEvent.AfterGBuffer, this.copyBuffer);

            //////////////////////////////
            // screen space normal blur //
            //////////////////////////////

            this.normalBlurBuffer = new CommandBuffer() { name = this.blurBufferName };

            this.normalBlurBuffer.GetTemporaryRT(flipRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010);
            this.normalBlurBuffer.GetTemporaryRT(flopRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010);
            this.normalBlurBuffer.GetTemporaryRT(buffRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010);

            if (HSSSS.skinSettings.normalBlurIter > 0)
            {
                this.normalBlurBuffer.Blit(BuiltinRenderTextureType.GBuffer2, flipRT, normalBlurMaterial, 0);
                this.normalBlurBuffer.Blit(flipRT, flopRT, normalBlurMaterial, 1);

                for (int i = 1; i < HSSSS.skinSettings.normalBlurIter; i++)
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
            if (hsrCompatible)
            {
                this.InitDummyBuffer();
            }

            else
            {
                // buffer 1: screen space scattering
                if (HSSSS.skinSettings.lutProfile == LUTProfile.jimenez)
                {
                    this.InitDiffuseBlurBuffer();
                }

                // buffer 2: pre-integrated scattering
                else
                {
                    this.InitNormalBlurBuffer();
                }
            }
        }

        private void DestroyBuffers()
        {
            if (this.copyBuffer != null)
            {
                this.mCamera.RemoveCommandBuffer(CameraEvent.AfterGBuffer, this.copyBuffer);
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

        /*
        public void ImportSettings()
        {
            skinSettings = HSSSS.skinSettings;
        }
        */
        #endregion
    }

    public class BackFaceDepthSampler : MonoBehaviour
    {
        public Camera mainCamera;

        private Camera depthCamera;
        private Shader depthShader;
        private RenderTexture depthBuffer;

        public void OnEnable()
        {
            this.SetUpDepthCamera();
            this.depthShader = HSSSS.backFaceDepthShader;
        }

        public void OnDisable()
        {
            DestroyObject(this.depthCamera);
            DestroyImmediate(this.depthBuffer);

            this.depthCamera = null;
            this.depthBuffer = null;
        }

        public void Update()
        {
            if (this.depthCamera && this.mainCamera)
            {
                this.UpdateDepthCamera();
            }
        }

        public void LateUpdate()
        {
            if (this.depthCamera && this.mainCamera)
            {
                this.CaptureDepth();
            }
        }

        private void SetUpDepthCamera()
        {
            if (this.depthCamera == null)
            {
                this.depthCamera = this.gameObject.AddComponent<Camera>();
            }

            this.depthCamera.name = "BackFaceDepthCamera";
            this.depthCamera.enabled = false;
            this.depthCamera.backgroundColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
            this.depthCamera.clearFlags = CameraClearFlags.SolidColor;
            this.depthCamera.renderingPath = RenderingPath.VertexLit;

            this.depthCamera.cullingMask = 0;
            this.depthCamera.cullingMask |= 1 << LayerMask.NameToLayer("Chara");
            this.depthCamera.cullingMask |= 1 << LayerMask.NameToLayer("Map");
        }

        private void UpdateDepthCamera()
        {
            this.depthCamera.transform.position = this.mainCamera.transform.position;
            this.depthCamera.transform.rotation = this.mainCamera.transform.rotation;
            this.depthCamera.fieldOfView = this.mainCamera.fieldOfView;
            this.depthCamera.nearClipPlane = this.mainCamera.nearClipPlane;
            this.depthCamera.farClipPlane = this.mainCamera.farClipPlane;
        }

        private void CaptureDepth()
        {
            this.depthBuffer = RenderTexture.GetTemporary(
                Screen.currentResolution.width, Screen.currentResolution.height,
                24, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear
                );

            this.depthCamera.targetTexture = this.depthBuffer;
            this.depthCamera.RenderWithShader(this.depthShader, "");
            Shader.SetGlobalTexture("_BackFaceDepthBuffer", this.depthBuffer);
            RenderTexture.ReleaseTemporary(this.depthBuffer);
        }
    }

    public class ScreenSpaceGlobalIllumination : MonoBehaviour
    {
        private Camera mCamera;
        private Material mMaterial;

        private CommandBuffer aoBuffer;
        private CommandBuffer giBuffer;

        private RenderTexture aoTexture;
        private RenderTexture giTexture;

        private int rtSizeW;
        private int rtSizeH;

        private void Awake()
        {
            this.rtSizeW = Screen.width / 2;
            this.rtSizeH = Screen.height / 2;
        }

        private void OnEnable()
        {
            this.mMaterial = new Material(HSSSS.ssgiShader);
            this.mCamera = GetComponent<Camera>();
            this.UpdateSSAOSettings(true);

            if (this.mCamera)
            {
                this.SetupAOCommandBuffer();
            }
        }

        private void OnDisable()
        {
            if (this.mCamera != null)
            {
                if (this.aoBuffer != null)
                {
                    this.RemoveAOCommandBuffer();
                }
            }
        }

        private void OnPreRender()
        {
            this.UpdateMatrices();
            this.mMaterial.SetTexture("_SSGITemporalAOBuffer", this.aoTexture);
        }

        private void SetupAOCommandBuffer()
        {
            this.aoTexture = new RenderTexture(rtSizeW, rtSizeH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            this.aoTexture.Create();

            ClearTemporalTexture();

            int flipRT = Shader.PropertyToID("_SSGITemporalFlipBuffer");
            int flopRT = Shader.PropertyToID("_SSGITemporalFlopBuffer");

            RenderTargetIdentifier tempAO = new RenderTargetIdentifier(this.aoTexture);

            this.aoBuffer = new CommandBuffer() { name = "HSSSS.SSAO" };
            this.aoBuffer.GetTemporaryRT(flipRT, rtSizeW, rtSizeH, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            this.aoBuffer.GetTemporaryRT(flopRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

            // ssao calculation
            this.aoBuffer.Blit(BuiltinRenderTextureType.CameraTarget, flipRT, this.mMaterial, (int)HSSSS.ssaoSettings.quality);
            // temporal filtering
            this.aoBuffer.Blit(flipRT, tempAO);
            // apply occlusion
            this.aoBuffer.Blit(tempAO, flopRT, this.mMaterial, 4);
            this.aoBuffer.Blit(flopRT, BuiltinRenderTextureType.GBuffer0);
            this.aoBuffer.Blit(tempAO, flopRT, this.mMaterial, 5);
            this.aoBuffer.Blit(flopRT, BuiltinRenderTextureType.CameraTarget);

            this.aoBuffer.ReleaseTemporaryRT(flipRT);
            //this.aoBuffer.ReleaseTemporaryRT(flopRT);

            this.mCamera.AddCommandBuffer(CameraEvent.BeforeReflections, this.aoBuffer);
        }

        private void RemoveAOCommandBuffer()
        {
            this.mCamera.RemoveCommandBuffer(CameraEvent.BeforeReflections, this.aoBuffer);
            this.aoBuffer = null;
            this.aoTexture.Release();
        }

        private void ClearTemporalTexture()
        {
            RenderTexture rt = RenderTexture.active;
            RenderTexture.active = this.aoTexture;
            GL.Clear(true, true, Color.white);
            RenderTexture.active = rt;
        }

        private void UpdateMatrices()
        {
            this.mMaterial.SetMatrix("_WorldToViewMatrix", HSSSS.CameraProjector.WorldToView);
            this.mMaterial.SetMatrix("_ViewToWorldMatrix", HSSSS.CameraProjector.ViewToWorld);

            this.mMaterial.SetMatrix("_ViewToClipMatrix", HSSSS.CameraProjector.ViewToClip);
            this.mMaterial.SetMatrix("_ClipToViewMatrix", HSSSS.CameraProjector.ClipToView);

            this.mMaterial.SetMatrix("_PrevWorldToViewMatrix", HSSSS.CameraProjector.TemporalWorldToView);
            this.mMaterial.SetMatrix("_PrevViewToWorldMatrix", HSSSS.CameraProjector.TemporalViewToWorld);

            this.mMaterial.SetMatrix("_PrevViewToClipMatrix", HSSSS.CameraProjector.TemporalViewToClip);
            this.mMaterial.SetMatrix("_PrevClipToViewMatrix", HSSSS.CameraProjector.TemporalClipToView);
        }

        public void UpdateSSAOSettings(bool soft = true)
        {
            if (this.enabled)
            {
                this.mMaterial.SetFloat("_SSAODepthBias", HSSSS.ssaoSettings.depthBias);
                this.mMaterial.SetFloat("_SSAODepthFade", HSSSS.ssaoSettings.depthFade);
                this.mMaterial.SetFloat("_SSAOMixFactor", HSSSS.ssaoSettings.mixFactor);
                this.mMaterial.SetFloat("_SSAORayLength", HSSSS.ssaoSettings.rayLength);
                this.mMaterial.SetFloat("_SSAOIndirectP", HSSSS.ssaoSettings.power);
            }

            if (!soft)
            {
                this.RemoveAOCommandBuffer();
                this.SetupAOCommandBuffer();
            }
        }
    }

    public class CameraHelper : MonoBehaviour
    {
        private Camera mCamera;

        public Matrix4x4 WorldToView;
        public Matrix4x4 ViewToWorld;

        public Matrix4x4 ViewToClip;
        public Matrix4x4 ClipToView;

        public Matrix4x4 TemporalWorldToView;
        public Matrix4x4 TemporalViewToWorld;

        public Matrix4x4 TemporalViewToClip;
        public Matrix4x4 TemporalClipToView;

        private void Awake()
        {
            this.WorldToView = Matrix4x4.identity;
            this.ViewToWorld = Matrix4x4.identity;

            this.ViewToClip = Matrix4x4.identity;
            this.ClipToView = Matrix4x4.identity;

            this.TemporalWorldToView = Matrix4x4.identity;
            this.TemporalViewToWorld = Matrix4x4.identity;

            this.TemporalViewToClip = Matrix4x4.identity;
            this.TemporalClipToView = Matrix4x4.identity;
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
            this.TemporalWorldToView = this.WorldToView;
            this.TemporalViewToWorld = this.ViewToWorld;

            this.TemporalViewToClip = this.ViewToClip;
            this.TemporalClipToView = this.ClipToView;

            // current frame
            this.WorldToView = this.mCamera.worldToCameraMatrix;
            this.ViewToWorld = this.WorldToView.inverse;

            this.ViewToClip = this.mCamera.projectionMatrix;
            this.ClipToView = this.ViewToClip.inverse;
        }
    }
}