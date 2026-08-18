using Unity.Entities;

namespace FireAlt.Core.Editor
{
    public static class InternalEditorRenderData
    {
        public static bool HasSceneCullingMask(EntityManager em, Entity entity)
        {
            return em.HasComponent<EditorRenderData>(entity);
        }

        public static ulong GetSceneCullingMask(EntityManager em, Entity entity)
        {
            return em.HasComponent<EditorRenderData>(entity)
                ? em.GetSharedComponent<EditorRenderData>(entity).SceneCullingMask
                : 0;
        }

        public static void SetSceneCullingMask(EntityManager em, Entity entity, ulong sceneCullingMask)
        {
            if (!em.HasComponent<EditorRenderData>(entity))
            {
                em.AddSharedComponent(entity, new EditorRenderData { SceneCullingMask = sceneCullingMask });
            }
            else
            {
                var comp = em.GetSharedComponent<EditorRenderData>(entity);
                if (comp.SceneCullingMask == sceneCullingMask) return;

                comp.SceneCullingMask = sceneCullingMask;
                em.SetSharedComponent(entity, comp);
            }
        }

        public static void RemoveSceneCullingMask(EntityManager em, Entity entity)
        {
            if (em.HasComponent<EditorRenderData>(entity)) em.RemoveComponent<EditorRenderData>(entity);
        }
    }
}
