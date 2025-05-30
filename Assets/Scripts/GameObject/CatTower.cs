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
        PositionAtBottomRight();
        gameObject.layer = 9; // CatTower layer

        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.5f, 2f);
        }

        CreateTowerSprite();

        Debug.Log("ĹŸ�� ���� �Ϸ� - �����ϴ� ��ġ");
        DebugLogger.LogToFile("ĹŸ�� ���� �Ϸ� - �����ϴ� ��ġ");
    }

    void PositionAtBottomRight()
    {
        Vector3 screenBottomRight = new Vector3(Screen.width - 70, 30, 0);
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenBottomRight);
        worldPosition.z = 0;

        transform.position = worldPosition;

        Debug.Log($"ĹŸ�� ��ġ: {worldPosition} (ȭ�� �����ϴ�)");
        DebugLogger.LogToFile($"ĹŸ�� ��ġ: {worldPosition} (ȭ�� �����ϴ�)");
    }

    public void CreateTowerSprite()
    {
        // Assets/Image/CatTower.png �̹��� �ε�
        Texture2D towerTexture = LoadTowerTexture();

        if (towerTexture != null)
        {
            // �̹��� ���Ͽ��� ��������Ʈ ����
            Sprite towerSprite = Sprite.Create(
                towerTexture,
                new Rect(0, 0, towerTexture.width, towerTexture.height),
                new Vector2(0.5f, 0f), // �ٴ� ������
                100f // Pixels Per Unit
            );

            spriteRenderer.sprite = towerSprite;

            // ������ ���� ���� ���� (�̹����� ƾƮ ����)
            Color levelColor = GetLevelColor();
            spriteRenderer.color = levelColor;

            Debug.Log($"Ÿ�� �̹��� �ε� ���� - ���� {level} (ũ��: {towerTexture.width}x{towerTexture.height})");
            DebugLogger.LogToFile($"Ÿ�� �̹��� �ε� ���� - ���� {level} (ũ��: {towerTexture.width}x{towerTexture.height})");
        }
        else
        {
            // �̹��� �ε� ���� �� �⺻ ���α׷��� ��������Ʈ ����
            CreateFallbackSprite();
            Debug.LogWarning("CatTower.png�� ã�� �� ���� �⺻ ��������Ʈ�� �����߽��ϴ�.");
            DebugLogger.LogToFile("CatTower.png�� ã�� �� ���� �⺻ ��������Ʈ�� �����߽��ϴ�.");
        }
    }

    Texture2D LoadTowerTexture()
    {
        // ���� ��ο��� �̹��� ���� ã��
        string[] imagePaths = {
            "Assets/Image/CatTower",
            "Assets/Images/CatTower",
            "Assets/Sprites/CatTower",
            "Assets/Art/CatTower"
        };

#if UNITY_EDITOR
        // �����Ϳ��� ���� �ε�
        foreach (string path in imagePaths)
        {
            Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path + ".png");
            if (texture != null)
            {
                Debug.Log($"Ÿ�� �̹��� �ε� ����: {path}.png");
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

        // Resources �������� �ε� (���忡���� �۵�)
        Texture2D resourceTexture = Resources.Load<Texture2D>("CatTower");
        if (resourceTexture != null)
        {
            Debug.Log("Ÿ�� �̹��� Resources���� �ε� ����");
            return resourceTexture;
        }

        return null;
    }

    Color GetLevelColor()
    {
        // ������ ���� ���� ƾƮ (�̹����� �������� ����)
        switch (level)
        {
            case 1:
                return new Color(1f, 1f, 1f, 1f); // ���� ���� (��� = ��ȭ ����)
            case 2:
                return new Color(1.2f, 1.1f, 0.8f, 1f); // �ణ ����� (���)
            case 3:
                return new Color(1.3f, 0.9f, 1.2f, 1f); // ����� (�ְ� ����)
            default:
                return Color.white;
        }
    }

    void CreateFallbackSprite()
    {
        // ���� ���α׷��� ��� ��������Ʈ (�����)
        Texture2D texture = new Texture2D(64, 96);
        Color[] colors = new Color[64 * 96];

        // Ÿ�� ������� ��ĥ
        for (int y = 0; y < 96; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                if (x >= 8 && x < 56 && y >= 8 && y < 88) // �׵θ� ������ ����
                {
                    // ������ ���� ���� ����
                    Color towerColor = level == 1 ? Color.green : (level == 2 ? Color.gray : Color.yellow);
                    colors[y * 64 + x] = towerColor;
                }
                else if (x >= 4 && x < 60 && y >= 4 && y < 92) // �׵θ�
                {
                    colors[y * 64 + x] = Color.black;
                }
                else
                {
                    colors[y * 64 + x] = Color.clear; // ����
                }
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 96), new Vector2(0.5f, 0f));
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = normalColor;

        Debug.Log($"�⺻ Ÿ�� ��������Ʈ ���� - ���� {level}");
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