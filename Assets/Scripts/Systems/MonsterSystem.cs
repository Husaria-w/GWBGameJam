using UnityEngine;

namespace GWBGameJam
{
    public class MonsterSystem : MonoBehaviour
    {
        [SerializeField] private MonsterConfig _config;
        [SerializeField] private LaneManager _laneManager;
        [SerializeField] private Transform _monsterContainer;
        [SerializeField] private MonsterController _monsterPrefab;

        private readonly MonsterController[] _monsters = new MonsterController[5];
        private bool _hasConfigError;

        private void Awake()
        {
            ValidateConfig();
        }

        private void ValidateConfig()
        {
            if (_config == null)
            {
                Debug.LogError("[MonsterSystem] MonsterConfig 未赋值");
                _hasConfigError = true;
            }
            if (_laneManager == null)
            {
                Debug.LogError("[MonsterSystem] LaneManager 未赋值");
                _hasConfigError = true;
            }
            if (_monsterContainer == null)
            {
                Debug.LogError("[MonsterSystem] MonsterContainer Transform 未赋值");
                _hasConfigError = true;
            }
            if (_monsterPrefab == null)
            {
                Debug.LogError("[MonsterSystem] MonsterController Prefab 未赋值");
                _hasConfigError = true;
            }
            if (!_hasConfigError)
                _config.Validate();
        }

        private void OnEnable()
        {
            EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
        }

        private void OnDestroy()
        {
            EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
        }

        public bool SpawnMonster(int laneIndex, MonsterData data)
        {
            if (_hasConfigError) return false;
            if (laneIndex < 0 || laneIndex >= 5) return false;
            if (IsLaneOccupied(laneIndex)) return false;
            if (data == null)
            {
                Debug.LogWarning($"[MonsterSystem] SpawnMonster: lane {laneIndex} 的 MonsterData 为 null");
                return false;
            }

            MonsterController mc = Instantiate(_monsterPrefab, _monsterContainer);
            mc.Initialize(laneIndex, data, _config, _laneManager);
            _monsters[laneIndex] = mc;

            // ╔══════════════════ DEBUG 区域：怪物生成点 ══════════════════╗
            // 整块删除即可移除；DebugLogSpawn 改 false 关闭本区域
#pragma warning disable 162
            const bool DebugLogSpawn = true;
            if (DebugLogSpawn)
            {
                string dir = System.IO.Path.Combine(Application.dataPath, "../Debug");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(dir, "MonsterSpawn.log"),
                    $"[{System.DateTime.Now:HH:mm:ss}] lane={laneIndex} type={data.name} spawnPos={(Vector2)mc.transform.position}\n");
            }
#pragma warning restore 162
            // ╚════════════════════════════════════════════════════════════╝

            EventBus<OnMonsterSpawned>.Publish(new OnMonsterSpawned(laneIndex, data));
            return true;
        }

        public MonsterController GetMonsterInLane(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= 5) return null;
            return _monsters[laneIndex];
        }

        public bool IsLaneOccupied(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= 5) return false;
            // Unity's null check returns true for destroyed objects
            return _monsters[laneIndex] != null;
        }

        public void DefeatMonster(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= 5) return;
            var monster = _monsters[laneIndex];
            if (monster == null) return;
            _monsters[laneIndex] = null;
            monster.Defeat();
        }

        public void TriggerWrongHitFeedback(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= 5) return;
            _monsters[laneIndex]?.TriggerWrongHitFeedback();
        }

        public Vector2 GetTargetPosition(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= 5) return Vector2.zero;
            var monster = _monsters[laneIndex];
            if (monster == null) return Vector2.zero;
            return monster.GetTargetPosition();
        }

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            // TimeScale=0 on pause naturally freezes all Update timers and WaitForSeconds coroutines
        }
    }
}
