using System;
using TeamOdd.Combat;
using TMPro;
using UnityEngine;

public class HitDamageTest : MonoBehaviour
{
    [SerializeField] private CombatEntity player;
    [SerializeField] TMP_Text hpText;

    private void Update()
    {
        string hp = player.Hp.ToString();
        hpText.SetText(hp);
    }
}
