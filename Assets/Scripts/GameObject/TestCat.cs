using UnityEngine;
using System.Collections;

public class TestCat : MonoBehaviour
{
    [Header("고양이 설정")]
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.cyan;
    public Color hoverColor = Color.yellow;
    public Color clickColor = Color.green;

    [Header("이동 설정")]
    public float moveSpeed = 2f;
    public float changeDirectionTime = 3f;

    private Vector2 moveDirection;
    private float directionTimer;
    private Camera mainCamera;
    private Vector2 screenBounds;

    // 상호작용 상태
    private enum InteractionState
    {
        Normal,
        Hover,
        Clicked
    }
    private InteractionState currentState = InteractionState.Normal;

    // 싱글톤
    public static TestCat Instance { get; private set; }

    void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer.sprite == null)
        {
            CreateCatSprite();
        }

        spriteRenderer.color = normalColor;
        mainCamera = Camera.main;
        screenBounds = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, mainCamera.transform.position.z));
        SetRandomDirection();

        gameObject.layer = 8; // Interactable layer

        if (GetComponent<Collider2D>() == null)
        {
            CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
        }
    }

    void CreateCatSprite()
    {
        Texture2D texture = new Texture2D(64, 64);
        Color[] colors = new Color[64 * 64];

        Vector2 center = new Vector2(32, 32);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= 30)
                {
                    colors[y * 64 + x] = Color.cyan;
                }
                else
                {
                    colors[y * 64 + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        spriteRenderer.sprite = sprite;

        Debug.Log("기본 고양이 스프라이트 생성 완료");
        DebugLogger.LogToFile("기본 고양이 스프라이트 생성 완료");
    }

    void Update()
    {
        MoveCat();
        CheckInteraction();
    }

    void SetRandomDirection()
    {
        // x축 또는 y축 중 하나만 선택하도록 방향 설정
        if (Random.value > 0.5f)
        {
            moveDirection = new Vector2(Random.Range(-1f, 1f), 0).normalized; // x축으로만 이동
        }
        else
        {
            moveDirection = new Vector2(0, Random.Range(-1f, 1f)).normalized; // y축으로만 이동
        }

        directionTimer = 0f;
    }

    void MoveCat()
    {
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime);

        Vector3 pos = transform.position;
        bool changedDirection = false;

        // 화면 경계에 도달했을 때 x축 또는 y축 방향만 반전
        if (pos.x <= -screenBounds.x || pos.x >= screenBounds.x)
        {
            moveDirection = new Vector2(-moveDirection.x, 0); // x축 반전, y축은 0으로 유지
            changedDirection = true;
        }

        if (pos.y <= -screenBounds.y || pos.y >= screenBounds.y - 8.0f )
        {
            moveDirection = new Vector2(0, -moveDirection.y); // y축 반전, x축은 0으로 유지
            changedDirection = true;
        }
        pos.x = Mathf.Clamp(pos.x, -screenBounds.x, screenBounds.x);
        pos.y = Mathf.Clamp(pos.y, -screenBounds.y, screenBounds.y);
        transform.position = pos;

        directionTimer += Time.deltaTime;
        if (directionTimer >= changeDirectionTime && !changedDirection)
        {
            SetRandomDirection();
        }
    }

    void CheckInteraction()
    {
        if (CompatibilityWindowManager.Instance == null) return;

        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        float distance = Vector2.Distance(transform.position, mouseWorldPos);
        float interactionRadius = GetComponent<Collider2D>().bounds.size.x / 2f;

        // 우클릭 체크 - Modern UI Context Menu 표시
        if (Input.GetMouseButtonDown(1) && distance <= interactionRadius)
        {
            OnCatRightClicked(mouseWorldPos);
        }
        // 좌클릭 체크
        else if (Input.GetMouseButtonDown(0) && distance <= interactionRadius)
        {
            OnCatClicked();
        }
        // 마우스 호버 체크
        else if (distance <= interactionRadius)
        {
            if (currentState != InteractionState.Hover)
                OnCatHover();
        }
        else
        {
            if (currentState != InteractionState.Normal)
                OnCatNormal();
        }
    }

    void OnCatClicked()
    {
        currentState = InteractionState.Clicked;
        spriteRenderer.color = clickColor;
        transform.localScale = Vector3.one * 1.2f;

        Debug.Log("고양이를 클릭했습니다! (쓰다듬기)");
        DebugLogger.LogToFile("고양이를 클릭했습니다! (쓰다듬기)");

        Invoke(nameof(ResetClickEffect), 0.2f);
    }

    void OnCatRightClicked(Vector3 mousePosition)
    {
        currentState = InteractionState.Clicked;
        spriteRenderer.color = clickColor;
        transform.localScale = Vector3.one * 1.1f;

        // ContextMenuManager 호출
        if (ContextMenuManager.Instance != null)
        {
            ContextMenuManager.Instance.ShowCatMenu(transform.position);
        }

        Debug.Log("고양이 우클릭! 컨텍스트 메뉴 표시");
        DebugLogger.LogToFile("고양이 우클릭! 컨텍스트 메뉴 표시");

        Invoke(nameof(ResetClickEffect), 0.2f);
    }

    void OnCatHover()
    {
        currentState = InteractionState.Hover;
        spriteRenderer.color = hoverColor;
    }

    void OnCatNormal()
    {
        currentState = InteractionState.Normal;
        spriteRenderer.color = normalColor;
    }

    void ResetClickEffect()
    {
        transform.localScale = Vector3.one;
        OnCatNormal();
    }

    // 쓰다듬기 효과 코루틴 (Modern UI Context Menu에서 사용)
    public IEnumerator PetEffect()
    {
        // 색상 변화 효과
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.magenta; // 쓰다듬기 색상

        // 크기 변화 효과
        Vector3 originalScale = transform.localScale;
        transform.localScale = originalScale * 1.3f;

        // 0.5초 동안 효과 유지
        yield return new WaitForSeconds(0.5f);

        // 원래 상태로 복구
        spriteRenderer.color = originalColor;
        transform.localScale = originalScale;

        Debug.Log("쓰다듬기 효과 완료!");
    }

    void OnDrawGizmos()
    {
        if (GetComponent<Collider2D>() != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, GetComponent<Collider2D>().bounds.size.x / 2f);
        }
    }
}