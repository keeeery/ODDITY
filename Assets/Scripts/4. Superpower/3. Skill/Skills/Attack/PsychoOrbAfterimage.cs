using UnityEngine;

namespace TeamOdd.Superpower
{
    // 염동 구체가 날아가며 남기는 잔상 하나. 생긴 자리에 멈춰서 duration초 동안 작아지다 풀로 돌아간다
    // 잔상 프리팹(Resources/Skill/Effect/PsychoOrbAfterimage, 지름 1인 검은 구)에 붙어 있다
    // ObjectPoolManager에 'PsychoOrbAfterimage' 키로 등록된 풀에서 PsychoOrb가 꺼내 Show를 부른다
    public class PsychoOrbAfterimage : MonoBehaviour, IObjectPoolItem
    {
        [SerializeField] private float duration; // 다 작아질 때까지 걸리는 시간 (초)

        private Vector3 startScale;
        private float elapsed;

        public string Key { get; set; }
        public GameObject GameObject => gameObject;

        // 풀에서 꺼낸 잔상을 position에 지름 diameter인 크기로 켠다
        public void Show(Vector3 position, float diameter)
        {
            transform.position = position;
            startScale = Vector3.one * diameter;
            transform.localScale = startScale;
            elapsed = 0f;

            gameObject.SetActive(true);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Min(elapsed / duration, 1f);

            transform.localScale = startScale * (1f - progress);

            if (progress >= 1f)
                ReturnToPool();
        }

        public void ReturnToPool()
        {
            ObjectPoolManager.Instance.ReturnToPool(this);
        }
    }
}
