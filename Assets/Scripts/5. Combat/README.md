# Combat 시스템 사용 가이드

## 개요

이 폴더는 피해와 회복을 `CombatEvent`로 만들어 큐에 넣고, `CombatManager`가 이벤트를 순서대로 실행하는 기능을 제공합니다.

현재 구현 범위는 다음과 같습니다.

- 피해 이벤트 생성 및 실행
- 회복 이벤트 생성 및 실행
- 이벤트 실행 완료 알림
- Collider로 전투 대상 조회

HP의 최솟값·최댓값, 사망, 방어력, 치명타, 중복 피격 방지 등의 규칙은 이 시스템에서 처리하지 않습니다. 각 `ICombatable` 구현체가 필요한 규칙을 직접 처리해야 합니다.

## 구성 파일

| 파일 | 역할 |
| --- | --- |
| `ICombatable.cs` | 피해와 회복을 받을 수 있는 객체의 공통 규약 |
| `CombatEvent.cs` | 피해·회복 이벤트와 이벤트 데이터 정의 |
| `CombatManager.cs` | 이벤트를 FIFO 큐에 저장하고 실행 |
| `1. Registry/MonsterRegistry.cs` | Collider와 `ICombatable` 구현체를 연결하여 조회 |

모든 타입은 `TeamOdd.Combat` 네임스페이스에 있습니다.

## 처리 흐름

1. 공격 또는 회복을 발생시키는 코드가 `DamageEvent` 또는 `HealEvent`를 생성합니다.
2. 생성한 이벤트를 `CombatManager.Instance.AddCombatEvent(...)`로 큐에 추가합니다.
3. `CombatManager.Update()`가 큐에 들어온 이벤트를 등록 순서대로 실행합니다.
4. 대상의 `TakeDamage(...)` 또는 `TakeHeal(...)`가 호출됩니다.
5. 처리가 끝나면 `OnDamageEventExecuted` 또는 `OnHealEventExecuted`가 호출됩니다.

`CombatManager`는 `SingletonBase<CombatManager>`를 사용합니다. 씬에 미리 배치하지 않아도 `CombatManager.Instance`에 처음 접근할 때 생성되며, 생성된 오브젝트는 씬 전환 시 유지됩니다.

## 1. 전투 대상 구현하기

피해나 회복을 받을 컴포넌트는 `ICombatable`을 구현해야 합니다.

```csharp
using TeamOdd.Combat;
using UnityEngine;

public class MonsterCombat : MonoBehaviour, ICombatable
{
    [SerializeField] private Collider hitCollider;
    [SerializeField] private int hp = 100;
    [SerializeField] private int maxHp = 100;

    public Collider Collider => hitCollider;
    public GameObject GameObject => gameObject;

    private void OnEnable()
    {
        MonsterRegistry.RegisterMonster(hitCollider, this);
    }

    private void OnDisable()
    {
        MonsterRegistry.UnregisterMonster(hitCollider);
    }

    public void TakeDamage(DamageEvent @event)
    {
        hp = Mathf.Max(0, hp - @event.DamageInfo.Damage);

        // 필요한 경우 여기에서 피격 이펙트, 사망 처리 등을 호출합니다.
        // 피격 위치: @event.DamageInfo.HitPoint
    }

    public void TakeHeal(HealEvent @event)
    {
        hp = Mathf.Min(maxHp, hp + @event.HealAmount);
    }
}
```

설정 시 확인할 사항:

- `hitCollider`에는 실제 피격 판정에 사용하는 3D `Collider`를 연결합니다.
- Collider가 여러 개라면 조회에 사용할 Collider마다 등록이 필요합니다.
- 등록한 Collider는 오브젝트가 비활성화되거나 파괴될 때 반드시 해제합니다.
- `Collider`와 `GameObject` 프로퍼티는 유효한 객체를 반환해야 합니다.

위 HP 계산은 사용 예시입니다. 실제 HP 저장 위치와 피해·회복 규칙은 캐릭터 구조에 맞게 변경하세요.

## 2. 피해 이벤트 발생시키기

공격 판정으로 얻은 Collider를 `MonsterRegistry`에서 조회한 뒤 피해 이벤트를 큐에 추가합니다. 아래 메서드는 공격자 측 `ICombatable` 구현체 안에 있다고 가정합니다.

