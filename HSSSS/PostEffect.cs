using UnityEngine;
using UnityEngine.Rendering;

namespace HSSSS
{
    public class DeferredRenderer : MonoBehaviour
    {
        private const float c_blurDepthRangeMultiplier = 25.0f;
        private const string c_copyTransmissionBufferName = "AlloyCopyTransmission";
        private const string c_normalBufferName = "AlloyRenderBlurredNormals";
        private const string c_releaseDeferredBuffer = "AlloyReleaseDeferredPlusBuffers";

        public Shader DeferredTransmissionBlit;
        public Shader DeferredBlurredNormals;

        private Material m_deferredTransmissionBlitMaterial;
        private Material m_deferredBlurredNormalsMaterial;

        private CommandBuffer m_copyTransmission;
        private CommandBuffer m_renderBlurredNormals;
        private CommandBuffer m_releaseDeferredPlus;

        private Texture2D skinLut;
        private Texture2D shadowLut;
        private Texture2D skinJitter;

        private Camera m_camera;

        private SkinSettings skinSettings;

        private bool transmissionEnabled;
        private bool scatteringEnabled;

        public void Refresh()
        {
            bool scatteringEnabled = skinSettings.sssEnabled;
            bool transmissionEnabled = skinSettings.transEnabled || skinSettings.sssEnabled;

            if (this.transmissionEnabled != transmissionEnabled
                || this.scatteringEnabled != scatteringEnabled)
            {
                this.scatteringEnabled = scatteringEnabled;
                this.transmissionEnabled = transmissionEnabled;

                DestroyCommandBuffers();
                InitializeBuffers();
            }

            RefreshProperties();
        }

        public void ForceRefresh()
        {
            DestroyCommandBuffers();
            InitializeBuffers();
        }

        private void Awake()
        {
            this.m_camera = GetComponent<Camera>();
            this.DeferredBlurredNormals = HSSSS.deferredBlurredNormals;
            this.DeferredTransmissionBlit = HSSSS.deferredTransmissionBlit;
            this.skinSettings = new SkinSettings();
        }

        private void Reset()
        {
            InitializeBuffers();
        }

        private void OnEnable()
        {
            InitializeBuffers();
        }

        private void OnDisable()
        {
            RemoveCommandBuffers();
        }

        private void OnDestroy()
        {
            DestroyCommandBuffers();
        }

        //per camera properties
        private void RefreshProperties()
        {
            GetLookupTextures();

            if (transmissionEnabled || scatteringEnabled)
            {
                float transmissionWeight = transmissionEnabled ? Mathf.GammaToLinearSpace(skinSettings.transWeight) : 0.0f;

                Shader.SetGlobalVector("_DeferredTransmissionParams",
                    new Vector4(transmissionWeight, skinSettings.transFalloff, skinSettings.transDistortion, skinSettings.transShadowWeight));

                if (scatteringEnabled)
                {
                    RefreshBlurredNormalProperties(m_camera, m_deferredBlurredNormalsMaterial);

                    Shader.SetGlobalTexture("_DeferredSkinLut", skinLut);
                    Shader.SetGlobalTexture("_DeferredShadowLut", shadowLut);

                    Shader.SetGlobalVector("_DeferredSkinParams",
                        new Vector4(skinSettings.sssWeight, skinSettings.skinLutBias, skinSettings.skinLutScale, skinSettings.normalBlurWeight));
                    Shader.SetGlobalVector("_DeferredSkinColorBleedAoWeights", skinSettings.colorBleedWeights);
                    Shader.SetGlobalVector("_DeferredSkinTransmissionAbsorption", skinSettings.transAbsorption);

                    Shader.SetGlobalVector("_DeferredShadowParams", new Vector2(skinSettings.shadowLutBias, skinSettings.shadowLutScale));
                }
            }
        }

