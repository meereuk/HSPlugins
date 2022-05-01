using UnityEngine;
using UnityEngine.Rendering;

namespace HSSSS
{
    public class ShadowMapDispatcher : MonoBehaviour
    {
        private Light mLight;
        private CommandBuffer buffer;

        private void Awake()
        {
            this.mLight = GetComponent<Light>();
        }

        private void Reset()
        {
            if (this.mLight)
            {
                this.mLight.RemoveAllCommandBuffers();
                this.InitializeCommandBuffer();
            }
        }

        private void OnEnable()
        {
            if (this.mLight)
            {
                this.InitializeCommandBuffer();
            }
        }

        private void OnDisable()
        {
            if (this.mLight)
            {
                this.mLight.RemoveAllCommandBuffers();
            }
        }

        private void InitializeCommandBuffer()
        {
            if (this.mLight.type == LightType.Directional)
            {
                RenderTargetIdentifier sourceID = BuiltinRenderTextureType.CurrentActive;

                this.buffer = new CommandBuffer();
                this.buffer.SetGlobalTexture("_CustomShadowMap", sourceID);

                this.mLight.AddCommandBuffer(LightEvent.AfterShadowMap, this.buffer);
            }
        }
    }

    public class VarianceShadowMap : MonoBehaviour
    {
        private Light mLight;
        //private Shader depthShader;
        //private Material depthMaterial;
        private CommandBuffer buffer;

        private LightType lightType;
        //private int resolution;

        private void Awake()
        {
            this.mLight = GetComponent<Light>();

            if (this.mLight != null)
            {
                this.lightType = this.mLight.type;

                /*
                switch (this.lightType)
                {
                    case LightType.Directional:
                        this.resolution = 4096;
                        break;

                    case LightType.Spot:
                        this.resolution = 2048;
                        break;

                    case LightType.Point:
                        this.resolution = 1024;
                        break;
                }
                */
            }

            //depthShader = HSSSS.shadowMapSampler;
            //depthMaterial = new Material(depthShader);
        }

        private void Reset()
        {
            this.DestroyCommandBuffer();
            this.InitializeCommandBuffer();
        }

        private void OnEnable()
        {
            this.InitializeCommandBuffer();
        }

        private void OnDisable()
        {
            this.DestroyCommandBuffer();
        }

        private void InitializeCommandBuffer()
        {
            if (this.mLight != null)
            {
                if (this.lightType == LightType.Directional)
                {
                    this.DirectionalCommandBuffer();
                }

                /*
                if (this.lightType == LightType.Point)
                {
                    this.PointLightCommandBuffer();
                }

                else
                {
                    this.OtherLightCommandBuffer();
                }
                */
            }
        }

        
        private void DestroyCommandBuffer()
        {
            if (this.mLight != null)
            {
                mLight.RemoveAllCommandBuffers();
            }
        }

        
        private void DirectionalCommandBuffer()
        {
            RenderTargetIdentifier sourceID = BuiltinRenderTextureType.CurrentActive;

            this.buffer = new CommandBuffer();
            this.buffer.SetGlobalTexture("_CustomShadowMap", sourceID);

            mLight.AddCommandBuffer(LightEvent.AfterShadowMap, this.buffer);
        }

        /*
        private void OtherLightCommandBuffer()
        {
            RenderTargetIdentifier sourceID = BuiltinRenderTextureType.CurrentActive;
            int depthRT = Shader.PropertyToID("_DepthBufferTexture");
            int blurXRT = Shader.PropertyToID("_BlurXBufferTexture");
            int blurYRT = Shader.PropertyToID("_BlurYBufferTexture");

            this.buffer = new CommandBuffer();
            // Sampling depth & depth square
            this.buffer.GetTemporaryRT(depthRT, this.resolution, this.resolution, 0, FilterMode.Point, RenderTextureFormat.RGFloat);
            this.buffer.Blit(sourceID, depthRT, depthMaterial, 0);
            // Gaussian blur with x-axis
            this.buffer.GetTemporaryRT(blurXRT, this.resolution, this.resolution, 0, FilterMode.Point, RenderTextureFormat.RGFloat);
            this.buffer.Blit(depthRT, blurXRT, depthMaterial, 1);
            // Gaussian blur with y-axis
            this.buffer.GetTemporaryRT(blurYRT, this.resolution, this.resolution, 0, FilterMode.Point, RenderTextureFormat.RGFloat);
            this.buffer.Blit(blurXRT, blurYRT, depthMaterial, 2);
            //
            this.buffer.SetGlobalTexture("_CustomShadowMap", blurYRT);
            //
            this.buffer.ReleaseTemporaryRT(depthRT);
            this.buffer.ReleaseTemporaryRT(blurXRT);
            this.buffer.ReleaseTemporaryRT(blurYRT);
            mLight.AddCommandBuffer(LightEvent.AfterShadowMap, this.buffer);
        }

        private void PointLightCommandBuffer()
        {
            RenderTargetIdentifier sourceID = BuiltinRenderTextureType.CurrentActive;
            this.buffer = new CommandBuffer();
            this.buffer.SetGlobalTexture("_CustomShadowMap", sourceID);
            mLight.AddCommandBuffer(LightEvent.AfterShadowMap, this.buffer);
        }

        private void SetPenumbraSize()
        {
            if (this.lightType == LightType.Directional)
            {
                this.depthMaterial.SetFloat("_Penumbra", this.mLight.shadowNearPlane);
            }

            if (this.lightType == LightType.Spot)
            {
                this.depthMaterial.SetFloat("_Penumbra", this.mLight.shadowNormalBias * 3.33f);
            }
        }
        */
    }
}