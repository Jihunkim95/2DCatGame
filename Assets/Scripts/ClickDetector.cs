using UnityEngine;
using System.Collections.Generic;

public class ClickDetector : MonoBehaviour
{
    [Header("Ŭ�� ���� ����")]
    public LayerMask interactableLayer = -1; // ����� ���̾�
    public float updateRate = 60f; // �ʴ� ���� Ƚ��

    private Camera mainCamera;
    private List<Collider2D> interactableObjects = new List<Collider2D>();
    private bool lastFrameHitSomething = false;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("���� ī�޶� ã�� �� �����ϴ�!");
            return;
        }

        // �ʱ⿡�� Ŭ�� ��� Ȱ��ȭ (����ȭ�� ��� ����)
        CompatibilityWindowManager.Instance?.EnableClickThrough();

        // ������Ʈ �ֱ� ����
        InvokeRepeating(nameof(CheckMousePosition), 0f, 1f / updateRate);
    }

    void CheckMousePosition()
    {
        if (CompatibilityWindowManager.Instance == null) return;

        // ������ �� ���콺 ��ġ ��������
        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();

        // ��ũ�� ��ǥ�� ���� ��ǥ�� ��ȯ
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        // �ش� ��ġ�� ��ȣ�ۿ� ������ ������Ʈ�� �ִ��� Ȯ��
        Collider2D hitCollider = Physics2D.OverlapPoint(worldPos, interactableLayer);

        bool currentFrameHitSomething = (hitCollider != null);

        // ���°� ����Ǿ��� ���� ������ �Ӽ� ����
        if (currentFrameHitSomething != lastFrameHitSomething)
        {
            if (currentFrameHitSomething)
            {
                // ����� ���� ���콺�� ���� - Ŭ�� ��� ��Ȱ��ȭ
                CompatibilityWindowManager.Instance.DisableClickThrough();

                // ���콺 Ŀ�� ���� (���û���)
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
            else
            {
                // ����� �ۿ� ���콺�� ���� - Ŭ�� ��� Ȱ��ȭ
                CompatibilityWindowManager.Instance.EnableClickThrough();
            }

            lastFrameHitSomething = currentFrameHitSomething;
        }
    }

    void OnDestroy()
    {
        // ���� ���� �� Ŭ�� ��� ��Ȱ��ȭ
        CompatibilityWindowManager.Instance?.DisableClickThrough();
    }

    // ����׿� - ���콺 ��ġ �ð�ȭ
    void OnDrawGizmos()
    {
        if (CompatibilityWindowManager.Instance != null && mainCamera != null)
        {
            Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(worldPos, 0.1f);
        }
    }
}