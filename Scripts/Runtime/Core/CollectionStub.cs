using UnityEngine;

namespace BrunoMikoski.ScriptableObjectCollections
{
    // Gives a collection a Resources path without the collection living in Resources
    public class CollectionStub : ScriptableObject
    {
        public ScriptableObjectCollection Collection;
    }
}
