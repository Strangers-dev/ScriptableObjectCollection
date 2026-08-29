using System;
using UnityEngine;

namespace BrunoMikoski.ScriptableObjectCollections
{
    public class ScriptableObjectCollectionItem : ScriptableObject, IComparable<ScriptableObjectCollectionItem>, ISOCItem
    {
        [SerializeField, HideInInspector]
        private LongGuid guid;
        public LongGuid GUID
        {
            get
            {
                if (guid.IsValid())
                    return guid;
                
                GenerateNewGUID();
                return guid;
            }
        }

        [SerializeField, CollectionReferenceLongGuid]
        private LongGuid collectionGUID;


        [NonSerialized]
        private bool hasCachedCollection;
        [NonSerialized]
        private ScriptableObjectCollection cachedCollection;
        public ScriptableObjectCollection Collection
        {
            get
            {
                if (!hasCachedCollection)
                {
                    if (collectionGUID.IsValid())
                    {
                        cachedCollection = CollectionsRegistry.Instance.GetCollectionByGUID(collectionGUID);
                    }
                    else
                    {
                        CollectionsRegistry.Instance.TryGetCollectionFromItemType(GetType(), out cachedCollection);
                        if (cachedCollection != null)
                        {
                            collectionGUID = cachedCollection.GUID;
                            ObjectUtility.SetDirty(this);
                        }
                    }

                    hasCachedCollection = cachedCollection != null;
                }
                
                return cachedCollection;
            }
        }
        
        [NonSerialized] private bool didCacheIndex;
        [NonSerialized] private int cachedIndex;
        public int Index
        {
            get
            {
                if (!didCacheIndex)
                {
                    didCacheIndex = true;
                    cachedIndex = Collection.IndexOf(this);
                }
                return cachedIndex;
            }
        }

        public void SetCollection(ScriptableObjectCollection collection)
        {
            cachedCollection = collection;
            collectionGUID = cachedCollection.GUID;
            ObjectUtility.SetDirty(this);
        }

        public void ClearCollection()
        {
            cachedCollection = null;
            hasCachedCollection = false;
            collectionGUID = default;
            ObjectUtility.SetDirty(this);
        }
        
        public void GenerateNewGUID()
        {
            guid = LongGuid.NewGuid();
            ObjectUtility.SetDirty(this);
        }

        public int CompareTo(ScriptableObjectCollectionItem other)
        {
            return string.Compare(name, other.name, StringComparison.Ordinal);
        }
    }
}