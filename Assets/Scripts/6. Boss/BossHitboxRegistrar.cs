using System.Collections.Generic;
using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Boss
{
    // 보스 최상위 오브젝트 아래의 콜라이더(부위 콜라이더, 이동용 콜라이더)를 모두 같은 피격 대상으로 MonsterRegistry에 등록
    // 플레이어 공격은 맞은 콜라이더로 레지스트리를 조회하므로, 몸 어디에 맞아도 보스가 피격됨
    // 피격 대상이 직접 등록하는 콜라이더(ICombatable.Collider)와 트리거는 등록하지 않음
    public sealed class BossHitboxRegistrar : MonoBehaviour
    {
        private readonly List<Collider> registered = new List<Collider>();
        private ICombatable owner;

        private void Awake()
        {
            owner = GetComponent<ICombatable>();
        }

        private void OnEnable()
        {
            if (owner == null)
            {
                Debug.LogWarning($"{name}: 같은 오브젝트에 ICombatable이 없어 피격 콜라이더를 등록하지 않습니다.", this);
                return;
            }

            foreach (Collider hitbox in GetComponentsInChildren<Collider>())
            {
                if (hitbox.isTrigger || hitbox == owner.Collider) continue;
                if (MonsterRegistry.TryGetMonster(hitbox, out _)) continue;

                MonsterRegistry.RegisterMonster(hitbox, owner);
                registered.Add(hitbox);
            }
        }

        private void OnDisable()
        {
            foreach (Collider hitbox in registered)
                MonsterRegistry.UnregisterMonster(hitbox);

            registered.Clear();
        }
    }
}
