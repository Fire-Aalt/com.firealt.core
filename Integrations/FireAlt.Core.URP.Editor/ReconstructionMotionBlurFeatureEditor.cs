using UnityEditor;

namespace FireAlt.Core.URP.Editor
{
    [CustomEditor(typeof(ReconstructionMotionBlurFeature))]
    internal sealed class ReconstructionMotionBlurFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Reconstruction Motion Blur is controlled by the Reconstruction Motion Blur Volume Override. "
                + "Turn off URP's Motion Blur override where this one is used.",
                MessageType.Info);
        }
    }
}
