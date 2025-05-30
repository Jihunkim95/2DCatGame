using UnityEngine;

public class CatTower : MonoBehaviour
{
    [Header("ĹŸ�� ����")]
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.green;
    public Color hoverColor = Color.yellow;

    [Header("���� ������")]
    public int level = 1;
    public int churCount = 0;
    public float productionTimer = 0f;
    public float productionInterval = 10f; // 10�ʸ��� ����

    private Camera mainCamera;
    private bool isHovered = false;

    // �̱���
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

        Debug.Log("ĹŸ�� ���� �Ϸ� - ���׷��̵� ���� ����");
        DebugLogger.LogToFile("ĹŸ�� ���� �Ϸ� - ���׷��̵� ���� ����");
    }

    public void CreateTowerSprite()
    {
        // ���忡�� ���������� �۵��ϴ� Ÿ�� ��������Ʈ ����
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

            Debug.Log($"Ÿ�� �̹��� �ε� ���� - ���� {level} (ũ��: {towerTexture.width}x{towerTexture.height})");
            DebugLogger.LogToFile($"Ÿ�� �̹��� �ε� ���� - ���� {level} (ũ��: {towerTexture.width}x{towerTexture.height})");
        }
        else
        {
            // �̹��� �ε� ���� �� ���α׷��� ������� Ÿ�� ��������Ʈ ����
            CreateProceduralTowerSprite();
            Debug.Log($"���α׷��� ������� Ÿ�� ��������Ʈ ���� - ���� {level}");
            DebugLogger.LogToFile($"���α׷��� ������� Ÿ�� ��������Ʈ ���� - ���� {level}");
        }
    }

    Texture2D LoadTowerTextureForBuild()
    {
        // ���忡�� Ȯ���ϰ� �۵��ϴ� ����鸸 �õ�

        // 1. Resources �������� �ε� (���� Ȯ���� ���)
        Texture2D resourceTexture = Resources.Load<Texture2D>("CatTower");
        if (resourceTexture != null)
        {
            Debug.Log("Ÿ�� �̹��� Resources���� �ε� ����");
            return resourceTexture;
        }

#if UNITY_EDITOR
        // �����Ϳ����� �߰� �õ�
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
                Debug.Log($"Ÿ�� �̹��� �����Ϳ��� �ε� ����: {path}.png");
                return texture;
            }
        }

        // GUID�� �˻�
        string[] guids = UnityEditor.AssetDatabase.FindAssets("CatTower t:Texture2D");
        if (guids.Length > 0)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
            {
                Debug.Log($"Ÿ�� �̹��� �˻����� �ε�: {assetPath}");
                return texture;
            }
        }
