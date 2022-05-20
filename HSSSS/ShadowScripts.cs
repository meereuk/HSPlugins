using UnityEngine;
using UnityEngine.Rendering;

namespace HSSSS
{
    public class ShadowMapDispatcher : MonoBehaviour
    {
        private Light mLight;
        private CommandBuffer buffer;
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
                    this.mLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, this.buffer);
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
                this.mLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, this.buffer);
            }
        }

        private void OnDestroy()
        {
            if (this.mLight && this.HasCommandBuffer())
            {
                this.mLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, this.buffer);
            }

        }

        private void InitializeCommandBuffer()
        {
            if (this.mLight.type == LightType.Directional)
            {
                RenderTargetIdentifier sourceID = BuiltinRenderTextureType.CurrentActive;

                this.buffer = new CommandBuffer();
                this.buffer.name = this.bufferName;
                this.buffer.SetGlobalTexture("_CustomShadowMap", sourceID);

                this.mLight.AddCommandBuffer(LightEvent.AfterShadowMap, this.buffer);
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