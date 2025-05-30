using UnityEngine;
using System.Collections.Generic;

public class ClickDetector : MonoBehaviour
{
    [Header("클릭 감지 설정")]
    public LayerMask interactableLayer = -1; // 고양이 레이어
    public float updateRate = 60f; // 초당 감지 횟수

    private Camera mainCamera;
    private List<Collider2D> interactableObjects = new List<Collider2D>();
    private bool lastFrameHitSomething = false;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("메인 카메라를 찾을 수 없습니다!");
            return;
        }

        // 초기에는 클릭 통과 활성화 (바탕화면 사용 가능)
        CompatibilityWindowManager.Instance?.EnableClickThrough();

        // 업데이트 주기 설정
        InvokeRepeating(nameof(CheckMousePosition), 0f, 1f / updateRate);
    }

    void CheckMousePosition()
    {
        if (CompatibilityWindowManager.Instance == null) return;

        // 윈도우 내 마우스 위치 가져오기
        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();

        // 스크린 좌표를 월드 좌표로 변환
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        // 해당 위치에 상호작용 가능한 오브젝트가 있는지 확인
        Collider2D hitCollider = Physics2D.OverlapPoint(worldPos, interactableLayer);

        bool currentFrameHitSomething = (hitCollider != null);

        // 상태가 변경되었을 때만 윈도우 속성 변경
        if (currentFrameHitSomething != lastFrameHitSomething)
        {
            if (currentFrameHitSomething)
            {
                // 고양이 위에 마우스가 있음 - 클릭 통과 비활성화
                CompatibilityWindowManager.Instance.DisableClickThrough();

                // 마우스 커서 변경 (선택사항)
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
            else
            {
                // 고양이 밖에 마우스가 있음 - 클릭 통과 활성화
                CompatibilityWindowManager.Instance.EnableClickThrough();
            }

            lastFrameHitSomething = currentFrameHitSomething;
        }
    }

    void OnDestroy()
    {
        // 게임 종료 시 클릭 통과 비활성화
        CompatibilityWindowManager.Instance?.DisableClickThrough();
    }

    // 디버그용 - 마우스 위치 시각화
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