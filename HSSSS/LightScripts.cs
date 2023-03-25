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
                        this.mMaterial.SetTexture("_MainTex", HSSSS.spotCookie);
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

    public class ContactShadowSampler : MonoBehaviour
    {
        private Light mLight;
        private Shader mShader;
        private Material mMaterial;
        private CommandBuffer mBuffer;

        private static Matrix4x4 WorldToViewMatrix;
        private static Matrix4x4 ViewToWorldMatrix;

        private static Camera mainCamera;

        private void Awake()
        {
        }

        private void OnEnable()
        {
            this.mLight = GetComponent<Light>();
            this.mShader = HSSSS.contactShadowShader;
            this.mMaterial = new Material(this.mShader);
            this.mMaterial.SetTexture("_ShadowJitterTexture", HSSSS.shadowJitter);

            if (this.mLight)
            {
                this.SetupCommandBuffer();
            }
        }

        private void OnDisable()
        {
            if (this.mLight)
            {
                this.DestroyCommandBuffer();
            }
        }

        private void Update()
        {
            this.mMaterial.SetMatrix("_WorldToViewMatrix", WorldToViewMatrix);
            this.mMaterial.SetMatrix("_ViewToWorldMatrix", ViewToWorldMatrix);

            this.mMaterial.SetVector("_LightPosition", this.mLight.gameObject.transform.position);
        }

        private void SetupCommandBuffer()
        {
            int shadowMap = Shader.PropertyToID("_ScreenSpaceShadowMap");

            this.mBuffer = new CommandBuffer();
            this.mBuffer.name = "SSCSSampler";
            this.mBuffer.GetTemporaryRT(shadowMap, -1, -1, 0, FilterMode.Point, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
            this.mBuffer.Blit(BuiltinRenderTextureType.CurrentActive, shadowMap, this.mMaterial, 0);
            this.mBuffer.ReleaseTemporaryRT(shadowMap);
            this.mLight.AddCommandBuffer(LightEvent.AfterShadowMap, this.mBuffer);
        }

        private void DestroyCommandBuffer()
        {
            this.mLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, this.mBuffer);
            this.mBuffer = null;
        }

        public static void SetMainCamera(Camera camera)
        {
            if (camera)
            {
                mainCamera = camera;
            }
        }

        public static void UpdateViewMatrix()
        {
            if (mainCamera)
            {
                WorldToViewMatrix = mainCamera.worldToCameraMatrix;
                ViewToWorldMatrix = WorldToViewMatrix.inverse;
            }
        }
    }
}