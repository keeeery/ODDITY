using UnityEngine;

namespace TeamOdd.Player
{
    // 플레이어의 그 순간 자세를 메시로 굳혀 parent 아래에 같은 자리·같은 모양으로 그린다. 회피 잔상(DodgeAfterimage)과 허물(DodgeShell)이 함께 쓴다
    // 캐릭터가 여러 조각(SkinnedMeshRenderer)으로 나뉘어 있어서 보이는 조각마다 하나씩 굳힌다
    // 코드로 만든 메시는 오브젝트와 함께 지워지지 않으므로, 돌려받은 메시는 부른 쪽이 직접 지운다 (렌더러가 꺼져 건너뛴 칸은 null)
    public static class PoseBaker
    {
        public static Mesh[] Bake(Transform player, Transform parent, Material material)
        {
            // 꺼진 오브젝트의 조각은 GetComponentsInChildren이 빼 준다. 켜져 있어도 렌더러가 꺼진 조각은 건너뛴다
            SkinnedMeshRenderer[] skins = player.GetComponentsInChildren<SkinnedMeshRenderer>();
            Mesh[] meshes = new Mesh[skins.Length];
            for (int i = 0; i < skins.Length; i++)
            {
                if (skins[i].enabled == false)
                    continue;

                meshes[i] = new Mesh();
                skins[i].BakeMesh(meshes[i], true); // 조각의 크기 배율까지 메시에 굳힌다

                GameObject piece = new GameObject(skins[i].name);
                piece.transform.SetPositionAndRotation(skins[i].transform.position, skins[i].transform.rotation);
                piece.transform.SetParent(parent, true);
                piece.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                MeshRenderer pieceRenderer = piece.AddComponent<MeshRenderer>();
                pieceRenderer.sharedMaterial = material;
                pieceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return meshes;
        }
    }
}
