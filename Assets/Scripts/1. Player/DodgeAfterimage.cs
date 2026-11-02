using UnityEngine;

namespace TeamOdd.Player
{
    // 자동 회피 잔상. 플레이어의 그 순간 자세를 메시로 굳혀 그 자리에 남기고, duration초 동안 흐려지다 스스로 사라진다
    // SkillEffectFactory가 만든 AutoDodgeEffect 프리팹에 붙어 있고, DodgeEffect가 띄운 뒤 Bake를 부른다
    public class DodgeAfterimage : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Material material; // 겹칠수록 밝아지는(Additive) 머티리얼
        [SerializeField] private Color color;       // 처음 색. 투명도(a)가 0이 될 때까지 흐려진다
        [SerializeField] private float duration;    // 사라질 때까지 걸리는 시간 (초)

        private Material fadingMaterial; // 이 잔상만 흐리게 하려고 복사한 머티리얼
        private Mesh[] meshes;
        private float elapsed;

        public void Bake(Transform player)
        {
            fadingMaterial = new Material(material);
            fadingMaterial.SetColor(BaseColor, color);
            meshes = PoseBaker.Bake(player, transform, fadingMaterial);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Min(elapsed / duration, 1f);

            Color fading = color;
            fading.a = color.a * (1f - progress);
            fadingMaterial.SetColor(BaseColor, fading);

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
            Destroy(fadingMaterial);
        }
    }
}