        private void RefreshBlurredNormalProperties(Camera camera, Material blurMaterial)
        {
            if (blurMaterial == null)
            {
                return;
            }

            float distanceToProjectionWindow = 1.0f / Mathf.Tan(0.5f * Mathf.Deg2Rad * camera.fieldOfView);

            float blurWidth = skinSettings.normalBlurRadius * distanceToProjectionWindow;
            float blurDepthRange = skinSettings.normalBlurDepthRange * distanceToProjectionWindow * c_blurDepthRangeMultiplier;

            blurMaterial.SetTexture("_SkinJitter", skinJitter);
            blurMaterial.SetVector("_DeferredBlurredNormalsParams", new Vector2(blurWidth, blurDepthRange));
        }

        private void InitializeBuffers()
        {
            scatteringEnabled = skinSettings.sssEnabled;
            transmissionEnabled = skinSettings.transEnabled || scatteringEnabled;

            if ((transmissionEnabled || scatteringEnabled)
                && m_camera != null
                && DeferredTransmissionBlit != null
                && m_copyTransmission == null
                && m_releaseDeferredPlus == null)
            {
                int opacityBufferId = Shader.PropertyToID("_DeferredTransmissionBuffer");
                int blurredNormalsBufferIdTemp = Shader.PropertyToID("_DeferredBlurredNormalBufferTemp");
                int blurredNormalBuffer = Shader.PropertyToID("_DeferredBlurredNormalBuffer");

                m_deferredTransmissionBlitMaterial = new Material(DeferredTransmissionBlit);
                m_deferredTransmissionBlitMaterial.hideFlags = HideFlags.HideAndDontSave;

                // Copy Gbuffer emission buffer so we can get at the alpha channel for transmission.
                m_copyTransmission = new CommandBuffer();
                m_copyTransmission.name = c_copyTransmissionBufferName;
                m_copyTransmission.GetTemporaryRT(opacityBufferId, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB32);
                m_copyTransmission.Blit(BuiltinRenderTextureType.CameraTarget, opacityBufferId, m_deferredTransmissionBlitMaterial);

                // Blurred normals for skin
                if (scatteringEnabled)
                {
                    GenerateNormalBlurMaterialAndCommandBuffer(blurredNormalBuffer, blurredNormalsBufferIdTemp,
                        out m_deferredBlurredNormalsMaterial, out m_renderBlurredNormals);
                }

                // Cleanup resources.
                m_releaseDeferredPlus = new CommandBuffer();
                m_releaseDeferredPlus.name = c_releaseDeferredBuffer;
                m_releaseDeferredPlus.ReleaseTemporaryRT(opacityBufferId);

                if (scatteringEnabled)
                {
                    m_releaseDeferredPlus.ReleaseTemporaryRT(blurredNormalsBufferIdTemp);
                }
            }

            AddCommandBuffersToCamera(m_camera, m_renderBlurredNormals);
        }

        private void GenerateNormalBlurMaterialAndCommandBuffer(int blurredNormalBuffer, int blurredNormalsBufferIdTemp,
            out Material blurMaterial, out CommandBuffer blurCommandBuffer)
        {
            blurMaterial = new Material(DeferredBlurredNormals);
            blurMaterial.hideFlags = HideFlags.HideAndDontSave;

            blurCommandBuffer = new CommandBuffer();
            blurCommandBuffer.name = c_normalBufferName;
            blurCommandBuffer.GetTemporaryRT(blurredNormalsBufferIdTemp, -1, -1, 0, FilterMode.Point,
                RenderTextureFormat.ARGB2101010);
            blurCommandBuffer.GetTemporaryRT(blurredNormalBuffer, -1, -1, 0, FilterMode.Point, RenderTextureFormat.ARGB2101010);

            blurCommandBuffer.Blit(BuiltinRenderTextureType.GBuffer2, blurredNormalsBufferIdTemp, blurMaterial, 0);
            blurCommandBuffer.Blit(blurredNormalsBufferIdTemp, blurredNormalBuffer, blurMaterial, 1);

            for(int i = 1; i < skinSettings.normalBlurIter; i++)
            {
                blurCommandBuffer.Blit(blurredNormalBuffer, blurredNormalsBufferIdTemp, blurMaterial, 0);
                blurCommandBuffer.Blit(blurredNormalsBufferIdTemp, blurredNormalBuffer, blurMaterial, 1);
            }
        }

