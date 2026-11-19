using System;
using Unity.Entities;
using UnityEngine;

namespace FireAlt.Core
{
    [DisallowMultipleComponent]
    public sealed class HybridGameObject : MonoBehaviour
    {
        [NonSerialized] public Entity Entity;
        [NonSerialized] public World World;
    }
}
