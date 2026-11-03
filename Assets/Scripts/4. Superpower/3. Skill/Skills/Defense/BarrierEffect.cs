using TeamOdd.Combat;
using UnityEngine;

namespace TeamOdd.Superpower
{
    // 보호막 이펙트에 붙어, 주인의 보호막이 사라지면(피해를 다 막아 깨지거나 시간이 끝나면) 이펙트를 걷어 내고 지운다
    // 보호막이 피해를 막을 때마다 막는 소리(SkillChip.tsv의 Block)를 낸다
    // 이펙트 프리팹은 상용 에셋이라 고치지 않고, BarrierSkill이 이펙트를 띄울 때 이 컴포넌트를 붙인다
    // 생길 때는 에셋의 등장 페이드(NewMaterialChange)로 1초 동안 서서히 나타나고, 사라질 때는 머리끝부터 발밑까지 걷힌다
    // - 이펙트 셰이더(Shader_IntegratedEffect)가 정점 색의 알파를 마스크에 곱하므로, 메시 복사본의 정점 알파를 높이에 따라 바꿔 경계선을 내린다
    // - 경계는 정점 사이에서 보간되어 부드럽게 번지고, 셰이더의 마스크 노이즈가 얹혀 일렁이는 가장자리가 됨
    public class BarrierEffect : MonoBehaviour
    {
        private const float UncoverDuration = 0.45f; // 머리끝에서 발밑까지 걷히는 시간 (초)
        private const float EdgeWidth = 0.2f;       // 경계가 번지는 폭 (메시 높이 대비)

        private CombatEntity owner;
        private SkillSounds sounds;

        private MeshFilter shell;
        private Mesh sweepMesh;    // 정점 알파를 바꾸려고 만든 메시 복사본, 지울 때 함께 지움
        private float[] heights;   // 정점별 높이 (0 발밑 ~ 1 머리끝)
        private Color32[] colors;

        private float progress;    // 시간에 따라 일정하게 진행하는 값, 경계 위치는 여기에 이징을 걸어서 씀
        private bool isUncovering;

        public void Bind(CombatEntity owner, SkillSounds sounds)
        {
            this.owner = owner;
            this.sounds = sounds;
            owner.Hit += OnOwnerHit;
        }

        private void OnDestroy()
        {
            if (owner != null) owner.Hit -= OnOwnerHit;
            if (sweepMesh != null) Destroy(sweepMesh);
        }

        private void Update()
        {
            if (isUncovering == false)
            {
                if (owner.Shield > 0) return;

                // 메시를 읽을 수 없으면 걷히는 연출 없이 바로 지움
                isUncovering = true;
                if (PrepareSweep() == false)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            progress = Mathf.Min(progress + Time.deltaTime / UncoverDuration, 1f);
            ApplySweep();

            if (progress >= 1f)
                Destroy(gameObject);
        }

        // Hit은 보호막이 피해를 깎기 전에 오므로, 이때 보호막이 남아 있으면 이번 피해를 보호막이 막는다
        private void OnOwnerHit(DamageEvent damageEvent)
        {
            if (owner.Shield > 0)
                sounds.Play("Block");
        }

        // 원본 메시는 공유되므로 걷히기 시작할 때 복사본을 만들어 이 이펙트에만 씀, 읽을 수 없는 메시면 false
        private bool PrepareSweep()
        {
            shell = GetComponentInChildren<MeshFilter>();
            if (shell == null || shell.sharedMesh == null || shell.sharedMesh.isReadable == false)
                return false;

            sweepMesh = Instantiate(shell.sharedMesh);
            sweepMesh.name = shell.sharedMesh.name + " (Barrier Sweep)";
            shell.sharedMesh = sweepMesh;

            Vector3[] vertices = sweepMesh.vertices;
            Bounds bounds = sweepMesh.bounds;
            float bottom = bounds.min.y;
            float size = Mathf.Max(bounds.size.y, 0.0001f);

            heights = new float[vertices.Length];
            colors = new Color32[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                heights[i] = (vertices[i].y - bottom) / size;

            return true;
        }

        // 경계선 아래만 보이게 정점 알파를 칠함, 경계선은 머리끝에서 발밑으로 내려감
        // 빠르게 내려가다 발밑에서 감속(cubic ease-out)
        private void ApplySweep()
        {
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            float line = (1f + EdgeWidth) * (1f - eased);

            for (int i = 0; i < heights.Length; i++)
            {
                float alpha = Mathf.Clamp01((line - heights[i]) / EdgeWidth);
                colors[i] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }

            sweepMesh.colors32 = colors;
        }
    }
}
