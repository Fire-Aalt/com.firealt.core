#if UNITY_PHYSICS
using Unity.Physics;

namespace FireAlt.Core
{
    public static class CollisionWorldExtensions
    {
        /// <summary> Queries that visit only static bodies. See <see cref="CollisionWorldTree" />. </summary>
        public static CollisionWorldTree Static(this in CollisionWorld world) => new(world, false);

        /// <summary> Queries that visit only dynamic bodies. See <see cref="CollisionWorldTree" />. </summary>
        public static CollisionWorldTree Dynamic(this in CollisionWorld world) => new(world, true);
    }
}
#endif
