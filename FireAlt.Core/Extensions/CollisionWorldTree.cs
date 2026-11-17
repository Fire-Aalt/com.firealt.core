#if UNITY_PHYSICS
using Unity.Collections;
using Unity.Mathematics;
using Unity.Physics;

namespace FireAlt.Core
{
    /// <summary>
    /// A <see cref="CollisionWorld" /> view whose queries visit only one tree of the broadphase: the static bodies or the
    /// dynamic ones. Regular queries walk both, and a collision filter rejects bodies one by one instead of skipping a
    /// tree, which is costly when many bodies of the unwanted kind sit near the query. Get one with
    /// <see cref="CollisionWorldExtensions.Static" /> or <see cref="CollisionWorldExtensions.Dynamic" />. The queries
    /// mirror <see cref="CollisionWorld" /> and report rigid body indices of the whole world.
    /// </summary>
    public struct CollisionWorldTree : ICollidable
    {
        private BoundingVolumeHierarchy _tree;
        private Broadphase.BvhLeafProcessor _leafProcessor;

        public CollisionWorldTree(in CollisionWorld world, bool dynamic)
        {
            // Leaves are numbered within their tree, while the bodies array puts the dynamic bodies first. Like the
            // broadphase, only the static tree uses the cached mesh sub-AABBs.
            if (dynamic)
            {
                _tree = world.Broadphase.DynamicTree.BoundingVolumeHierarchy;
                _leafProcessor = new Broadphase.BvhLeafProcessor(world.Bodies);
            }
            else
            {
                _tree = world.Broadphase.StaticTree.BoundingVolumeHierarchy;
                _leafProcessor = new Broadphase.BvhLeafProcessor(world.Bodies,
                    world.Broadphase.StaticTree.MeshSubAabbs.AsArray())
                {
                    BaseRigidBodyIndex = world.NumDynamicBodies,
                };
            }
        }

        public Aabb CalculateAabb() => _tree.Domain;

        // Ray casts

        public bool CastRay(RaycastInput input) => QueryWrappers.RayCast(in this, input);

        public bool CastRay(RaycastInput input, out RaycastHit closestHit)
            => QueryWrappers.RayCast(in this, input, out closestHit);

        public bool CastRay(RaycastInput input, ref NativeList<RaycastHit> allHits)
            => QueryWrappers.RayCast(in this, input, ref allHits);

        public bool CastRay<T>(RaycastInput input, ref T collector)
            where T : struct, ICollector<RaycastHit>
        {
            if (input.Filter.IsEmpty)
            {
                return false;
            }

            input.QueryContext.InitScale();
            var leafProcessor = _leafProcessor;
            return _tree.Raycast(input, ref leafProcessor, ref collector);
        }

        // Collider casts

        public bool CastCollider(ColliderCastInput input) => QueryWrappers.ColliderCast(in this, input);

        public bool CastCollider(ColliderCastInput input, out ColliderCastHit closestHit)
            => QueryWrappers.ColliderCast(in this, input, out closestHit);

        public bool CastCollider(ColliderCastInput input, ref NativeList<ColliderCastHit> allHits)
            => QueryWrappers.ColliderCast(in this, input, ref allHits);

        public unsafe bool CastCollider<T>(ColliderCastInput input, ref T collector)
            where T : struct, ICollector<ColliderCastHit>
        {
            if (input.Collider->GetCollisionFilter().IsEmpty)
            {
                return false;
            }

            input.InitScale();
            var leafProcessor = _leafProcessor;
            return _tree.ColliderCast(input, ref leafProcessor, ref collector);
        }

        // Point distance queries

        public bool CalculateDistance(PointDistanceInput input) => QueryWrappers.CalculateDistance(in this, input);

        public bool CalculateDistance(PointDistanceInput input, out DistanceHit closestHit)
            => QueryWrappers.CalculateDistance(in this, input, out closestHit);

        public bool CalculateDistance(PointDistanceInput input, ref NativeList<DistanceHit> allHits)
            => QueryWrappers.CalculateDistance(in this, input, ref allHits);

        public bool CalculateDistance<T>(PointDistanceInput input, ref T collector)
            where T : struct, ICollector<DistanceHit>
        {
            if (input.Filter.IsEmpty)
            {
                return false;
            }

            input.QueryContext.InitScale();
            var leafProcessor = _leafProcessor;
            return _tree.Distance(input, ref leafProcessor, ref collector);
        }

        // Collider distance queries

        public bool CalculateDistance(ColliderDistanceInput input) => QueryWrappers.CalculateDistance(in this, input);

        public bool CalculateDistance(ColliderDistanceInput input, out DistanceHit closestHit)
            => QueryWrappers.CalculateDistance(in this, input, out closestHit);

        public bool CalculateDistance(ColliderDistanceInput input, ref NativeList<DistanceHit> allHits)
            => QueryWrappers.CalculateDistance(in this, input, ref allHits);

        public unsafe bool CalculateDistance<T>(ColliderDistanceInput input, ref T collector)
            where T : struct, ICollector<DistanceHit>
        {
            if (input.Collider->GetCollisionFilter().IsEmpty)
            {
                return false;
            }

            input.InitScale();
            var leafProcessor = _leafProcessor;
            return _tree.Distance(input, ref leafProcessor, ref collector);
        }

        // Shape queries, written like the ones in CollisionWorld

