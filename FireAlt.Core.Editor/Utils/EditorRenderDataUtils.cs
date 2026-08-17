using Unity.Entities;

namespace FireAlt.Core.Editor
{
    public static class EditorRenderDataUtils
    {
        public static void Set(EntityManager em, Entity entity, ulong sceneCullingMask)
        {
            if (!em.HasComponent<EditorRenderData>(entity))
            {
                em.AddSharedComponent(entity, new EditorRenderData { SceneCullingMask = sceneCullingMask });
            }
            else
            {
                var comp = em.GetSharedComponent<EditorRenderData>(entity);
                comp.SceneCullingMask = sceneCullingMask;
                em.SetSharedComponent(entity, comp);
            }
        }
    }
}