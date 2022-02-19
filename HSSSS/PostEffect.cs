// Alloy Physical Shader Framework
// Copyright 2013-2016 RUST LLC.
// http://www.alloy.rustltd.com/

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HSSSS
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Alloy/Deferred Renderer Plus")]
    public class AlloyDeferredRendererPlus : MonoBehaviour
    {
        [Serializable]
        public struct SkinSettingsData
        {
            public bool Enabled;
            public float Weight;
            public Texture2D Lut;

            public float Bias;
            public float Scale;
            public float BumpBlur;

            public Vector3 ColorBleedAoWeights;
            public Vector3 TransmissionAbsorption;

            public float BlurWidth;
            public float BlurDepthRange;
        }

        [Serializable]
        public struct TransmissionSettingsData
        {
            public bool Enabled;

            public float Weight;
            public float ShadowWeight;

            public float BumpDistortion;

            public float Falloff;
        }

        public SkinSettingsData SkinSettings = new SkinSettingsData()
        {
            Enabled = true,
            Weight = 1.0f,
            Bias = 0.5f,
            Scale = 1.0f,
            BumpBlur = 1.0f,
            BlurWidth = 0.1f,
            BlurDepthRange = 1.0f,
            ColorBleedAoWeights = new Vector3(0.40f, 0.15f, 0.13f),
            TransmissionAbsorption = new Vector3(-8.0f, -40.0f, -64.0f)
        };

        public TransmissionSettingsData TransmissionSettings = new TransmissionSettingsData
        {
            Enabled = true,
            Weight = 1.0f,
            Falloff = 1.0f,
            BumpDistortion = 0.2f,
            ShadowWeight = 0.8f
        };

        // Arbitrary range multiplier.
        private const float c_blurDepthRangeMultiplier = 25.0f;
        private const string c_copyTransmissionBufferName = "AlloyCopyTransmission";
        private const string c_normalBufferName = "AlloyRenderBlurredNormals";
        private const string c_releaseDeferredBuffer = "AlloyReleaseDeferredPlusBuffers";


        // LUT
        public Texture2D SkinLut;

        // Shaders
        public Shader DeferredTransmissionBlit;
        public Shader DeferredBlurredNormals;

        // Private
        private Material m_deferredTransmissionBlitMaterial;
        private Material m_deferredBlurredNormalsMaterial;

        private Camera m_camera;
        private bool m_isTransmissionEnabled;
        private bool m_isScatteringEnabled;

        private CommandBuffer m_copyTransmission;
        private CommandBuffer m_renderBlurredNormals;
        private CommandBuffer m_releaseDeferredPlus;

#if UNITY_EDITOR
    private Material m_sceneViewBlurredNormalsMaterial;
    private Camera m_sceneCamera;
    private CommandBuffer m_sceneViewBlurredNormals;
#endif

        // TODO: Debug views of the buffers? (Blurred normals, edge difference averages)
        public void Refresh()
        {
            bool scatteringEnabled = SkinSettings.Enabled;
            bool transmissionEnabled = TransmissionSettings.Enabled || scatteringEnabled;

            if (m_isTransmissionEnabled != transmissionEnabled
                || m_isScatteringEnabled != scatteringEnabled)
            {
                m_isScatteringEnabled = scatteringEnabled;
                m_isTransmissionEnabled = transmissionEnabled;

                DestroyCommandBuffers();
                InitializeBuffers();
            }

            RefreshProperties();
        }

        private void Awake()
        {
            m_camera = GetComponent<Camera>();
            this.DeferredTransmissionBlit = HSSSS.deferredTransmissionBlit;
            this.DeferredBlurredNormals = HSSSS.deferredBlurredNormals;
            this.SkinLut = HSSSS.skinLUT;
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
            RemoveCommandBuffersFromAllCameras();
        }

        private void OnDestroy()
        {
            DestroyCommandBuffers();
        }

        //per camera properties
        private void RefreshProperties()
        {
            if (m_isTransmissionEnabled || m_isScatteringEnabled)
            {
                float transmissionWeight = m_isTransmissionEnabled ? Mathf.GammaToLinearSpace(TransmissionSettings.Weight) : 0.0f;

                Shader.SetGlobalVector("_DeferredTransmissionParams",
                    new Vector4(transmissionWeight, TransmissionSettings.Falloff, TransmissionSettings.BumpDistortion, TransmissionSettings.ShadowWeight));

                if (m_isScatteringEnabled)
                {
                    RefreshBlurredNormalProperties(m_camera, m_deferredBlurredNormalsMaterial);

                    Shader.SetGlobalTexture("_DeferredSkinLut", SkinSettings.Lut);
                    Shader.SetGlobalVector("_DeferredSkinParams",
                        new Vector4(SkinSettings.Weight, SkinSettings.Bias, SkinSettings.Scale, SkinSettings.BumpBlur));
                    Shader.SetGlobalVector("_DeferredSkinColorBleedAoWeights", SkinSettings.ColorBleedAoWeights);
                    Shader.SetGlobalVector("_DeferredSkinTransmissionAbsorption", SkinSettings.TransmissionAbsorption);
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
            float normalBlurWidth = SkinSettings.BlurWidth * distanceToProjectionWindow;
            float normalBlurDepthRange = SkinSettings.BlurDepthRange * distanceToProjectionWindow * c_blurDepthRangeMultiplier;

            blurMaterial.SetVector("_DeferredBlurredNormalsParams", new Vector2(normalBlurWidth, normalBlurDepthRange));
        }

        private void InitializeBuffers()
        {
            m_isScatteringEnabled = SkinSettings.Enabled;
            m_isTransmissionEnabled = TransmissionSettings.Enabled || m_isScatteringEnabled;

            if (SkinSettings.Lut == null)
            {
                SkinSettings.Lut = SkinLut;

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
            }

            if ((m_isTransmissionEnabled || m_isScatteringEnabled)
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
                if (m_isScatteringEnabled)
                {
                    GenerateNormalBlurMaterialAndCommandBuffer(blurredNormalBuffer, blurredNormalsBufferIdTemp,
                        out m_deferredBlurredNormalsMaterial, out m_renderBlurredNormals);

#if UNITY_EDITOR
                GenerateNormalBlurMaterialAndCommandBuffer(blurredNormalBuffer, blurredNormalsBufferIdTemp,
                    out m_sceneViewBlurredNormalsMaterial, out m_sceneViewBlurredNormals);
#endif
                }

                // Cleanup resources.
                m_releaseDeferredPlus = new CommandBuffer();
                m_releaseDeferredPlus.name = c_releaseDeferredBuffer;
                m_releaseDeferredPlus.ReleaseTemporaryRT(opacityBufferId);

                if (m_isScatteringEnabled)
                {
                    m_releaseDeferredPlus.ReleaseTemporaryRT(blurredNormalsBufferIdTemp);
                }

#if UNITY_EDITOR
            SceneView.onSceneGUIDelegate += OnSceneGUIDelegate;
#endif
            }

            AddCommandBuffersToCamera(m_camera, m_renderBlurredNormals);

#if UNITY_EDITOR
        EditorUtility.SetDirty(m_camera);
#endif
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

            blurCommandBuffer.Blit(blurredNormalBuffer, blurredNormalsBufferIdTemp, blurMaterial, 0);
            blurCommandBuffer.Blit(blurredNormalsBufferIdTemp, blurredNormalBuffer, blurMaterial, 1);
        }

        private void DestroyCommandBuffers()
        {
            RemoveCommandBuffersFromAllCameras();

            m_copyTransmission = null;
            m_renderBlurredNormals = null;
            m_releaseDeferredPlus = null;

#if UNITY_EDITOR
        m_sceneViewBlurredNormals = null;
        SceneView.onSceneGUIDelegate -= OnSceneGUIDelegate;
#endif

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

#if UNITY_EDITOR
        if (m_sceneViewBlurredNormalsMaterial != null) {
            DestroyImmediate(m_sceneViewBlurredNormalsMaterial);
            m_sceneViewBlurredNormalsMaterial = null;
        }
#endif
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

        private void RemoveCommandBuffersFromCamera(Camera camera, CommandBuffer normalBuffer)
        {
            if (m_copyTransmission != null)
            {
                camera.RemoveCommandBuffer(CameraEvent.AfterGBuffer, m_copyTransmission);
            }

            if (normalBuffer != null)
            {
                camera.RemoveCommandBuffer(CameraEvent.BeforeLighting, normalBuffer);
            }

            if (m_releaseDeferredPlus != null)
            {
                camera.RemoveCommandBuffer(CameraEvent.AfterLighting, m_releaseDeferredPlus);
            }
        }

        private void RemoveCommandBuffersFromAllCameras()
        {
#if UNITY_EDITOR
        if (m_sceneCamera != null) {
            RemoveCommandBuffersFromCamera(m_sceneCamera, m_sceneViewBlurredNormals);
        }
#endif

            RemoveCommandBuffersFromCamera(m_camera, m_renderBlurredNormals);
        }


#if UNITY_EDITOR
    private void OnSceneGUIDelegate(SceneView sceneView) {
        m_sceneCamera = sceneView.camera;
        AddCommandBuffersToCamera(m_sceneCamera, m_sceneViewBlurredNormals);
        RefreshBlurredNormalProperties(m_sceneCamera, m_sceneViewBlurredNormalsMaterial);
    }
#endif
    }
}