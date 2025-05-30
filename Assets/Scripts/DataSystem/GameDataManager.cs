using UnityEngine;

public class GameDataManager : MonoBehaviour
{
 
    [Header("���� ������")]
    public float happiness = 100f;
    public float maxHappiness = 100f;
    public float happinessDecayRate = 10f; // 1�ð��� 10 ����

    [Header("�����ֱ� ����")]
    public float happinessPerChur = 10f; // �� 1���� �ູ�� ������

    // �̱���
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
        Debug.Log("���� ������ �Ŵ��� �ʱ�ȭ");
        DebugLogger.LogToFile($"���� ������ �Ŵ��� �ʱ�ȭ - �ʱ� �ູ��: {happiness}%");
    }

    void Update()
    {
        UpdateHappiness();
    }

    void UpdateHappiness()
    {
        // �ð��� ���� �ູ�� ����
        float decayAmount = (happinessDecayRate / 3600f) * Time.deltaTime; // �ʴ� ���ҷ�
        happiness -= decayAmount;
        happiness = Mathf.Clamp(happiness, 0f, maxHappiness);
    }

    public void FeedCat(int churAmount)
    {
        float happinessIncrease = churAmount * happinessPerChur;
        happiness += happinessIncrease;
        happiness = Mathf.Clamp(happiness, 0f, maxHappiness);

        Debug.Log($"����� �����ֱ�: +{happinessIncrease} �ູ�� (����: {happiness:F1}%)");
        DebugLogger.LogToFile($"����� �����ֱ�: �� {churAmount}��, +{happinessIncrease} �ູ�� (����: {happiness:F1}%)");
    }

    // �ܺο��� ������ �� �ִ� ������Ƽ��
    public float Happiness => happiness;
    public float HappinessPercentage => (happiness / maxHappiness) * 100f;
    public string HappinessStatus
    {
        get
        {
            if (happiness > 80f) return "�ſ� �ູ";
            else if (happiness > 60f) return "�ູ";
            else if (happiness > 40f) return "����";
            else if (happiness > 20f) return "���";
            else return "�ſ� ���";
        }
    }
}