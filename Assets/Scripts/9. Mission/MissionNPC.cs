using System;
using System.Collections.Generic;
using Study.Utilities;
using TeamOdd.Superpower;
using TeamOdd.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace TeamOdd.Mission
{
    public class MissionNPC : MonoBehaviour
    {
        [SerializeField] private GameObject popupUI;
        [SerializeField] private  float distance;
        [SerializeField] private Player.Player player;
        [Tooltip("켜면 중심이 아니라 모델 겉면(경계 상자)에서 거리를 잰다. 원자로처럼 플레이어가 중심까지 다가갈 수 없는 큰 오브젝트용")]
        [SerializeField] private bool measureFromBounds;

        [Header("Outline")]
        [Tooltip("상호작용할 수 있을 때 염동력 대상과 같은 방식으로 그리는 외곽선")]
        [SerializeField] private Color outlineColor = new(0f, 1f, 0f, 1f);
        [SerializeField, Range(1f, 8f)] private float outlineWidthPixels = 3f;

        private static readonly List<MissionNPC> npcs = new();
        private static int focusFrame = -1;

        private readonly List<Renderer> outlineRenderers = new();
        private readonly List<int> outlineSubMeshCounts = new();

        // 거리 안에 있는 가장 가까운 NPC. F·우클릭은 이 NPC만 받는다
        private static MissionNPC Focused { get; set; }
        // 지금 외곽선·조준점 링을 그릴 NPC. 창이 열려 있으면 null
        internal static MissionNPC OutlinedNPC { get; private set; }
        internal IReadOnlyList<Renderer> OutlineRenderers => outlineRenderers;
        internal IReadOnlyList<int> OutlineSubMeshCounts => outlineSubMeshCounts;
        internal Color OutlineColor => outlineColor;
        internal float OutlineWidthPixels => outlineWidthPixels;
        // 정비소 안이면 F 말고 우클릭으로도 말을 걸 수 있다 (상호작용 안내 표시용)
        internal bool CanInteractWithRightClick => player != null && RepairShopZone.Contains(player.transform.position);

        private void Awake()
        {
            // 로봇처럼 모델이 자식에 있는 NPC도 있어 자식 렌더러를 모두 그린다
            // 이름표 같은 글자(TextMeshPro)는 뒤집혀 놓인 경우 뒤에서 앞면으로 그려지므로 외곽선에서 뺀다
            foreach (Renderer target in GetComponentsInChildren<Renderer>(true))
            {
                if (target.TryGetComponent(out TMP_Text _)) continue;

                Mesh mesh = target switch
                {
                    SkinnedMeshRenderer skinned => skinned.sharedMesh,
                    MeshRenderer => target.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null,
                    _ => null,
                };
                if (mesh == null) continue;

                outlineRenderers.Add(target);
                outlineSubMeshCounts.Add(mesh.subMeshCount);
            }
        }

        private void OnEnable() => npcs.Add(this);

        private void OnDisable()
        {
            npcs.Remove(this);
            if (Focused == this) Focused = null;
            if (OutlinedNPC == this) OutlinedNPC = null;
        }

        private void Start()
        {
            player = Player.Player.Instance;
        }

        private void Update()
        {
            RefreshFocus();
            if (Focused != this) return;
            // 증강 칩 창의 검색 칸에 글자를 치는 중이면 F(ㄹ)·Esc는 글자 입력이므로 받지 않는다
            if (TextInputFocus.IsTyping) return;

            // 로비에서 E로 연 증강 칩 창처럼 다른 창이 열려 있으면 그 위에 이 NPC의 창을 겹쳐 열지 않는다
            // (우클릭은 그 창의 조작인 칩 빠른 장착/해제와도 겹친다). 이 NPC의 창이 열려 있으면 F로 닫는 것은 그대로 한다
            bool isOpen = popupUI.activeSelf;
            bool canOpen = isOpen == false && UIManager.Instance.IsUIClose;

            // 정비소에서는 염동력 대신 우클릭으로도 말을 건다. 창을 닫는 것은 F와 Esc만 한다
            bool rightClick = CanInteractWithRightClick && canOpen &&
                              SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Right);
            bool fKey = (isOpen || canOpen) && SimpleInput.GetKeyDown(Key.F);

            if (fKey || rightClick)
            {
                bool isClose = popupUI.activeSelf == false;

                if(popupUI.TryGetComponent(out AugmentationChipPanel panel))
                    panel.IsActivePanel(isClose, player.GetComponent<AugmentationChipInventory>());
                else popupUI.SetActive(isClose);

                UIManager.Instance.LockCursor(isClose == false, isClose);
            }
            else if (SimpleInput.GetKeyDown(Key.Escape))
            {
                if(popupUI.TryGetComponent(out AugmentationChipPanel panel))
                    panel.Close();
                else popupUI.SetActive(false);

                UIManager.Instance.LockCursor(true);
            }
        }

        private float DistanceTo(Vector3 position)
        {
            if (measureFromBounds == false || outlineRenderers.Count == 0)
                return Vector3.Distance(position, transform.position);

            Bounds bounds = outlineRenderers[0].bounds;
            for (int i = 1; i < outlineRenderers.Count; i++)
                bounds.Encapsulate(outlineRenderers[i].bounds);
            return Vector3.Distance(position, bounds.ClosestPoint(position));
        }

        // 여러 NPC가 각자 거리를 재면 둘이 함께 열릴 수 있어, 프레임마다 한 번 가장 가까운 NPC 하나만 고른다
        private static void RefreshFocus()
        {
            if (focusFrame == Time.frameCount) return;
            focusFrame = Time.frameCount;

            MissionNPC nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (MissionNPC npc in npcs)
            {
                if (npc.player == null) continue;

                float distance = npc.DistanceTo(npc.player.transform.position);
                if (distance < npc.distance && distance < nearestDistance)
                {
                    nearest = npc;
                    nearestDistance = distance;
                }
            }

            Focused = nearest;
            OutlinedNPC = nearest != null && nearest.popupUI.activeSelf == false && UIManager.Instance.IsUIClose ? nearest : null;
        }
    }

}
