using UnityEngine;

public class CatTower : MonoBehaviour
{
    [Header("ĹŸ�� ����")]
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.brown;
    public Color hoverColor = Color.orange;

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

        // ���� �ϴܿ� ��ġ
        PositionAtBottomRight();

        // CatTower ���̾� ���� (Layer 9 = CatTower)
        gameObject.layer = 9;

        // Collider2D�� ������ �߰�
        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1.5f, 2f); // ĹŸ�� ũ�⿡ �°� ����
        }

        // �⺻ ĹŸ�� ��������Ʈ ����
        CreateTowerSprite();

        Debug.Log("ĹŸ�� ���� �Ϸ� - �����ϴ� ��ġ");
        DebugLogger.LogToFile("ĹŸ�� ���� �Ϸ� - �����ϴ� ��ġ");
    }

    void PositionAtBottomRight()
    {
        // ȭ�� �����ϴܿ� ĹŸ�� ��ġ
        Vector3 screenBottomRight = new Vector3(Screen.width - 100, 100, 0); // ���� 100�ȼ�
        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenBottomRight);
        worldPosition.z = 0; // 2D�̹Ƿ� z�� 0

        transform.position = worldPosition;

        Debug.Log($"ĹŸ�� ��ġ: {worldPosition} (ȭ�� �����ϴ�)");
        DebugLogger.LogToFile($"ĹŸ�� ��ġ: {worldPosition} (ȭ�� �����ϴ�)");
    }

    void CreateTowerSprite()
    {
        // �⺻ ĹŸ�� ��������Ʈ ���� (�簢�� Ÿ�� ���)
        Texture2D texture = new Texture2D(64, 96); // ���ΰ� �� �� Ÿ�� ���
        Color[] colors = new Color[64 * 96];

        // Ÿ�� ������� ��ĥ
        for (int y = 0; y < 96; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                if (x >= 8 && x < 56 && y >= 8 && y < 88) // �׵θ� ������ ����
                {
                    // ������ ���� ���� ����
                    Color towerColor = level == 1 ? Color.brown : (level == 2 ? Color.gray : Color.yellow);
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

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 96), new Vector2(0.5f, 0f)); // �ٴ� ������
        spriteRenderer.sprite = sprite;
        spriteRenderer.color = normalColor;

        Debug.Log($"ĹŸ�� ��������Ʈ ���� - ���� {level}");
        DebugLogger.LogToFile($"ĹŸ�� ��������Ʈ ���� - ���� {level}");
    }

    void Update()
    {
        UpdateProduction();
        CheckInteraction();
    }

    void UpdateProduction()
    {
        // �� ���� Ÿ�̸�
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

        // ���콺 ��ġ ��������
        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        // ĹŸ���� ���콺 �Ÿ� ���
        float distance = Vector2.Distance(transform.position, mouseWorldPos);
        float interactionRadius = GetComponent<Collider2D>().bounds.size.x / 2f;

        // ��Ŭ�� üũ
        if (Input.GetMouseButtonDown(1) && distance <= interactionRadius) // ��Ŭ��
        {
            OnTowerRightClicked(mouseWorldPos);
        }
        // ���콺 ȣ�� üũ
        else if (distance <= interactionRadius)
        {
            if (!isHovered)
                OnTowerHover();
        }
        // ���콺�� �־����� ��
        else
        {
            if (isHovered)
                OnTowerNormal();
        }
    }

    void OnTowerRightClicked(Vector3 mousePosition)
    {
        spriteRenderer.color = Color.white; // ��� �Ͼ������

        // ���ؽ�Ʈ �޴� ǥ��
        if (ContextMenuManager.Instance != null)
        {
            ContextMenuManager.Instance.ShowTowerMenu(mousePosition);
        }

        Debug.Log("ĹŸ�� ��Ŭ��!");
        DebugLogger.LogToFile("ĹŸ�� ��Ŭ�� - ���ؽ�Ʈ �޴� ǥ��");

        // 0.2�� �� ���� ������
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
        // ������ ���� ���귮: 1����=2��, 2����=3��, 3����=4��
        return level + 1;
    }

    public int GetUpgradeCost()
    {
        // ���׷��̵� ���: 1��2����=6, 2��3����=8
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

            // ��������Ʈ �ٽ� ���� (������ �� ���� ����)
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