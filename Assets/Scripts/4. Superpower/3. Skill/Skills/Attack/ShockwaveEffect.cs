using UnityEngine;

namespace TeamOdd.Superpower
{
    // 염동 충격파 이펙트의 맨 위에 붙어, 투명한 반구를 충격파 반경까지 키운다. ShockwaveSkill이 띄우면서 Play로 반경을 넘긴다
    // - 반구가 growDuration초 동안 처음엔 빠르게, 끝에선 느리게 반경까지 커진다
    // - 다 커진 뒤 fadeDuration초 동안 흐려져 사라진다
    // - 바닥에서 피어오르는 가루(맨 위 파티클)도 같은 반경 안에서 생기게 맞춘다
    // 이펙트 전체를 지우는 건 맨 위 파티클의 Stop Action이 한다. 그보다 먼저 반구가 다 사라지게 시간을 맞춰 둔다
    public class ShockwaveEffect : MonoBehaviour
    {
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        [SerializeField] private Renderer dome;
        [SerializeField] private float growDuration = 0.25f;
        [SerializeField] private float fadeDuration = 0.35f;

        private float radius;
        private float elapsed;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        public void Play(float radius)
        {
            this.radius = radius;

            ParticleSystem.ShapeModule shape = GetComponent<ParticleSystem>().shape;
            shape.radius = radius;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            float grow = Mathf.Clamp01(elapsed / growDuration);
            grow = 1f - (1f - grow) * (1f - grow); // 처음엔 빠르게, 끝에선 느리게
            dome.transform.localScale = Vector3.one * (radius * 2f * grow); // 기본 구 메시는 지름 1m라서 반경의 두 배로 키운다

            float fade = 1f - Mathf.Clamp01((elapsed - growDuration) / fadeDuration);
            block.SetFloat(FadeId, fade);
            dome.SetPropertyBlock(block);
        }
    }
}
