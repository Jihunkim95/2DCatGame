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
        gameObject.layer = 9; // CatTower layer

        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.5f, 2.0f);
            collider.offset = Vector2.zero;
        }

        CreateTowerSprite();

        Debug.Log("캣타워 생성 완료 - 업그레이드 가능 빌드");
        DebugLogger.LogToFile("캣타워 생성 완료 - 업그레이드 가능 빌드");
    }

    public void CreateTowerSprite()
    {
        // 빌드에서 안정적으로 작동하는 타워 스프라이트 생성
        Texture2D towerTexture = LoadTowerTextureForBuild();

        if (towerTexture != null)
        {
            Sprite towerSprite = Sprite.Create(
                towerTexture,
                new Rect(0, 0, towerTexture.width, towerTexture.height),
                new Vector2(0.5f, 0.5f),
                100f
            );

            spriteRenderer.sprite = towerSprite;
            spriteRenderer.color = GetLevelColor();

            Debug.Log($"타워 이미지 로드 성공 - 레벨 {level} (크기: {towerTexture.width}x{towerTexture.height})");
            DebugLogger.LogToFile($"타워 이미지 로드 성공 - 레벨 {level} (크기: {towerTexture.width}x{towerTexture.height})");
        }
        else
        {
            // 이미지 로드 실패 시 프로그래밍 방식으로 타워 스프라이트 생성
            CreateProceduralTowerSprite();
            Debug.Log($"프로그래밍 방식으로 타워 스프라이트 생성 - 레벨 {level}");
            DebugLogger.LogToFile($"프로그래밍 방식으로 타워 스프라이트 생성 - 레벨 {level}");
        }
    }

    Texture2D LoadTowerTextureForBuild()
    {
        // 빌드에서 확실하게 작동하는 방법들만 시도

        // 1. Resources 폴더에서 로드 (가장 확실한 방법)
        Texture2D resourceTexture = Resources.Load<Texture2D>("CatTower");
        if (resourceTexture != null)
        {
            Debug.Log("타워 이미지 Resources에서 로드 성공");
            return resourceTexture;
        }

#if UNITY_EDITOR
        // 에디터에서만 추가 시도
        string[] imagePaths = {
            "Assets/Image/CatTower",
            "Assets/Images/CatTower",
            "Assets/Sprites/CatTower",
            "Assets/Art/CatTower"
        };

        foreach (string path in imagePaths)
        {
            Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path + ".png");
            if (texture != null)
            {
                Debug.Log($"타워 이미지 에디터에서 로드 성공: {path}.png");
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

        Debug.LogWarning("타워 이미지를 찾을 수 없습니다. 프로그래밍 방식으로 생성합니다.");
        return null;
    }

    void CreateProceduralTowerSprite()
    {
        // 더 세밀하고 예쁜 타워 스프라이트를 프로그래밍 방식으로 생성
        int width = 64;
        int height = 96;
        Texture2D texture = new Texture2D(width, height);
        Color[] colors = new Color[width * height];

        // 배경 투명화
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.clear;
        }

        // 레벨별 기본 색상
        Color baseColor = GetLevelBaseColor();
        Color darkColor = new Color(baseColor.r * 0.7f, baseColor.g * 0.7f, baseColor.b * 0.7f, 1f);
        Color lightColor = new Color(
            Mathf.Min(baseColor.r * 1.3f, 1f),
            Mathf.Min(baseColor.g * 1.3f, 1f),
            Mathf.Min(baseColor.b * 1.3f, 1f),
            1f
        );

        // 타워 베이스 (하단 넓은 부분)
        DrawRectangle(colors, width, height, 8, 8, 48, 25, darkColor);      // 테두리
        DrawRectangle(colors, width, height, 10, 10, 44, 21, baseColor);    // 내부

        // 타워 중간 부분
        DrawRectangle(colors, width, height, 12, 30, 40, 35, darkColor);    // 테두리
        DrawRectangle(colors, width, height, 14, 32, 36, 31, baseColor);    // 내부

        // 타워 상단 부분
        DrawRectangle(colors, width, height, 16, 60, 32, 25, darkColor);    // 테두리
        DrawRectangle(colors, width, height, 18, 62, 28, 21, lightColor);   // 내부 (밝게)

        // 레벨별 장식 추가
        AddLevelDecorations(colors, width, height, baseColor);

        // 윈도우 (작은 사각형들)
        DrawRectangle(colors, width, height, 20, 40, 6, 6, Color.cyan);
        DrawRectangle(colors, width, height, 38, 40, 6, 6, Color.cyan);
        DrawRectangle(colors, width, height, 29, 50, 6, 6, Color.cyan);

        // 텍스처 적용
        texture.SetPixels(colors);
        texture.Apply();

        // 스프라이트 생성
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = Color.white; // 이미 색상이 적용되었으므로 흰색 사용

        Debug.Log($"프로그래밍 방식 타워 스프라이트 생성 완료 - 레벨 {level}");
    }

    Color GetLevelBaseColor()
    {
        switch (level)
        {
            case 1:
                return new Color(0.4f, 0.8f, 0.4f, 1f); // 연한 녹색
            case 2:
                return new Color(0.6f, 0.6f, 0.8f, 1f); // 연한 보라색
            case 3:
                return new Color(0.8f, 0.6f, 0.4f, 1f); // 연한 주황색
            default:
                return new Color(0.7f, 0.7f, 0.7f, 1f); // 회색
        }
    }

    void AddLevelDecorations(Color[] colors, int width, int height, Color baseColor)
    {
        Color decorColor = new Color(baseColor.r * 1.5f, baseColor.g * 1.5f, baseColor.b * 0.5f, 1f);

        switch (level)
        {
            case 2:
                // 레벨 2: 안테나 추가
                DrawRectangle(colors, width, height, 31, 85, 2, 8, decorColor);
                break;
            case 3:
                // 레벨 3: 더 큰 안테나와 깃발
                DrawRectangle(colors, width, height, 31, 85, 2, 10, decorColor);
                DrawRectangle(colors, width, height, 28, 88, 8, 3, Color.red);
                break;
        }
    }

    void DrawRectangle(Color[] colors, int textureWidth, int textureHeight, int x, int y, int width, int height, Color color)
    {
        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                int pixelX = x + dx;
                int pixelY = y + dy;

                if (pixelX >= 0 && pixelX < textureWidth && pixelY >= 0 && pixelY < textureHeight)
                {
                    int index = pixelY * textureWidth + pixelX;
                    if (index >= 0 && index < colors.Length)
                    {
                        colors[index] = color;
                    }
                }
            }
        }
    }

    Color GetLevelColor()
    {
        // 이제 프로그래밍 방식으로 생성할 때는 이미 색상이 적용되므로 흰색 반환
        return Color.white;
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