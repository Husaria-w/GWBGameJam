using UnityEngine;
using UnityEngine.SceneManagement;

namespace GWBGameJam
{
    public class SceneLoader : MonoBehaviour
    {
        public void LoadGame() => SceneManager.LoadScene("Game");
    }
}