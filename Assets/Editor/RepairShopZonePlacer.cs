using TeamOdd.Mission;
using TeamOdd.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TeamOdd.EditorTools
{
    // 로비 씬(02. Lobby)에 정비소 구역과 NPC 상호작용 안내를 놓는 메뉴. 가운데 홀과 서쪽 서버실·동쪽 온실을 정비소로, 남쪽(z 3.4 아래)을 훈련장으로 나눈다.
    // 임무 시작 NPC(Stage Move NPC)는 홀 가운데 원자로라, 임무 이동 창을 연결하고 거리를 원자로 겉면에서 재게 한다.
    // 같은 씬에서 다시 실행하면 새로 만들지 않고 기존 구역의 위치와 크기만 고친다.
    internal static class RepairShopZonePlacer
    {
        private const string ZoneName = "Repair Shop Zone";
        private const string ParentName = "=Stage";
        private const string PromptName = "Interaction Prompt";
        private const string PromptParentName = "=UI";
        private const string StageMoveNPCName = "Stage Move NPC";
        // 원자로 겉면에서 이 거리(m) 안이면 말을 걸 수 있다
        private const float ReactorInteractDistance = 2f;

        // 홀(x -5.76~14.24, z 3.4~19.4)과 양옆 방(x -13.76~22.24, z 3.4~15.4)을 덮는 상자. 바닥 아래부터 천장까지
        private static readonly Vector3 ZonePosition = new(4.24262f, 2.75f, 11.40296f);
        private static readonly Vector3 ZoneSize = new(36f, 7.5f, 16f);

        [MenuItem("Tools/Team Odd/Set Up Lobby Repair Shop")]
        private static void SetUp()
        {
            RepairShopZone zone = PlaceZone();
            PlacePrompt();
            SetUpStageMoveNPC();

            EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);
            Selection.activeGameObject = zone.gameObject;
            Debug.Log($"[RepairShopZonePlacer] {zone.gameObject.scene.name}에 정비소 구역과 상호작용 안내를 배치했습니다. 씬을 저장하세요.");
        }

        private static RepairShopZone PlaceZone()
        {
            RepairShopZone zone = Object.FindAnyObjectByType<RepairShopZone>(FindObjectsInactive.Include);
            if (zone == null)
            {
                GameObject zoneObject = new(ZoneName);
                Undo.RegisterCreatedObjectUndo(zoneObject, "Place Repair Shop Zone");
                GameObject parent = GameObject.Find(ParentName);
                if (parent != null) Undo.SetTransformParent(zoneObject.transform, parent.transform, "Place Repair Shop Zone");
                zone = Undo.AddComponent<RepairShopZone>(zoneObject);
            }

            Undo.RecordObject(zone.transform, "Place Repair Shop Zone");
            zone.transform.SetPositionAndRotation(ZonePosition, Quaternion.identity);
            zone.transform.localScale = Vector3.one;

            SerializedObject serialized = new(zone);
            serialized.FindProperty("center").vector3Value = Vector3.zero;
            serialized.FindProperty("size").vector3Value = ZoneSize;
            serialized.ApplyModifiedProperties();
            return zone;
        }

        // 폰트는 InteractionPrompt.Reset이 Pretendard-Medium SDF로 채운다
        private static void PlacePrompt()
        {
            if (Object.FindAnyObjectByType<InteractionPrompt>(FindObjectsInactive.Include) != null) return;

            GameObject promptObject = new(PromptName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(promptObject, "Place Interaction Prompt");
            promptObject.layer = LayerMask.NameToLayer("UI");
            GameObject parent = GameObject.Find(PromptParentName);
            if (parent != null) Undo.SetTransformParent(promptObject.transform, parent.transform, "Place Interaction Prompt");
            Undo.AddComponent<InteractionPrompt>(promptObject);
        }

        private static void SetUpStageMoveNPC()
        {
            GameObject reactor = GameObject.Find(StageMoveNPCName);
            if (reactor == null)
            {
                Debug.LogWarning($"[RepairShopZonePlacer] '{StageMoveNPCName}'을(를) 찾지 못해 임무 시작 NPC를 설정하지 못했습니다.");
                return;
            }

            if (reactor.TryGetComponent(out MissionNPC npc) == false)
                npc = Undo.AddComponent<MissionNPC>(reactor);

            SerializedObject serialized = new(npc);
            SerializedProperty popup = serialized.FindProperty("popupUI");
            if (popup.objectReferenceValue == null)
            {
                StageMoveUI stageMoveUI = Object.FindAnyObjectByType<StageMoveUI>(FindObjectsInactive.Include);
                if (stageMoveUI != null) popup.objectReferenceValue = stageMoveUI.gameObject;
                else Debug.LogWarning("[RepairShopZonePlacer] 씬에 StageMoveUI가 없어 임무 시작 창을 연결하지 못했습니다.");
            }
            serialized.FindProperty("measureFromBounds").boolValue = true;
            serialized.FindProperty("distance").floatValue = ReactorInteractDistance;
            serialized.ApplyModifiedProperties();
        }
    }
}
