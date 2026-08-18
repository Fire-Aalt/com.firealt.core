using System;
using Unity.Collections;
using Unity.Entities;

namespace FireAlt.Core
{
    public static class EntityManagerExtensions
    {        
        private const EntityQueryOptions QUERY_OPTIONS = EntityQueryOptions.IncludeSystems;
        
        public static bool TryGetUnmanagedSingleton<T>(this EntityManager em, out T singleton, bool completeDependency = true)
            where T : unmanaged, IComponentData
        {
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<T>().WithOptions(QUERY_OPTIONS).Build(em);
            
            if (completeDependency || query.HasFilter())
            {
                query.CompleteDependency();
            }

            if (query.IsEmpty)
            {
                singleton = default;
                return false;
            }

            singleton = query.GetSingleton<T>();
            return true;
        }
        
        public static T GetUnmanagedSingleton<T>(this EntityManager em, bool completeDependency = true)
            where T : unmanaged, IComponentData
        {
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<T>().WithOptions(QUERY_OPTIONS).Build(em);
            if (completeDependency || query.HasFilter())
            {
                query.CompleteDependency();
            }

            return query.GetSingleton<T>();
        }
        
        public static bool TryGetBuffer<T>(this EntityManager em, Entity entity, out DynamicBuffer<T> buffer)
            where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(entity))
            {
                buffer = default;
                return false;
            }
            buffer = em.GetBuffer<T>(entity);
            return true;
        }
        
        // From https://forum.unity.com/threads/really-hoped-for-refrw-refro-getcomponentrw-ro-entity.1369275/
        /// <summary>
        /// Gets the value of a component for an entity associated with a system.
        /// </summary>
        /// <param name="system">The system handle.</param>
        /// <typeparam name="T">The type of component to retrieve.</typeparam>
        /// <returns>A <see cref="RefRW{T}"/> struct of type T containing access to the component value.</returns>
        /// <exception cref="ArgumentException">Thrown if the component type has no fields.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the system isn't from thie world.</exception>
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(BurstCompatibleComponentData) })]
        public static unsafe RefRW<T> GetComponentDataRW<T>(this EntityManager entityManager, Entity entity) where T : unmanaged, IComponentData
        {
            var access = entityManager.GetUncheckedEntityDataAccess();

            var typeIndex = TypeManager.GetTypeIndex<T>();
            var data = access->GetComponentDataRW_AsBytePointer(entity, typeIndex);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            return new RefRW<T>(data, access->DependencyManager->Safety.GetSafetyHandle(typeIndex, false));
#else
            return new RefRW<T>(data);
#endif
        }
    }
}