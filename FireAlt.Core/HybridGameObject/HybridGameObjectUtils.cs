using Unity.Mathematics;
using UnityEngine;

namespace FireAlt.Core
{
    public static class HybridGameObjectUtils
    {
        public static void Decompose(in float4x4 matrix, out float3 position, out quaternion rotation, out float3 scale)
        {
            position = matrix.c3.xyz;
            scale = new float3(math.length(matrix.c0.xyz), math.length(matrix.c1.xyz), math.length(matrix.c2.xyz));
            rotation = new quaternion(new float3x3(matrix.c0.xyz / scale.x, matrix.c1.xyz / scale.y, matrix.c2.xyz / scale.z));
        }

        public static void ApplyPose(Transform transform, in float4x4 matrix)
        {
            Decompose(matrix, out var position, out var rotation, out var scale);
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = scale;
        }

        public const HideFlags ReadOnlyFlags = HideFlags.DontSave | HideFlags.NotEditable;

        public static void SetHideFlags(GameObject root, HideFlags flags)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.hideFlags = flags;
            }
        }

        public static void Destroy(Object obj)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }
    }
}
