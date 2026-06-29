using UnityEngine;

namespace GWBGameJam
{
    [CreateAssetMenu(fileName = "MonsterData", menuName = "GWBGameJam/MonsterData")]
    public class MonsterData : ScriptableObject
    {
        public string MonsterName;
        public Sprite IdleSprite;
        public Sprite HitSprite;
        public DoughState TargetDoughState = DoughState.Medium;

        [Tooltip("额外整体缩放倍率，乘在透视 ScaleCurve 之上。sprite 太小时调大此值")]
        [Min(0.01f)]
        public float DisplayScale = 1f;
    }
}