#endif

        Debug.LogWarning("Ÿ�� �̹����� ã�� �� �����ϴ�. ���α׷��� ������� �����մϴ�.");
        return null;
    }

    void CreateProceduralTowerSprite()
    {
        // �� �����ϰ� ���� Ÿ�� ��������Ʈ�� ���α׷��� ������� ����
        int width = 64;
        int height = 96;
        Texture2D texture = new Texture2D(width, height);
        Color[] colors = new Color[width * height];

        // ��� ����ȭ
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.clear;
        }

        // ������ �⺻ ����
        Color baseColor = GetLevelBaseColor();
        Color darkColor = new Color(baseColor.r * 0.7f, baseColor.g * 0.7f, baseColor.b * 0.7f, 1f);
        Color lightColor = new Color(
            Mathf.Min(baseColor.r * 1.3f, 1f),
            Mathf.Min(baseColor.g * 1.3f, 1f),
            Mathf.Min(baseColor.b * 1.3f, 1f),
            1f
        );

        // Ÿ�� ���̽� (�ϴ� ���� �κ�)
        DrawRectangle(colors, width, height, 8, 8, 48, 25, darkColor);      // �׵θ�
        DrawRectangle(colors, width, height, 10, 10, 44, 21, baseColor);    // ����

        // Ÿ�� �߰� �κ�
        DrawRectangle(colors, width, height, 12, 30, 40, 35, darkColor);    // �׵θ�
        DrawRectangle(colors, width, height, 14, 32, 36, 31, baseColor);    // ����

        // Ÿ�� ��� �κ�
        DrawRectangle(colors, width, height, 16, 60, 32, 25, darkColor);    // �׵θ�
        DrawRectangle(colors, width, height, 18, 62, 28, 21, lightColor);   // ���� (���)

        // ������ ��� �߰�
        AddLevelDecorations(colors, width, height, baseColor);

        // ������ (���� �簢����)
        DrawRectangle(colors, width, height, 20, 40, 6, 6, Color.cyan);
        DrawRectangle(colors, width, height, 38, 40, 6, 6, Color.cyan);
        DrawRectangle(colors, width, height, 29, 50, 6, 6, Color.cyan);

        // �ؽ�ó ����
        texture.SetPixels(colors);
        texture.Apply();

        // ��������Ʈ ����
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = Color.white; // �̹� ������ ����Ǿ����Ƿ� ��� ���

        Debug.Log($"���α׷��� ��� Ÿ�� ��������Ʈ ���� �Ϸ� - ���� {level}");
    }

    Color GetLevelBaseColor()
    {
        switch (level)
        {
            case 1:
                return new Color(0.4f, 0.8f, 0.4f, 1f); // ���� ���
            case 2:
                return new Color(0.6f, 0.6f, 0.8f, 1f); // ���� �����
            case 3:
                return new Color(0.8f, 0.6f, 0.4f, 1f); // ���� ��Ȳ��
            default:
                return new Color(0.7f, 0.7f, 0.7f, 1f); // ȸ��
        }
    }

    void AddLevelDecorations(Color[] colors, int width, int height, Color baseColor)
    {
        Color decorColor = new Color(baseColor.r * 1.5f, baseColor.g * 1.5f, baseColor.b * 0.5f, 1f);

        switch (level)
        {
            case 2:
                // ���� 2: ���׳� �߰�
                DrawRectangle(colors, width, height, 31, 85, 2, 8, decorColor);
                break;
            case 3:
                // ���� 3: �� ū ���׳��� ���
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
        // ���� ���α׷��� ������� ������ ���� �̹� ������ ����ǹǷ� ��� ��ȯ
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

            Debug.Log($"�� ����: +{production} (�� {churCount}��)");
            DebugLogger.LogToFile($"�� ����: +{production} (�� {churCount}��)");
        }
    }

    void CheckInteraction()
    {
        if (CompatibilityWindowManager.Instance == null) return;

        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        float distance = Vector2.Distance(transform.position, mouseWorldPos);
        float interactionRadius = GetComponent<Collider2D>().bounds.size.x / 2f;

        // ��Ŭ�� üũ - Modern UI Context Menu ǥ��
        if (Input.GetMouseButtonDown(1) && distance <= interactionRadius)
        {
            OnTowerRightClicked(mouseWorldPos);
        }
        // ���콺 ȣ�� üũ
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

        // ContextMenuManager ȣ��
        if (ContextMenuManager.Instance != null)
        {
            ContextMenuManager.Instance.ShowTowerMenu(transform.position);
        }

        Debug.Log("Ÿ�� ��Ŭ��!");
        DebugLogger.LogToFile("Ÿ�� ��Ŭ�� - ���ؽ�Ʈ �޴� ǥ��");

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

    // ���� ���� �޼����
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

            Debug.Log($"ĹŸ�� ���׷��̵�! ���� {level}, ��� {cost}");
            DebugLogger.LogToFile($"ĹŸ�� ���׷��̵�! ���� {level}, ��� {cost}");
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

    // �ܺο��� ������ �� �ִ� ������Ƽ��
    public int Level => level;
    public int ChurCount => churCount;
    public string ProductionInfo => $"���귮: {GetProductionAmount()}�� (����: {GetProductionTimeLeft():F1}��)";
    public string UpgradeInfo => level < 3 ? $"���׷��̵� ���: {GetUpgradeCost()}��" : "�ִ� ����";
}