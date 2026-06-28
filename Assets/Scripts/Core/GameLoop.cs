using UnityEngine;
using UnityEngine.SceneManagement;

namespace GWBGameJam
{
    public class GameLoop : MonoBehaviour
    {
        [SerializeField] private GameLoopConfig _config;

        private GameState _currentState = GameState.MainMenu;
        private int _currentLevelIndex;
        private bool _levelClearedThisFrame;
        private bool _tableDestroyedThisFrame;

        private void OnEnable()
        {
            EventBus<OnTableDestroyed>.Subscribe(HandleTableDestroyed);
            EventBus<OnLevelCleared>.Subscribe(HandleLevelCleared);
        }

        private void OnDestroy()
        {
            EventBus<OnTableDestroyed>.Unsubscribe(HandleTableDestroyed);
            EventBus<OnLevelCleared>.Unsubscribe(HandleLevelCleared);
        }

        private void Start()
        {
            StartGame(); // Game 场景加载即开始第一关
        }

        private void Update()
        {
            if (_tableDestroyedThisFrame || _levelClearedThisFrame)
            {
                // 同帧冲突：OnTableDestroyed 优先
                if (_tableDestroyedThisFrame)
                    TransitionTo(GameState.Death);
                else
                    HandleLevelComplete();

                _tableDestroyedThisFrame = false;
                _levelClearedThisFrame = false;
            }

            HandleDevSpeed();
            HandlePauseInput();
        }

        // ── 公开方法（供 UI 按钮调用）──────────────────────────────

        public void StartGame()
        {
            _currentLevelIndex = 0;
            BeginLevel();
        }

        public void ResumeGame()
        {
            if (_currentState != GameState.Paused) return;
            Time.timeScale = _devSpeedActive ? _config.DevSpeedMultiplier : 1f;
            TransitionTo(GameState.Playing);
        }

        public void GoToMainMenu()
        {
            Time.timeScale = 1f;
            _devSpeedActive = false;
            SceneManager.LoadScene("MainMenu");
        }

        public void AdvanceFromTransition()
        {
            if (_currentState != GameState.LevelTransition) return;
            _currentLevelIndex++;
            BeginLevel();
        }

        // ── 内部状态流转 ────────────────────────────────────────────

        private void BeginLevel()
        {
            TransitionTo(GameState.Playing);
            EventBus<OnLevelStarted>.Publish(new OnLevelStarted(_currentLevelIndex));
        }

        private void HandleLevelComplete()
        {
            bool isLastLevel = _currentLevelIndex >= _config.TotalLevels - 1;
            TransitionTo(isLastLevel ? GameState.Victory : GameState.LevelTransition);
        }

        private void TransitionTo(GameState newState)
        {
            var prev = _currentState;
            _currentState = newState;
            EventBus<OnGameStateChanged>.Publish(new OnGameStateChanged(newState, prev));
        }

        // ── 事件处理（标记本帧，延迟到 Update 统一处理）────────────

        private void HandleTableDestroyed(OnTableDestroyed _) => _tableDestroyedThisFrame = true;
        private void HandleLevelCleared(OnLevelCleared _) => _levelClearedThisFrame = true;

        // ── 暂停 ────────────────────────────────────────────────────

        private void HandlePauseInput()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (_currentState == GameState.Playing)
            {
                Time.timeScale = 0f;
                TransitionTo(GameState.Paused);
            }
        }

        // ── Dev 加速 ────────────────────────────────────────────────

        private bool _devSpeedActive;

        private void HandleDevSpeed()
        {
            if (_currentState != GameState.Playing) return;
            if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl)) return;
            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) return;
            if (!Input.GetKeyDown(KeyCode.P)) return;

            _devSpeedActive = !_devSpeedActive;
            Time.timeScale = _devSpeedActive ? _config.DevSpeedMultiplier : 1f;
        }

        public GameState CurrentState => _currentState;
        public int CurrentLevelIndex => _currentLevelIndex;
    }
}
