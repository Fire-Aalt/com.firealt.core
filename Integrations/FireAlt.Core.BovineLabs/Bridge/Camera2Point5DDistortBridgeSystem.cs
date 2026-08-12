#if BL_BRIDGE
using BovineLabs.Bridge.Camera;
using BovineLabs.Bridge.Data;
using BovineLabs.Bridge.Data.Camera;
using FireAlt.Core.Rendering;
using Unity.Entities;

namespace FireAlt.Core
{
    [UpdateInGroup(typeof(BridgeSyncSystemGroup))]
    [UpdateAfter(typeof(CameraMatrixShiftSyncSystem))]
    public partial class Camera2Point5DDistortBridgeSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            foreach (var (cameraComponent, entity) in SystemAPI.Query<RefRW<CameraBridge>>().WithEntityAccess())
            {
                var camera = cameraComponent.ValueRW.Value.Value;
                if (camera == null || !camera.TryGetComponent<Camera2Point5DDistort>(out _))
                {
                    continue;
                }

                Camera2Point5DDistort.DistortProjectionMatrix(
                    camera,
                    !EntityManager.HasComponent<CameraViewSpaceOffset>(entity));
            }
        }
    }
}
#endif
