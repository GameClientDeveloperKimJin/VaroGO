using UnityEditor;
using UnityEngine;

namespace VaroGO.Client.EditorTools
{
    // 흙 경사로 인스펙터: 조작 안내 + 현재 길이·높이 차·경사 각도
    [CustomEditor(typeof(TerrainRampMarker))]
    [CanEditMultipleObjects]
    class TerrainRampMarkerEditor : Editor
    {
        const float MaxWalkable = 45f; // 플레이어 CharacterController slopeLimit

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("W 이동 · E(Y축) 방향 · R 스케일 X = 폭, Y = 높이 차, Z = 길이\n파란 화살표(Z) 앞쪽이 위 끝입니다.", MessageType.None);
            var m = (TerrainRampMarker)target;
            EditorGUILayout.LabelField("길이", m.Length.ToString("0.0") + " m");
            EditorGUILayout.LabelField("높이 차", m.Rise.ToString("0.0") + " m");
            EditorGUILayout.LabelField("폭", m.Width.ToString("0.0") + " m");
            EditorGUILayout.LabelField("경사", m.SlopeDegrees.ToString("0") + "°");
            if (m.SlopeDegrees > MaxWalkable)
                EditorGUILayout.HelpBox($"경사가 {MaxWalkable:0}°를 넘어서 플레이어가 오르지 못합니다. 길이(Z)를 늘리거나 높이 차(Y)를 줄여 주세요.", MessageType.Warning);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
