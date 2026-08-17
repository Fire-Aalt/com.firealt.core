using Unity.Entities;

namespace FireAlt.Core.Editor
{
    public static class InternalEditorRenderData
    {
        public static ulong GetSceneCullingMask(EntityManager em, Entity entity)
        {
            return em.HasComponent<EditorRenderData>(entity)
                ? em.GetSharedComponent<EditorRenderData>(entity).SceneCullingMask
                : 0;
        }

        public static void Set(EntityManager em, Entity entity, ulong sceneCullingMask)
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
    }
}
