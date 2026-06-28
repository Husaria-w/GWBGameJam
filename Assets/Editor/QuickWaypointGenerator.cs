using UnityEditor;
using UnityEngine;

namespace GWBGameJam
{
    /// <summary>
    /// 快速生成透视球道点位，无需设置 PolygonCollider2D。
    /// 基于屏幕水平分 5 条球道，垂直按透视比例生成。
    /// </summary>
    public class QuickWaypointGenerator : EditorWindow
    {
        [SerializeField] private MonsterConfig _monsterConfig;
        [SerializeField] private LaneWaypointConfig _waypointConfig;

        [Header("透视参数")]
        [SerializeField] private float _topY = 4.5f;       // 远处（上）
        [SerializeField] private float _bottomY = -4.0f;    // 近处（下）
        [SerializeField] private float _topWidth = 3.0f;    // 远处总宽度
        [SerializeField] private float _bottomWidth = 7.0f; // 近处总宽度

        [MenuItem("GWBGameJam/Quick Waypoint Generator")]
        public static void ShowWindow()
        {
            GetWindow<QuickWaypointGenerator>("Quick Waypoint Generator");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("快速球道点位生成器", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            _monsterConfig  = (MonsterConfig)EditorGUILayout.ObjectField("MonsterConfig",  _monsterConfig,  typeof(MonsterConfig),  false);
            _waypointConfig = (LaneWaypointConfig)EditorGUILayout.ObjectField("WaypointConfig", _waypointConfig, typeof(LaneWaypointConfig), false);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("透视参数（调整直到匹配你的场景）");
            _topY     = EditorGUILayout.FloatField("顶端 Y（远处）", _topY);
            _bottomY  = EditorGUILayout.FloatField("底端 Y（近处）", _bottomY);
            _topWidth = EditorGUILayout.FloatField("远处总宽度", _topWidth);
            _bottomWidth = EditorGUILayout.FloatField("近处总宽度", _bottomWidth);

            EditorGUILayout.Space(6);

            GUI.enabled = _monsterConfig != null && _waypointConfig != null;
            if (GUILayout.Button("生成并烘焙", GUILayout.Height(30)))
                Generate();

            if (GUILayout.Button("场景预览（在 Scene 中显示 Gizmos）"))
                SceneView.RepaintAll();

            GUI.enabled = true;
        }

        private void Generate()
        {
            int laneCount = 5;
            int stepCount = _monsterConfig.MoveStepCount;

            if (_waypointConfig.Lanes == null || _waypointConfig.Lanes.Length != laneCount)
                _waypointConfig.Lanes = new LaneWaypoints[laneCount];

            for (int l = 0; l < laneCount; l++)
            {
                float tLane = l / (float)(laneCount - 1); // 0 ~ 1

                if (_waypointConfig.Lanes[l] == null)
                    _waypointConfig.Lanes[l] = new LaneWaypoints();
                _waypointConfig.Lanes[l].Positions = new Vector2[stepCount];

                for (int i = 0; i < stepCount; i++)
                {
                    float tStep = i / (float)(stepCount - 1); // 0 ~ 1（上→下）
                    float y = Mathf.Lerp(_topY, _bottomY, tStep);

                    // 当前高度的球道宽度（线性插值）
                    float laneWidthAtY = Mathf.Lerp(_topWidth, _bottomWidth, tStep) / laneCount;
                    float totalWidthAtY = Mathf.Lerp(_topWidth, _bottomWidth, tStep);
                    float x = Mathf.Lerp(-totalWidthAtY * 0.5f, totalWidthAtY * 0.5f, tLane) + laneWidthAtY * 0.5f;
                    // 居中偏移修正：使每条球道的x在正确的区间内
                    x = -totalWidthAtY * 0.5f + tLane * totalWidthAtY + laneWidthAtY * 0.5f;

                    _waypointConfig.Lanes[l].Positions[i] = new Vector2(x, y);
                }
            }

            _waypointConfig.RecordedStepCount = stepCount;
            EditorUtility.SetDirty(_waypointConfig);
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();

            Debug.Log($"[QuickWaypoint] 已生成 {laneCount} 条球道 × {stepCount} 步 = {laneCount * stepCount} 个点位");
        }

        // 在 Scene 中显示点位预览
        private void OnDrawGizmos()
        {
            if (_waypointConfig == null || _waypointConfig.Lanes == null) return;

            Color[] colors = { Color.red, Color.green, Color.blue, Color.yellow, Color.cyan };
            for (int l = 0; l < _waypointConfig.Lanes.Length; l++)
            {
                var lane = _waypointConfig.Lanes[l];
                if (lane?.Positions == null) continue;
                Gizmos.color = colors[Mathf.Min(l, colors.Length - 1)];
                foreach (var pos in lane.Positions)
                    Gizmos.DrawSphere(pos, 0.12f);
            }
        }
    }
}
