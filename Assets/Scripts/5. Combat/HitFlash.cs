using System;
using System.Collections.Generic;
using UnityEngine;

namespace TeamOdd.Combat
{
    // 피격 시 윤곽 번쩍임의 렌더러 목록과 세기, 그리는 일은 HitFlashFeature가 맡음
    // 히트스톱 중에도 보이도록 unscaled 시간으로 줄어든다.
    [DisallowMultipleComponent]
    public sealed class HitFlash : MonoBehaviour
    {
        private static readonly List<HitFlash> active = new();

        private Renderer[] renderers;
        private MeshFilter[] meshFilters; // 메시가 바뀔 수 있어 그릴 때마다 읽음
        private float duration;
        private float remain;

        // 번쩍이는 중인 대상
        public static IReadOnlyList<HitFlash> Active => active;

        // 0~1, 맞은 순간 1에서 줄어듦
        public float Intensity
        {
            get
            {
                float ratio = duration > 0f ? remain / duration : 0f;
                return ratio * ratio;
            }
        }

        public IReadOnlyList<Renderer> Renderers => renderers;

        private void Awake()
        {
            renderers = Array.FindAll(GetComponentsInChildren<Renderer>(true), item => item is MeshRenderer or SkinnedMeshRenderer);
            meshFilters = Array.ConvertAll(renderers, item => item.GetComponent<MeshFilter>());
        }

        private void Update()
        {
            if (remain <= 0f) return;

            remain -= Time.unscaledDeltaTime;
            if (remain <= 0f) Stop();
        }

        private void OnDisable() => Stop();

        #region PUBLIC_METHODS

        public static HitFlash GetOrAdd(GameObject target)
        {
            return target.TryGetComponent(out HitFlash flash) ? flash : target.AddComponent<HitFlash>();
        }

        public void Play(float seconds)
        {
            if (isActiveAndEnabled == false || seconds <= 0f) return;

            if (remain <= 0f) active.Add(this);
            duration = seconds;
            remain = seconds;
        }

        // 메시가 없으면 0
        public int GetSubMeshCount(int index)
        {
            Renderer target = renderers[index];
            Mesh mesh = target is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                : meshFilters[index] != null ? meshFilters[index].sharedMesh : null;
            return mesh != null ? mesh.subMeshCount : 0;
        }

        #endregion

        // 도메인 리로드 없이 플레이할 때를 위한 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => active.Clear();

        private void Stop()
        {
            active.Remove(this);
            remain = 0f;
        }
    }
}
