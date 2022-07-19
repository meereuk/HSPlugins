using UnityEngine;
using UnityEngine.Rendering;

namespace HSSSS
{
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
            if (this.mLight.type == LightType.Directional)
            {
                RenderTargetIdentifier sourceID = BuiltinRenderTextureType.CurrentActive;
                int targetID = Shader.PropertyToID("_CustomShadowMap");
                this.mBuffer = new CommandBuffer() { name = this.bufferName };
                this.mBuffer.SetShadowSamplingMode(sourceID, ShadowSamplingMode.RawDepth);
                this.mBuffer.GetTemporaryRT(targetID, 4096, 4096, 0, FilterMode.Bilinear, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
                this.mBuffer.Blit(sourceID, targetID);
                this.mBuffer.ReleaseTemporaryRT(targetID);
                this.mLight.AddCommandBuffer(LightEvent.AfterShadowMap, this.mBuffer);
            }
        }

        private bool HasCommandBuffer()
        {
            foreach (var buf in this.mLight.GetCommandBuffers(LightEvent.AfterShadowMap))
            {
                if (buf.name == this.bufferName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}