        public bool CheckSphere(float3 position, float radius, CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CheckSphere(in this, position, radius, filter, queryInteraction);

        public bool OverlapSphere(float3 position, float radius, ref NativeList<DistanceHit> outHits,
            CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.OverlapSphere(in this, position, radius, ref outHits, filter, queryInteraction);

        public bool OverlapSphereCustom<T>(float3 position, float radius, ref T collector, CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : struct, ICollector<DistanceHit>
            => QueryWrappers.OverlapSphereCustom(in this, position, radius, ref collector, filter, queryInteraction);

        public bool CheckCapsule(float3 point1, float3 point2, float radius, CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CheckCapsule(in this, point1, point2, radius, filter, queryInteraction);

        public bool OverlapCapsule(float3 point1, float3 point2, float radius, ref NativeList<DistanceHit> outHits,
            CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.OverlapCapsule(in this, point1, point2, radius, ref outHits, filter, queryInteraction);

        public bool OverlapCapsuleCustom<T>(float3 point1, float3 point2, float radius, ref T collector,
            CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : struct, ICollector<DistanceHit>
            => QueryWrappers.OverlapCapsuleCustom(in this, point1, point2, radius, ref collector, filter,
                queryInteraction);

        public bool CheckBox(float3 center, quaternion orientation, float3 halfExtents, CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CheckBox(in this, center, orientation, halfExtents, filter, queryInteraction);

        public bool OverlapBox(float3 center, quaternion orientation, float3 halfExtents,
            ref NativeList<DistanceHit> outHits, CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.OverlapBox(in this, center, orientation, halfExtents, ref outHits, filter,
                queryInteraction);

        public bool OverlapBoxCustom<T>(float3 center, quaternion orientation, float3 halfExtents, ref T collector,
            CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : struct, ICollector<DistanceHit>
            => QueryWrappers.OverlapBoxCustom(in this, center, orientation, halfExtents, ref collector, filter,
                queryInteraction);

        public bool SphereCast(float3 origin, float radius, float3 direction, float maxDistance, CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.SphereCast(in this, origin, radius, direction, maxDistance, filter, queryInteraction);

        public bool SphereCast(float3 origin, float radius, float3 direction, float maxDistance,
            out ColliderCastHit hitInfo, CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.SphereCast(in this, origin, radius, direction, maxDistance, out hitInfo, filter,
                queryInteraction);

        public bool SphereCastAll(float3 origin, float radius, float3 direction, float maxDistance,
            ref NativeList<ColliderCastHit> outHits, CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.SphereCastAll(in this, origin, radius, direction, maxDistance, ref outHits, filter,
                queryInteraction);

        public bool SphereCastCustom<T>(float3 origin, float radius, float3 direction, float maxDistance,
            ref T collector, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : struct, ICollector<ColliderCastHit>
            => QueryWrappers.SphereCastCustom(in this, origin, radius, direction, maxDistance, ref collector, filter,
                queryInteraction);

        public bool BoxCast(float3 center, quaternion orientation, float3 halfExtents, float3 direction,
            float maxDistance, CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.BoxCast(in this, center, orientation, halfExtents, direction, maxDistance, filter,
                queryInteraction);

        public bool BoxCast(float3 center, quaternion orientation, float3 halfExtents, float3 direction,
            float maxDistance, out ColliderCastHit hitInfo, CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.BoxCast(in this, center, orientation, halfExtents, direction, maxDistance, out hitInfo,
                filter, queryInteraction);

        public bool BoxCastAll(float3 center, quaternion orientation, float3 halfExtents, float3 direction,
            float maxDistance, ref NativeList<ColliderCastHit> outHits, CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.BoxCastAll(in this, center, orientation, halfExtents, direction, maxDistance, ref outHits,
                filter, queryInteraction);

        public bool BoxCastCustom<T>(float3 center, quaternion orientation, float3 halfExtents, float3 direction,
            float maxDistance, ref T collector, CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : struct, ICollector<ColliderCastHit>
            => QueryWrappers.BoxCastCustom(in this, center, orientation, halfExtents, direction, maxDistance,
                ref collector, filter, queryInteraction);

        public bool CapsuleCast(float3 point1, float3 point2, float radius, float3 direction, float maxDistance,
            CollisionFilter filter, QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CapsuleCast(in this, point1, point2, radius, direction, maxDistance, filter,
                queryInteraction);

        public bool CapsuleCast(float3 point1, float3 point2, float radius, float3 direction, float maxDistance,
            out ColliderCastHit hitInfo, CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CapsuleCast(in this, point1, point2, radius, direction, maxDistance, out hitInfo, filter,
                queryInteraction);

        public bool CapsuleCastAll(float3 point1, float3 point2, float radius, float3 direction, float maxDistance,
            ref NativeList<ColliderCastHit> outHits, CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
            => QueryWrappers.CapsuleCastAll(in this, point1, point2, radius, direction, maxDistance, ref outHits,
                filter, queryInteraction);

        public bool CapsuleCastCustom<T>(float3 point1, float3 point2, float radius, float3 direction,
            float maxDistance, ref T collector, CollisionFilter filter,
            QueryInteraction queryInteraction = QueryInteraction.Default)
            where T : struct, ICollector<ColliderCastHit>
            => QueryWrappers.CapsuleCastCustom(in this, point1, point2, radius, direction, maxDistance, ref collector,
                filter, queryInteraction);

        // Aabb overlap

        public bool OverlapAabb(OverlapAabbInput input, ref NativeList<int> allHits)
        {
            if (input.Filter.IsEmpty)
            {
                return false;
            }

            var hitsBefore = allHits.Length;
            var leafProcessor = _leafProcessor;
            var collector = new Broadphase.RigidBodyOverlapsCollector { RigidBodyIndices = allHits };
            _tree.AabbOverlap(input, ref leafProcessor, ref collector);
            return allHits.Length > hitsBefore;
        }
    }
}
#endif
