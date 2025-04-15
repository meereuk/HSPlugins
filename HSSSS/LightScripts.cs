using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HSSSS
{
    public class CookieUpdater : MonoBehaviour
    {
        private Light mLight;
        private GameObject mObject;
        private MeshRenderer mRenderer;
        private Material mMaterial;
        private Texture mCookie;

        private void Awake()
        {
            this.mMaterial = new Material(Shader.Find("Unlit/Texture"))
            {
                name = "SpotLightCookie",
                mainTexture = AssetLoader.spotCookie
            };
        }

        private void OnEnable()
        {
            this.mLight = GetComponent<Light>();
            this.mObject = this.mLight.gameObject;

            if (this.mLight == null || this.mObject == null)
            {
                return;
            }

            this.mRenderer = this.mObject.AddComponent<MeshRenderer>();
            this.mRenderer.name = "SpotLightCookie";
            this.mRenderer.material = this.mMaterial;
        }

        private void OnDisable()
        {
            Destroy(this.mRenderer);
        }

        private void OnDestroy()
        {
            Destroy(this.mMaterial);
        }

        private void Update()
        {
            this.mCookie = this.mRenderer.material.mainTexture;

            if (ReferenceEquals(this.mCookie, null))
            {
                return;
            }
            
            this.mCookie.wrapMode = TextureWrapMode.Clamp;
            this.mLight.cookie = this.mCookie;
        }
    }

    public class ScreenSpaceShadows : MonoBehaviour
    {
        private Light mLight;
        private Material mMaterial;
        // blit buffer (for directional)
        private CommandBuffer bBuffer;
        // shadow calculation buffer (for all)
        private CommandBuffer mBuffer;
        // shader properties
        private static readonly int blueNoise = Shader.PropertyToID("_BlueNoise");
        private static readonly int slopeBias = Shader.PropertyToID("_SlopeBiasScale");
        private static readonly int depthParams = Shader.PropertyToID("_ShadowDepthParams");
        private static readonly int projMatrix = Shader.PropertyToID("_ShadowProjMatrix");
        private static readonly int shadowDistance = Shader.PropertyToID("_ShadowDistance");

        private void OnEnable()
        {
            this.mMaterial = new Material(AssetLoader.softShadows);
            this.mMaterial.SetTexture(blueNoise, AssetLoader.blueNoise);

            this.mMaterial.SetVector(Properties.pcss.dirLightPenumbra.Key, Properties.pcss.dirLightPenumbra.Value);
            this.mMaterial.SetVector(Properties.pcss.spotLightPenumbra.Key, Properties.pcss.spotLightPenumbra.Value);
            this.mMaterial.SetVector(Properties.pcss.pointLightPenumbra.Key, Properties.pcss.pointLightPenumbra.Value);

            this.mMaterial.SetFloat(Properties.sscs.rayRadius.Key, Properties.sscs.rayRadius.Value * 0.01f);
            this.mMaterial.SetFloat(Properties.sscs.depthBias.Key, Properties.sscs.depthBias.Value * 0.001f);
            this.mMaterial.SetFloat(Properties.sscs.meanDepth.Key, Properties.sscs.meanDepth.Value);

            this.mLight = GetComponent<Light>();

            if (this.mLight)
            {
                this.SetupCommandBuffer();
            }
        }

        private void OnDisable()
        {
            this.RemoveCommandBuffer();
        }

        private void Reset()
        {
            this.RemoveCommandBuffer();
            this.SetupCommandBuffer();
        }

        private void Update()
        {
            this.mMaterial.SetFloat(slopeBias, this.mLight.shadowNormalBias);
            this.mMaterial.SetFloat(shadowDistance, QualitySettings.shadowDistance);
            this.UpdateProjectionMatrix();
        }

        private void SetupCommandBuffer()
        {
            if (Properties.pcss.pcfState == Properties.PCFState.disable)
            {
                return;
            }

            int pass = Convert.ToInt16(Properties.pcss.pcfState) - 1;
            pass += Properties.pcss.pcssEnabled ? 4 : 0;

            RenderTargetIdentifier source = BuiltinRenderTextureType.CurrentActive;
            int target = Shader.PropertyToID("_ScreenSpaceShadowMap");
            int flipsm = Shader.PropertyToID("_TemporaryFlipShadowMap");
            int flopsm = Shader.PropertyToID("_TemporaryFlopShadowMap");

            this.mBuffer = new CommandBuffer() { name = "HSSSS.ScreenSpaceShadow" };
            this.mBuffer.SetShadowSamplingMode(source, ShadowSamplingMode.RawDepth);
            this.mBuffer.GetTemporaryRT(target, -1, -1, 0, FilterMode.Point, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);

            // full rendering
            if (Properties.sscs.enabled)
            {
                this.mBuffer.GetTemporaryRT(flipsm, -1, -1, 0, FilterMode.Point, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);
                this.mBuffer.GetTemporaryRT(flopsm, -1, -1, 0, FilterMode.Point, RenderTextureFormat.RGHalf, RenderTextureReadWrite.Linear);

                // just shadow calculation
                this.mBuffer.Blit(source, flipsm, this.mMaterial, pass);
                this.mBuffer.Blit(source, flopsm, this.mMaterial, 10 + Convert.ToInt16(Properties.sscs.quality));
                this.mBuffer.Blit(source, target, this.mMaterial, 14);

                this.mBuffer.ReleaseTemporaryRT(target);
                this.mBuffer.ReleaseTemporaryRT(flipsm);
                this.mBuffer.ReleaseTemporaryRT(flopsm);
            }

            else
            {
                this.mBuffer.Blit(source, target, this.mMaterial, pass);

                this.mBuffer.ReleaseTemporaryRT(target);
            }

            // directional light needs additional shadowmap blit pass
            if (this.mLight.type == LightType.Directional)
            {
                int cascade = Shader.PropertyToID("_CascadeShadowMap");

                this.bBuffer = new CommandBuffer() { name = "HSSSS.BlitCascadeShadow" };
                this.bBuffer.SetShadowSamplingMode(source, ShadowSamplingMode.RawDepth);
                this.bBuffer.GetTemporaryRT(cascade, 4096, 4096, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
                this.bBuffer.Blit(source, cascade);
                this.bBuffer.ReleaseTemporaryRT(cascade);

                this.mLight.AddCommandBuffer(LightEvent.AfterShadowMap, this.bBuffer);
                this.mLight.AddCommandBuffer(LightEvent.BeforeScreenspaceMask, this.mBuffer);
            }

            else
            {
                this.mLight.AddCommandBuffer(LightEvent.AfterShadowMap, this.mBuffer);
            }
        }

        private void RemoveCommandBuffer()
        {
            if (this.mLight.type == LightType.Directional)
            {
                if (this.bBuffer != null)
                {
                    this.mLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, this.bBuffer);
                }

                if (this.mBuffer != null)
                {
                    this.mLight.RemoveCommandBuffer(LightEvent.BeforeScreenspaceMask, this.mBuffer);
                }
            }

            else
            {
                if (this.mBuffer != null)
                {
                    this.mLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, this.mBuffer);
                }
            }
        }
        
        private void UpdateProjectionMatrix()
        {
            if (this.mLight.type != LightType.Spot)
            {
                return;
            }
            
            // light projection
            float near = this.mLight.shadowNearPlane;
            float far = this.mLight.range;

            Vector4 Params = new Vector4(
                1.0f - far / near,
                far / near,
                1.0f / far - 1.0f / near,
                1.0f / near
            );

            this.mMaterial.SetVector(depthParams, Params);

            // shadow coordinates
            Matrix4x4 LightClip = Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity, new Vector3(0.5f, 0.5f, 0.5f));
            Matrix4x4 LightView = Matrix4x4.TRS(this.mLight.transform.position, this.mLight.transform.rotation, Vector3.one).inverse;
            Matrix4x4 LightProj = Matrix4x4.Perspective(this.mLight.spotAngle, 1, this.mLight.shadowNearPlane, this.mLight.range);

            Matrix4x4 m = LightClip * LightProj;

            m[0, 2] *= -1;
            m[1, 2] *= -1;
            m[2, 2] *= -1;
            m[3, 2] *= -1;
            
            this.mMaterial.SetMatrix(projMatrix, m * LightView);
        }

        public void UpdateSettings()
        {
            this.RemoveCommandBuffer();
            this.SetupCommandBuffer();

            if (this.mMaterial)
            {
                this.mMaterial.SetVector(Properties.pcss.dirLightPenumbra.Key, Properties.pcss.dirLightPenumbra.Value);
                this.mMaterial.SetVector(Properties.pcss.spotLightPenumbra.Key, Properties.pcss.spotLightPenumbra.Value);
                this.mMaterial.SetVector(Properties.pcss.pointLightPenumbra.Key, Properties.pcss.pointLightPenumbra.Value);

                this.mMaterial.SetFloat(Properties.sscs.rayRadius.Key, Properties.sscs.rayRadius.Value * 0.01f);
                this.mMaterial.SetFloat(Properties.sscs.depthBias.Key, Properties.sscs.depthBias.Value * 0.001f);
                this.mMaterial.SetFloat(Properties.sscs.meanDepth.Key, Properties.sscs.meanDepth.Value);
            }
        }
    }
}