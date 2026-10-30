using System.Collections.Generic;
using TeamOdd.Psychokinesis;
using UnityEngine;

namespace TeamOdd.Arena
{
    // 바닥의 구멍 아래처럼 플레이어가 되찾을 수 없는 곳에 떨어진 염동력 오브젝트를 없앤다.
    // 맵에 배치된 오브젝트는 파괴하고, 풀 파편은 풀로 돌려보낸다. 영역은 이 Transform 위치를 중심으로 한 상자
    public sealed class PsychokinesisFallZone : MonoBehaviour
    {
        [SerializeField] private Vector3 size = new(50f, 30f, 40f);

        private void FixedUpdate()
        {
            Bounds zone = new(transform.position, size);

            // 없앤 오브젝트가 목록에서 빠져도 건너뛰지 않도록 뒤에서부터 순회
            IReadOnlyList<PsychokinesisObject> objects = PsychokinesisObject.ActiveObjects;
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                PsychokinesisObject target = objects[index];
                if (zone.Contains(target.Body.worldCenterOfMass)) target.Vanish();
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.5f);
            Gizmos.DrawWireCube(transform.position, size);
        }
#endif
    }
}
