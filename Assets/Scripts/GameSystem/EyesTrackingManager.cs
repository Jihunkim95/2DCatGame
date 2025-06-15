using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 시선 추적 미니게임 관리자 - 실제 웹캠 눈 추적 호환 버전
/// </summary>
public class EyesTrackingManager : MonoBehaviour
{
    [Header("미니게임 설정")]
    public float gameDuration = 30f;           // 게임 지속 시간
    public float pointFollowSpeed = 5f;         // point 스프라이트 따라가는 속도
    public float footEffectDuration = 0.5f;    // cat_foot 효과 지속 시간

    [Header("UI 요소")]
    public Button minigameBtn;                  // 미니게임 시작 버튼
    public Text timerText;                      // 타이머 텍스트 (선택사항)
    public Text scoreText;                      // 점수 텍스트 (선택사항)

    [Header("폰트 설정")]
    public Font dungGeunMoFont;                 // DungGeunMo 폰트

    [Header("스프라이트 요소")]
    public Sprite catFootSprite;                // cat_foot 스프라이트
    public Sprite pointSprite;                  // point 스프라이트
    public Sprite churuSprite;                  // churu 스프라이트

    [Header("츄르 생성 설정")]
    public float churuSpawnInterval = 5f;       // 츄르 생성 간격 (5초)
    public int maxChuruCount = 5;               // 최대 츄르 개수

    [Header("게임 오브젝트")]
    public TestCat targetCat;                   // 조작할 고양이
    public Camera mainCamera;                   // 메인 카메라

    [Header("시선 추적 모드 선택")]
    public EyeTrackingMode trackingMode = EyeTrackingMode.RealWebcam;
    public float gazeFixationTime = 1.5f;       // 시선 고정 시간 (초)
    public float gazeFixationRadius = 50f;      // 시선 고정 반경 (픽셀)

    [Header("호환성 설정")]
    public bool fallbackToMouse = true;         // 실패 시 마우스로 폴백

    public enum EyeTrackingMode
    {
        RealWebcam,         // 실제 웹캠 눈 추적
        SimplifiedMouse,    // 마우스 시뮬레이션
        Mouse               // 순수 마우스 모드
    }

    // 게임 상태
    private bool isGameActive = false;
    private float gameTimer = 0f;
    private int currentScore = 0;

    // 게임 오브젝트들
    private GameObject pointObject;             // point 스프라이트 오브젝트
    private Vector3 targetPosition;             // 고양이가 향할 목표 위치
    private bool catIsMovingToTarget = false;
    private List<GameObject> churuObjects = new List<GameObject>(); // 생성된 츄르들
    private float churuSpawnTimer = 0f;         // 츄르 생성 타이머

    // 시선 고정 감지용 변수들
    private Vector2 lastGazePos = Vector2.zero;
    private float fixationTimer = 0f;
    private bool fixationInitialized = false;

    // 현재 사용 중인 추적 모드
    private EyeTrackingMode activeTrackingMode;

    // 싱글톤
    public static EyesTrackingManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        InitializeComponents();
        LoadDungGeunMoFont();
        SetupUI();
        DetermineTrackingMode();

