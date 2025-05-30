using UnityEngine;

public class CatTower : MonoBehaviour
{
    [Header("캣타워 설정")]
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.brown;
    public Color hoverColor = Color.orange;

    [Header("게임 데이터")]
    public int level = 1;
    public int churCount = 0;
    public float productionTimer = 0f;
    public float productionInterval = 10f; // 10초마다 생산

    private Camera mainCamera;
    private bool isHovered = false;

    // 싱글톤
    public static CatTower Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        mainCamera = Camera.main;

        // 우측 하단에 배치
        PositionAtBottomRight();

        // CatTower 레이어 설정 (Layer 9 = CatTower)
        gameObject.layer = 9;

        // Collider2D가 없으면 추가
        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.5f, 2f); // 캣타워 크기에 맞게 조정
        }

        // 기본 캣타워 스프라이트 생성
        CreateTowerSprite();

        Debug.Log("캣타워 생성 완료 - 우측하단 배치");
        DebugLogger.LogToFile("캣타워 생성 완료 - 우측하단 배치");
    }

    void PositionAtBottomRight()
    {
        // 화면 우측하단에 캣타워 배치
        Vector3 screenBottomRight = new Vector3(Screen.width - 100, 100, 0); // 여백 100픽셀
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenBottomRight);
        worldPosition.z = 0; // 2D이므로 z는 0

        transform.position = worldPosition;

        Debug.Log($"캣타워 위치: {worldPosition} (화면 우측하단)");
        DebugLogger.LogToFile($"캣타워 위치: {worldPosition} (화면 우측하단)");
    }

    void CreateTowerSprite()
    {
        // 기본 캣타워 스프라이트 생성 (사각형 타워 모양)
        Texture2D texture = new Texture2D(64, 96); // 세로가 더 긴 타워 모양
        Color[] colors = new Color[64 * 96];

        // 타워 모양으로 색칠
        for (int y = 0; y < 96; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                if (x >= 8 && x < 56 && y >= 8 && y < 88) // 테두리 제외한 내부
                {
                    // 레벨에 따라 색상 변경
                    Color towerColor = level == 1 ? Color.brown : (level == 2 ? Color.gray : Color.yellow);
                    colors[y * 64 + x] = towerColor;
                }
                else if (x >= 4 && x < 60 && y >= 4 && y < 92) // 테두리
                {
                    colors[y * 64 + x] = Color.black;
                }
                else
                {
                    colors[y * 64 + x] = Color.clear; // 투명
                }
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 96), new Vector2(0.5f, 0f)); // 바닥 기준점
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = normalColor;

        Debug.Log($"캣타워 스프라이트 생성 - 레벨 {level}");
        DebugLogger.LogToFile($"캣타워 스프라이트 생성 - 레벨 {level}");
    }

    void Update()
    {
        UpdateProduction();
        CheckInteraction();
    }

    void UpdateProduction()
    {
        // 츄르 생산 타이머
        productionTimer += Time.deltaTime;

        if (productionTimer >= productionInterval)
        {
            int production = GetProductionAmount();
            churCount += production;
            productionTimer = 0f;

            Debug.Log($"츄르 생산: +{production} (총 {churCount}개)");
            DebugLogger.LogToFile($"츄르 생산: +{production} (총 {churCount}개)");
        }
    }

    void CheckInteraction()
    {
        if (CompatibilityWindowManager.Instance == null) return;

        // 마우스 위치 가져오기
        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        // 캣타워와 마우스 거리 계산
        float distance = Vector2.Distance(transform.position, mouseWorldPos);
        float interactionRadius = GetComponent<Collider2D>().bounds.size.x / 2f;

        // 우클릭 체크
        if (Input.GetMouseButtonDown(1) && distance <= interactionRadius) // 우클릭
        {
            OnTowerRightClicked(mouseWorldPos);
        }
        // 마우스 호버 체크
        else if (distance <= interactionRadius)
        {
            if (!isHovered)
                OnTowerHover();
        }
        // 마우스가 멀어졌을 때
        else
        {
            if (isHovered)
                OnTowerNormal();
        }
    }

    void OnTowerRightClicked(Vector3 mousePosition)
    {
        spriteRenderer.color = Color.white; // 잠시 하얀색으로

        // 컨텍스트 메뉴 표시
        if (ContextMenuManager.Instance != null)
        {
            ContextMenuManager.Instance.ShowTowerMenu(mousePosition);
        }

        Debug.Log("캣타워 우클릭!");
        DebugLogger.LogToFile("캣타워 우클릭 - 컨텍스트 메뉴 표시");

        // 0.2초 후 원래 색으로
        Invoke(nameof(ResetColor), 0.2f);
    }

    void OnTowerHover()
    {
        isHovered = true;
        spriteRenderer.color = hoverColor;
    }

    void OnTowerNormal()
    {
        isHovered = false;
        spriteRenderer.color = normalColor;
    }

    void ResetColor()
    {
        spriteRenderer.color = isHovered ? hoverColor : normalColor;
    }

    // 게임 로직 메서드들
    public int GetProductionAmount()
    {
        // 레벨에 따른 생산량: 1레벨=2개, 2레벨=3개, 3레벨=4개
        return level + 1;
    }

    public int GetUpgradeCost()
    {
        // 업그레이드 비용: 1→2레벨=6, 2→3레벨=8
        return level == 1 ? 6 : 8;
    }

    public bool CanUpgrade()
    {
        return level < 3 && churCount >= GetUpgradeCost();
    }

    public void Upgrade()
    {
        if (CanUpgrade())
        {
            int cost = GetUpgradeCost();
            churCount -= cost;
            level++;

            // 스프라이트 다시 생성 (레벨업 시 색상 변경)
            CreateTowerSprite();

            Debug.Log($"캣타워 업그레이드! 레벨 {level}, 비용 {cost}");
            DebugLogger.LogToFile($"캣타워 업그레이드! 레벨 {level}, 비용 {cost}");
        }
    }

    public float GetProductionTimeLeft()
    {
        return productionInterval - productionTimer;
    }

    public bool SpendChur(int amount)
    {
        if (churCount >= amount)
        {
            churCount -= amount;
            return true;
        }
        return false;
    }

    // 외부에서 접근할 수 있는 프로퍼티들
    public int Level => level;
    public int ChurCount => churCount;
    public string ProductionInfo => $"생산량: {GetProductionAmount()}개 (다음: {GetProductionTimeLeft():F1}초)";
    public string UpgradeInfo => level < 3 ? $"업그레이드 비용: {GetUpgradeCost()}개" : "최대 레벨";
}