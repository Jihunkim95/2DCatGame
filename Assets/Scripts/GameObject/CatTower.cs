using UnityEngine;

public class CatTower : MonoBehaviour
{
    [Header("캣타워 설정")]
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.green;
    public Color hoverColor = Color.yellow;

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
        PositionAtBottomRight();
        gameObject.layer = 9; // CatTower layer

        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.5f, 2f);
        }

        CreateTowerSprite();

        Debug.Log("캣타워 생성 완료 - 우측하단 배치");
        DebugLogger.LogToFile("캣타워 생성 완료 - 우측하단 배치");
    }

    void PositionAtBottomRight()
    {
        Vector3 screenBottomRight = new Vector3(Screen.width - 70, 30, 0);
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenBottomRight);
        worldPosition.z = 0;

        transform.position = worldPosition;

        Debug.Log($"캣타워 위치: {worldPosition} (화면 우측하단)");
        DebugLogger.LogToFile($"캣타워 위치: {worldPosition} (화면 우측하단)");
    }

    public void CreateTowerSprite()
    {
        // Assets/Image/CatTower.png 이미지 로드
        Texture2D towerTexture = LoadTowerTexture();

        if (towerTexture != null)
        {
            // 이미지 파일에서 스프라이트 생성
            Sprite towerSprite = Sprite.Create(
                towerTexture,
                new Rect(0, 0, towerTexture.width, towerTexture.height),
                new Vector2(0.5f, 0f), // 바닥 기준점
                100f // Pixels Per Unit
            );

            spriteRenderer.sprite = towerSprite;

            // 레벨에 따른 색상 변경 (이미지에 틴트 적용)
            Color levelColor = GetLevelColor();
            spriteRenderer.color = levelColor;

            Debug.Log($"타워 이미지 로드 성공 - 레벨 {level} (크기: {towerTexture.width}x{towerTexture.height})");
            DebugLogger.LogToFile($"타워 이미지 로드 성공 - 레벨 {level} (크기: {towerTexture.width}x{towerTexture.height})");
        }
        else
        {
            // 이미지 로드 실패 시 기본 프로그래밍 스프라이트 생성
            CreateFallbackSprite();
            Debug.LogWarning("CatTower.png를 찾을 수 없어 기본 스프라이트를 생성했습니다.");
            DebugLogger.LogToFile("CatTower.png를 찾을 수 없어 기본 스프라이트를 생성했습니다.");
        }
    }

    Texture2D LoadTowerTexture()
    {
        // 여러 경로에서 이미지 파일 찾기
        string[] imagePaths = {
            "Assets/Image/CatTower",
            "Assets/Images/CatTower",
            "Assets/Sprites/CatTower",
            "Assets/Art/CatTower"
        };

#if UNITY_EDITOR
        // 에디터에서 직접 로드
        foreach (string path in imagePaths)
        {
            Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path + ".png");
            if (texture != null)
            {
                Debug.Log($"타워 이미지 로드 성공: {path}.png");
                return texture;
            }
        }

        // GUID로 검색
        string[] guids = UnityEditor.AssetDatabase.FindAssets("CatTower t:Texture2D");
        if (guids.Length > 0)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
            {
                Debug.Log($"타워 이미지 검색으로 로드: {assetPath}");
                return texture;
            }
        }
#endif

        // Resources 폴더에서 로드 (빌드에서도 작동)
        Texture2D resourceTexture = Resources.Load<Texture2D>("CatTower");
        if (resourceTexture != null)
        {
            Debug.Log("타워 이미지 Resources에서 로드 성공");
            return resourceTexture;
        }

        return null;
    }

    Color GetLevelColor()
    {
        // 레벨에 따른 색상 틴트 (이미지에 곱셈으로 적용)
        switch (level)
        {
            case 1:
                return new Color(1f, 1f, 1f, 1f); // 원본 색상 (흰색 = 변화 없음)
            case 2:
                return new Color(1.2f, 1.1f, 0.8f, 1f); // 약간 노란빛 (골드)
            case 3:
                return new Color(1.3f, 0.9f, 1.2f, 1f); // 보라빛 (최고 레벨)
            default:
                return Color.white;
        }
    }

    void CreateFallbackSprite()
    {
        // 기존 프로그래밍 방식 스프라이트 (백업용)
        Texture2D texture = new Texture2D(64, 96);
        Color[] colors = new Color[64 * 96];

        // 타워 모양으로 색칠
        for (int y = 0; y < 96; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                if (x >= 8 && x < 56 && y >= 8 && y < 88) // 테두리 제외한 내부
                {
                    // 레벨에 따라 색상 변경
                    Color towerColor = level == 1 ? Color.green : (level == 2 ? Color.gray : Color.yellow);
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

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 96), new Vector2(0.5f, 0f));
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = normalColor;

        Debug.Log($"기본 타워 스프라이트 생성 - 레벨 {level}");
    }

    void Update()
    {
        UpdateProduction();
        CheckInteraction();
    }

    void UpdateProduction()
    {
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

        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        float distance = Vector2.Distance(transform.position, mouseWorldPos);
        float interactionRadius = GetComponent<Collider2D>().bounds.size.x / 2f;

        // 우클릭 체크 - Modern UI Context Menu 표시
        if (Input.GetMouseButtonDown(1) && distance <= interactionRadius)
        {
            OnTowerRightClicked(mouseWorldPos);
        }
        // 마우스 호버 체크
        else if (distance <= interactionRadius)
        {
            if (!isHovered)
                OnTowerHover();
        }
        else
        {
            if (isHovered)
                OnTowerNormal();
        }
    }

    void OnTowerRightClicked(Vector3 mousePosition)
    {
        spriteRenderer.color = Color.white;

        // ContextMenuManager 호출
        if (ContextMenuManager.Instance != null)
        {
            ContextMenuManager.Instance.ShowTowerMenu(transform.position);
        }

        Debug.Log("타워 우클릭!");
        DebugLogger.LogToFile("타워 우클릭 - 컨텍스트 메뉴 표시");

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
        return level + 1;
    }

    public int GetUpgradeCost()
    {
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