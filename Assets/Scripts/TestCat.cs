using UnityEngine;

public class TestCat : MonoBehaviour
{
    [Header("고양이 설정")]
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.cyan; // 흰색과 구별되도록 변경
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

    void Start()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // 고양이 스프라이트가 없으면 기본 스프라이트 생성
        if (spriteRenderer.sprite == null)
        {
            // 기본 원형 스프라이트 생성
            Texture2D texture = new Texture2D(64, 64);
            Color[] colors = new Color[64 * 64];

            // 원형 모양으로 색칠
            Vector2 center = new Vector2(32, 32);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance <= 30)
                    {
                        colors[y * 64 + x] = Color.cyan; // 고양이 색상 (흰색 피함)
                    }
                    else
                    {
                        colors[y * 64 + x] = Color.clear; // 투명
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

        // 색상 강제 설정
        spriteRenderer.color = normalColor;

        mainCamera = Camera.main;

        // 화면 경계 계산
        screenBounds = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, mainCamera.transform.position.z));

        // 초기 이동 방향 설정
        SetRandomDirection();

        // 고양이 레이어 설정 (Layer 8 = Interactable)
        gameObject.layer = 8;

        // Collider2D가 없으면 추가
        if (GetComponent<Collider2D>() == null)
        {
            CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
        }
    }

    void Update()
    {
        MoveCat();
        CheckInteraction();
    }

    void MoveCat()
    {
        // 이동
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime);

        // 화면 경계 체크 및 반사
        Vector3 pos = transform.position;
        bool changedDirection = false;

        if (pos.x <= -screenBounds.x || pos.x >= screenBounds.x)
        {
            moveDirection.x = -moveDirection.x;
            changedDirection = true;
        }

        if (pos.y <= -screenBounds.y || pos.y >= screenBounds.y)
        {
            moveDirection.y = -moveDirection.y;
            changedDirection = true;
        }

        // 화면 내부로 위치 보정
        pos.x = Mathf.Clamp(pos.x, -screenBounds.x, screenBounds.x);
        pos.y = Mathf.Clamp(pos.y, -screenBounds.y, screenBounds.y);
        transform.position = pos;

        // 주기적으로 방향 변경
        directionTimer += Time.deltaTime;
        if (directionTimer >= changeDirectionTime && !changedDirection)
        {
            SetRandomDirection();
        }
    }

    void SetRandomDirection()
    {
        moveDirection = new Vector2(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)
        ).normalized;

        directionTimer = 0f;
    }

    void CheckInteraction()
    {
        if (CompatibilityWindowManager.Instance == null) return;

        // 마우스 위치 가져오기
        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        // 고양이와 마우스 거리 계산
        float distance = Vector2.Distance(transform.position, mouseWorldPos);
        float interactionRadius = GetComponent<Collider2D>().bounds.size.x / 2f;

        // 마우스 클릭 체크
        if (Input.GetMouseButtonDown(0) && distance <= interactionRadius)
        {
            OnCatClicked();
        }
        // 마우스 호버 체크
        else if (distance <= interactionRadius)
        {
            if (currentState != InteractionState.Hover)
                OnCatHover();
        }
        // 마우스가 멀어졌을 때
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

        // 클릭 효과
        transform.localScale = Vector3.one * 1.2f;

        Debug.Log("고양이를 클릭했습니다!");

        // 0.2초 후 원래대로
        Invoke(nameof(ResetClickEffect), 0.2f);
    }

    void OnCatHover()
    {
        currentState = InteractionState.Hover;
        spriteRenderer.color = hoverColor;

        Debug.Log("고양이에 마우스를 올렸습니다!");
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

    // 디버그용
    void OnDrawGizmos()
    {
        if (GetComponent<Collider2D>() != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, GetComponent<Collider2D>().bounds.size.x / 2f);
        }
    }
}