using UnityEngine;

public class SaveSystem : MonoBehaviour
{
    [Header("�ڵ� ���� ����")]
    public float autoSaveInterval = 30f; // 30�ʸ��� �ڵ� ����
    public bool enableAutoSave = true;

    private float autoSaveTimer = 0f;

    // �̱���
    public static SaveSystem Instance { get; private set; }

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
        // ���� ���� �� �ڵ� �ε�
        LoadGameData();

        Debug.Log("���̺� �ý��� �ʱ�ȭ �Ϸ�");
        DebugLogger.LogToFile("���̺� �ý��� �ʱ�ȭ �Ϸ� - Easy Save ���");
    }

    void Update()
    {
        // �ڵ� ���� Ÿ�̸�
        if (enableAutoSave)
        {
            autoSaveTimer += Time.deltaTime;

            if (autoSaveTimer >= autoSaveInterval)
            {
                SaveGameData();
                autoSaveTimer = 0f;
            }
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // ������ �Ͻ������� �� ����
            SaveGameData();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            // ��Ŀ���� ���� �� ����
            SaveGameData();
        }
    }

    void OnApplicationQuit()
    {
        // ���� ���� �� ����
        SaveGameData();
    }

    public void SaveGameData()
    {
        try
        {
            // CatTower ������ ����
            if (CatTower.Instance != null)
            {
                ES3.Save("catTowerLevel", CatTower.Instance.Level);
                ES3.Save("churCount", CatTower.Instance.ChurCount);
                ES3.Save("productionTimer", CatTower.Instance.productionTimer);

                Debug.Log($"ĹŸ�� ������ ����: ���� {CatTower.Instance.Level}, �� {CatTower.Instance.ChurCount}��");
                DebugLogger.LogToFile($"ĹŸ�� ������ ����: ���� {CatTower.Instance.Level}, �� {CatTower.Instance.ChurCount}��");
            }

            // GameDataManager ������ ����
            if (GameDataManager.Instance != null)
            {
                ES3.Save("happiness", GameDataManager.Instance.Happiness);

                Debug.Log($"���� ������ ����: �ູ�� {GameDataManager.Instance.Happiness:F1}%");
                DebugLogger.LogToFile($"���� ������ ����: �ູ�� {GameDataManager.Instance.Happiness:F1}%");
            }

            // ����� ��ġ ���� (���û���)
            if (TestCat.Instance != null)
            {
                Vector3 catPosition = TestCat.Instance.transform.position;
                ES3.Save("catPositionX", catPosition.x);
                ES3.Save("catPositionY", catPosition.y);

                Debug.Log($"����� ��ġ ����: ({catPosition.x:F2}, {catPosition.y:F2})");
                DebugLogger.LogToFile($"����� ��ġ ����: ({catPosition.x:F2}, {catPosition.y:F2})");
            }

            // ������ ���� �ð� ���
            ES3.Save("lastSaveTime", System.DateTime.Now.ToBinary());

            Debug.Log("���� ������ ���� �Ϸ�!");
            DebugLogger.LogToFile("���� ������ ���� �Ϸ�!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"���� ������ ���� ����: {e.Message}");
            DebugLogger.LogToFile($"���� ������ ���� ����: {e.Message}");
        }
    }

    public void LoadGameData()
    {
        try
        {
            // ����� �����Ͱ� �ִ��� Ȯ��
            if (!ES3.KeyExists("catTowerLevel"))
            {
                Debug.Log("����� ���� �����Ͱ� �����ϴ�. �� �������� �����մϴ�.");
                DebugLogger.LogToFile("����� ���� �����Ͱ� �����ϴ�. �� �������� �����մϴ�.");
                return;
            }

            // ������ ���� �ð� Ȯ��
            if (ES3.KeyExists("lastSaveTime"))
            {
                long lastSaveTimeBinary = ES3.Load<long>("lastSaveTime");
                System.DateTime lastSaveTime = System.DateTime.FromBinary(lastSaveTimeBinary);
                System.TimeSpan timeDifference = System.DateTime.Now - lastSaveTime;

                Debug.Log($"������ ����: {lastSaveTime}, ��� �ð�: {timeDifference.TotalMinutes:F1}��");
                DebugLogger.LogToFile($"������ ����: {lastSaveTime}, ��� �ð�: {timeDifference.TotalMinutes:F1}��");

                // �������� ���� ��� (���߿� ����)
                CalculateOfflineProgress(timeDifference);
            }

            // CatTower ������ �ε� (1�� �� - ������Ʈ �ʱ�ȭ ���)
            Invoke(nameof(LoadCatTowerData), 1f);

            // GameDataManager ������ �ε� (1�� ��)
            Invoke(nameof(LoadGameManagerData), 1f);

            // ����� ��ġ �ε� (1.5�� ��)
            Invoke(nameof(LoadCatPosition), 1.5f);

            Debug.Log("���� ������ �ε� ����!");
            DebugLogger.LogToFile("���� ������ �ε� ����!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"���� ������ �ε� ����: {e.Message}");
            DebugLogger.LogToFile($"���� ������ �ε� ����: {e.Message}");
        }
    }

    void LoadCatTowerData()
    {
        if (CatTower.Instance != null)
        {
            // ����� �� �ε� (�⺻�� ����)
            int savedLevel = ES3.Load("catTowerLevel", 1);
            int savedChurCount = ES3.Load("churCount", 0);
            float savedProductionTimer = ES3.Load("productionTimer", 0f);

            // CatTower�� ����
            CatTower.Instance.level = savedLevel;
            CatTower.Instance.churCount = savedChurCount;
            CatTower.Instance.productionTimer = savedProductionTimer;

            // ��������Ʈ ������Ʈ (������ ���� ���� ����)
            CatTower.Instance.CreateTowerSprite();

            Debug.Log($"ĹŸ�� ������ �ε�: ���� {savedLevel}, �� {savedChurCount}��");
            DebugLogger.LogToFile($"ĹŸ�� ������ �ε�: ���� {savedLevel}, �� {savedChurCount}��");
        }
    }

    void LoadGameManagerData()
    {
        if (GameDataManager.Instance != null)
        {
            float savedHappiness = ES3.Load("happiness", 100f);
            GameDataManager.Instance.happiness = savedHappiness;

            Debug.Log($"���� ������ �ε�: �ູ�� {savedHappiness:F1}%");
            DebugLogger.LogToFile($"���� ������ �ε�: �ູ�� {savedHappiness:F1}%");
        }
    }

    void LoadCatPosition()
    {
        if (TestCat.Instance != null && ES3.KeyExists("catPositionX"))
        {
            float savedX = ES3.Load("catPositionX", 0f);
            float savedY = ES3.Load("catPositionY", 0f);

            Vector3 savedPosition = new Vector3(savedX, savedY, 0f);
            TestCat.Instance.transform.position = savedPosition;

            Debug.Log($"����� ��ġ �ε�: ({savedX:F2}, {savedY:F2})");
            DebugLogger.LogToFile($"����� ��ġ �ε�: ({savedX:F2}, {savedY:F2})");
        }
    }

    void CalculateOfflineProgress(System.TimeSpan offlineTime)
    {
        if (CatTower.Instance == null) return;

        // �������� �� �� ���� ���
        double offlineMinutes = offlineTime.TotalMinutes;
        double productionCycles = offlineMinutes / 10.0; // 10�и��� ����

        if (productionCycles >= 1.0)
        {
            int productionAmount = CatTower.Instance.GetProductionAmount();
            int offlineProduction = (int)(productionCycles * productionAmount);

            // �ִ� 1�ð�ġ�� ���� (�ʹ� ���� ���� �ʱ� ����)
            int maxOfflineProduction = productionAmount * 6; // 1�ð� = 6�� ����
            offlineProduction = Mathf.Min(offlineProduction, maxOfflineProduction);

            if (offlineProduction > 0)
            {
                CatTower.Instance.churCount += offlineProduction;

                Debug.Log($"�������� ����: {offlineTime.TotalMinutes:F1}��, �� +{offlineProduction}��");
                DebugLogger.LogToFile($"�������� ����: {offlineTime.TotalMinutes:F1}��, �� +{offlineProduction}��");
            }
        }

        // �������� �� �ູ�� ���� ���
        if (GameDataManager.Instance != null)
        {
            float happinessDecay = (float)(offlineMinutes / 60.0 * GameDataManager.Instance.happinessDecayRate);
            GameDataManager.Instance.happiness -= happinessDecay;
            GameDataManager.Instance.happiness = Mathf.Clamp(GameDataManager.Instance.happiness, 0f, 100f);

            Debug.Log($"�������� �ູ�� ����: -{happinessDecay:F1}% (����: {GameDataManager.Instance.happiness:F1}%)");
            DebugLogger.LogToFile($"�������� �ູ�� ����: -{happinessDecay:F1}% (����: {GameDataManager.Instance.happiness:F1}%)");
        }
    }

    // ���� ����/�ε� �޼����
    public void ManualSave()
    {
        SaveGameData();
        Debug.Log("���� ���� �Ϸ�!");
    }

    public void ManualLoad()
    {
        LoadGameData();
        Debug.Log("���� �ε� �Ϸ�!");
    }

    // ������ �ʱ�ȭ (�� ����)
    public void ResetGameData()
    {
        ES3.DeleteKey("catTowerLevel");
        ES3.DeleteKey("churCount");
        ES3.DeleteKey("productionTimer");
        ES3.DeleteKey("happiness");
        ES3.DeleteKey("catPositionX");
        ES3.DeleteKey("catPositionY");
        ES3.DeleteKey("lastSaveTime");

        Debug.Log("���� ������ �ʱ�ȭ �Ϸ�!");
        DebugLogger.LogToFile("���� ������ �ʱ�ȭ �Ϸ�!");
    }

    // ���� ���� ���� ���� Ȯ��
    public bool HasSaveData()
    {
        return ES3.KeyExists("catTowerLevel");
    }
}