using UnityEngine;

public class SaveSystem : MonoBehaviour
{
    [Header("자동 저장 설정")]
    public float autoSaveInterval = 30f; // 30초마다 자동 저장
    public bool enableAutoSave = true;

    private float autoSaveTimer = 0f;

    // 싱글톤
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
        // 게임 시작 시 자동 로드
        LoadGameData();

        Debug.Log("세이브 시스템 초기화 완료");
        DebugLogger.LogToFile("세이브 시스템 초기화 완료 - Easy Save 사용");
    }

    void Update()
    {
        // 자동 저장 타이머
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
            // 게임이 일시정지될 때 저장
            SaveGameData();
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            // 포커스를 잃을 때 저장
            SaveGameData();
        }
    }

    void OnApplicationQuit()
    {
        // 게임 종료 시 저장
        SaveGameData();
    }

    public void SaveGameData()
    {
        try
        {
            // CatTower 데이터 저장
            if (CatTower.Instance != null)
            {
                ES3.Save("catTowerLevel", CatTower.Instance.Level);
                ES3.Save("churCount", CatTower.Instance.ChurCount);
                ES3.Save("productionTimer", CatTower.Instance.productionTimer);

                Debug.Log($"캣타워 데이터 저장: 레벨 {CatTower.Instance.Level}, 츄르 {CatTower.Instance.ChurCount}개");
                DebugLogger.LogToFile($"캣타워 데이터 저장: 레벨 {CatTower.Instance.Level}, 츄르 {CatTower.Instance.ChurCount}개");
            }

            // GameDataManager 데이터 저장
            if (GameDataManager.Instance != null)
            {
                ES3.Save("happiness", GameDataManager.Instance.Happiness);

                Debug.Log($"게임 데이터 저장: 행복도 {GameDataManager.Instance.Happiness:F1}%");
                DebugLogger.LogToFile($"게임 데이터 저장: 행복도 {GameDataManager.Instance.Happiness:F1}%");
            }

            // 고양이 위치 저장 (선택사항)
            if (TestCat.Instance != null)
            {
                Vector3 catPosition = TestCat.Instance.transform.position;
                ES3.Save("catPositionX", catPosition.x);
                ES3.Save("catPositionY", catPosition.y);

                Debug.Log($"고양이 위치 저장: ({catPosition.x:F2}, {catPosition.y:F2})");
                DebugLogger.LogToFile($"고양이 위치 저장: ({catPosition.x:F2}, {catPosition.y:F2})");
            }

            // 마지막 저장 시간 기록
            ES3.Save("lastSaveTime", System.DateTime.Now.ToBinary());

            Debug.Log("게임 데이터 저장 완료!");
            DebugLogger.LogToFile("게임 데이터 저장 완료!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"게임 데이터 저장 실패: {e.Message}");
            DebugLogger.LogToFile($"게임 데이터 저장 실패: {e.Message}");
        }
    }

    public void LoadGameData()
    {
        try
        {
            // 저장된 데이터가 있는지 확인
            if (!ES3.KeyExists("catTowerLevel"))
            {
                Debug.Log("저장된 게임 데이터가 없습니다. 새 게임으로 시작합니다.");
                DebugLogger.LogToFile("저장된 게임 데이터가 없습니다. 새 게임으로 시작합니다.");
                return;
            }

            // 마지막 저장 시간 확인
            if (ES3.KeyExists("lastSaveTime"))
            {
                long lastSaveTimeBinary = ES3.Load<long>("lastSaveTime");
                System.DateTime lastSaveTime = System.DateTime.FromBinary(lastSaveTimeBinary);
                System.TimeSpan timeDifference = System.DateTime.Now - lastSaveTime;

                Debug.Log($"마지막 저장: {lastSaveTime}, 경과 시간: {timeDifference.TotalMinutes:F1}분");
                DebugLogger.LogToFile($"마지막 저장: {lastSaveTime}, 경과 시간: {timeDifference.TotalMinutes:F1}분");

                // 오프라인 진행 계산 (나중에 구현)
                CalculateOfflineProgress(timeDifference);
            }

            // CatTower 데이터 로드 (1초 후 - 오브젝트 초기화 대기)
            Invoke(nameof(LoadCatTowerData), 1f);

            // GameDataManager 데이터 로드 (1초 후)
            Invoke(nameof(LoadGameManagerData), 1f);

            // 고양이 위치 로드 (1.5초 후)
            Invoke(nameof(LoadCatPosition), 1.5f);

            Debug.Log("게임 데이터 로드 시작!");
            DebugLogger.LogToFile("게임 데이터 로드 시작!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"게임 데이터 로드 실패: {e.Message}");
            DebugLogger.LogToFile($"게임 데이터 로드 실패: {e.Message}");
        }
    }

    void LoadCatTowerData()
    {
        if (CatTower.Instance != null)
        {
            // 저장된 값 로드 (기본값 설정)
            int savedLevel = ES3.Load("catTowerLevel", 1);
            int savedChurCount = ES3.Load("churCount", 0);
            float savedProductionTimer = ES3.Load("productionTimer", 0f);

            // CatTower에 적용
            CatTower.Instance.level = savedLevel;
            CatTower.Instance.churCount = savedChurCount;
            CatTower.Instance.productionTimer = savedProductionTimer;

            // 스프라이트 업데이트 (레벨에 따른 외형 변경)
            CatTower.Instance.CreateTowerSprite();

            Debug.Log($"캣타워 데이터 로드: 레벨 {savedLevel}, 츄르 {savedChurCount}개");
            DebugLogger.LogToFile($"캣타워 데이터 로드: 레벨 {savedLevel}, 츄르 {savedChurCount}개");
        }
    }

    void LoadGameManagerData()
    {
        if (GameDataManager.Instance != null)
        {
            float savedHappiness = ES3.Load("happiness", 100f);
            GameDataManager.Instance.happiness = savedHappiness;

            Debug.Log($"게임 데이터 로드: 행복도 {savedHappiness:F1}%");
            DebugLogger.LogToFile($"게임 데이터 로드: 행복도 {savedHappiness:F1}%");
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

            Debug.Log($"고양이 위치 로드: ({savedX:F2}, {savedY:F2})");
            DebugLogger.LogToFile($"고양이 위치 로드: ({savedX:F2}, {savedY:F2})");
        }
    }

    void CalculateOfflineProgress(System.TimeSpan offlineTime)
    {
        if (CatTower.Instance == null) return;

        // 오프라인 중 츄르 생산 계산
        double offlineMinutes = offlineTime.TotalMinutes;
        double productionCycles = offlineMinutes / 10.0; // 10분마다 생산

        if (productionCycles >= 1.0)
        {
            int productionAmount = CatTower.Instance.GetProductionAmount();
            int offlineProduction = (int)(productionCycles * productionAmount);

            // 최대 1시간치만 적용 (너무 많이 주지 않기 위해)
            int maxOfflineProduction = productionAmount * 6; // 1시간 = 6번 생산
            offlineProduction = Mathf.Min(offlineProduction, maxOfflineProduction);

            if (offlineProduction > 0)
            {
                CatTower.Instance.churCount += offlineProduction;

                Debug.Log($"오프라인 진행: {offlineTime.TotalMinutes:F1}분, 츄르 +{offlineProduction}개");
                DebugLogger.LogToFile($"오프라인 진행: {offlineTime.TotalMinutes:F1}분, 츄르 +{offlineProduction}개");
            }
        }

        // 오프라인 중 행복도 감소 계산
        if (GameDataManager.Instance != null)
        {
            float happinessDecay = (float)(offlineMinutes / 60.0 * GameDataManager.Instance.happinessDecayRate);
            GameDataManager.Instance.happiness -= happinessDecay;
            GameDataManager.Instance.happiness = Mathf.Clamp(GameDataManager.Instance.happiness, 0f, 100f);

            Debug.Log($"오프라인 행복도 감소: -{happinessDecay:F1}% (현재: {GameDataManager.Instance.happiness:F1}%)");
            DebugLogger.LogToFile($"오프라인 행복도 감소: -{happinessDecay:F1}% (현재: {GameDataManager.Instance.happiness:F1}%)");
        }
    }

    // 수동 저장/로드 메서드들
    public void ManualSave()
    {
        SaveGameData();
        Debug.Log("수동 저장 완료!");
    }

    public void ManualLoad()
    {
        LoadGameData();
        Debug.Log("수동 로드 완료!");
    }

    // 데이터 초기화 (새 게임)
    public void ResetGameData()
    {
        ES3.DeleteKey("catTowerLevel");
        ES3.DeleteKey("churCount");
        ES3.DeleteKey("productionTimer");
        ES3.DeleteKey("happiness");
        ES3.DeleteKey("catPositionX");
        ES3.DeleteKey("catPositionY");
        ES3.DeleteKey("lastSaveTime");

        Debug.Log("게임 데이터 초기화 완료!");
        DebugLogger.LogToFile("게임 데이터 초기화 완료!");
    }

    // 저장 파일 존재 여부 확인
    public bool HasSaveData()
    {
        return ES3.KeyExists("catTowerLevel");
    }
}