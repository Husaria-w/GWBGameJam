using UnityEngine;

namespace GWBGameJam
{
    [RequireComponent(typeof(Camera))]
    public class AspectRatioEnforcer : MonoBehaviour
    {
        [SerializeField] private float _targetWidth = 1920f;
        [SerializeField] private float _targetHeight = 1080f;

        private Camera _camera;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            ApplyLetterbox();
        }

        private void Update()
        {
            if (Screen.width == _lastScreenWidth && Screen.height == _lastScreenHeight)
                return;
            ApplyLetterbox();
        }

        private void ApplyLetterbox()
        {
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;

            if (_targetWidth < 0.01f || _targetHeight < 0.01f)
            {
                Debug.LogError("[AspectRatioEnforcer] 目标宽/高必须大于 0，已跳过本次应用");
                return;
            }

            float targetAspect = _targetWidth / _targetHeight;
            float windowAspect = (float)Screen.width / Screen.height;
            float scaleHeight = windowAspect / targetAspect;

            Rect rect = _camera.rect;
            if (scaleHeight < 1f)
            {
                rect.width = 1f;
                rect.height = scaleHeight;
                rect.x = 0f;
                rect.y = (1f - scaleHeight) * 0.5f;
            }
            else
            {
                float scaleWidth = 1f / scaleHeight;
                rect.width = scaleWidth;
                rect.height = 1f;
                rect.x = (1f - scaleWidth) * 0.5f;
                rect.y = 0f;
            }
            _camera.rect = rect;
        }
    }
}
