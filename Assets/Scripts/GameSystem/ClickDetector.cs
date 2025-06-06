using UnityEngine;
using System.Collections.Generic;

public class ClickDetector : MonoBehaviour
{
    [Header("클릭 감지 설정")]
    public LayerMask interactableLayer = -1; // 고양이 레이어 (Layer 8)
    public LayerMask towerLayer = -1; // 캣타워 레이어 (Layer 9)
    public LayerMask uiLayer = -1; // UI 레이어 (Layer 5) - BGM 버튼 포함
    public float updateRate = 60f; // 초당 감지 횟수

    [Header("UI 감지 설정")]
    public bool enableUIDetection = true; // UI 감지 활성화 여부
    public bool debugUIDetection = false; // UI 감지 디버그 로그

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

        // 초기에는 클릭 통과 효과 활성화 (백그라운드면 사용 가능)
        CompatibilityWindowManager.Instance?.EnableClickThrough();

        // 레이어 마스크 자동 설정 (Inspector에서 설정하지 않은 경우)
        SetupDefaultLayerMasks();

        // 업데이트 주기 설정
        InvokeRepeating(nameof(CheckMousePosition), 0f, 1f / updateRate);

        Debug.Log("ClickDetector 초기화 완료 - UI 감지 포함");
        DebugLogger.LogToFile("ClickDetector 초기화 완료 - UI 감지 포함");
    }

    void SetupDefaultLayerMasks()
    {
        // 기본 레이어 마스크 설정 (Inspector에서 -1로 설정된 경우)
        if (interactableLayer == -1)
        {
            interactableLayer = 1 << 8; // Layer 8 (Interactable)
            Debug.Log("Interactable Layer 자동 설정: Layer 8");
        }

        if (towerLayer == -1)
        {
            towerLayer = 1 << 9; // Layer 9 (Tower)
            Debug.Log("Tower Layer 자동 설정: Layer 9");
        }

        if (uiLayer == -1)
        {
            uiLayer = 1 << 5; // Layer 5 (UI)
            Debug.Log("UI Layer 자동 설정: Layer 5");
        }

        Debug.Log($"레이어 마스크 설정 완료 - Interactable: {interactableLayer.value}, Tower: {towerLayer.value}, UI: {uiLayer.value}");
    }

    void CheckMousePosition()
    {
        if (CompatibilityWindowManager.Instance == null) return;

        // 컨텍스트 메뉴가 표시 중이면 click-through 상태 변경하지 않음
        if (ContextMenuManager.Instance != null && ContextMenuManager.Instance.IsMenuVisible)
        {
            if (debugUIDetection)
                Debug.Log("컨텍스트 메뉴 표시 중이므로 click-through 상태 변경 생략");
            return;
        }

        // 윈도우 내 마우스 위치 가져오기
        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();

        // 스크린 좌표를 월드 좌표로 변환
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        // 상호작용 가능한 오브젝트 확인
        bool currentFrameHitSomething = CheckInteractableObjects(worldPos, mousePos);

        // 상태가 변경되었을 때만 윈도우 속성 변경
        if (currentFrameHitSomething != lastFrameHitSomething)
        {
            if (currentFrameHitSomething)
            {
                // 상호작용 가능한 오브젝트 위에 마우스가 있음 - 클릭 통과 비활성화
                CompatibilityWindowManager.Instance.DisableClickThrough();

                // 마우스 커서 변경 (선택사항)
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

                if (debugUIDetection)
                    Debug.Log("상호작용 오브젝트 위에 마우스 - click-through 비활성화");
            }
            else
            {
                // 상호작용 오브젝트 밖에 마우스가 있음 - 클릭 통과 활성화
                CompatibilityWindowManager.Instance.EnableClickThrough();

                if (debugUIDetection)
                    Debug.Log("빈 공간에 마우스 - click-through 활성화");
            }

            lastFrameHitSomething = currentFrameHitSomething;
        }
    }

    bool CheckInteractableObjects(Vector3 worldPos, Vector2 screenPos)
    {
        bool hitSomething = false;

        // 1. 2D 월드 오브젝트 확인 (고양이, 캣타워)
        Collider2D catCollider = Physics2D.OverlapPoint(worldPos, interactableLayer);
        Collider2D towerCollider = Physics2D.OverlapPoint(worldPos, towerLayer);

        if (catCollider != null)
        {
            if (debugUIDetection)
                Debug.Log($"고양이 감지: {catCollider.name}");
            hitSomething = true;
        }

        if (towerCollider != null)
        {
            if (debugUIDetection)
                Debug.Log($"타워 감지: {towerCollider.name}");
            hitSomething = true;
        }

        // 2. UI 오브젝트 확인 (BGM 버튼 등)
        if (enableUIDetection)
        {
            bool hitUI = CheckUIObjects(screenPos);
            if (hitUI)
            {
                hitSomething = true;
            }
        }

        return hitSomething;
    }

    bool CheckUIObjects(Vector2 screenPos)
    {
        // Canvas의 UI 요소들 확인
        Canvas[] canvases = FindObjectsOfType<Canvas>();

        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                // Screen Space - Overlay 모드의 Canvas
                if (CheckUIElementsInCanvas(canvas, screenPos))
                {
                    return true;
                }
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                // Screen Space - Camera 모드의 Canvas
                if (CheckUIElementsInCanvas(canvas, screenPos))
                {
                    return true;
                }
            }
        }

        // 추가: 특정 UI 레이어의 2D Collider 확인 (혹시 UI 오브젝트가 2D Collider를 사용하는 경우)
        Collider2D uiCollider = Physics2D.OverlapPoint(
            mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, mainCamera.nearClipPlane)),
            uiLayer
        );

        if (uiCollider != null)
        {
            if (debugUIDetection)
                Debug.Log($"UI 2D Collider 감지: {uiCollider.name}");
            return true;
        }

        return false;
    }

    bool CheckUIElementsInCanvas(Canvas canvas, Vector2 screenPos)
    {
        // Canvas 내의 모든 Graphic 컴포넌트 (Button, Image, Text 등) 확인
        UnityEngine.UI.Graphic[] graphics = canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>();

        foreach (var graphic in graphics)
        {
            // Raycast 가능한 UI 요소인지 확인
            if (graphic.raycastTarget && graphic.gameObject.activeInHierarchy)
            {
                RectTransform rectTransform = graphic.rectTransform;

                // UI 요소가 마우스 위치와 겹치는지 확인
                if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPos, canvas.worldCamera))
                {
                    // BGM 관련 오브젝트인지 특별히 확인
                    if (IsBGMRelatedObject(graphic.gameObject))
                    {
                        if (debugUIDetection)
                            Debug.Log($"BGM UI 요소 감지: {graphic.name}");
                        return true;
                    }
                    // 기타 상호작용 가능한 UI 요소
                    else if (IsInteractableUI(graphic.gameObject))
                    {
                        if (debugUIDetection)
                            Debug.Log($"상호작용 가능한 UI 요소 감지: {graphic.name}");
                        return true;
                    }
                }
            }
        }

        return false;
    }

    bool IsBGMRelatedObject(GameObject obj)
    {
        // BGM 관련 오브젝트 확인
        BGMManager bgmManager = obj.GetComponent<BGMManager>();
        if (bgmManager != null)
        {
            return true;
        }

        // 오브젝트 이름으로 BGM 관련 여부 확인
        string objName = obj.name.ToLower();
        if (objName.Contains("bgm") || objName.Contains("music") || objName.Contains("sound"))
        {
            return true;
        }

        // Button 컴포넌트가 있고 BGM 관련 기능을 하는지 확인
        UnityEngine.UI.Button button = obj.GetComponent<UnityEngine.UI.Button>();
        if (button != null)
        {
            // 버튼의 OnClick 이벤트에 BGM 관련 메서드가 연결되어 있는지 확인
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                string methodName = button.onClick.GetPersistentMethodName(i);
                if (methodName.ToLower().Contains("bgm") || methodName.ToLower().Contains("toggle"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    bool IsInteractableUI(GameObject obj)
    {
        // 일반적인 상호작용 가능한 UI 컴포넌트들 확인
        return obj.GetComponent<UnityEngine.UI.Button>() != null ||
               obj.GetComponent<UnityEngine.UI.Slider>() != null ||
               obj.GetComponent<UnityEngine.UI.Toggle>() != null ||
               obj.GetComponent<UnityEngine.UI.Dropdown>() != null ||
               obj.GetComponent<UnityEngine.UI.InputField>() != null ||
               obj.GetComponent<UnityEngine.UI.Scrollbar>() != null;
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

            // 감지 영역 표시
            if (lastFrameHitSomething)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(worldPos, 0.2f);
            }
        }
    }

    // 런타임에서 설정 변경할 수 있는 메서드들
    public void SetUIDetection(bool enabled)
    {
        enableUIDetection = enabled;
        Debug.Log($"UI 감지 설정 변경: {enabled}");
    }

    public void SetDebugMode(bool enabled)
    {
        debugUIDetection = enabled;
        Debug.Log($"UI 감지 디버그 모드: {enabled}");
    }

    // 현재 상태 확인 메서드
    public bool IsMouseOverInteractable()
    {
        return lastFrameHitSomething;
    }
}