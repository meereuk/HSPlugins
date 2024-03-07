using System;
using UnityEngine;

public class CubemapDispatcher : MonoBehaviour
{
    private void Awake()
    {
    }

    private void OnEnable()
    {
        if (RenderSettings.skybox)
        {
            if (GetComponent<MeshRenderer>())
            {
                MeshRenderer meshRenderer = GetComponent<MeshRenderer>();

                Cubemap cubemap = RenderSettings.skybox.GetTexture("_Tex") as Cubemap;

                if (meshRenderer.sharedMaterial)
                {
                    meshRenderer.sharedMaterial.SetTexture("_Tex", cubemap);
                }
            }
        }
    }

    private void OnDisable()
    {
    }
}