        Debug.Log("EyesTracking 미니게임 시스템 초기화 완료");
        Debug.Log($"👁️ 시선 추적 모드: {activeTrackingMode}");
        DebugLogger.LogToFile($"EyesTracking 미니게임 시스템 초기화 완료 - 모드: {activeTrackingMode}");
    }

    void DetermineTrackingMode()
    {
        activeTrackingMode = trackingMode;

        // 실제 웹캠 모드 확인
        if (trackingMode == EyeTrackingMode.RealWebcam)
        {
#if OPENCV_FOR_UNITY
            if (RealWebcamEyeTracker.Instance != null)
            {
                Debug.Log("✅ 실제 웹캠 눈 추적 시스템 감지");
                activeTrackingMode = EyeTrackingMode.RealWebcam;
            }
            else
            {
                Debug.LogWarning("⚠️ RealWebcamEyeTracker를 찾을 수 없습니다.");
                HandleTrackingModeFallback();
            }
#else
            Debug.LogWarning("⚠️ OpenCV for Unity가 설치되지 않았습니다.");
            HandleTrackingModeFallback();
#endif
        }
        // SimplifiedMouse 모드 확인
        else if (trackingMode == EyeTrackingMode.SimplifiedMouse)
        {
            if (SimplifiedEyeTracker.Instance != null)
            {
                Debug.Log("✅ SimplifiedEyeTracker 시스템 감지");
                activeTrackingMode = EyeTrackingMode.SimplifiedMouse;
            }
            else
            {
                Debug.LogWarning("⚠️ SimplifiedEyeTracker를 찾을 수 없습니다.");
                HandleTrackingModeFallback();
            }
        }
        // 순수 마우스 모드
        else
        {
            Debug.Log("🖱️ 순수 마우스 모드 사용");
            activeTrackingMode = EyeTrackingMode.Mouse;
        }
    }

    void HandleTrackingModeFallback()
    {
        if (fallbackToMouse)
        {
            Debug.Log("🔄 마우스 모드로 폴백");
            activeTrackingMode = EyeTrackingMode.Mouse;
        }
        else if (SimplifiedEyeTracker.Instance != null)
        {
            Debug.Log("🔄 SimplifiedEyeTracker로 폴백");
            activeTrackingMode = EyeTrackingMode.SimplifiedMouse;
        }
        else
        {
            Debug.Log("🔄 순수 마우스 모드로 폴백");
            activeTrackingMode = EyeTrackingMode.Mouse;
        }
    }

    void InitializeComponents()
    {
        // 컴포넌트 자동 할당
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (targetCat == null)
            targetCat = FindObjectOfType<TestCat>();

        // 스프라이트 로드 (Resources에서)
        LoadSprites();

        // MinigameBtn 자동 찾기
        if (minigameBtn == null)
        {
            GameObject minigameBtnObj = GameObject.Find("Minigamebtn");
            if (minigameBtnObj != null)
            {
                minigameBtn = minigameBtnObj.GetComponent<Button>();
            }
        }
    }

    void LoadSprites()
    {
        if (catFootSprite == null)
        {
            catFootSprite = Resources.Load<Sprite>("Image/Minigame/cat_foot");
            if (catFootSprite == null)
                Debug.LogWarning("cat_foot 스프라이트를 찾을 수 없습니다!");
        }

        if (pointSprite == null)
        {
            pointSprite = Resources.Load<Sprite>("Image/Minigame/point");
            if (pointSprite == null)
                Debug.LogWarning("point 스프라이트를 찾을 수 없습니다!");
        }

        if (churuSprite == null)
        {
            churuSprite = Resources.Load<Sprite>("Image/Minigame/churu");
            if (churuSprite == null)
                Debug.LogWarning("churu 스프라이트를 찾을 수 없습니다!");
        }
    }

    void LoadDungGeunMoFont()
    {
        if (dungGeunMoFont != null) return;

        string[] resourcePaths = {
            "Font/DungGeunMo SDF",
            "Font/DungGeunMo",
            "DungGeunMo SDF",
            "DungGeunMo"
        };

        foreach (string path in resourcePaths)
        {
            Font font = Resources.Load<Font>(path);
            if (font != null)
            {
                dungGeunMoFont = font;
                Debug.Log($"DungGeunMo 폰트 로드 성공: {path}");
                return;
            }
        }

        dungGeunMoFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        Debug.LogWarning("DungGeunMo 폰트를 찾을 수 없어 Arial 폰트 사용");
    }

    Font GetSafeFont()
    {
        return dungGeunMoFont != null ? dungGeunMoFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    void SetupUI()
    {
        // 미니게임 버튼 이벤트 연결
        if (minigameBtn != null)
        {
            minigameBtn.onClick.AddListener(StartEyesTrackingGame);
            Debug.Log("Minigamebtn 이벤트 연결 완료");
        }

        // UI 텍스트 자동 생성 (없는 경우)
        if (timerText == null) CreateTimerText();
        if (scoreText == null) CreateScoreText();
    }

    void CreateTimerText()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        GameObject timerObj = new GameObject("TimerText");
        timerObj.transform.SetParent(canvas.transform, false);

        Text text = timerObj.AddComponent<Text>();
        text.text = "30";
        text.fontSize = 24;
        text.color = Color.yellow;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = GetSafeFont();

        RectTransform rect = timerObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0, -50);
        rect.sizeDelta = new Vector2(100, 40);

        timerText = text;
        timerObj.SetActive(false);
    }

    void CreateScoreText()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        GameObject scoreObj = new GameObject("ScoreText");
        scoreObj.transform.SetParent(canvas.transform, false);

        Text text = scoreObj.AddComponent<Text>();
        text.text = "Score: 0";
        text.fontSize = 20;
        text.color = Color.cyan;
        text.alignment = TextAnchor.MiddleLeft;
        text.font = GetSafeFont();

        RectTransform rect = scoreObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20, -20);
        rect.sizeDelta = new Vector2(200, 30);

        scoreText = text;
        scoreObj.SetActive(false);
    }

    void Update()
    {
        if (!isGameActive) return;

        UpdateGameTimer();
        UpdateEyeTracking();
        UpdateCatMovement();
        UpdateChuruSpawning();
        CheckCatChuruCollision();
    }

    void UpdateGameTimer()
    {
        gameTimer -= Time.deltaTime;

        if (timerText != null)
        {
            timerText.text = Mathf.Ceil(gameTimer).ToString();
        }

        if (gameTimer <= 0f)
        {
            EndEyesTrackingGame();
        }
    }

    void UpdateEyeTracking()
    {
        if (pointObject == null) return;

        Vector3 eyePosition = GetCurrentEyePosition();

        // point 스프라이트를 시선/마우스 위치로 이동
        pointObject.transform.position = Vector3.Lerp(
            pointObject.transform.position,
            eyePosition,
            pointFollowSpeed * Time.deltaTime
        );

        // 클릭 처리
        bool shouldClick = GetClickInput();

        if (shouldClick)
        {
            SetCatTarget(eyePosition);
        }
    }

    Vector3 GetCurrentEyePosition()
    {
        switch (activeTrackingMode)
        {
            case EyeTrackingMode.RealWebcam:
                return GetRealWebcamPosition();

            case EyeTrackingMode.SimplifiedMouse:
                return GetSimplifiedEyePosition();

            case EyeTrackingMode.Mouse:
            default:
                return GetMouseWorldPosition();
        }
    }

    Vector3 GetRealWebcamPosition()
    {
#if OPENCV_FOR_UNITY
        if (RealWebcamEyeTracker.Instance != null && RealWebcamEyeTracker.Instance.IsGazeValid)
        {
            return RealWebcamEyeTracker.Instance.GetGazeWorldPosition(mainCamera);
        }
#endif
        // 폴백
        return GetMouseWorldPosition();
    }

    Vector3 GetSimplifiedEyePosition()
    {
        if (SimplifiedEyeTracker.Instance != null && SimplifiedEyeTracker.Instance.IsGazeValid)
        {
            return SimplifiedEyeTracker.Instance.GetGazeWorldPosition(mainCamera);
        }

        // 폴백
        return GetMouseWorldPosition();
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = 10f;
        return mainCamera.ScreenToWorldPoint(mousePos);
    }

    bool GetClickInput()
    {
        switch (activeTrackingMode)
        {
            case EyeTrackingMode.RealWebcam:
                return GetRealWebcamClick();

            case EyeTrackingMode.SimplifiedMouse:
                return GetSimplifiedEyeClick();

            case EyeTrackingMode.Mouse:
            default:
                return Input.GetMouseButtonDown(0);
        }
    }

    bool GetRealWebcamClick()
    {
#if OPENCV_FOR_UNITY
        if (RealWebcamEyeTracker.Instance != null && RealWebcamEyeTracker.Instance.IsGazeValid)
        {
            return IsEyeFixated(RealWebcamEyeTracker.Instance.GazePosition);
        }
#endif
        // 폴백
        return Input.GetMouseButtonDown(0);
    }

    bool GetSimplifiedEyeClick()
    {
        if (SimplifiedEyeTracker.Instance != null && SimplifiedEyeTracker.Instance.IsGazeValid)
        {
            return IsEyeFixated(SimplifiedEyeTracker.Instance.GazePosition);
        }

        // 폴백
        return Input.GetMouseButtonDown(0);
    }

    bool IsEyeFixated(Vector2 currentGaze)
    {
        if (!fixationInitialized)
        {
            lastGazePos = currentGaze;
            fixationTimer = 0f;
            fixationInitialized = true;
            return false;
        }

        float distance = Vector2.Distance(currentGaze, lastGazePos);

        if (distance < gazeFixationRadius)
        {
            fixationTimer += Time.deltaTime;

            if (fixationTimer >= gazeFixationTime)
            {
                fixationTimer = 0f; // 리셋하여 연속 클릭 방지
                Debug.Log($"👁️ 시선 고정 클릭! 위치: {currentGaze}");
                return true;
            }
        }
        else
        {
            fixationTimer = 0f;
            lastGazePos = currentGaze;
        }

        return false;
    }

    public void StartEyesTrackingGame()
    {
        if (isGameActive)
        {
            Debug.Log("이미 게임이 진행 중입니다!");
            return;
        }

        Debug.Log($"EyesTracking 미니게임 시작! 모드: {activeTrackingMode}");
        DebugLogger.LogToFile($"EyesTracking 미니게임 시작! 모드: {activeTrackingMode}");

        isGameActive = true;
        gameTimer = gameDuration;
        currentScore = 0;
        churuSpawnTimer = 0f;

        ClearAllChuru();
        DisableCatAutoMovement();

        if (timerText != null) timerText.gameObject.SetActive(true);
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(true);
            UpdateScoreText();
        }

        CreatePointSprite();

        if (targetCat != null)
        {
            targetCat.WakeCatUp();
        }

        // 미니게임 중에는 click-through 비활성화 (중요!)
        if (CompatibilityWindowManager.Instance != null)
        {
            CompatibilityWindowManager.Instance.DisableClickThrough();
            Debug.Log("🔒 미니게임 중 click-through 비활성화");
        }

        CheckTrackingSystemStatus();
    }

    void CheckTrackingSystemStatus()
    {
        switch (activeTrackingMode)
        {
            case EyeTrackingMode.RealWebcam:
#if OPENCV_FOR_UNITY
                if (RealWebcamEyeTracker.Instance != null)
                {
                    if (RealWebcamEyeTracker.Instance.IsCalibrated)
                    {
                        Debug.Log("👁️ 실제 웹캠 눈 추적 준비 완료 - 보정됨");
                    }
                    else
                    {
                        Debug.LogWarning("⚠️ 웹캠 눈 추적이 보정되지 않았습니다.");
                        Debug.LogWarning("💡 미니게임을 시작하기 전에 C키로 보정을 완료하세요.");
                        Debug.LogWarning("💡 보정 없이는 정확한 눈 추적이 어렵습니다.");

                        // 보정되지 않은 상태에서도 게임을 진행할 수 있도록 경고만 표시
                    }

                    if (!RealWebcamEyeTracker.Instance.IsGazeValid)
                    {
                        Debug.LogWarning("⚠️ 현재 시선이 감지되지 않습니다.");
                        Debug.LogWarning("💡 얼굴이 웹캠에 잘 보이는지 확인하세요.");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠️ RealWebcamEyeTracker가 없습니다.");
                    HandleTrackingModeFallback();
                }
#else
                Debug.LogWarning("⚠️ OpenCV for Unity가 필요합니다.");
                HandleTrackingModeFallback();
#endif
                break;

            case EyeTrackingMode.SimplifiedMouse:
                if (SimplifiedEyeTracker.Instance != null)
                {
                    if (SimplifiedEyeTracker.Instance.IsCalibrated)
                    {
                        Debug.Log("👁️ 시뮬레이션 눈 추적 준비 완료 - 보정됨");
                    }
                    else
                    {
                        Debug.LogWarning("⚠️ 시뮬레이션 눈 추적이 보정되지 않았습니다.");
                        Debug.LogWarning("💡 미니게임을 시작하기 전에 C키로 보정을 완료하세요.");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠️ SimplifiedEyeTracker가 없습니다.");
                    HandleTrackingModeFallback();
                }
                break;

            case EyeTrackingMode.Mouse:
                Debug.Log("🖱️ 마우스 모드로 미니게임 진행");
                break;
        }
    }

    // 추가: 보정 상태 확인 및 자동 보정 제안 메서드
    public bool CheckCalibrationStatus()
    {
        switch (activeTrackingMode)
        {
            case EyeTrackingMode.RealWebcam:
#if OPENCV_FOR_UNITY
                return RealWebcamEyeTracker.Instance != null && RealWebcamEyeTracker.Instance.IsCalibrated;
#else
                return false;
#endif
            case EyeTrackingMode.SimplifiedMouse:
                return SimplifiedEyeTracker.Instance != null && SimplifiedEyeTracker.Instance.IsCalibrated;
            case EyeTrackingMode.Mouse:
                return true; // 마우스 모드는 항상 보정됨
            default:
                return false;
        }
    }

    public void SuggestCalibration()
    {
        if (CheckCalibrationStatus()) return;

        Debug.Log("💡 눈 추적 보정 제안:");
        Debug.Log("   1. C키를 눌러 보정을 시작하세요");
        Debug.Log("   2. 화면의 9개 점을 순서대로 바라보세요");
        Debug.Log("   3. 각 점에서 스페이스키를 누르세요");
        Debug.Log("   4. 보정 완료 후 미니게임을 시작하세요");
    }

    // 추가: 보정 품질 확인 메서드
    public string GetCalibrationQualityInfo()
    {
        switch (activeTrackingMode)
        {
            case EyeTrackingMode.RealWebcam:
#if OPENCV_FOR_UNITY
                if (RealWebcamEyeTracker.Instance != null)
                {
                    if (RealWebcamEyeTracker.Instance.IsCalibrated)
                    {
                        return "✅ 웹캠 보정 완료";
                    }
                    else if (RealWebcamEyeTracker.Instance.IsGazeValid)
                    {
                        return "⚠️ 웹캠 감지됨, 보정 필요";
                    }
                    else
                    {
                        return "❌ 웹캠 시선 감지 실패";
                    }
                }
                return "❌ 웹캠 추적기 없음";
#else
                return "❌ OpenCV 필요";
#endif

            case EyeTrackingMode.SimplifiedMouse:
                if (SimplifiedEyeTracker.Instance != null)
                {
                    return SimplifiedEyeTracker.Instance.IsCalibrated ? "✅ 시뮬레이션 보정 완료" : "⚠️ 시뮬레이션 보정 필요";
                }
                return "❌ 시뮬레이션 추적기 없음";

            case EyeTrackingMode.Mouse:
                return "✅ 마우스 모드 (보정 불필요)";

            default:
                return "❓ 알 수 없는 상태";
        }
    }
    void CreatePointSprite()
    {
        pointObject = new GameObject("PointSprite");

        SpriteRenderer renderer = pointObject.AddComponent<SpriteRenderer>();
        renderer.sprite = pointSprite;
        renderer.sortingOrder = 10;
        renderer.color = Color.yellow;

        pointObject.transform.position = Vector3.zero;

        Debug.Log("Point 스프라이트 생성 완료");
    }

    void SetCatTarget(Vector3 worldPosition)
    {
        if (targetCat == null) return;

        targetPosition = worldPosition;
        catIsMovingToTarget = true;

        Vector3 direction = (targetPosition - targetCat.transform.position).normalized;
        if (direction.x > 0)
        {
            targetCat.SetFacingDirection(CatPlayerAnimator.CatDirection.Right);
        }
        else
        {
            targetCat.SetFacingDirection(CatPlayerAnimator.CatDirection.Left);
        }

        if (targetCat.catAnimator != null && targetCat.catAnimator.animator != null)
        {
            Animator animator = targetCat.catAnimator.animator;

            animator.SetBool("IsWalking", true);
            animator.SetBool("IsSleeping", false);
            animator.SetFloat("Speed", 1.5f);

            bool facingRight = (direction.x > 0);
            animator.SetBool("IsFacingRight", facingRight);
        }

        Debug.Log($"고양이 목표 설정: {worldPosition}");
    }

    void DisableCatAutoMovement()
    {
        if (targetCat == null) return;

        if (targetCat.MovementController != null)
        {
            targetCat.MovementController.enabled = false;
        }

        if (targetCat.catAnimator != null)
        {
            targetCat.catAnimator.enabled = false;

            if (targetCat.catAnimator.animator != null)
            {
                targetCat.catAnimator.animator.SetBool("IsWalking", false);
                targetCat.catAnimator.animator.SetBool("IsSleeping", false);
                targetCat.catAnimator.animator.SetFloat("Speed", 0f);
            }
        }

        Debug.Log("고양이 자동 움직임 비활성화 완료");
    }

    void EnableCatAutoMovement()
    {
        if (targetCat == null) return;

        if (targetCat.MovementController != null)
        {
            targetCat.MovementController.enabled = true;
        }

        if (targetCat.catAnimator != null)
        {
            targetCat.catAnimator.enabled = true;
        }

        Debug.Log("고양이 자동 움직임 복원 완료");
    }

    void UpdateChuruSpawning()
    {
        churuSpawnTimer += Time.deltaTime;

        if (churuSpawnTimer >= churuSpawnInterval)
        {
            if (churuObjects.Count < maxChuruCount)
            {
                SpawnChuru();
            }
            churuSpawnTimer = 0f;
        }
    }

    void SpawnChuru()
    {
        Vector3 randomPosition = GetRandomScreenPosition();

        GameObject churuObj = new GameObject("Churu");
        churuObj.transform.position = randomPosition;

        SpriteRenderer renderer = churuObj.AddComponent<SpriteRenderer>();
        renderer.sprite = churuSprite;
        renderer.sortingOrder = 8;
        renderer.color = Color.white;

        CircleCollider2D collider = churuObj.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.3f;

        churuObjects.Add(churuObj);

        Debug.Log($"츄르 생성됨: {randomPosition}, 총 {churuObjects.Count}개");
    }

    Vector3 GetRandomScreenPosition()
    {
        float margin = 100f;
        float randomX = Random.Range(margin, Screen.width - margin);
        float randomY = Random.Range(margin, Screen.height - margin);

        Vector3 screenPos = new Vector3(randomX, randomY, 10f);
        return mainCamera.ScreenToWorldPoint(screenPos);
    }

    void CheckCatChuruCollision()
    {
        if (targetCat == null || churuObjects.Count == 0) return;

        Vector3 catPosition = targetCat.transform.position;

        for (int i = churuObjects.Count - 1; i >= 0; i--)
        {
            if (churuObjects[i] == null) continue;

            Vector3 churuPosition = churuObjects[i].transform.position;
            float distance = Vector3.Distance(catPosition, churuPosition);

            if (distance < 0.4f)
            {
                OnCatEatChuru(churuObjects[i], i);
            }
        }
    }

    void OnCatEatChuru(GameObject churuObj, int index)
    {
        if (churuObj == null) return;

        Vector3 churuPosition = churuObj.transform.position;

        Destroy(churuObj);
        churuObjects.RemoveAt(index);

        StartCoroutine(ShowCatFootEffectAtPosition(churuPosition));

        currentScore++;
        AddChur(1);

        if (targetCat != null && targetCat.catAnimator != null && targetCat.catAnimator.animator != null)
        {
            Animator animator = targetCat.catAnimator.animator;
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsSleeping", false);
            animator.SetFloat("Speed", 0f);
        }

        catIsMovingToTarget = false;

        Debug.Log($"고양이가 츄르를 먹었습니다! 점수: {currentScore}");
    }

    IEnumerator ShowCatFootEffectAtPosition(Vector3 position)
    {
        GameObject footEffect = new GameObject("CatFootEffect");
        footEffect.transform.position = position;

        SpriteRenderer renderer = footEffect.AddComponent<SpriteRenderer>();
        renderer.sprite = catFootSprite;
        renderer.sortingOrder = 9;
        renderer.color = Color.white;

        Vector3 originalScale = Vector3.one;
        Vector3 targetScale = Vector3.one * 1.5f;

        float elapsedTime = 0f;
        while (elapsedTime < footEffectDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / footEffectDuration;

            footEffect.transform.localScale = Vector3.Lerp(originalScale, targetScale,
                Mathf.Sin(t * Mathf.PI));

            Color color = renderer.color;
            color.a = 1f - t;
            renderer.color = color;

            yield return null;
        }

        Destroy(footEffect);
    }

    void UpdateCatMovement()
    {
        if (!catIsMovingToTarget || targetCat == null) return;

        Vector3 currentPos = targetCat.transform.position;
        Vector3 direction = (targetPosition - currentPos).normalized;

        float moveSpeed = 1.5f;
        Vector3 newPosition = currentPos + direction * moveSpeed * Time.deltaTime;

        targetCat.transform.position = newPosition;

        float distance = Vector3.Distance(currentPos, targetPosition);

        if (distance < 0.5f)
        {
            OnCatReachedTarget();
        }
    }

    void OnCatReachedTarget()
    {
        catIsMovingToTarget = false;

        if (targetCat != null && targetCat.catAnimator != null && targetCat.catAnimator.animator != null)
        {
            Animator animator = targetCat.catAnimator.animator;
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsSleeping", false);
            animator.SetFloat("Speed", 0f);
        }

        Debug.Log("고양이가 목표 지점에 도달했습니다.");
    }

    void ClearAllChuru()
    {
        foreach (GameObject churuObj in churuObjects)
        {
            if (churuObj != null)
            {
                Destroy(churuObj);
            }
        }
        churuObjects.Clear();
        Debug.Log("모든 츄르 정리 완료");
    }

    void AddChur(int amount)
    {
        if (CatTower.Instance != null)
        {
            CatTower.Instance.churCount += amount;
            Debug.Log($"츄르 +{amount} (총 {CatTower.Instance.churCount}개)");
        }

        UpdateScoreText();
    }

    void UpdateScoreText()
    {
        if (scoreText != null)
        {
            int totalChur = CatTower.Instance != null ? CatTower.Instance.churCount : 0;
            scoreText.text = $"Score: {currentScore} | Chur: {totalChur}";
        }
    }

    void EndEyesTrackingGame()
    {
        Debug.Log($"EyesTracking 미니게임 종료! 최종 점수: {currentScore}");
        DebugLogger.LogToFile($"EyesTracking 미니게임 종료! 최종 점수: {currentScore}");

        isGameActive = false;
        catIsMovingToTarget = false;

        EnableCatAutoMovement();
        ClearAllChuru();

        if (timerText != null) timerText.gameObject.SetActive(false);
        if (scoreText != null) scoreText.gameObject.SetActive(false);

        if (pointObject != null)
        {
            Destroy(pointObject);
            pointObject = null;
        }

        RestoreClickThroughState();
        ShowGameResult();
    }

    void RestoreClickThroughState()
    {
        if (CompatibilityWindowManager.Instance != null && mainCamera != null)
        {
            Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

            Collider2D catCollider = Physics2D.OverlapPoint(mouseWorldPos, 1 << 8);
            Collider2D towerCollider = Physics2D.OverlapPoint(mouseWorldPos, 1 << 9);

            bool isOverInteractableObject = (catCollider != null || towerCollider != null);

            if (isOverInteractableObject)
            {
                CompatibilityWindowManager.Instance.DisableClickThrough();
            }
            else
            {
                CompatibilityWindowManager.Instance.EnableClickThrough();
            }
        }
    }

    void ShowGameResult()
    {
        StartCoroutine(ShowResultCoroutine());
    }

    IEnumerator ShowResultCoroutine()
    {
        GameObject resultObj = new GameObject("GameResult");
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            resultObj.transform.SetParent(canvas.transform, false);

            Text resultText = resultObj.AddComponent<Text>();
            resultText.text = $"Game Over!\nScore: {currentScore}\nChur Earned: {currentScore}";
            resultText.fontSize = 28;
            resultText.color = Color.green;
            resultText.alignment = TextAnchor.MiddleCenter;
            resultText.font = GetSafeFont();

            RectTransform rect = resultObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(300, 100);

            yield return new WaitForSeconds(3f);
            Destroy(resultObj);
        }
    }

    void OnGUI()
    {
        if (isGameActive)
        {
            DrawEyeTrackingDebugInfo();
        }
    }

    void DrawEyeTrackingDebugInfo()
    {
        GUILayout.BeginArea(new Rect(10, 200, 500, 350));
        GUILayout.Label("=== 눈 추적 미니게임 디버그 ===");
        GUILayout.Label($"추적 모드: {GetTrackingModeString()}");

        bool isTrackerReady = IsCurrentTrackerReady();
        GUILayout.Label($"추적 시스템: {(isTrackerReady ? "✅ 준비됨" : "❌ 문제 있음")}");

        // 보정 상태 정보 추가
        string calibrationInfo = GetCalibrationQualityInfo();
        GUILayout.Label($"보정 상태: {calibrationInfo}");

        if (activeTrackingMode != EyeTrackingMode.Mouse)
        {
            Vector2 gazePos = GetCurrentGazePosition();
            GUILayout.Label($"시선 위치: ({gazePos.x:F0}, {gazePos.y:F0})");

            // CompatibilityWindowManager 상태 표시
            if (CompatibilityWindowManager.Instance != null)
            {
                bool isClickThrough = CompatibilityWindowManager.Instance.IsClickThrough;
                GUILayout.Label($"Click-through: {(isClickThrough ? "활성화" : "비활성화")}");
            }
        }

        if (pointObject != null)
        {
            Vector3 pointPos = pointObject.transform.position;
            GUILayout.Label($"Point 위치: ({pointPos.x:F2}, {pointPos.y:F2})");
        }

        GUILayout.Space(5);
        string inputMethod = GetCurrentInputMethodString();
        GUILayout.Label($"조작 방법: {inputMethod}");

        if (activeTrackingMode != EyeTrackingMode.Mouse)
        {
            GUILayout.Label($"고정 진행: {fixationTimer:F1}s / {gazeFixationTime:F1}s");
        }

        GUILayout.Space(5);
        GUILayout.Label("⌨️ 단축키:");
        GUILayout.Label("ESC: 미니게임 종료");
        GUILayout.Label("E: 추적 모드 전환");

        if (!CheckCalibrationStatus())
        {
            GUILayout.Label("C: 보정 시작 (권장!)");
        }

        if (!isTrackerReady)
        {
            GUILayout.Space(5);
            GUILayout.Label("⚠️ 추적 시스템 문제:");

            switch (activeTrackingMode)
            {
                case EyeTrackingMode.RealWebcam:
                    GUILayout.Label("- 웹캠이 연결되어 있는지 확인");
                    GUILayout.Label("- 얼굴이 웹캠에 잘 보이는지 확인");
                    GUILayout.Label("- 조명이 충분한지 확인");
                    break;
                case EyeTrackingMode.SimplifiedMouse:
                    GUILayout.Label("- SimplifiedEyeTracker 활성화 확인");
                    break;
            }
        }

        if (!CheckCalibrationStatus() && activeTrackingMode != EyeTrackingMode.Mouse)
        {
            GUILayout.Space(5);
            GUILayout.Label("💡 보정이 필요합니다!");
            GUILayout.Label("C키를 눌러 보정을 시작하세요.");
        }

        GUILayout.EndArea();
    }

    string GetTrackingModeString()
    {
        switch (activeTrackingMode)
        {
            case EyeTrackingMode.RealWebcam:
                return "📹 실제 웹캠 눈 추적";
            case EyeTrackingMode.SimplifiedMouse:
                return "🖱️ 마우스 시뮬레이션";
            case EyeTrackingMode.Mouse:
                return "🖱️ 순수 마우스";
            default:
                return "❓ 알 수 없음";
        }
    }

    bool IsCurrentTrackerReady()
    {
        switch (activeTrackingMode)
        {
            case EyeTrackingMode.RealWebcam:
#if OPENCV_FOR_UNITY
                return RealWebcamEyeTracker.Instance != null && RealWebcamEyeTracker.Instance.IsGazeValid;
#else
                return false;
#endif
            case EyeTrackingMode.SimplifiedMouse:
                return SimplifiedEyeTracker.Instance != null && SimplifiedEyeTracker.Instance.IsGazeValid;
            case EyeTrackingMode.Mouse:
                return true;
            default:
                return false;
        }
    }

    Vector2 GetCurrentGazePosition()
    {
        switch (activeTrackingMode)
        {
            case EyeTrackingMode.RealWebcam:
#if OPENCV_FOR_UNITY
                if (RealWebcamEyeTracker.Instance != null)
                    return RealWebcamEyeTracker.Instance.GazePosition;
#endif
                break;
            case EyeTrackingMode.SimplifiedMouse:
                if (SimplifiedEyeTracker.Instance != null)
                    return SimplifiedEyeTracker.Instance.GazePosition;
                break;
        }
        return Input.mousePosition;
    }

    bool IsCurrentTrackerCalibrated()
    {
        switch (activeTrackingMode)
        {
            case EyeTrackingMode.RealWebcam:
#if OPENCV_FOR_UNITY
                return RealWebcamEyeTracker.Instance != null && RealWebcamEyeTracker.Instance.IsCalibrated;
#else
                return false;
#endif
            case EyeTrackingMode.SimplifiedMouse:
                return SimplifiedEyeTracker.Instance != null && SimplifiedEyeTracker.Instance.IsCalibrated;
            case EyeTrackingMode.Mouse:
                return true;
            default:
                return false;
        }
    }

    string GetCurrentInputMethodString()
    {
        switch (activeTrackingMode)
        {
            case EyeTrackingMode.RealWebcam:
                return $"👁️ 실제 시선 고정 ({gazeFixationTime:F1}초)";
            case EyeTrackingMode.SimplifiedMouse:
                return $"👁️ 시뮬레이션 시선 고정 ({gazeFixationTime:F1}초)";
            case EyeTrackingMode.Mouse:
                return "🖱️ 마우스 클릭";
            default:
                return "❓ 알 수 없음";
        }
    }

    void LateUpdate()
    {
        // ESC 키로 미니게임 강제 종료
        if (Input.GetKeyDown(KeyCode.Escape) && isGameActive)
        {
            Debug.Log("ESC 키로 미니게임 강제 종료");
            EndEyesTrackingGame();
        }

        // E 키로 추적 모드 전환
        if (Input.GetKeyDown(KeyCode.E) && !isGameActive)
        {
            CycleTrackingMode();
        }
    }

    void CycleTrackingMode()
    {
        switch (trackingMode)
        {
            case EyeTrackingMode.RealWebcam:
                trackingMode = EyeTrackingMode.SimplifiedMouse;
                break;
            case EyeTrackingMode.SimplifiedMouse:
                trackingMode = EyeTrackingMode.Mouse;
                break;
            case EyeTrackingMode.Mouse:
                trackingMode = EyeTrackingMode.RealWebcam;
                break;
        }

        DetermineTrackingMode();
        Debug.Log($"추적 모드 변경: {activeTrackingMode}");
    }

    // 외부에서 접근할 수 있는 프로퍼티들
    public bool IsGameActive => isGameActive;
    public float GameTimer => gameTimer;
    public int CurrentScore => currentScore;
    public int ChuruCount => churuObjects.Count;
    public EyeTrackingMode ActiveTrackingMode => activeTrackingMode;

    public bool IsEyeTrackingWorking
    {
        get
        {
            switch (activeTrackingMode)
            {
                case EyeTrackingMode.RealWebcam:
#if OPENCV_FOR_UNITY
                    return RealWebcamEyeTracker.Instance != null && RealWebcamEyeTracker.Instance.IsGazeValid;
#else
                    return false;
#endif
                case EyeTrackingMode.SimplifiedMouse:
                    return SimplifiedEyeTracker.Instance != null && SimplifiedEyeTracker.Instance.IsGazeValid;
                case EyeTrackingMode.Mouse:
                    return true;
                default:
                    return false;
            }
        }
    }

    // 디버그용 메서드들
    [ContextMenu("Force Real Webcam Mode")]
    public void ForceRealWebcamMode()
    {
        trackingMode = EyeTrackingMode.RealWebcam;
        DetermineTrackingMode();
        Debug.Log("강제로 실제 웹캠 모드로 전환");
    }

    [ContextMenu("Force Simplified Mode")]
    public void ForceSimplifiedMode()
    {
        trackingMode = EyeTrackingMode.SimplifiedMouse;
        DetermineTrackingMode();
        Debug.Log("강제로 시뮬레이션 모드로 전환");
    }

    [ContextMenu("Force Mouse Mode")]
    public void ForceMouseMode()
    {
        trackingMode = EyeTrackingMode.Mouse;
        DetermineTrackingMode();
        Debug.Log("강제로 마우스 모드로 전환");
    }
    // 디버그용 보정 테스트 메서드들
    [ContextMenu("Test All Tracking Systems")]
    public void TestAllTrackingSystems()
    {
        Debug.Log("=== 모든 추적 시스템 테스트 ===");

        // 실제 웹캠 테스트
#if OPENCV_FOR_UNITY
        bool realWebcamAvailable = RealWebcamEyeTracker.Instance != null;
        Debug.Log($"실제 웹캠 추적: {(realWebcamAvailable ? "✅ 사용 가능" : "❌ 불가능")}");

        if (realWebcamAvailable)
        {
            var realTracker = RealWebcamEyeTracker.Instance;
            Debug.Log($"  - 얼굴 감지: {realTracker.IsFaceDetected}");
            Debug.Log($"  - 눈 감지: {realTracker.AreEyesDetected}");
            Debug.Log($"  - 시선 유효: {realTracker.IsGazeValid}");
            Debug.Log($"  - 보정 상태: {realTracker.IsCalibrated}");

            if (realTracker.IsGazeValid)
            {
                Debug.Log($"  - 현재 시선: {realTracker.GazePosition}");
            }
        }
#else
        Debug.Log("실제 웹캠 추적: ❌ OpenCV for Unity 필요");
#endif

        // 시뮬레이션 추적 테스트
        bool simplifiedAvailable = SimplifiedEyeTracker.Instance != null;
        Debug.Log($"시뮬레이션 추적: {(simplifiedAvailable ? "✅ 사용 가능" : "❌ 불가능")}");

        if (simplifiedAvailable)
        {
            var simTracker = SimplifiedEyeTracker.Instance;
            Debug.Log($"  - 시선 유효: {simTracker.IsGazeValid}");
            Debug.Log($"  - 보정 상태: {simTracker.IsCalibrated}");

            if (simTracker.IsGazeValid)
            {
                Debug.Log($"  - 현재 시선: {simTracker.GazePosition}");
            }
        }

        // CompatibilityWindowManager 테스트
        bool compatibilityAvailable = CompatibilityWindowManager.Instance != null;
        Debug.Log($"CompatibilityWindowManager: {(compatibilityAvailable ? "✅ 사용 가능" : "❌ 불가능")}");

        if (compatibilityAvailable)
        {
            var compat = CompatibilityWindowManager.Instance;
            Debug.Log($"  - Click-through 상태: {compat.IsClickThrough}");

            Vector2 unityMouse = Input.mousePosition;
            Vector2 compatMouse = compat.GetMousePositionInWindow();
            float mouseDiff = Vector2.Distance(unityMouse, compatMouse);

            Debug.Log($"  - Unity 마우스: {unityMouse}");
            Debug.Log($"  - Compat 마우스: {compatMouse}");
            Debug.Log($"  - 마우스 좌표 차이: {mouseDiff:F1}px");

            if (mouseDiff > 10f)
            {
                Debug.LogWarning("⚠️ 마우스 좌표 차이가 큽니다. 보정에 영향을 줄 수 있습니다.");
            }
        }

        Debug.Log("마우스 추적: ✅ 항상 사용 가능");

        Debug.Log("\n=== 권장 사항 ===");
        if (!CheckCalibrationStatus())
        {
            Debug.Log("💡 보정을 완료하면 더 정확한 눈 추적이 가능합니다.");
        }

        Debug.Log("💡 미니게임 중에는 ESC키로 언제든지 종료할 수 있습니다.");
    }

    [ContextMenu("Quick Calibration All")]
    public void QuickCalibrationAll()
    {
        Debug.Log("⚡ 모든 추적 시스템 빠른 보정 시도...");

        // SimplifiedEyeTracker 빠른 보정
        if (SimplifiedEyeTracker.Instance != null)
        {
            SimplifiedEyeTracker.Instance.QuickCalibration();
            Debug.Log("✅ SimplifiedEyeTracker 빠른 보정 완료");
        }

#if OPENCV_FOR_UNITY
        // RealWebcamEyeTracker 빠른 보정
        if (RealWebcamEyeTracker.Instance != null)
        {
            RealWebcamEyeTracker.Instance.QuickPerfectCalibration();
            Debug.Log("✅ RealWebcamEyeTracker 빠른 보정 완료");
        }
#endif

        Debug.Log("⚡ 모든 가능한 추적 시스템의 빠른 보정이 완료되었습니다!");
    }

    // 시선 추적 상태 정보
    public string GetEyeTrackingStatus()
    {
        switch (activeTrackingMode)
        {
            case EyeTrackingMode.RealWebcam:
#if OPENCV_FOR_UNITY
                if (RealWebcamEyeTracker.Instance == null)
                    return "❌ RealWebcamEyeTracker 없음";
                if (!RealWebcamEyeTracker.Instance.IsGazeValid)
                    return "❌ 실제 시선 추적 실패";
                if (!RealWebcamEyeTracker.Instance.IsCalibrated)
                    return "⚠️ 보정 필요";
                return "👁️ 실제 웹캠 시선 추적 활성";
#else
                return "❌ OpenCV for Unity 필요";
#endif

            case EyeTrackingMode.SimplifiedMouse:
                if (SimplifiedEyeTracker.Instance == null)
                    return "❌ SimplifiedEyeTracker 없음";
                if (!SimplifiedEyeTracker.Instance.IsGazeValid)
                    return "❌ 시뮬레이션 시선 추적 실패";
                if (!SimplifiedEyeTracker.Instance.IsCalibrated)
                    return "⚠️ 보정 필요";
                return "👁️ 시뮬레이션 시선 추적 활성";

            case EyeTrackingMode.Mouse:
                return "🖱️ 마우스 모드";

            default:
                return "❓ 알 수 없는 모드";
        }
    }

}