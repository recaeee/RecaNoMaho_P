using System;
using UnityEngine;

namespace Unity.FilmInternalUtilities.Tests {

[Serializable]
internal class DummyScriptableObject : ScriptableObject {
    [SerializeField] internal ExposedReference<GameObject> exposedGameObject;
}

} //end namespace

