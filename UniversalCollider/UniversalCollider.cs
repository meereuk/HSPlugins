using System;
using UnityEngine;
using IllusionPlugin;
using Harmony;

namespace UniversalCollider
{
    public class UniversalCollider : IEnhancedPlugin
    {
        #region info
        public string Name { get { return "UniversalCollider"; } }
        public string Version { get { return "1.0.0"; } }
        public string[] Filter { get { return new[] { "HoneySelect_32", "HoneySelect_64", "StudioNEO_32", "StudioNEO_64" }; } }
        #endregion

        #region variables
        #endregion

        #region ipa methods
        public void OnApplicationStart()
        {
            HarmonyInstance harmony = HarmonyInstance.Create("com.kkul.universalcollider");

            harmony.Patch(
                AccessTools.Method(typeof(CharFemaleBody), nameof(CharFemaleBody.Reload)), null,
                new HarmonyMethod(typeof(UniversalCollider), nameof(AddColliders))
                );
        }

        public void OnLevelWasLoaded(int level)
        {
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnUpdate()
        {
        }

        public void OnFixedUpdate()
        {
        }

        public void OnLateUpdate()
        {
        }

        public void OnApplicationQuit()
        {
        }
        #endregion

        private static void AddColliders(CharFemaleBody __instance)
        {
            DynamicBoneCollider[] dbColliders = __instance.GetComponentsInChildren<DynamicBoneCollider>();

            foreach (DynamicBoneCollider dbcol in dbColliders)
            {
                CapsuleCollider col = dbcol.gameObject.GetComponent<CapsuleCollider>();

                if (col == null)
                {
                    col = dbcol.gameObject.AddComponent<CapsuleCollider>();

                    col.enabled = true;
                    col.center = dbcol.m_Center;
                    col.radius = dbcol.m_Radius;
                    col.height = dbcol.m_Height;
                    col.direction = (int)dbcol.m_Direction;

                    Console.WriteLine("Adding capsule colliders... " + dbcol.name);
                }

                else
                {
                    col.enabled = true;
                    col.center = dbcol.m_Center;
                    col.radius = dbcol.m_Radius;
                    col.height = dbcol.m_Height;
                    col.direction = (int)dbcol.m_Direction;

                    Console.WriteLine("Updating capsule colliders... " + dbcol.name);
                }
            }

            foreach (Cloth clo in __instance.GetComponentsInChildren<Cloth>())
            {
                clo.capsuleColliders = __instance.GetComponentsInChildren<CapsuleCollider>();
                Console.WriteLine("Updating cloth components... " +  clo.name);
            }
        }
    }
}
