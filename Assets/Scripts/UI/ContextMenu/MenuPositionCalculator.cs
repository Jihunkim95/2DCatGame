using UnityEngine;

/// <summary>
/// 메뉴 위치 계산을 담당하는 클래스
/// </summary>
public class MenuPositionCalculator : MonoBehaviour
{
    [Header("메뉴 위치 설정")]
    public Vector2 objectMenuOffset = new Vector2(100f, 0f);
    public bool useObjectPosition = true;
    public Vector2 menuSize = new Vector2(120f, 180f);

    private Camera mainCamera;
    private Canvas targetCanvas;

    public void Initialize(Camera camera, Canvas canvas)
    {
        mainCamera = camera;
        targetCanvas = canvas;
    }

    /// <summary>
    /// 오브젝트 위치 기준으로 메뉴 위치 계산
    /// </summary>
    public Vector3 CalculateMenuPosition(Vector3 objectWorldPosition)
    {
        if (!useObjectPosition)
        {
            return objectWorldPosition;
        }

        // 오브젝트를 스크린 좌표로 변환
        Vector3 objectScreenPos = mainCamera.WorldToScreenPoint(objectWorldPosition);

        // 화면 좌표에서 오프셋 적용 (오브젝트 우측으로)
        Vector3 menuScreenPos = objectScreenPos + new Vector3(objectMenuOffset.x, objectMenuOffset.y, 0);

        // 화면 경계 체크
        menuScreenPos.x = Mathf.Clamp(menuScreenPos.x, menuSize.x * 0.5f, Screen.width - menuSize.x * 0.5f);
        menuScreenPos.y = Mathf.Clamp(menuScreenPos.y, menuSize.y * 0.5f, Screen.height - menuSize.y * 0.5f);

        // 다시 월드 좌표로 변환
        Vector3 menuWorldPos = mainCamera.ScreenToWorldPoint(menuScreenPos);

        Debug.Log($"메뉴 위치 계산: 오브젝트 {objectWorldPosition} → 메뉴 {menuWorldPos}");
        return menuWorldPos;
    }

    /// <summary>
    /// UI 좌표로 변환
    /// </summary>
    public Vector2 ConvertToUIPosition(Vector3 worldPosition)
    {
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);

        Vector2 uiPosition;
        bool converted = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            targetCanvas.transform as RectTransform,
            screenPosition,
            targetCanvas.worldCamera,
            out uiPosition
        );

        if (!converted)
        {
            Debug.LogWarning("UI 좌표 변환 실패");
        }

        return uiPosition;
    }

    /// <summary>
    /// 메뉴가 화면 경계를 벗어나지 않도록 조정
    /// </summary>
    public void ClampMenuToScreen(RectTransform menuRect)
    {
        Canvas canvasComponent = targetCanvas.GetComponent<Canvas>();
        if (canvasComponent.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Vector3[] corners = new Vector3[4];
            menuRect.GetWorldCorners(corners);

            float menuWidth = corners[2].x - corners[0].x;
            float menuHeight = corners[2].y - corners[0].y;

            Vector3 pos = menuRect.localPosition;

            // 경계 체크 및 조정
            if (corners[2].x > Screen.width)
            {
                pos.x -= menuWidth;
                Debug.Log("메뉴가 오른쪽 경계를 벗어나서 왼쪽으로 이동");
            }

            if (corners[0].x < 0)
            {
                pos.x += Mathf.Abs(corners[0].x);
                Debug.Log("메뉴가 왼쪽 경계를 벗어나서 오른쪽으로 이동");
            }

            if (corners[0].y < 0)
            {
                pos.y += Mathf.Abs(corners[0].y);
                Debug.Log("메뉴가 아래쪽 경계를 벗어나서 위로 이동");
            }

            if (corners[2].y > Screen.height)
            {
                pos.y -= (corners[2].y - Screen.height);
                Debug.Log("메뉴가 위쪽 경계를 벗어나서 아래로 이동");
            }

            menuRect.localPosition = pos;
            Debug.Log($"메뉴 위치 조정 완료: {pos}");
        }
    }
}