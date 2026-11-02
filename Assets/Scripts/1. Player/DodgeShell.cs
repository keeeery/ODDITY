using UnityEngine;

namespace TeamOdd.Player
{
    // 수동 회피 허물. 플레이어의 그 순간 자세를 하얀 메시로 굳혀 그 자리에 남긴다
    // holdDuration 동안 하얗게 머물다가 dissolveDuration 동안 바스러져 사라진다. 몸은 구르기로 빠져나가고 허물만 남는다
    // 수동 회피 슬로모션이 실제 시간으로 재므로 여기서도 실제 시간으로 잰다. 그래야 "느려진 동안 하얗게 → 풀리면서 바스러짐"이 맞물린다
    // SkillEffectFactory가 만든 ManualDodgeEffect 프리팹에 붙어 있고, DodgeEffect가 띄운 뒤 Bake를 부른다
    public class DodgeShell : MonoBehaviour
    {
        private static readonly int Dissolve = Shader.PropertyToID("_Dissolve");

        [SerializeField] private Material material;         // TeamOdd/Dodge Shell 셰이더 머티리얼
        [SerializeField] private float holdDuration;        // 하얗게 머무는 시간 (실제 시간, 초)
        [SerializeField] private float dissolveDuration;    // 바스러져 사라지는 시간 (실제 시간, 초)

        private Material dissolvingMaterial; // 이 허물만 바스러지게 하려고 복사한 머티리얼
        private Mesh[] meshes;
        private float elapsed;

        public void Bake(Transform player)
        {
            dissolvingMaterial = new Material(material);
            dissolvingMaterial.SetFloat(Dissolve, 0f);
            meshes = PoseBaker.Bake(player, transform, dissolvingMaterial);
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01((elapsed - holdDuration) / dissolveDuration);
            dissolvingMaterial.SetFloat(Dissolve, progress);

            if (progress >= 1f)
                Destroy(gameObject);
        }

        // 코드로 만든 메시와 머티리얼은 오브젝트와 함께 지워지지 않으므로 직접 지운다
        private void OnDestroy()
        {
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] != null)
                    Destroy(meshes[i]);
            }
            Destroy(dissolvingMaterial);
        }
    }
}
