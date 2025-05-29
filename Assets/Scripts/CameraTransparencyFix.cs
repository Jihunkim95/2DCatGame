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
            Debug.LogError("���� ī�޶� ã�� �� �����ϴ�!");
            return;
        }

        // Unity ����ȭ ���� �ذ�: ���������� ���� ����
        // Windows API�� �������� �������� ó���ϵ��� ��������
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = Color.black; // ���� ���������� ����!

        // Built-in Pipeline�� �߰� ����
        mainCamera.allowHDR = false;
        mainCamera.allowMSAA = false;
        mainCamera.useOcclusionCulling = false;

        // ������ ����ȭ
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        Debug.Log($"ī�޶� ���� �Ϸ�!");
        Debug.Log($"Clear Flags: {mainCamera.clearFlags}");
        Debug.Log($"Background Color: {mainCamera.backgroundColor}");

        // �α� ���Ͽ��� ���
        DebugLogger.LogToFile($"ī�޶� Clear Flags: {mainCamera.clearFlags}");
        DebugLogger.LogToFile($"ī�޶� Background Color: {mainCamera.backgroundColor}");
        DebugLogger.LogToFile("������ ���� ������ ��� Ȱ��ȭ!");
    }

    // �� �����Ӹ��� ī�޶� ���� Ȯ��
    void Update()
    {
        if (mainCamera != null)
        {
            if (mainCamera.clearFlags != CameraClearFlags.SolidColor)
            {
                Debug.LogWarning("ī�޶� Clear Flags�� �����! ���� ��...");
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
            }

            if (mainCamera.backgroundColor != Color.black)
            {
                Debug.LogWarning("ī�޶� Background Color�� �����! ���� ��...");
                mainCamera.backgroundColor = Color.black; // ������ ����
            }
        }
    }
}