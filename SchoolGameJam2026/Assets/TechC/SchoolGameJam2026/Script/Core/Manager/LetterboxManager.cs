using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TechC.SchoolGameJam2026.Scripts.Core.Manager
{
    /// <summary>
    /// 画面の比率を 16:9 に保つ。違う比率の画面では、上下か左右に黒い帯を出す
    /// ・シーンのカメラ（タイトルの背景など）の映す範囲を、画面の真ん中の 16:9 の範囲にする
    /// ・画面の UI（Screen Space - Overlay の Canvas）は、そのままだと画面全体に広がるので、
    ///   UI 用のカメラ（16:9 の範囲を映す）に映す形（Screen Space - Camera）に切り替える
    /// ・黒い帯は、いちばん先に画面全体を黒で塗るカメラで作る
    /// 起動時に自動で作られる（シーンに置かなくてよい）。あとから作られた Canvas も毎フレーム切り替える
    /// </summary>
    public class LetterboxManager : MonoBehaviour
    {
        /// <summary> 保つ画面の比率（1920×1080） </summary>
        public const float TargetAspect = 16f / 9f;

        /// <summary> UI 用のカメラから Canvas までの距離 </summary>
        private const float CanvasPlaneDistance = 1f;

        public static LetterboxManager Instance { get; private set; }

        /// <summary> 画面の UI を映すカメラ </summary>
        public Camera UICamera => uiCamera;

        private Camera barCamera;
        private Camera uiCamera;
        private UniversalAdditionalCameraData uiCameraData;
        private int uiLayer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnStartup()
        {
            if (Instance != null) return;
            var go = new GameObject("[Letterbox]");
            DontDestroyOnLoad(go);
            go.AddComponent<LetterboxManager>();
        }

        /// <summary>
        /// 画面の大きさから、16:9 の範囲を返す（画面全体を 0〜1 とした割合。Camera.rect に使う）
        /// 横長の画面なら左右、縦長の画面なら上下に余白（黒い帯）ができる
        /// </summary>
        public static Rect CalculateViewport(int screenWidth, int screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0) return new Rect(0f, 0f, 1f, 1f);

            float screenAspect = (float)screenWidth / screenHeight;
            if (Mathf.Approximately(screenAspect, TargetAspect)) return new Rect(0f, 0f, 1f, 1f);

            if (screenAspect > TargetAspect)
            {
                float width = TargetAspect / screenAspect;
                return new Rect((1f - width) / 2f, 0f, width, 1f);
            }

            float height = screenAspect / TargetAspect;
            return new Rect(0f, (1f - height) / 2f, 1f, height);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            uiLayer = LayerMask.NameToLayer("UI");

            // 黒い帯: いちばん先に、画面全体を黒で塗る（何も映さない）
            barCamera = CreateCamera("BarCamera");
            barCamera.depth = -100;
            barCamera.clearFlags = CameraClearFlags.SolidColor;
            barCamera.backgroundColor = Color.black;
            barCamera.cullingMask = 0;

            // UI 用のカメラ: UI のレイヤーだけを、ゆがみなく（正投影で）映す
            // ふだんはシーンのカメラの上に重ねる（Overlay）。シーンにカメラがないときは単独で映す
            uiCamera = CreateCamera("UICamera");
            uiCamera.transform.localPosition = new Vector3(0f, -10000f, 0f);
            uiCamera.depth = 100;
            uiCamera.orthographic = true;
            uiCamera.nearClipPlane = 0.1f;
            uiCamera.farClipPlane = 10f;
            uiCamera.clearFlags = CameraClearFlags.SolidColor;
            uiCamera.backgroundColor = Color.black;
            uiCamera.cullingMask = 1 << uiLayer;
            uiCameraData = uiCamera.GetUniversalAdditionalCameraData();
            uiCameraData.renderType = CameraRenderType.Overlay;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private Camera CreateCamera(string cameraName)
        {
            var go = new GameObject(cameraName);
            go.transform.SetParent(transform, false);
            var cam = go.AddComponent<Camera>();
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            return cam;
        }

        private void LateUpdate()
        {
            Rect viewport = CalculateViewport(Screen.width, Screen.height);
            ApplyToCameras(viewport);
            ConvertOverlayCanvases();
        }

        /// <summary> シーンのカメラと UI 用のカメラが映す範囲を 16:9 にし、UI 用のカメラをシーンのカメラに重ねる </summary>
        private void ApplyToCameras(Rect viewport)
        {
            foreach (Camera cam in Camera.allCameras)
            {
                if (cam == barCamera) continue;
                if (cam.rect != viewport) cam.rect = viewport;
            }
            if (uiCamera.rect != viewport) uiCamera.rect = viewport;

            Camera baseCamera = Camera.main;
            if (baseCamera != null && baseCamera != uiCamera && baseCamera.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Base)
            {
                if (uiCameraData.renderType != CameraRenderType.Overlay) uiCameraData.renderType = CameraRenderType.Overlay;
                List<Camera> stack = baseCamera.GetUniversalAdditionalCameraData().cameraStack;
                if (!stack.Contains(uiCamera)) stack.Add(uiCamera);
            }
            else if (uiCameraData.renderType != CameraRenderType.Base)
            {
                // シーンにカメラがない（シーンの切り替え中など）ときは、UI 用のカメラだけで映す
                uiCameraData.renderType = CameraRenderType.Base;
            }
        }

        /// <summary> 画面全体に広がる UI（Screen Space - Overlay）を、UI 用のカメラに映す形に切り替える </summary>
        private void ConvertOverlayCanvases()
        {
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = CanvasPlaneDistance;
                // UI 用のカメラは UI のレイヤーだけを映すので、Canvas も UI のレイヤーにする（オプション画面は Default だった）
                canvas.gameObject.layer = uiLayer;
            }
        }
    }
}
