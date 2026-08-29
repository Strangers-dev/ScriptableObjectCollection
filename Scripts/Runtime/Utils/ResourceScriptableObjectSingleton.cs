using UnityEngine;
using UnityEngine.Scripting;

namespace BrunoMikoski.ScriptableObjectCollections.Core
{
    [Preserve]
    public class ResourceScriptableObjectSingleton<T> : ScriptableObject where T: ScriptableObject
    {
        private static T instance;
        public static T Instance
        {
            get
            {
                if (instance == null)
                    instance = LoadOrCreateInstance<T>();
                return instance;
            }
        }

        public static TInstance LoadOrCreateInstance<TInstance>() where TInstance : ScriptableObject
        {
            if (!TryToLoadInstance<TInstance>(out TInstance resultInstance))
            {
#if !UNITY_EDITOR
                return null;
#else
                resultInstance = CreateInstance<TInstance>();

                AssetDatabaseUtils.CreatePathIfDoesntExist("Assets/Resources");
                UnityEditor.AssetDatabase.CreateAsset(resultInstance, $"Assets/Resources/{typeof(TInstance).Name}.asset");
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();
                return resultInstance;
#endif         
            }

            return resultInstance;
        }

        public static bool Exist()
        {
            return TryToLoadInstance<T>(out _);
        }

        private static bool TryToLoadInstance<TInstance>(out TInstance result) where TInstance: ScriptableObject
        {
            // TEMP diagnostic, remove: who forces the load, when, and what the sync cost is.
            // File, not Debug.Log: the game's TBLogHandler mutes player logs.
            float t0 = Time.realtimeSinceStartup;
            TInstance newInstance = Resources.Load<TInstance>(typeof(TInstance).Name);
            string tracePath = System.Environment.GetEnvironmentVariable("TB_SOC_TRACE");
            if (!string.IsNullOrEmpty(tracePath))
            {
                System.IO.File.AppendAllText(tracePath,
                    $"sync load {typeof(TInstance).Name} {(Time.realtimeSinceStartup - t0) * 1000f:F0}ms at t={t0 * 1000f:F0}ms\n{System.Environment.StackTrace}\n");
            }

            if (newInstance != null)
            {
                result = newInstance;
                return true;
            }

#if UNITY_EDITOR
            string[] assets = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(TInstance).Name}");
            
            string registryGUID = "";

            if (assets.Length > 0)
                registryGUID = assets[0];

            if (!string.IsNullOrEmpty(registryGUID))
            {
                newInstance = (TInstance) UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(registryGUID));
            }

            if (newInstance != null)
            {
                result = newInstance;
                return true;
            }
#endif
            result = null;
            return false;
        }
        
    }
}
