# Combat 사용 가이드

## 1. 전투 객체 설정

1. Project 창에서 `Create > Scriptable > Combat > Stats`를 선택합니다.
2. 생성한 `CombatStats`에 `MaxHp`, `Atk`, `Def`를 입력합니다.
3. 전투에 참여할 GameObject에 `CombatEntity` 또는 이를 상속한 컴포넌트를 추가합니다.
4. Inspector에서 다음 필드를 연결합니다.
   - `Collider`: 피격 판정에 사용할 3D Collider
   - `Stats`: 생성한 `CombatStats` 에셋

코드에서는 다음 네임스페이스를 사용합니다.

```csharp
using TeamOdd.Combat;
```

## 2. 피해 주기

아래 코드는 `CombatEntity` 또는 다른 `ICombatable` 구현체 안에서 사용합니다.

```csharp
private void Attack(ICombatable target, Vector3 hitPoint)
{
    DamageInfo info = new DamageInfo(
        damage: Stats.Atk,
        hitPoint: hitPoint,
        collider: target.Collider
    );

    DamageEvent damageEvent = new DamageEvent(
        sender: this,
        receiver: target,
        info: info
    );

    CombatManager.Instance.AddCombatEvent(damageEvent);
}
```

## 3. 회복시키기

```csharp
private void Heal(ICombatable target, int amount)
{
    HealEvent healEvent = new HealEvent(
        sender: this,
        receiver: target,
        healAmount: amount
    );

    CombatManager.Instance.AddCombatEvent(healEvent);
}
```

## 4. Collider로 몬스터 찾기

`MonsterRegistry`를 사용할 몬스터는 활성화될 때 등록하고 비활성화될 때 해제합니다.

```csharp
private void OnEnable()
{
    MonsterRegistry.RegisterMonster(Collider, this);
}

private void OnDisable()
{
    MonsterRegistry.UnregisterMonster(Collider);
}
```

공격 판정에서 얻은 Collider로 대상을 조회합니다.

```csharp
private void AttackMonster(Collider hitCollider, Vector3 hitPoint)
{
    if (!MonsterRegistry.TryGetMonster(hitCollider, out ICombatable target))
        return;

    DamageInfo info = new DamageInfo(Stats.Atk, hitPoint, hitCollider);
    DamageEvent damageEvent = new DamageEvent(this, target, info);

    CombatManager.Instance.AddCombatEvent(damageEvent);
}
```

등록할 때 사용한 Collider와 조회할 때 전달하는 Collider가 같아야 합니다.

## 5. 처리 결과 구독하기

```csharp
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

private void HandleDamage(DamageEvent damageEvent, int damage)
{
    Debug.Log($"{damageEvent.Receiver.GameObject.name}: {damage} damage");
}

private void HandleHeal(HealEvent healEvent, int healAmount)
{
    Debug.Log($"{healEvent.Receiver.GameObject.name}: {healAmount} heal");
}
```

## 6. 피격 동작 추가하기

엔티티별 피격 연출이 필요하면 `TakeDamage`를 재정의합니다.

```csharp
public override int TakeDamage(DamageEvent damageEvent)
{
    PlayHitEffect();
    return base.TakeDamage(damageEvent);
}
```

## 샘플

`99. Sample` 폴더의 다음 코드를 참고할 수 있습니다.

- `SampleCombatPlayer`: Registry에서 몬스터를 찾아 공격
- `SampleCombatMonster`: Registry 등록 및 플레이어 공격
- DOTween을 이용한 공격·피격 연출
- Odin Inspector 버튼을 이용한 테스트

샘플 사용 시 각 오브젝트의 `Collider`와 `Stats`를 연결하고, 플레이어의 `hitCollider`에 몬스터 Collider를 지정합니다.
