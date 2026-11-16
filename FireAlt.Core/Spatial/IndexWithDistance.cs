using System.Collections.Generic;
using Unity.Entities;

namespace FireAlt.Core
{
    public struct IndexWithDistance
    {
        public int Index;
        public float Distance;
    }
    
    public struct EntityWithDistance
    {
        public Entity Entity;
        public float Distance;
    }
    
    public struct EntityWithDistanceComparer : IComparer<EntityWithDistance>
    {
        public int Compare(EntityWithDistance x, EntityWithDistance y)
        {
            return x.Distance.CompareTo(y.Distance);
        }
    }
}