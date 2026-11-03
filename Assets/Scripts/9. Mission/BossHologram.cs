using System;
using UnityEngine;

namespace FantasyGarden.Mission
{
    [Serializable]
    public class BossHologram
    {
        private static readonly int DISSOLVE_HIDE = Shader.PropertyToID("_DissolveHide");
        private static readonly int RANDOM_GLITCH_AMOUNT = Shader.PropertyToID("_RandomGlitchAmount");

        // _DissolveHide 는 노이즈(-1 ~ 1)에서 빼는 값이라 -1 이면 전부 보이고, 1 이면 전부 사라짐
        private const float DISSOLVE_VISIBLE = -1f;
        private const float DISSOLVE_HIDDEN = 1f;

        [field: SerializeField] public string BossName { get; private set; }
        [field: SerializeField] public Transform Model { get; private set; }

        private Renderer[] renderers;
        private float defaultGlitchAmount = 0f;

        public void Initialize()
        {
            renderers = Model.GetComponentsInChildren<Renderer>(true);

            // 세 모델이 같은 머티리얼을 공유하므로, 머티리얼 값은 건드리지 않고 원래 값만 읽어 둠
            Material material = renderers[0].sharedMaterial;
            if (material.HasProperty(RANDOM_GLITCH_AMOUNT))
                defaultGlitchAmount = material.GetFloat(RANDOM_GLITCH_AMOUNT);
        }

        public void SetActive(bool isActive) => Model.gameObject.SetActive(isActive);

        /// <summary>
        /// 0 이면 디졸브로 완전히 사라지고 글리치가 최대, 1 이면 완전히 보이고 글리치가 원래 값
        /// </summary>
        public void ApplyVisibility(float visibility, float glitchPeakAmount, MaterialPropertyBlock propertyBlock)
        {
            float dissolveHide = Mathf.Lerp(DISSOLVE_HIDDEN, DISSOLVE_VISIBLE, visibility);
            float glitchAmount = Mathf.Lerp(glitchPeakAmount, defaultGlitchAmount, visibility);

            // 공유 머티리얼이 바뀌지 않도록 렌더러마다 PropertyBlock 으로 적용
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(DISSOLVE_HIDE, dissolveHide);
                propertyBlock.SetFloat(RANDOM_GLITCH_AMOUNT, glitchAmount);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
