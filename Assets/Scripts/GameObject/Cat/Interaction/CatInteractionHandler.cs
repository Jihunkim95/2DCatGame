using UnityEngine;

/// <summary>
/// 고양이와의 상호작용 처리를 담당하는 클래스
/// </summary>
public class CatInteractionHandler : MonoBehaviour
{
    [Header("상호작용 설정")]
    public Color normalColor = Color.white;
    public Color hoverColor = Color.yellow;
    public Color clickColor = Color.green;

    // 상호작용 상태
    private enum InteractionState
    {
        Normal,
        Hover,
        Clicked
    }

    private InteractionState currentState = InteractionState.Normal;
    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;
    private Vector3 originalScale;

    // 이벤트들
    public System.Action OnCatClicked;
    public System.Action<Vector3> OnCatRightClicked;
    public System.Action OnCatHovered;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        mainCamera = Camera.main;
        originalScale = transform.localScale;

        // 초기 색상 설정
        spriteRenderer.color = normalColor;

        // 고양이 레이어 설정
        gameObject.layer = 8;

        // Collider 설정
        if (GetComponent<Collider2D>() == null)
        {
            CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
        }
    }

    void Update()
    {
        CheckInteraction();
    }

    void CheckInteraction()
    {
        if (CompatibilityWindowManager.Instance == null) return;

        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        float distance = Vector2.Distance(transform.position, mouseWorldPos);
        float interactionRadius = GetComponent<Collider2D>().bounds.size.x / 2f;

        // 마우스 클릭 체크
        if (Input.GetMouseButtonDown(0) && distance <= interactionRadius)
        {
            HandleLeftClick();
        }
        else if (Input.GetMouseButtonDown(1) && distance <= interactionRadius)
        {
            HandleRightClick(mouseWorldPos);
        }
        else if (distance <= interactionRadius)
        {
            if (currentState != InteractionState.Hover)
                HandleHover();
        }
        else
        {
            if (currentState != InteractionState.Normal)
                HandleNormal();
        }
    }

    void HandleLeftClick()
    {
        currentState = InteractionState.Clicked;
        spriteRenderer.color = clickColor;
        transform.localScale = originalScale * 1.2f;

        Debug.Log("고양이를 클릭했습니다! (쓰다듬기)");
        DebugLogger.LogToFile("고양이를 클릭했습니다! (쓰다듬기)");

        OnCatClicked?.Invoke();

        Invoke(nameof(ResetClickEffect), 0.2f);
    }

    void HandleRightClick(Vector3 mousePosition)
    {
        currentState = InteractionState.Clicked;
        spriteRenderer.color = clickColor;
        transform.localScale = originalScale * 1.1f;

        Debug.Log("고양이 우클릭! 컨텍스트 메뉴 표시");
        DebugLogger.LogToFile("고양이 우클릭! 컨텍스트 메뉴 표시");

        OnCatRightClicked?.Invoke(mousePosition);

        Invoke(nameof(ResetClickEffect), 0.2f);
    }

    void HandleHover()
    {
        currentState = InteractionState.Hover;
        spriteRenderer.color = hoverColor;
        OnCatHovered?.Invoke();
    }

    void HandleNormal()
    {
        currentState = InteractionState.Normal;
        spriteRenderer.color = normalColor;
    }

    void ResetClickEffect()
    {
        transform.localScale = originalScale;
        HandleNormal();
        Debug.Log($"클릭 효과 리셋 - 스케일: {transform.localScale}");
    }

    // 외부에서 호출할 수 있는 행동들
    public void PerformPetting()
    {
        // 행복도 증가
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.happiness += 2f;
            GameDataManager.Instance.happiness = Mathf.Clamp(GameDataManager.Instance.happiness, 0f, 100f);
        }
    }

    public void PerformFeeding()
    {
        // 행복도 증가
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.FeedCat(1);
        }
    }

    // Gizmos로 상호작용 반경 표시
    void OnDrawGizmos()
    {
        if (GetComponent<Collider2D>() != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, GetComponent<Collider2D>().bounds.size.x / 2f);
        }
    }
}