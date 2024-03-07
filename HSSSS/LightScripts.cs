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
            this.mLight = GetComponent<Light>();

            if (this.mLight != null)
            {
                this.mObject = this.mLight.gameObject;

                if (this.mObject != null )
                {
                    this.mMaterial = new Material(Shader.Find("Unlit/Texture"));
                    this.mMaterial.name = "SpotLightCookie";

                    if (this.mMaterial.GetTexture("_MainTex") == null)
                    {
                        this.mMaterial.SetTexture("_MainTex", AssetLoader.spotCookie);
                    }

                    this.mRenderer = this.mObject.AddComponent<MeshRenderer>();
                    this.mRenderer.name = "SpotLightCookie";
                    this.mRenderer.material = this.mMaterial;
                }
            }
        }

        private void OnDestroy()
        {
            DestroyImmediate(this.mMaterial);
            DestroyImmediate(this.mRenderer);
        }

        private void Update()
        {
            this.mCookie = this.mRenderer.material.mainTexture;
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

        private void OnEnable()
        {
            this.mMaterial = new Material(AssetLoader.softShadows);

            this.mMaterial.SetTexture("_BlueNoise", AssetLoader.blueNoise);
            this.mMaterial.SetInt("_SparseRendering", Convert.ToInt16(Properties.pcss.checkerboard));
            this.mMaterial.SetVector("_DirLightPenumbra", Properties.pcss.dirLightPenumbra);
            this.mMaterial.SetVector("_SpotLightPenumbra", Properties.pcss.spotLightPenumbra);
            this.mMaterial.SetVector("_PointLightPenumbra", Properties.pcss.pointLightPenumbra);

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
            if (this.mLight.type == LightType.Spot)
            {
                this.UpdateProjectionMatrix();
            }

            this.mMaterial.SetFloat("_SlopeBiasScale", this.mLight.shadowNormalBias);
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

            this.mBuffer = new CommandBuffer() { name = "HSSSS.ScreenSpaceShadow" };
            this.mBuffer.SetShadowSamplingMode(source, ShadowSamplingMode.RawDepth);
            this.mBuffer.GetTemporaryRT(target, -1, -1, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);

            // sparse rendering
            if (Properties.pcss.checkerboard)
            {
                int flipsm = Shader.PropertyToID("_TemporaryFlipShadowMap");
                int flopsm = Shader.PropertyToID("_TemporaryFlopShadowMap");

                this.mBuffer.GetTemporaryRT(flipsm, -1, -1, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
                this.mBuffer.GetTemporaryRT(flopsm, -1, -1, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);

                // shadow calculation
                this.mBuffer.Blit(source, flipsm, this.mMaterial, pass);

                // un-checkerboarding
                this.mBuffer.Blit(flipsm, flopsm, this.mMaterial, 8);
                // median interpolation
                this.mBuffer.Blit(flopsm, flipsm, this.mMaterial, 9);
                this.mBuffer.Blit(flipsm, target);

                this.mBuffer.ReleaseTemporaryRT(flipsm);
                this.mBuffer.ReleaseTemporaryRT(flopsm);
            }

            // full rendering
            else
            {
                // just shadow calculation
                this.mBuffer.Blit(source, target, this.mMaterial, pass);
            }

            this.mBuffer.ReleaseTemporaryRT(target);

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
            Matrix4x4 LightClip = Matrix4x4.TRS(new Vector3(0.5f, 0.5f, 0.5f), Quaternion.identity, new Vector3(0.5f, 0.5f, 0.5f));
            Matrix4x4 LightView = Matrix4x4.TRS(this.mLight.transform.position, this.mLight.transform.rotation, Vector3.one).inverse;
            Matrix4x4 LightProj = Matrix4x4.Perspective(this.mLight.spotAngle, 1, this.mLight.shadowNearPlane, this.mLight.range);

            Matrix4x4 m = LightClip * LightProj;

            m[0, 2] *= -1;
            m[1, 2] *= -1;
            m[2, 2] *= -1;
            m[3, 2] *= -1;
            
            this.mMaterial.SetMatrix("_ShadowProjMatrix", m * LightView);
        }

        public void UpdateSettings()
        {
            this.RemoveCommandBuffer();
            this.SetupCommandBuffer();

            if (this.mMaterial)
            {
                this.mMaterial.SetInt("_SparseRendering", Convert.ToInt16(Properties.pcss.checkerboard));
                this.mMaterial.SetVector("_DirLightPenumbra", Properties.pcss.dirLightPenumbra);
                this.mMaterial.SetVector("_SpotLightPenumbra", Properties.pcss.spotLightPenumbra);
                this.mMaterial.SetVector("_PointLightPenumbra", Properties.pcss.pointLightPenumbra);
            }
        }
    }
}