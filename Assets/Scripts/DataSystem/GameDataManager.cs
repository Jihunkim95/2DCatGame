using UnityEngine;

public class GameDataManager : MonoBehaviour
{
 
    [Header("게임 데이터")]
    public float happiness = 100f;
    public float maxHappiness = 100f;
    public float happinessDecayRate = 10f; // 1시간당 10 감소

    [Header("먹이주기 설정")]
    public float happinessPerChur = 10f; // 츄르 1개당 행복도 증가량

    // 싱글톤
    public static GameDataManager Instance { get; private set; }

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
        Debug.Log("게임 데이터 매니저 초기화");
        DebugLogger.LogToFile($"게임 데이터 매니저 초기화 - 초기 행복도: {happiness}%");
    }

    void Update()
    {
        UpdateHappiness();
    }

    void UpdateHappiness()
    {
        // 시간에 따라 행복도 감소
        float decayAmount = (happinessDecayRate / 3600f) * Time.deltaTime; // 초당 감소량
        happiness -= decayAmount;
        happiness = Mathf.Clamp(happiness, 0f, maxHappiness);
    }

    public void FeedCat(int churAmount)
    {
        float happinessIncrease = churAmount * happinessPerChur;
        happiness += happinessIncrease;
        happiness = Mathf.Clamp(happiness, 0f, maxHappiness);

        Debug.Log($"고양이 먹이주기: +{happinessIncrease} 행복도 (현재: {happiness:F1}%)");
        DebugLogger.LogToFile($"고양이 먹이주기: 츄르 {churAmount}개, +{happinessIncrease} 행복도 (현재: {happiness:F1}%)");
    }

    // 외부에서 접근할 수 있는 프로퍼티들
    public float Happiness => happiness;
    public float HappinessPercentage => (happiness / maxHappiness) * 100f;
    public string HappinessStatus
    {
        get
        {
            if (happiness > 80f) return "매우 행복";
            else if (happiness > 60f) return "행복";
            else if (happiness > 40f) return "보통";
            else if (happiness > 20f) return "우울";
            else return "매우 우울";
        }
    }
}