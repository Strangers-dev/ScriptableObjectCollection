using System;
using System.Collections.Generic;
using System.Linq;
using BrunoMikoski.ScriptableObjectCollections.Core;
using UnityEngine;
using UnityEngine.Scripting;
#if UNITY_EDITOR
using System.Text;
using UnityEditor;
#endif

namespace BrunoMikoski.ScriptableObjectCollections
{
    [DefaultExecutionOrder(-1000)]
    [Preserve]
    public class CollectionsRegistry : ResourceScriptableObjectSingleton<CollectionsRegistry>, ISerializationCallbackReceiver
    {
        private const string NON_AUTO_INITIALIZED_COLLECTIONS_KEY = "NON_AUTO_INITIALIZED_COLLECTIONS";

#if UNITY_EDITOR
        [SerializeField]
        private List<ScriptableObjectCollection> collections = new List<ScriptableObjectCollection>();
#endif

        // Metadata mirror keeps the registry's serialized closure empty; collection roots load on demand from Resources/Collections/.
        [SerializeField] private List<string> collectionAssetNames = new List<string>();
        [SerializeField] private List<LongGuid> collectionGuids = new List<LongGuid>();
        [SerializeField] private List<string> collectionTypeNames = new List<string>();
        [SerializeField] private List<string> collectionItemTypeNames = new List<string>();

        private readonly Dictionary<int, ScriptableObjectCollection> loadedCollections = new Dictionary<int, ScriptableObjectCollection>();

        [SerializeField, HideInInspector]
        private bool autoSearchForCollections;
        public bool AutoSearchForCollections => autoSearchForCollections;

        // No boot warm: any request queued before scene load drains inside LoadFirstScene's wait,
        // putting the whole closure in the boot megaframe. Instance lazy-loads; the game preloads post-boot.

        public IReadOnlyList<ScriptableObjectCollection> Collections
        {
            get
            {
#if UNITY_EDITOR
                return collections;
#else
                List<ScriptableObjectCollection> result = new List<ScriptableObjectCollection>(collectionAssetNames.Count);
                for (int i = 0; i < collectionAssetNames.Count; i++)
                    result.Add(ResolveAt(i));
                return result;
#endif
            }
        }

        private int CollectionCount
        {
#if UNITY_EDITOR
            get => collections.Count;
#else
            get => collectionAssetNames.Count;
#endif
        }

        private ScriptableObjectCollection ResolveAt(int index)
        {
#if UNITY_EDITOR
            return collections[index];
#else
            if (loadedCollections.TryGetValue(index, out ScriptableObjectCollection cached) && cached != null)
                return cached;

            string assetName = collectionAssetNames[index];

            ScriptableObjectCollection collection = Resources.Load<ScriptableObjectCollection>("Collections/" + assetName);
            loadedCollections[index] = collection;
            return collection;
#endif
        }

        public void WarmAsync(IReadOnlyList<string> assetNames)
        {
#if !UNITY_EDITOR
            for (int i = 0; i < assetNames.Count; i++)
            {
                TryWarmAsync(assetNames[i]);
            }
#endif
        }

