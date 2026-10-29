using System.IO;
using UnityEditor;
using UnityEngine;

namespace TeamOdd.EditorTools
{
    // 현재 씬을 정해 둔 몇 개의 시점에서 렌더링해 Temp/ScenePreview에 PNG로 저장하는 메뉴
    // 배치한 환경을 에디터 밖에서(리뷰, 공유) 확인할 때 쓴다.
    internal static class ScenePreviewCapture
    {
        private const string OutputFolder = "Temp/ScenePreview";
        private const int Width = 1600;
        private const int Height = 900;

        private static readonly (string name, Vector3 position, Vector3 lookAt)[] Views =
        {
            ("01_overview", new Vector3(0f, 55f, -60f), new Vector3(0f, 0f, 2f)),
            ("02_player_view", new Vector3(0.5f, 3f, -14f), new Vector3(0f, 2f, 9f)),
            ("03_inside_corner", new Vector3(-20f, 6f, -18f), new Vector3(10f, 2f, 12f)),
            ("04_wall_close", new Vector3(8f, 3f, 14f), new Vector3(0f, 4f, 26f)),
            ("05_outside", new Vector3(45f, 20f, -45f), new Vector3(0f, 4f, 2f)),
            ("06_top_down", new Vector3(0f, 110f, 2f), new Vector3(0f, 0f, 2.01f)),
            ("07_south_door", new Vector3(0f, 3f, 10f), new Vector3(0f, 3f, -22f)),
            ("08_ceiling", new Vector3(0f, 1.5f, -8f), new Vector3(0f, 16f, 12f)),
            ("09_catwalk", new Vector3(-21f, 9.5f, -20f), new Vector3(10f, 6f, 15f)),
            ("10_corner_ne", new Vector3(8f, 4f, 6f), new Vector3(20f, 0.5f, 19f)),
            ("11_corner_sw", new Vector3(-8f, 4f, -8f), new Vector3(-19f, 0.5f, -19f)),
            ("12_corner_se", new Vector3(6f, 4f, -8f), new Vector3(18f, 0.5f, -17f)),
            ("13_corner_nw", new Vector3(-6f, 4f, 8f), new Vector3(-17f, 0.5f, 18f)),
        };

        [MenuItem("Tools/Team Odd/Capture Scene Preview")]
        private static void Capture()
        {
            Directory.CreateDirectory(OutputFolder);
            Camera camera = new GameObject("Scene Preview Camera") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Camera>();
            camera.fieldOfView = 55f;
            camera.farClipPlane = 1000f;
            RenderTexture target = new(Width, Height, 24, RenderTextureFormat.ARGB32);
            Texture2D image = new(Width, Height, TextureFormat.RGB24, false);

            try
            {
                camera.targetTexture = target;
                foreach ((string name, Vector3 position, Vector3 lookAt) in Views)
                {
                    camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
                    camera.Render();

                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(OutputFolder, name + ".png"), image.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = null;
                camera.targetTexture = null;
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }

            Debug.Log($"[Preview] {Views.Length}장을 {Path.GetFullPath(OutputFolder)}에 저장했습니다.");
        }
    }
}
