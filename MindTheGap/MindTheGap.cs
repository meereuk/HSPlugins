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
        public void OnApplicationStart()
        {
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
    }

    public class MTGPatches: MonoBehaviour
    {
    }

    public class MTGSolver: MonoBehaviour
    {
    }
}
