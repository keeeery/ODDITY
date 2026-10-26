using UnityEngine;

namespace TeamOdd.Boss
{
    // 염동력 오브젝트가 닿기만 해도 밟힌 것처럼 파편화되는 보스 부위 콜라이더 표시 (다리, 발 등)
    // 이 표시가 없는 부위에 닿으면 파편화하지 않고 물리적으로 부딪히기만 함
    [RequireComponent(typeof(Collider))]
    public sealed class BossShatterCollider : MonoBehaviour
    {
    }
}