        private void AddCommandBuffersToCamera(Camera setCamera, CommandBuffer normalBuffer)
        {
            //Need depth texture for depth aware upsample
            setCamera.depthTextureMode |= DepthTextureMode.Depth;

            if (m_copyTransmission != null && !HasCommandBuffer(setCamera, CameraEvent.AfterGBuffer, c_copyTransmissionBufferName))
            {
                setCamera.AddCommandBuffer(CameraEvent.AfterGBuffer, m_copyTransmission);
            }

            if (normalBuffer != null && !HasCommandBuffer(setCamera, CameraEvent.BeforeLighting, c_normalBufferName))
            {
                setCamera.AddCommandBuffer(CameraEvent.BeforeLighting, normalBuffer);
            }

            if (m_releaseDeferredPlus != null && !HasCommandBuffer(setCamera, CameraEvent.AfterLighting, c_releaseDeferredBuffer))
            {
                setCamera.AddCommandBuffer(CameraEvent.AfterLighting, m_releaseDeferredPlus);
            }

            RefreshProperties();
        }

        private static bool HasCommandBuffer(Camera setCamera, CameraEvent evt, string name)
        {
            foreach (var buf in setCamera.GetCommandBuffers(evt))
            {
                if (buf.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveCommandBuffers()
        {
            if (m_copyTransmission != null)
            {
                m_camera.RemoveCommandBuffer(CameraEvent.AfterGBuffer, m_copyTransmission);
            }

            if (m_renderBlurredNormals != null)
            {
                m_camera.RemoveCommandBuffer(CameraEvent.BeforeLighting, m_renderBlurredNormals);
            }

            if (m_releaseDeferredPlus != null)
            {
                m_camera.RemoveCommandBuffer(CameraEvent.AfterLighting, m_releaseDeferredPlus);
            }
        }

        private void DestroyCommandBuffers()
        {
            RemoveCommandBuffers();

            m_copyTransmission = null;
            m_renderBlurredNormals = null;
            m_releaseDeferredPlus = null;

            if (m_deferredTransmissionBlitMaterial != null)
            {
                DestroyImmediate(m_deferredTransmissionBlitMaterial);
                m_deferredTransmissionBlitMaterial = null;
            }

            if (m_deferredBlurredNormalsMaterial != null)
            {
                DestroyImmediate(m_deferredBlurredNormalsMaterial);
                m_deferredBlurredNormalsMaterial = null;
            }
        }

        public void GetLookupTextures()
        {
            this.skinJitter = HSSSS.skinJitter;

            switch (this.skinSettings.lutProfile)
            {
                case LUTProfile.penner:
                    this.skinLut = HSSSS.defaultSkinLUT;
                    Shader.DisableKeyword("_FACEWORKS_TYPE1");
                    Shader.DisableKeyword("_FACEWORKS_TYPE2");
                    break;

                case LUTProfile.nvidia1:
                    this.skinLut = HSSSS.faceWorksSkinLUT;
                    Shader.DisableKeyword("_FACEWORKS_TYPE2");
                    Shader.EnableKeyword("_FACEWORKS_TYPE1");
                    break;

                case LUTProfile.nvidia2:
                    this.skinLut = HSSSS.faceWorksSkinLUT;
                    this.shadowLut = HSSSS.faceWorksShadowLUT;
                    Shader.DisableKeyword("_FACEWORKS_TYPE1");
                    Shader.EnableKeyword("_FACEWORKS_TYPE2");
                    break;
            }
        }

        public void ImportSettings()
        {
            this.skinSettings = HSSSS.skinSettings;
        }

        public void ExportSettings()
        {
            HSSSS.skinSettings = this.skinSettings;
        }
    }
}