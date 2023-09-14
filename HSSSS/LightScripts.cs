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


    public class ShadowMapDispatcher : MonoBehaviour
    {
        private Light mLight;
        private CommandBuffer mBuffer;
        private string bufferName;

        private void Awake()
        {
            this.mLight = GetComponent<Light>();
            this.bufferName = "ShadowMapDispatcher_" + this.mLight.name;
        }

        private void Reset()
        {
            if (this.mLight)
            {
                if (this.HasCommandBuffer())
                {
                    this.mLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, this.mBuffer);
                }

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
            if (this.mLight && this.HasCommandBuffer())
            {
                this.mLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, this.mBuffer);
            }
        }

        private void OnDestroy()
        {
            if (this.mLight && this.HasCommandBuffer())
            {
                this.mLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, this.mBuffer);
            }

        }

        private void InitializeCommandBuffer()
        {
            RenderTargetIdentifier sourceID = BuiltinRenderTextureType.CurrentActive;
            int targetID = Shader.PropertyToID("_CustomShadowMap");
            this.mBuffer = new CommandBuffer() { name = this.bufferName };
            this.mBuffer.SetShadowSamplingMode(sourceID, ShadowSamplingMode.RawDepth);

            if (this.mLight.type == LightType.Directional)
            {
                this.mBuffer.GetTemporaryRT(targetID, 4096, 4096, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            }

            else if (this.mLight.type == LightType.Spot)
            {
                this.mBuffer.GetTemporaryRT(targetID, 2048, 2048, 0, FilterMode.Point, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            }

            this.mBuffer.Blit(sourceID, targetID);
            this.mBuffer.ReleaseTemporaryRT(targetID);
            this.mLight.AddCommandBuffer(LightEvent.AfterShadowMap, this.mBuffer);
        }

        private bool HasCommandBuffer()
        {
            foreach (var buffer in this.mLight.GetCommandBuffers(LightEvent.AfterShadowMap))
            {
                if (buffer.name == this.bufferName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}