        public ResourceRequest TryWarmAsync(string assetName)
        {
#if !UNITY_EDITOR
            int index = IndexOfAssetName(assetName);
            if (index < 0)
            {
                return null;
            }
            if (loadedCollections.TryGetValue(index, out ScriptableObjectCollection cached) && cached != null)
            {
                return null;
            }

            // A sync ResolveAt racing this in-flight warm loads too; Unity dedupes the underlying request.
            ResourceRequest request = Resources.LoadAsync<ScriptableObjectCollection>("Collections/" + assetName);
            request.completed += _ =>
            {
                loadedCollections[index] = request.asset as ScriptableObjectCollection;
            };
            return request;
#else
            return null;
#endif
        }

#if !UNITY_EDITOR
        private int IndexOfAssetName(string assetName)
        {
            for (int i = 0; i < collectionAssetNames.Count; i++)
            {
                if (string.Equals(collectionAssetNames[i], assetName, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }
#endif

        private LongGuid GuidAt(int index)
        {
#if UNITY_EDITOR
            return collections[index] != null ? collections[index].GUID : default;
#else
            return collectionGuids[index];
#endif
        }

        private string NameAt(int index)
        {
#if UNITY_EDITOR
            return collections[index] != null ? collections[index].name : null;
#else
            return collectionAssetNames[index];
#endif
        }

        private Type CollectionTypeAt(int index)
        {
#if UNITY_EDITOR
            return collections[index] != null ? collections[index].GetType() : null;
#else
            string typeName = collectionTypeNames[index];
            return string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);
#endif
        }

        private Type ItemTypeAt(int index)
        {
#if UNITY_EDITOR
            return collections[index] != null ? collections[index].GetItemType() : null;
#else
            string typeName = collectionItemTypeNames[index];
            return string.IsNullOrEmpty(typeName) ? null : Type.GetType(typeName);
#endif
        }

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            collectionAssetNames = collections.Select(c => c != null ? c.name : null).ToList();
            collectionGuids = collections.Select(c => c != null ? c.GUID : default).ToList();
            collectionTypeNames = collections.Select(c => c != null ? c.GetType().AssemblyQualifiedName : null).ToList();
            collectionItemTypeNames = collections.Select(c => c != null ? c.GetItemType()?.AssemblyQualifiedName : null).ToList();
#endif
        }

        public void OnAfterDeserialize() { }

        public bool IsKnowCollection(ScriptableObjectCollection targetCollection)
        {
            for (int i = 0; i < CollectionCount; i++)
            {
                if (GuidAt(i) == targetCollection.GUID)
                    return true;
            }

            return false;
        }

        public bool TryGetCollectionByName<T>(string targetCollectionName, out ScriptableObjectCollection<T> resultCollection) where T: ScriptableObject, ISOCItem
        {
            if (TryGetCollectionByName(targetCollectionName, out ScriptableObjectCollection collection))
            {
                resultCollection = (ScriptableObjectCollection<T>) collection;
                return true;
            }

            resultCollection = null;
            return false;
        }

        public bool TryGetCollectionByName(string targetCollectionName, out ScriptableObjectCollection resultCollection)
        {
            for (int i = 0; i < CollectionCount; i++)
            {
                if (string.Equals(NameAt(i), targetCollectionName, StringComparison.Ordinal))
                {
                    resultCollection = ResolveAt(i);
                    return true;
                }
            }

            resultCollection = null;
            return false;
        }


        public List<T> GetAllCollectionItemsOfType<T>() where T : ScriptableObject, ISOCItem
        {
            List<T> result = new List<T>();
            List<ScriptableObject> items = GetAllCollectionItemsOfType(typeof(T));
            for (int i = 0; i < items.Count; i++)
            {
                ScriptableObject scriptableObjectCollectionItem = items[i];
                result.Add(scriptableObjectCollectionItem as T);
            }

            return result;
        }

        public List<ScriptableObject> GetAllCollectionItemsOfType(Type targetItemType)
        {
            List<ScriptableObject> results = new List<ScriptableObject>();
            for (int i = 0; i < CollectionCount; i++)
            {
                Type collectionItemType = ItemTypeAt(i);
                if (collectionItemType == null || !targetItemType.IsAssignableFrom(collectionItemType))
                    continue;

                ScriptableObjectCollection collection = ResolveAt(i);
                if (collection != null)
                    results.AddRange(collection.Items);
            }

            return results;
        }

        public bool TryGetCollectionsOfItemType(Type targetType, out List<ScriptableObjectCollection> results)
        {
            List<ScriptableObjectCollection> availables = new();
            int minDistance = int.MaxValue;

            for (int i = 0; i < CollectionCount; i++)
            {
                Type itemType = ItemTypeAt(i);

                if (itemType == null)
                    continue;

                if (itemType == typeof(ISOCItem) || itemType == typeof(ScriptableObjectCollectionItem) || itemType.BaseType == null)
                    continue;

                if (!itemType.IsAssignableFrom(targetType))
                    continue;

                int distance = GetInheritanceDistance(targetType, itemType);
                if (distance < minDistance)
                {
                    availables.Clear();
                    availables.Add(ResolveAt(i));
                    minDistance = distance;
                }
                else if (distance == minDistance)
                {
                    availables.Add(ResolveAt(i));
                }
            }

            if (availables.Count == 0)
            {
                results = null;
                return false;
            }

            results = availables;
            return true;
        }

        private int GetInheritanceDistance(Type fromType, Type toType)
        {
            int distance = 0;
            Type currentType = fromType;
            while (currentType != null && currentType != toType)
            {
                currentType = currentType.BaseType;
                distance++;
            }
            if (currentType == toType)
                return distance;
            return int.MaxValue;
        }

        public bool TryGetCollectionsOfItemType<T>(out List<ScriptableObjectCollection<T>> results)
            where T : ScriptableObject, ISOCItem
        {
            Type targetType = typeof(T);

            if (TryGetCollectionsOfItemType(targetType, out List<ScriptableObjectCollection> collections))
            {
                results = collections.Cast<ScriptableObjectCollection<T>>().ToList();
                return true;
            }

            results = null;
            return false;
        }

        public bool TryGetCollectionsOfType<T>(out List<T> inputActionMapCollections, bool allowSubclasses = true) where T : ScriptableObjectCollection
        {
            List<T> result = new List<T>();
            Type targetType = typeof(T);
            for (int i = 0; i < CollectionCount; i++)
            {
                Type collectionType = CollectionTypeAt(i);
                if (collectionType == null)
                    continue;
                if (collectionType == targetType || (allowSubclasses && collectionType.IsSubclassOf(targetType)))
                    result.Add((T)ResolveAt(i));
            }

            inputActionMapCollections = result;
            return result.Count > 0;
        }

        public List<ScriptableObjectCollection> GetCollectionsByItemType<T>() where T : ScriptableObjectCollectionItem
        {
            return GetCollectionsByItemType(typeof(T));
        }

        public List<ScriptableObjectCollection> GetCollectionsByItemType(Type targetCollectionItemType)
        {
            List<ScriptableObjectCollection> result = new List<ScriptableObjectCollection>();

            for (int i = 0; i < CollectionCount; i++)
            {
                Type itemType = ItemTypeAt(i);
                if (itemType == null)
                    continue;
                if (itemType.IsAssignableFrom(targetCollectionItemType))
                    result.Add(ResolveAt(i));
            }

            return result;
        }


        [Obsolete("Use GetCollectionByGUID(ULongGuid guid) is obsolete, please regenerate your static class")]
        public ScriptableObjectCollection GetCollectionByGUID(string guid)
        {
            throw new Exception("GetCollectionByGUID(ULongGuid guid) is obsolete, please regenerate your static class");
        }

        public ScriptableObjectCollection GetCollectionByGUID(LongGuid guid)
        {
            for (int i = 0; i < CollectionCount; i++)
            {
                if (GuidAt(i) == guid)
                    return ResolveAt(i);
            }

            return null;
        }

        public bool TryGetCollectionOfType(Type type, out ScriptableObjectCollection resultCollection)
        {
            for (int i = 0; i < CollectionCount; i++)
            {
                if (CollectionTypeAt(i) == type)
                {
                    resultCollection = ResolveAt(i);
                    return true;
                }
            }

            resultCollection = null;
            return false;
        }

        public bool TryGetCollectionOfType<T>(out T resultCollection) where T: ScriptableObjectCollection
        {
            bool didFind = TryGetCollectionOfType(typeof(T), out ScriptableObjectCollection baseCollection);
            resultCollection = baseCollection as T;
            return didFind;
        }

        public bool TryGetCollectionFromItemType(Type targetType, out ScriptableObjectCollection resultCollection)
        {
            if (TryGetCollectionsOfItemType(targetType, out List<ScriptableObjectCollection> possibleCollections))
            {
                if (possibleCollections.Count == 1)
                {
                    resultCollection = possibleCollections[0];
                    return true;
                }
            }

            resultCollection = null;
            return false;
        }

        public bool TryGetCollectionFromItemType<TargetType>(out ScriptableObjectCollection<TargetType> scriptableObjectCollection) where TargetType : ScriptableObjectCollectionItem
        {
            if (TryGetCollectionFromItemType(typeof(TargetType), out ScriptableObjectCollection resultCollection))
            {
                scriptableObjectCollection = (ScriptableObjectCollection<TargetType>) resultCollection;
                return true;
            }

            scriptableObjectCollection = null;
            return false;
        }


        public bool TryGetCollectionByGUID<T>(LongGuid targetGUID, out T resultCollection) where T: ScriptableObjectCollection
        {
            if (targetGUID.IsValid())
            {
                for (int i = 0; i < CollectionCount; i++)
                {
                    if (GuidAt(i) == targetGUID)
                    {
                        resultCollection = (T) ResolveAt(i);
                        return resultCollection != null;
                    }
                }
            }

            resultCollection = null;
            return false;
        }

        public bool TryGetCollectionByGUID<T>(LongGuid targetGUID, out ScriptableObjectCollection resultCollection) where T : ScriptableObject, ISOCItem
        {
            if (targetGUID.IsValid())
            {
                if (TryGetCollectionByGUID(targetGUID, out ScriptableObjectCollection foundCollection))
                {
                    resultCollection = foundCollection as ScriptableObjectCollection;
                    return true;
                }
            }

            resultCollection = null;
            return false;
        }

        public void SetAutoSearchForCollections(bool isOn)
        {
            if (isOn == autoSearchForCollections)
                return;

            autoSearchForCollections = isOn;
            ObjectUtility.SetDirty(this);
        }

#if UNITY_EDITOR
        public void RegisterCollection(ScriptableObjectCollection targetCollection)
        {
            if (collections.Contains(targetCollection))
                return;

            collections.Add(targetCollection);

            ObjectUtility.SetDirty(this);
        }

        public void UnregisterCollection(ScriptableObjectCollection targetCollection)
        {
            if (!collections.Contains(targetCollection))
                return;

            if (!collections.Remove(targetCollection))
                return;

            ObjectUtility.SetDirty(this);
        }

        public void ReloadCollections()
        {
            if (Application.isPlaying)
                return;

            HashSet<ScriptableObjectCollection> foundCollections  = new HashSet<ScriptableObjectCollection>();

            bool changed = false;
            string[] typeGUIDs = AssetDatabase.FindAssets($"t:{nameof(ScriptableObjectCollection)}");

            for (int j = 0; j < typeGUIDs.Length; j++)
            {
                string typeGUID = typeGUIDs[j];
                ScriptableObjectCollection collection =
                    AssetDatabase.LoadAssetAtPath<ScriptableObjectCollection>(AssetDatabase.GUIDToAssetPath(typeGUID));

                if (collection == null)
                    continue;

                if (foundCollections.Contains(collection))
                    continue;

                if (!collections.Contains(collection))
                    changed = true;

                collection.RefreshCollection();
                foundCollections.Add(collection);
            }

            if (changed)
            {
                ValidateCollections();
                collections = foundCollections.ToList();
                ObjectUtility.SetDirty(this);
            }
        }

        public void PreBuildProcess()
        {
            ReloadCollections();
            RemoveNonAutomaticallyInitializedCollections();
            AssetDatabase.SaveAssets();
        }

        public void RemoveNonAutomaticallyInitializedCollections()
        {
            StringBuilder removedAssetPaths = new StringBuilder();
            bool dirty = false;
            for (int i = collections.Count - 1; i >= 0; i--)
            {
                ScriptableObjectCollection collection = collections[i];

                // A branch switch can leave a dead reference here; drop it so it can't NRE on entering play mode
                if (collection == null)
                {
                    collections.RemoveAt(i);
                    dirty = true;
                    continue;
                }

                if (collection.AutomaticallyLoaded)
                    continue;

                collections.Remove(collection);
                removedAssetPaths.Append($"{AssetDatabase.GetAssetPath(collection)}|");

                dirty = true;
            }

            if (dirty)
            {
                EditorPrefs.SetString(NON_AUTO_INITIALIZED_COLLECTIONS_KEY, removedAssetPaths.ToString());
                ObjectUtility.SetDirty(this);
            }
            else
            {
                EditorPrefs.DeleteKey(NON_AUTO_INITIALIZED_COLLECTIONS_KEY);
            }
        }

        public void ReloadUnloadedCollectionsIfNeeded()
        {
            string removedAssetPaths = EditorPrefs.GetString(NON_AUTO_INITIALIZED_COLLECTIONS_KEY, string.Empty);
            if (string.IsNullOrEmpty(removedAssetPaths))
                return;

            string[] paths = removedAssetPaths.Split('|', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                ScriptableObjectCollection collection = AssetDatabase.LoadAssetAtPath<ScriptableObjectCollection>(path);
                if (collection == null)
                    continue;

                collections.Add(collection);
            }

            EditorPrefs.DeleteKey(NON_AUTO_INITIALIZED_COLLECTIONS_KEY);
            ObjectUtility.SetDirty(this);
        }

        public void PostBuildProcess()
        {
            ReloadCollections();
        }

        public void ValidateCollections()
        {
            for (int i = collections.Count - 1; i >= 0; i--)
            {
                if (collections[i] == null)
                    collections.RemoveAt(i);
            }

            for (int i = collections.Count - 1; i >= 0; i--)
            {
                ScriptableObjectCollection collectionA = collections[i];

                for (int j = collections.Count - 1; j >= 0; j--)
                {
                    ScriptableObjectCollection collectionB = collections[j];

                    if (i == j)
                        continue;

                    if (collectionA.GUID == collectionB.GUID)
                    {
                        collectionA.GenerateNewGUID();
                        Debug.LogWarning(
                            $"Found duplicated GUID between {collectionA} and {collectionB}, please run the validation again to make sure this is fixed");
                    }
                }

                for (int j = collectionA.Items.Count - 1; j >= 0; j--)
                {
                    ScriptableObject scriptableObjectA = collectionA.Items[j];
                    ISOCItem itemA = scriptableObjectA as ISOCItem;

                    for (int k = 0; k < collectionA.Items.Count; k++)
                    {
                        ScriptableObject scriptableObjectB = collectionA.Items[k];
                        ISOCItem itemB = scriptableObjectB as ISOCItem;

                        if (j == k)
                            continue;

                        if (itemA.GUID == itemB.GUID)
                        {
                            itemA.GenerateNewGUID();
                            Debug.LogWarning($"Found duplicated GUID between {itemA} and {itemB}, please run the validation again to make sure this is fixed");
                        }
                    }
                }
            }
        }

        public void UpdateAutoSearchForCollections()
        {
            foreach (ScriptableObjectCollection collection in collections)
            {
                if (!collection)
                {
                    continue;
                }
                if (collection != null && !collection.AutomaticallyLoaded)
                {
                    SetAutoSearchForCollections(true);
                    return;
                }
            }

            SetAutoSearchForCollections(false);
        }

        public bool HasUniqueGUID(ISOCItem targetItem)
        {
            foreach (ScriptableObjectCollection collection in collections)
            {
                if (!collection)
                {
                    continue;
                }
                foreach (ScriptableObject scriptableObject in collection)
                {
                    if (scriptableObject is ISOCItem socItem)
                    {
                        if(!Equals(socItem, targetItem) && socItem.GUID == targetItem.GUID)
                            return false;
                    }
                }
            }

            return true;
        }

        public bool HasUniqueGUID(ScriptableObjectCollection targetCollection)
        {
            foreach (ScriptableObjectCollection collection in collections)
            {
                if (!collection)
                {
                    continue;
                }
                if (collection != targetCollection && collection.GUID == targetCollection.GUID)
                    return false;
            }

            return true;
        }
#endif
    }
}
