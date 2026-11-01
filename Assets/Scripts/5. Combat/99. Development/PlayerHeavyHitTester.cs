using Study.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TeamOdd.Combat.Development
{
    // 피격 연출 확인용, K는 강공격(Knockdown) L은 일반 피격을 플레이어에게 준다.
    public class PlayerHeavyHitTester : MonoBehaviour
    {
        [SerializeField] private Key heavyHitKey = Key.K;
        [SerializeField] private Key normalHitKey = Key.L;
        [SerializeField, Min(0)] private int heavyDamage = 25;
        [SerializeField, Min(0)] private int normalDamage = 8;
        [Tooltip("비어 있으면 씬의 플레이어에서 찾는다.")]
        [SerializeField] private CombatEntity target;

        private void Update()
        {
            if (heavyHitKey.GetKeyDown()) Hit(heavyDamage, HitReaction.Knockdown);
            else if (normalHitKey.GetKeyDown()) Hit(normalDamage, HitReaction.Knockback);
        }

        private void Hit(int damage, HitReaction reaction)
        {
            if (target == null)
            {
                Player.Player player = FindAnyObjectByType<Player.Player>();
                if (player != null) target = player.GetComponent<CombatEntity>();
                if (target == null) return;
            }

            Debug.Log($"[피격 테스트] {target.name}: 피해 {damage}, 반응 {reaction}", this);
            // 공격자는 정면 1m에 있다고 치고, 밀지 않고 피격 모션만 본다
            DamageInfo info = new DamageInfo(damage, target.transform.position, target.Collider, reaction,
                attackerPosition: target.transform.position + target.transform.forward);
            CombatManager.Instance.AddCombatEvent(new DamageEvent(null, target, info));
        }
    }
}
