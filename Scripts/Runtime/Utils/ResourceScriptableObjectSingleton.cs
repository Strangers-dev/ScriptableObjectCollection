using UnityEngine;
using UnityEngine.Scripting;

namespace BrunoMikoski.ScriptableObjectCollections.Core
{
    // Armed by the game while its async registry preload is in flight
    public static class SyncLoadGuard
    {
        public static bool Armed;
    }

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
            if (SyncLoadGuard.Armed)
            {
                string error = $"Sync load of {typeof(TInstance).Name} while the async preload is in flight: this materializes the whole closure. Gate on SOCollectionsPreloader.Ready.";
                Debug.LogError(error);
                // stderr survives any log-category settings in the game's log handler
                System.Console.Error.WriteLine(error);
            }

            TInstance newInstance = Resources.Load<TInstance>(typeof(TInstance).Name);

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
