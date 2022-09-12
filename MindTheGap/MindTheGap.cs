using System;
using UnityEngine;
using IllusionPlugin;
using Harmony;

namespace MindTheGap
{
    public class MindTheGap: IEnhancedPlugin
    {
        public string Name { get { return "MindTheGap"; } }
        public string Version { get { return "0.0.1"; } }
        public string[] Filter { get { return new[] { "HoneySelect_32", "HoneySelect_64", "StudioNEO_32", "StudioNEO_64" }; } }

        public static MindTheGap instance = null;

        #region Unity Methods
        public void OnApplicationStart()
        {
            instance = this;

            HarmonyInstance harmony = HarmonyInstance.Create("com.kkul.mindthegap");

            harmony.Patch(
                AccessTools.Method(typeof(CharFemaleBody), nameof(CharFemaleBody.ForceUpdate)), null,
                new HarmonyMethod(typeof(MindTheGap), nameof(CharFemaleBodyPatcher))
                );
        }

        public void OnApplicationQuit()
        {
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnLevelWasLoaded(int level)
        {
        }

        public void OnUpdate()
        {
        }

        public void OnLateUpdate()
        {
        }

        public void OnFixedUpdate()
        {
        }
        #endregion

        private static void CharFemaleBodyPatcher(CharFemaleBody __instance)
        {
            GameObject objHead = __instance.objHead.transform.FindChild("cf_N_head/cf_O_head").gameObject;

            if (objHead.GetComponent<MeshCollider>() == null)
            {
                objHead.AddComponent<MeshCollider>();
                Console.WriteLine("#### MindTheGap: Added MeshCollider");
            }

            else
            {
                Mesh bakedMesh = new Mesh();
                objHead.GetComponent<SkinnedMeshRenderer>().BakeMesh(bakedMesh);
                Console.WriteLine("#### MindTheGap: Baked Mesh");
                Console.WriteLine(bakedMesh.vertexCount);

                objHead.GetComponent<MeshCollider>().sharedMesh = bakedMesh;
            }

            foreach (GameObject objBrow in __instance.chaInfo.GetTagInfo(CharReference.TagObjKey.ObjEyebrow))
            {
                Vector3 pos = objBrow.GetComponent<SkinnedMeshRenderer>().sharedMesh.vertices[0];

                Console.WriteLine("{0:00.0000}, {1:00.0000}, {2:00.0000}\n", pos.x, pos.y, pos.z);

                pos = objBrow.GetComponent<SkinnedMeshRenderer>().sharedMesh.vertices[9];

                Console.WriteLine("{0:00.0000}, {1:00.0000}, {2:00.0000}\n", pos.x, pos.y, pos.z);
            }
        }
    }

    public class MTGPatches: MonoBehaviour
    {
    }

    public class MTGSolver: MonoBehaviour
    {
    }
}
