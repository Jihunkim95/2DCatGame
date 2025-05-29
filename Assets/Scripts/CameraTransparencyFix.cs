using UnityEngine;

public class CameraTransparencyFix : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        SetupCameraTransparency();
    }

    void SetupCameraTransparency()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("메인 카메라를 찾을 수 없습니다!");
            return;
        }

        // Unity 투명화 문제 해결: 검은색으로 강제 설정
        // Windows API가 검은색을 투명으로 처리하도록 설정했음
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = Color.black; // 완전 검은색으로 변경!

        // Built-in Pipeline용 추가 설정
        mainCamera.allowHDR = false;
        mainCamera.allowMSAA = false;
        mainCamera.useOcclusionCulling = false;

        // 렌더링 최적화
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        Debug.Log($"카메라 설정 완료!");
        Debug.Log($"Clear Flags: {mainCamera.clearFlags}");
        Debug.Log($"Background Color: {mainCamera.backgroundColor}");

        // 로그 파일에도 기록
        DebugLogger.LogToFile($"카메라 Clear Flags: {mainCamera.clearFlags}");
        DebugLogger.LogToFile($"카메라 Background Color: {mainCamera.backgroundColor}");
        DebugLogger.LogToFile("검은색 강제 렌더링 모드 활성화!");
    }

    // 매 프레임마다 카메라 설정 확인
    void Update()
    {
        if (mainCamera != null)
        {
            if (mainCamera.clearFlags != CameraClearFlags.SolidColor)
            {
                Debug.LogWarning("카메라 Clear Flags가 변경됨! 복구 중...");
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
            }

            if (mainCamera.backgroundColor != Color.black)
            {
                Debug.LogWarning("카메라 Background Color가 변경됨! 복구 중...");
                mainCamera.backgroundColor = Color.black; // 검은색 유지
            }
        }
    }
}