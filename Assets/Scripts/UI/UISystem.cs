using TMPro;
using UnityEngine;

namespace GWBGameJam
{
    public class UISystem : MonoBehaviour
    {
        [Header("Canvas Panels")]
        [SerializeField] private GameObject _hudCanvas;
        [SerializeField] private GameObject _pauseMenuCanvas;
        [SerializeField] private GameObject _levelTransitionCanvas;
        [SerializeField] private GameObject _deathCanvas;
        [SerializeField] private GameObject _victoryCanvas;

        [Header("Dynamic Text")]
        [SerializeField] private TMP_Text _levelTransitionText;

        [Header("References")]
        [SerializeField] private LevelSystem _levelSystem;

        private int _clearedLevelIndex;
        private bool _hasConfigError;

        private void Awake()
        {
            ValidateConfig();
        }

        private void ValidateConfig()
        {
            if (_hudCanvas == null)              { Debug.LogError("[UISystem] HUD_Canvas 未赋值");              _hasConfigError = true; }
            if (_pauseMenuCanvas == null)        { Debug.LogError("[UISystem] PauseMenu_Canvas 未赋值");        _hasConfigError = true; }
            if (_levelTransitionCanvas == null)  { Debug.LogError("[UISystem] LevelTransition_Canvas 未赋值"); _hasConfigError = true; }
            if (_deathCanvas == null)            { Debug.LogError("[UISystem] Death_Canvas 未赋值");            _hasConfigError = true; }
            if (_victoryCanvas == null)          { Debug.LogError("[UISystem] Victory_Canvas 未赋值");          _hasConfigError = true; }
            if (_levelSystem == null)            Debug.LogWarning("[UISystem] LevelSystem 未赋值，过关文本将无法显示关卡编号");
            if (_levelTransitionText == null)    Debug.LogWarning("[UISystem] LevelTransitionText 未赋值，过关文本不会更新");
        }

        private void OnEnable()
        {
            EventBus<OnGameStateChanged>.Subscribe(HandleGameStateChanged);
            EventBus<OnLevelCleared>.Subscribe(HandleLevelCleared);
        }

        private void OnDestroy()
        {
            EventBus<OnGameStateChanged>.Unsubscribe(HandleGameStateChanged);
            EventBus<OnLevelCleared>.Unsubscribe(HandleLevelCleared);
        }

        private void HandleLevelCleared(OnLevelCleared e)
        {
            if (_levelSystem != null)
                _clearedLevelIndex = _levelSystem.GetCurrentLevelIndex();
        }

        private void HandleGameStateChanged(OnGameStateChanged e)
        {
            if (_hasConfigError) return;

            bool isPlaying = e.NewState == GameState.Playing;
            bool isPaused  = e.NewState == GameState.Paused;

            _hudCanvas.SetActive(isPlaying || isPaused);
            _pauseMenuCanvas.SetActive(isPaused);
            _levelTransitionCanvas.SetActive(e.NewState == GameState.LevelTransition);
            _deathCanvas.SetActive(e.NewState == GameState.Death);
            _victoryCanvas.SetActive(e.NewState == GameState.Victory);

            if (e.NewState == GameState.LevelTransition && _levelTransitionText != null)
                _levelTransitionText.text = $"Level {_clearedLevelIndex + 1} Clear!";
        }
    }
}