```csharp
private void DealDamage(Collider hitCollider, Vector3 hitPoint, int damage)
{
    if (!MonsterRegistry.TryGetMonster(hitCollider, out ICombatable target))
        return;

    DamageInfo damageInfo = new DamageInfo(
        damage,
        hitPoint,
        hitCollider
    );

    DamageEvent damageEvent = new DamageEvent(
        sender: this,
        receiver: target,
        damageInfo: damageInfo
    );

    CombatManager.Instance.AddCombatEvent(damageEvent);
}
```

`DamageInfo`의 값:

| 프로퍼티 | 의미 |
| --- | --- |
| `Damage` | 대상에게 전달할 피해량 |
| `HitPoint` | 피격 지점의 월드 좌표 |
| `Collider` | 공격 판정에 적중한 Collider |

현재 코드는 음수 피해량을 제한하지 않습니다. 이벤트를 만드는 쪽에서 유효한 값을 전달하거나, `TakeDamage`에서 프로젝트 규칙에 맞게 검사하세요.

## 3. 회복 이벤트 발생시키기

아래 메서드도 `ICombatable` 구현체 안에 있다고 가정합니다.

```csharp
private void HealTarget(ICombatable target, int amount)
{
    HealEvent healEvent = new HealEvent(
        sender: this,
        receiver: target,
        healAmount: amount
    );

    CombatManager.Instance.AddCombatEvent(healEvent);
}
```

현재 코드는 음수 회복량을 제한하지 않습니다. 유효한 회복량을 전달하거나 `TakeHeal`에서 검사하세요.

## 4. 실행된 이벤트 구독하기

데미지 UI, 사운드, 로그처럼 전투 처리 이후에 반응해야 하는 기능은 `CombatManager` 이벤트를 구독할 수 있습니다.

```csharp
using TeamOdd.Combat;
using UnityEngine;

public class CombatEventListener : MonoBehaviour
{
    private void OnEnable()
    {
        CombatManager.Instance.OnDamageEventExecuted += HandleDamage;
        CombatManager.Instance.OnHealEventExecuted += HandleHeal;
    }

    private void OnDisable()
    {
        if (!CombatManager.IsInitialized)
            return;

        CombatManager.Instance.OnDamageEventExecuted -= HandleDamage;
        CombatManager.Instance.OnHealEventExecuted -= HandleHeal;
    }

    private void HandleDamage(DamageEvent @event)
    {
        Debug.Log($"{@event.Receiver.GameObject.name} 피해: {@event.DamageInfo.Damage}");
    }

    private void HandleHeal(HealEvent @event)
    {
        Debug.Log($"{@event.Receiver.GameObject.name} 회복: {@event.HealAmount}");
    }
}
```

실행 완료 이벤트는 대상의 `TakeDamage` 또는 `TakeHeal` 호출이 끝난 뒤 발생합니다. 구독을 시작한 컴포넌트는 `OnDisable`에서 반드시 구독을 해제하세요.

## 사용 시 주의사항

- `AddCombatEvent` 호출 즉시 처리되는 것이 아니라 `CombatManager`의 `Update`에서 처리됩니다.
- 같은 프레임에 여러 이벤트가 들어오면 큐에 추가된 순서대로 모두 처리됩니다.
- `CombatEvent`, `Sender`, `Receiver`에는 `null`을 전달하지 않는 것을 전제로 합니다.
- 실행 시점에 `Receiver.GameObject`가 이미 파괴되었다면 해당 이벤트는 실행하지 않습니다.
- 실행 완료 이벤트의 콜백에서 이벤트를 다시 추가할 수 있지만, 무한히 이벤트를 추가하는 구조는 같은 프레임의 반복 처리를 끝내지 못하게 할 수 있습니다.
- `MonsterRegistry`는 Collider가 정확히 일치해야 대상을 찾습니다. 부모 Collider를 등록하고 자식 Collider로 조회하면 찾을 수 없습니다.
- `MonsterRegistry`는 전투 이벤트 실행에 필수는 아닙니다. 이미 `ICombatable` 참조를 가지고 있다면 해당 참조를 `Receiver`로 직접 전달할 수 있습니다.

## 현재 연동 상태

현재 프로젝트에서는 이 폴더 밖에서 `ICombatable`, `CombatManager`, `MonsterRegistry`를 사용하는 코드가 확인되지 않았습니다. 위 예시는 현재 API를 기준으로 작성한 기본 연동 방식이며, 실제 캐릭터·무기 코드에 연결할 때는 해당 코드의 생명주기와 HP 관리 구조에 맞춰 적용해야 합니다.
