using System.Collections;
using UnityEngine;

namespace GWBGameJam
{
    public class MonsterController : MonoBehaviour
    {
        [SerializeField] private Transform _visual;
        [SerializeField] private SpriteRenderer _spriteRenderer;

        public MonsterData Data { get; private set; }
        public int LaneIndex { get; private set; }

        private MonsterConfig _config;
        private LaneManager _laneManager;
        private int _posIndex;
        private float _moveTimer;
        private bool _isMoving;
        private int _targetPosIndex;
        private Vector2 _moveFromPos;
        private Vector2 _moveToPos;
        private float _moveProgress;

        // _config may be null before Initialize; guard before reading
        public bool IsPendingMove => _config != null && !_isMoving && _moveTimer <= _config.PendingMoveThreshold;

        // ╔══════════════════ DEBUG 区域：怪物位置 ══════════════════╗
        // 整块删除即可移除；DebugLogPosition 改 false 关闭本区域
        private const bool DebugLogPosition = true;
        private const float DebugPositionInterval = 1f;
        private float _debugPosTimer;
        // ╚══════════════════════════════════════════════════════════╝

        private void Awake()
        {
            if (_visual == null)
                Debug.LogError("[MonsterController] _visual 未赋值");
            if (_spriteRenderer == null)
                Debug.LogError("[MonsterController] _spriteRenderer 未赋值");
        }

        public void Initialize(int laneIndex, MonsterData data, MonsterConfig config, LaneManager laneManager)
        {
            LaneIndex = laneIndex;
            Data = data;
            _config = config;
            _laneManager = laneManager;
            _posIndex = 0;
            _moveTimer = config.MoveIntervalSeconds;

            _spriteRenderer.sprite = data.IdleSprite;
            transform.position = _laneManager.GetWaypoint(laneIndex, 0);
            ApplyScale(0f);
        }

        private void Update()
        {
            if (_config == null) return;
            if (_isMoving)
                UpdateMovement();
            else
                UpdateTimer();

            // ╔══════════════════ DEBUG 区域：怪物位置 ══════════════════╗
#pragma warning disable 162
            if (DebugLogPosition)
            {
                _debugPosTimer += Time.deltaTime;
                if (_debugPosTimer >= DebugPositionInterval)
                {
                    _debugPosTimer = 0f;
                    string dir = System.IO.Path.Combine(Application.dataPath, "../Debug");
                    System.IO.Directory.CreateDirectory(dir);
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(dir, "MonsterPosition.log"),
                        $"[{System.DateTime.Now:HH:mm:ss}] lane={LaneIndex} posIndex={_posIndex} pos={(Vector2)transform.position} scale={(_visual != null ? _visual.localScale.x : 0f):F3}\n");
                }
            }
#pragma warning restore 162
            // ╚══════════════════════════════════════════════════════════╝
        }

        private void UpdateTimer()
        {
            _moveTimer -= Time.deltaTime;
            if (_moveTimer <= 0f)
                StartMoving();
        }

        private void StartMoving()
        {
            if (_posIndex >= _config.MoveStepCount - 1)
            {
                ReachedTable();
                return;
            }

            _targetPosIndex = _posIndex + 1;
            _moveFromPos = _laneManager.GetWaypoint(LaneIndex, _posIndex);
            _moveToPos = _laneManager.GetWaypoint(LaneIndex, _targetPosIndex);
            _moveProgress = 0f;
            _isMoving = true;
        }

        private void UpdateMovement()
        {
            _moveProgress = Mathf.Min(_moveProgress + Time.deltaTime / _config.MoveDuration, 1f);
            transform.position = Vector2.Lerp(_moveFromPos, _moveToPos, _moveProgress);
            ApplyScale(Mathf.Lerp(_posIndex, _targetPosIndex, _moveProgress));

            if (_moveProgress >= 1f)
                FinishMoving();
        }

        private void FinishMoving()
        {
            _isMoving = false;
            _posIndex = _targetPosIndex;
            _moveTimer = _config.MoveIntervalSeconds;
        }

        private void ApplyScale(float posLerp)
        {
            if (_visual == null) return;
            float displayScale = Data != null ? Data.DisplayScale : 1f;
            _visual.localScale = Vector3.one * (_config.ScaleCurve.Evaluate(posLerp) * displayScale);
        }

        private void ReachedTable()
        {
            EventBus<OnMonsterReachedTable>.Publish(new OnMonsterReachedTable(LaneIndex));
            Destroy(gameObject);
        }

        public void Defeat()
        {
            EventBus<OnMonsterDefeated>.Publish(new OnMonsterDefeated(LaneIndex));
            Destroy(gameObject);
        }

        public void TriggerWrongHitFeedback()
        {
            StartCoroutine(FlashCoroutine());
        }

        private IEnumerator FlashCoroutine()
        {
            for (int i = 0; i < _config.WrongHitFlashCount; i++)
            {
                _spriteRenderer.sprite = Data.HitSprite;
                yield return new WaitForSeconds(_config.WrongHitFlashDuration);
                _spriteRenderer.sprite = Data.IdleSprite;
                yield return new WaitForSeconds(_config.WrongHitFlashDuration);
            }
        }

        public Vector2 GetTargetPosition()
        {
            if (_config == null) return Vector2.zero;
            if (IsPendingMove && _posIndex < _config.MoveStepCount - 1)
                return _laneManager.GetWaypoint(LaneIndex, _posIndex + 1);
            return _laneManager.GetWaypoint(LaneIndex, _posIndex);
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }
    }
}
