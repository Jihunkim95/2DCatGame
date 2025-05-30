using UnityEngine;
using System.Collections.Generic;

public class ClickDetector : MonoBehaviour
{
    [Header("Ŭ�� ���� ����")]
    public LayerMask interactableLayer = -1; // ����� ���̾� (Layer 8)
    public LayerMask towerLayer = -1; // ĹŸ�� ���̾� (Layer 9)
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

        // �ʱ⿡�� Ŭ�� ��� ȿ�� Ȱ��ȭ (��׶���� ��� ����)
        CompatibilityWindowManager.Instance?.EnableClickThrough();

        // ������Ʈ �ֱ� ����
        InvokeRepeating(nameof(CheckMousePosition), 0f, 1f / updateRate);
    }

    void CheckMousePosition()
    {
        if (CompatibilityWindowManager.Instance == null) return;

        // ���ؽ�Ʈ �޴��� ǥ�� ���̸� click-through ���� �������� ����
        if (ContextMenuManager.Instance != null && ContextMenuManager.Instance.IsMenuVisible)
        {
            Debug.Log("���ؽ�Ʈ �޴� ǥ�� ���̹Ƿ� click-through ���� ���� ����");
            return;
        }

        // ������ �� ���콺 ��ġ ��������
        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();

        // ��ũ�� ��ǥ�� ���� ��ǥ�� ��ȯ
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        // ��ȣ�ۿ� ������ ������Ʈ Ȯ�� (����� + ĹŸ��)
        Collider2D catCollider = Physics2D.OverlapPoint(worldPos, interactableLayer);
        Collider2D towerCollider = Physics2D.OverlapPoint(worldPos, towerLayer);

        bool currentFrameHitSomething = (catCollider != null || towerCollider != null);

        // ���°� ����Ǿ��� ���� ������ �Ӽ� ����
        if (currentFrameHitSomething != lastFrameHitSomething)
        {
            if (currentFrameHitSomething)
            {
                // ����̳� ĹŸ�� ���� ���콺�� ���� - Ŭ�� ��� ��Ȱ��ȭ
                CompatibilityWindowManager.Instance.DisableClickThrough();

                // ���콺 Ŀ�� ���� (���û���)
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

                Debug.Log("��ȣ�ۿ� ������Ʈ ���� ���콺 - click-through ��Ȱ��ȭ");
            }
            else
            {
                // ��ȣ�ۿ� ������Ʈ �ۿ� ���콺�� ���� - Ŭ�� ��� Ȱ��ȭ
                CompatibilityWindowManager.Instance.EnableClickThrough();

                Debug.Log("�� ������ ���콺 - click-through Ȱ��ȭ");
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