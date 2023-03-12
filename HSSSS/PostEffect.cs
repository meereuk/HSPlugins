using UnityEngine;
using UnityEngine.Rendering;

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

        private static SkinSettings skinSettings;

        public static bool hsrCompatible = false;

        public void Awake()
        {
            copyMaterial = new Material(HSSSS.transmissionBlitShader);
            normalBlurMaterial = new Material(HSSSS.normalBlurShader);
            diffuseBlurMaterial = new Material(HSSSS.diffuseBlurShader);

            skinJitter = HSSSS.skinJitter;
            defaultSkinLUT = HSSSS.defaultSkinLUT;
            faceWorksSkinLUT = HSSSS.faceWorksSkinLUT;
            faceWorksShadowLUT = HSSSS.faceWorksShadowLUT;
            deepScatterLUT = HSSSS.deepScatterLUT;

            skinSettings = HSSSS.skinSettings;
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
            Shader.SetGlobalVector("_DeferredSkinParams", new Vector4(skinSettings.sssWeight, skinSettings.skinLutBias, skinSettings.skinLutScale, skinSettings.normalBlurWeight));
            Shader.SetGlobalVector("_DeferredShadowParams", new Vector2(skinSettings.shadowLutBias, skinSettings.shadowLutScale));
            Shader.SetGlobalVector("_DeferredSkinColorBleedAoWeights", skinSettings.colorBleedWeights);
            Shader.SetGlobalVector("_DeferredSkinTransmissionAbsorption", skinSettings.transAbsorption);
        }

        private void RefreshBlurProperties()
        {
            if (skinSettings.lutProfile == LUTProfile.jimenez)
            {
                diffuseBlurMaterial.SetTexture("_SkinJitter", skinJitter);
                diffuseBlurMaterial.SetVector("_DeferredBlurredNormalsParams", new Vector2(skinSettings.normalBlurRadius, skinSettings.normalBlurDepthRange * 100.0f));
            }

            else
            {
                normalBlurMaterial.SetTexture("_SkinJitter", skinJitter);
                normalBlurMaterial.SetVector("_DeferredBlurredNormalsParams", new Vector2(skinSettings.normalBlurRadius, skinSettings.normalBlurDepthRange * 25.0f));
            }
        }

        private void RefreshLookupProperties()
        {
            Shader.DisableKeyword("_FACEWORKS_TYPE1");
            Shader.DisableKeyword("_FACEWORKS_TYPE2");
            Shader.DisableKeyword("_SCREENSPACE_SSS");

            // lookup texture replacement
            switch (skinSettings.lutProfile)
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

            if (skinSettings.bakedThickness)
            {
                Shader.EnableKeyword("_BAKED_THICKNESS");
            }

            else
            {
                Shader.SetGlobalTexture("_DeferredTransmissionLut", deepScatterLUT);
                Shader.SetGlobalFloat("_DeferredThicknessBias", skinSettings.thicknessBias * 0.01f);
            }

            Shader.SetGlobalVector("_DeferredTransmissionParams", new Vector4(skinSettings.transWeight, skinSettings.transFalloff, skinSettings.transDistortion, skinSettings.transShadowWeight));
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

            //// transmission & ambient lights
            this.copyBuffer = new CommandBuffer() { name = this.copyBufferName };
            // get temporary rendertextures
            this.copyBuffer.GetTemporaryRT(copyRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB32);
            this.copyBuffer.GetTemporaryRT(ambiRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);
            // extract thickness map from gbuffer 3
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, copyRT, copyMaterial, 0);
            // extract ambient diffuse
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, ambiRT, copyMaterial, 1);
            // remove gbuffer 3's alpha channel
            this.copyBuffer.Blit(ambiRT, BuiltinRenderTextureType.CameraTarget);
            // release rendertexture
            this.copyBuffer.ReleaseTemporaryRT(copyRT);
            // add commandbuffer
            this.mCamera.AddCommandBuffer(CameraEvent.AfterGBuffer, this.copyBuffer);

            // screen space diffusion blur
            this.diffuseBlurBuffer = new CommandBuffer() { name = this.blurBufferName };
            // get temporary rendertextures
            this.diffuseBlurBuffer.GetTemporaryRT(flipRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);
            this.diffuseBlurBuffer.GetTemporaryRT(flopRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);
            // get rid of ambient specular
            this.diffuseBlurBuffer.Blit(BuiltinRenderTextureType.CameraTarget, flipRT, diffuseBlurMaterial, 0);
            // separable blur
            for (int i = 0; i < skinSettings.normalBlurIter; i ++)
            {
                this.diffuseBlurBuffer.Blit(flipRT, flopRT, diffuseBlurMaterial, 1);
                this.diffuseBlurBuffer.Blit(flopRT, flipRT, diffuseBlurMaterial, 2);
            }
            // combine all light data
            this.diffuseBlurBuffer.Blit(flipRT, flopRT, diffuseBlurMaterial, 3);
            // to camera target
            this.diffuseBlurBuffer.Blit(flopRT, BuiltinRenderTextureType.CameraTarget);
            // release rendertextures
            this.diffuseBlurBuffer.ReleaseTemporaryRT(ambiRT);
            this.diffuseBlurBuffer.ReleaseTemporaryRT(flipRT);
            this.diffuseBlurBuffer.ReleaseTemporaryRT(flopRT);
            // add commandbuffer
            this.mCamera.AddCommandBuffer(CameraEvent.AfterLighting, this.diffuseBlurBuffer);
        }

        private void InitNormalBlurBuffer()
        {
            int flipRT = Shader.PropertyToID("_TemporaryFlipRenderTexture");
            int flopRT = Shader.PropertyToID("_TemporaryFlopRenderTexture");

            int ambiRT = Shader.PropertyToID("_AmbientDiffuseBuffer");
            int copyRT = Shader.PropertyToID("_DeferredTransmissionBuffer");
            int buffRT = Shader.PropertyToID("_DeferredBlurredNormalBuffer");

            //// transmission & ambient lights
            this.copyBuffer = new CommandBuffer() { name = this.copyBufferName };
            // get temporary rendertextures
            this.copyBuffer.GetTemporaryRT(copyRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB32);
            this.copyBuffer.GetTemporaryRT(ambiRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGBHalf);
            // extract thickness map from gbuffer 3
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, copyRT, copyMaterial, 0);
            // extract ambient diffuse
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, ambiRT, copyMaterial, 1);
            // remove gbuffer 3's alpha channel
            this.copyBuffer.Blit(ambiRT, BuiltinRenderTextureType.CameraTarget);
            // release rendertexture
            this.copyBuffer.ReleaseTemporaryRT(copyRT);
            // add commandbuffer
            this.mCamera.AddCommandBuffer(CameraEvent.AfterGBuffer, this.copyBuffer);

            this.normalBlurBuffer = new CommandBuffer() { name = this.blurBufferName };
            this.normalBlurBuffer.GetTemporaryRT(flipRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010);
            this.normalBlurBuffer.GetTemporaryRT(flopRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010);
            this.normalBlurBuffer.GetTemporaryRT(buffRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010);

            if (skinSettings.normalBlurIter > 0)
            {
                this.normalBlurBuffer.Blit(BuiltinRenderTextureType.GBuffer2, flipRT, normalBlurMaterial, 0);
                this.normalBlurBuffer.Blit(flipRT, flopRT, normalBlurMaterial, 1);

                for (int i = 1; i < skinSettings.normalBlurIter; i++)
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

            //// transmission & ambient lights
            this.copyBuffer = new CommandBuffer() { name = this.copyBufferName };
            // get temporary rendertextures
            this.copyBuffer.GetTemporaryRT(copyRT, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB32);
            // extract thickness map from gbuffer 3
            this.copyBuffer.Blit(BuiltinRenderTextureType.CameraTarget, copyRT, copyMaterial, 0);
            // release rendertexture
            this.copyBuffer.ReleaseTemporaryRT(copyRT);
            // add commandbuffer
            this.mCamera.AddCommandBuffer(CameraEvent.AfterGBuffer, this.copyBuffer);
        }

        private void InitializeBuffers()
        {
            if (hsrCompatible)
            {
                // buffer 0: hsr compatible buffer
                this.InitDummyBuffer();
            }

            else
            {
                // buffer 1: screen space scattering
                if (skinSettings.lutProfile == LUTProfile.jimenez)
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

        public void ImportSettings()
        {
            skinSettings = HSSSS.skinSettings;
        }
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
                //this.CaptureDepth();
            }
        }

        public void LateUpdate()
        {
            if (this.depthCamera && this.mainCamera)
            {
                //this.UpdateDepthCamera();
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
}