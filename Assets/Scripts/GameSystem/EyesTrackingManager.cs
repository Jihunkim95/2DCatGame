using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 시선 추적 미니게임 관리자
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

    [Header("웹캠 설정")]
    public bool useWebcam = true;               // 웹캠 사용 여부
    public int webcamIndex = 0;                 // 웹캠 인덱스
    public RawImage webcamDisplay;              // 웹캠 화면 표시용 (선택사항)

    // 웹캠 관련
    private WebCamTexture webCamTexture;
    private bool isWebcamInitialized = false;

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

        if (useWebcam)
        {
            InitializeWebcam();
        }

        Debug.Log("EyesTracking 미니게임 시스템 초기화 완료");
        DebugLogger.LogToFile("EyesTracking 미니게임 시스템 초기화 완료");
    }

    void InitializeComponents()
    {
        // 컴포넌트 자동 할당
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (targetCat == null)
            targetCat = FindObjectOfType<TestCat>();

        // 스프라이트 로드 (Resources에서)
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

    void LoadDungGeunMoFont()
    {
        if (dungGeunMoFont != null) return;

        // UIPrefabFactory와 같은 방식으로 폰트 로드
        string[] resourcePaths = {
            "Font/DungGeunMo SDF",
            "Font/DungGeunMo",
            "DungGeunMo SDF",
            "DungGeunMo",
            "Fonts/DungGeunMo",
            "UI/DungGeunMo"
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

        // 폰트를 찾지 못한 경우 기본 폰트 사용
        dungGeunMoFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        Debug.LogWarning("DungGeunMo 폰트를 찾을 수 없어 Arial 폰트 사용");
    }

    Font GetSafeFont()
    {
        if (dungGeunMoFont != null)
        {
            return dungGeunMoFont;
        }
        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    void SetupUI()
    {
        // 미니게임 버튼 이벤트 연결
        if (minigameBtn != null)
        {
            minigameBtn.onClick.AddListener(StartEyesTrackingGame);
            Debug.Log("Minigamebtn 이벤트 연결 완료");
        }
        else
        {
            Debug.LogError("Minigamebtn을 찾을 수 없습니다!");
        }

        // UI 텍스트 자동 생성 (없는 경우)
        if (timerText == null)
        {
            CreateTimerText();
        }

        if (scoreText == null)
        {
            CreateScoreText();
        }
    }

    void CreateTimerText()
    {
        // Canvas 찾기
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 타이머 텍스트 생성
        GameObject timerObj = new GameObject("TimerText");
        timerObj.transform.SetParent(canvas.transform, false);

        Text text = timerObj.AddComponent<Text>();
        text.text = "30";
        text.fontSize = 24;
        text.color = Color.yellow;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = GetSafeFont(); // 안전한 폰트 사용

        RectTransform rect = timerObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0, -50);
        rect.sizeDelta = new Vector2(100, 40);

        timerText = text;
        timerObj.SetActive(false); // 초기에는 숨김
    }

    void CreateScoreText()
    {
        // Canvas 찾기
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 점수 텍스트 생성
        GameObject scoreObj = new GameObject("ScoreText");
        scoreObj.transform.SetParent(canvas.transform, false);

        Text text = scoreObj.AddComponent<Text>();
        text.text = "Score: 0";
        text.fontSize = 20;
        text.color = Color.cyan;
        text.alignment = TextAnchor.MiddleLeft;
        text.font = GetSafeFont(); // 안전한 폰트 사용

        RectTransform rect = scoreObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20, -20);
        rect.sizeDelta = new Vector2(150, 30);

        scoreText = text;
        scoreObj.SetActive(false); // 초기에는 숨김
    }

    void InitializeWebcam()
    {
        // 사용 가능한 웹캠 확인
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            Debug.LogWarning("웹캠을 찾을 수 없습니다! 마우스 모드로 전환합니다.");
            useWebcam = false;
            return;
        }

        // 웹캠 목록 출력
        Debug.Log($"발견된 웹캠 수: {devices.Length}");
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"웹캠 {i}: {devices[i].name}");
        }

        // 웹캠 초기화
        if (webcamIndex < devices.Length)
        {
            webCamTexture = new WebCamTexture(devices[webcamIndex].name, 640, 480, 30);

            // 웹캠 디스플레이 설정 (선택사항)
            if (webcamDisplay != null)
            {
                webcamDisplay.texture = webCamTexture;
                webcamDisplay.gameObject.SetActive(false); // 초기에는 숨김
            }

            isWebcamInitialized = true;
            Debug.Log($"웹캠 초기화 완료: {devices[webcamIndex].name}");
        }
        else
        {
            Debug.LogError($"잘못된 웹캠 인덱스: {webcamIndex}");
            useWebcam = false;
        }
    }

    void StartWebcam()
    {
        if (isWebcamInitialized && webCamTexture != null)
        {
            webCamTexture.Play();

            if (webcamDisplay != null)
            {
                webcamDisplay.gameObject.SetActive(true);
            }

            Debug.Log("웹캠 시작됨");
        }
    }

    void StopWebcam()
    {
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Stop();

            if (webcamDisplay != null)
            {
                webcamDisplay.gameObject.SetActive(false);
            }

            Debug.Log("웹캠 중지됨");
        }
    }

    void Update()
    {
        if (!isGameActive) return;

        UpdateGameTimer();
        UpdateEyeTracking();
        UpdateCatMovement();
        UpdateChuruSpawning();      // 츄르 생성 업데이트
        CheckCatChuruCollision();   // 고양이와 츄르 충돌 체크
    }

    void UpdateGameTimer()
    {
        gameTimer -= Time.deltaTime;

        // 타이머 UI 업데이트
        if (timerText != null)
        {
            timerText.text = Mathf.Ceil(gameTimer).ToString();
        }

        // 게임 종료 체크
        if (gameTimer <= 0f)
        {
            EndEyesTrackingGame();
        }
    }

    void UpdateEyeTracking()
    {
        if (pointObject == null) return;

        Vector3 eyePosition;

        if (useWebcam && isWebcamInitialized)
        {
            // 실제 Eye Tracking (웹캠 사용)
            eyePosition = GetEyeTrackingPosition();
        }
        else
        {
            // 마우스로 시뮬레이션
            eyePosition = GetMouseWorldPosition();
        }

        // point 스프라이트를 시선 위치로 이동
        pointObject.transform.position = Vector3.Lerp(
            pointObject.transform.position,
            eyePosition,
            pointFollowSpeed * Time.deltaTime
        );

        // 클릭 시 고양이 목표 설정 (츄르와 상관없이 point 위치로)
        if (Input.GetMouseButtonDown(0))
        {
            SetCatTarget(eyePosition);
        }
    }

    Vector3 GetEyeTrackingPosition()
    {
        // 간단한 Eye Tracking 구현
        // 실제로는 OpenCV나 전용 라이브러리가 필요합니다

        if (webCamTexture == null || !webCamTexture.isPlaying)
        {
            return GetMouseWorldPosition(); // 폴백
        }

        // 화면 중앙을 기본값으로 (실제 구현에서는 얼굴/눈 인식 필요)
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 10f);

        // 웹캠에서 감지된 시선 방향에 따라 조정
        // 이 부분은 실제 Eye Tracking 라이브러리로 대체해야 합니다
        Vector3 eyeOffset = GetSimulatedEyeMovement();

        Vector3 eyeScreenPos = screenCenter + eyeOffset;
        return mainCamera.ScreenToWorldPoint(eyeScreenPos);
    }

    Vector3 GetSimulatedEyeMovement()
    {
        // 임시로 마우스 위치를 사용 (실제로는 Eye Tracking 데이터)
        Vector3 mousePos = Input.mousePosition;
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0);
        return (mousePos - screenCenter) * 0.5f; // 50% 감도
    }

    bool IsEyeFixated(Vector3 position)
    {
        // 시선이 일정 시간 한 곳에 고정되었는지 확인
        // 실제 구현에서는 시선 고정 시간을 측정해야 합니다
        return false; // 임시로 false 반환
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = 10f; // 카메라로부터의 거리
        return mainCamera.ScreenToWorldPoint(mousePos);
    }

    void DisableCatAutoMovement()
    {
        if (targetCat == null) return;

        // CatMovementController 비활성화
        if (targetCat.MovementController != null)
        {
            targetCat.MovementController.enabled = false;
            Debug.Log("CatMovementController 비활성화 - 미니게임 모드");
        }

        // CatPlayerAnimator도 비활성화하여 자동 업데이트 방지 (중요!)
        if (targetCat.catAnimator != null)
        {
            targetCat.catAnimator.enabled = false;
            Debug.Log("CatPlayerAnimator 비활성화 - 미니게임 모드");

            // 비활성화 후 수동으로 초기 상태 설정
            if (targetCat.catAnimator.animator != null)
            {
                targetCat.catAnimator.animator.SetBool("IsWalking", false);
                targetCat.catAnimator.animator.SetBool("IsSleeping", false);
                targetCat.catAnimator.animator.SetFloat("Speed", 0f);
            }
        }

        Debug.Log("고양이 자동 움직임 및 애니메이터 비활성화 완료");
    }

    void EnableCatAutoMovement()
    {
        if (targetCat == null) return;

        // CatMovementController 다시 활성화
        if (targetCat.MovementController != null)
        {
            targetCat.MovementController.enabled = true;
            Debug.Log("CatMovementController 다시 활성화");
        }

        // CatPlayerAnimator 다시 활성화
        if (targetCat.catAnimator != null)
        {
            targetCat.catAnimator.enabled = true;
            Debug.Log("CatPlayerAnimator 다시 활성화");
        }

        Debug.Log("고양이 자동 움직임 복원 완료");
    }

    public void StartEyesTrackingGame()
    {
        if (isGameActive)
        {
            Debug.Log("이미 게임이 진행 중입니다!");
            return;
        }

        Debug.Log("EyesTracking 미니게임 시작!");
        DebugLogger.LogToFile("EyesTracking 미니게임 시작!");

        // 웹캠 시작
        if (useWebcam)
        {
            StartWebcam();
        }

        // 게임 상태 초기화
        isGameActive = true;
        gameTimer = gameDuration;
        currentScore = 0;
        churuSpawnTimer = 0f;

        // 기존 츄르들 정리
        ClearAllChuru();

        // 고양이 자동 움직임 비활성화 (중요!)
        DisableCatAutoMovement();

        // UI 활성화
        if (timerText != null) timerText.gameObject.SetActive(true);
        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(true);
            UpdateScoreText();
        }

        // point 스프라이트 생성
        CreatePointSprite();

        // 고양이 상태 설정
        if (targetCat != null)
        {
            // 고양이를 깨운 상태로 만들기
            targetCat.WakeCatUp();
        }

        // click-through 비활성화 (게임 중에는 마우스 입력 필요)
        if (CompatibilityWindowManager.Instance != null)
        {
            CompatibilityWindowManager.Instance.DisableClickThrough();
        }
    }

    void CreatePointSprite()
    {
        // point 스프라이트 오브젝트 생성
        pointObject = new GameObject("PointSprite");

        SpriteRenderer renderer = pointObject.AddComponent<SpriteRenderer>();
        renderer.sprite = pointSprite;
        renderer.sortingOrder = 10; // 다른 오브젝트들보다 위에 표시
        renderer.color = Color.yellow;

        // 초기 위치 설정 (화면 중앙)
        pointObject.transform.position = Vector3.zero;

        Debug.Log("Point 스프라이트 생성 완료");
    }

    void SetCatTarget(Vector3 worldPosition)
    {
        if (targetCat == null) return;

        targetPosition = worldPosition;
        catIsMovingToTarget = true;

        // 고양이 방향 설정
        Vector3 direction = (targetPosition - targetCat.transform.position).normalized;
        if (direction.x > 0)
        {
            targetCat.SetFacingDirection(CatPlayerAnimator.CatDirection.Right);
        }
        else
        {
            targetCat.SetFacingDirection(CatPlayerAnimator.CatDirection.Left);
        }

        // CatPlayerAnimator가 비활성화된 상태에서 직접 애니메이터 제어
        if (targetCat.catAnimator != null && targetCat.catAnimator.animator != null)
        {
            Animator animator = targetCat.catAnimator.animator;

            // 걷기 애니메이션 설정 (CatPlayerAnimator 없이 직접 제어)
            animator.SetBool("IsWalking", true);
            animator.SetBool("IsSleeping", false);
            animator.SetFloat("Speed", 1.5f);

            // 방향 설정
            bool facingRight = (direction.x > 0);
            animator.SetBool("IsFacingRight", facingRight);

            Debug.Log($"애니메이터 직접 제어 - IsWalking: {animator.GetBool("IsWalking")}, Speed: {animator.GetFloat("Speed")}, FacingRight: {facingRight}");
        }

        Debug.Log($"고양이 목표 설정: {worldPosition} - 걷기 애니메이션 활성화");
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
        // 화면 경계 내에서 랜덤 위치 생성
        Vector3 randomPosition = GetRandomScreenPosition();

        // 츄르 오브젝트 생성
        GameObject churuObj = new GameObject("Churu");
        churuObj.transform.position = randomPosition;

        // 스프라이트 렌더러 추가
        SpriteRenderer renderer = churuObj.AddComponent<SpriteRenderer>();
        renderer.sprite = churuSprite;
        renderer.sortingOrder = 8; // point보다 아래, 고양이보다 위
        renderer.color = Color.white;

        // 충돌 감지용 콜라이더 추가
        CircleCollider2D collider = churuObj.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.3f;

        // 리스트에 추가
        churuObjects.Add(churuObj);

        Debug.Log($"츄르 생성됨: {randomPosition}, 총 {churuObjects.Count}개");
    }

    Vector3 GetRandomScreenPosition()
    {
        // 화면 경계 내에서 랜덤 위치 계산
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        // 가장자리에서 여백 두기
        float margin = 100f;
        float randomX = Random.Range(margin, screenWidth - margin);
        float randomY = Random.Range(margin, screenHeight - margin);

        // 스크린 좌표를 월드 좌표로 변환
        Vector3 screenPos = new Vector3(randomX, randomY, 10f);
        return mainCamera.ScreenToWorldPoint(screenPos);
    }

    void CheckCatChuruCollision()
    {
        if (targetCat == null || churuObjects.Count == 0) return;

        Vector3 catPosition = targetCat.transform.position;

        // 고양이와 모든 츄르 간의 거리 체크
        for (int i = churuObjects.Count - 1; i >= 0; i--)
        {
            if (churuObjects[i] == null) continue;

            Vector3 churuPosition = churuObjects[i].transform.position;
            float distance = Vector3.Distance(catPosition, churuPosition);

            // 충돌 감지 (거리 기반)
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

        // 츄르 제거
        Destroy(churuObj);
        churuObjects.RemoveAt(index);

        // cat_foot 효과 생성
        StartCoroutine(ShowCatFootEffectAtPosition(churuPosition));

        // 점수 증가
        currentScore++;
        AddChur(1);

        // 고양이를 잠시 아이들 상태로 (먹는 모션)
        if (targetCat != null && targetCat.catAnimator != null && targetCat.catAnimator.animator != null)
        {
            Animator animator = targetCat.catAnimator.animator;

            // 애니메이터 직접 제어로 아이들 상태 설정
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsSleeping", false);
            animator.SetFloat("Speed", 0f);
        }

        // 이동 중이었다면 중지
        catIsMovingToTarget = false;

        Debug.Log($"고양이가 츄르를 먹었습니다! 점수: {currentScore}, 츄르 +1");
        DebugLogger.LogToFile($"EyesTracking: 츄르 섭취, 점수: {currentScore}");
    }

    IEnumerator ShowCatFootEffectAtPosition(Vector3 position)
    {
        // cat_foot 스프라이트 오브젝트 생성
        GameObject footEffect = new GameObject("CatFootEffect");
        footEffect.transform.position = position;

        SpriteRenderer renderer = footEffect.AddComponent<SpriteRenderer>();
        renderer.sprite = catFootSprite;
        renderer.sortingOrder = 9; // 가장 위에 표시
        renderer.color = Color.white;

        // 효과 애니메이션 (크기 변화)
        Vector3 originalScale = Vector3.one;
        Vector3 targetScale = Vector3.one * 1.5f;

        float elapsedTime = 0f;
        while (elapsedTime < footEffectDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / footEffectDuration;

            // 크기 애니메이션
            footEffect.transform.localScale = Vector3.Lerp(originalScale, targetScale,
                Mathf.Sin(t * Mathf.PI));

            // 투명도 애니메이션
            Color color = renderer.color;
            color.a = 1f - t;
            renderer.color = color;

            yield return null;
        }

        // 효과 오브젝트 제거
        Destroy(footEffect);
        Debug.Log("cat_foot 효과 완료");
    }

    void UpdateCatMovement()
    {
        if (!catIsMovingToTarget || targetCat == null) return;

        // 고양이를 목표 지점으로 직접 이동
        Vector3 currentPos = targetCat.transform.position;
        Vector3 direction = (targetPosition - currentPos).normalized;

        // 이동 속도 (CatMovementController의 moveSpeed와 비슷하게)
        float moveSpeed = 1.5f;
        Vector3 newPosition = currentPos + direction * moveSpeed * Time.deltaTime;

        targetCat.transform.position = newPosition;

        // 고양이와 목표 지점 사이의 거리 확인
        float distance = Vector3.Distance(currentPos, targetPosition);

        if (distance < 0.5f) // 목표에 도달
        {
            OnCatReachedTarget();
        }
    }

    void OnCatReachedTarget()
    {
        catIsMovingToTarget = false;

        // 걷기 애니메이션 중지 (목표 지점에 도달했을 때)
        if (targetCat != null && targetCat.catAnimator != null && targetCat.catAnimator.animator != null)
        {
            Animator animator = targetCat.catAnimator.animator;

            // 애니메이터 직접 제어로 아이들 상태 설정
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsSleeping", false);
            animator.SetFloat("Speed", 0f);

            Debug.Log($"걷기 애니메이션 중지 - IsWalking: {animator.GetBool("IsWalking")}");
        }

        Debug.Log($"고양이가 목표 지점에 도달했습니다.");
    }

    void ClearAllChuru()
    {
        // 기존에 생성된 모든 츄르 제거
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

        // 고양이 자동 움직임 복원
        EnableCatAutoMovement();

        // 모든 츄르 제거
        ClearAllChuru();

        // 웹캠 중지
        if (useWebcam)
        {
            StopWebcam();
        }

        // UI 숨기기
        if (timerText != null) timerText.gameObject.SetActive(false);
        if (scoreText != null) scoreText.gameObject.SetActive(false);

        // point 스프라이트 제거
        if (pointObject != null)
        {
            Destroy(pointObject);
            pointObject = null;
        }

        // 고양이를 자연스러운 상태로 복원 (이제 MovementController가 다시 제어함)
        // MovementController가 활성화되면 자동으로 자연스러운 상태로 돌아감

        // click-through 상태 복원
        RestoreClickThroughState();

        // 결과 표시 (선택사항)
        ShowGameResult();
    }

    void RestoreClickThroughState()
    {
        // 원래 click-through 로직으로 복원
        if (CompatibilityWindowManager.Instance != null && mainCamera != null)
        {
            Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

            // 상호작용 가능한 오브젝트 확인
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
        // 간단한 결과 표시 (3초 후 사라짐)
        StartCoroutine(ShowResultCoroutine());
    }

    IEnumerator ShowResultCoroutine()
    {
        // 결과 텍스트 생성
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
            resultText.font = GetSafeFont(); // 안전한 폰트 사용

            RectTransform rect = resultObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(300, 100);

            // 3초 후 제거
            yield return new WaitForSeconds(3f);
            Destroy(resultObj);
        }
    }

    void OnDestroy()
    {
        // 웹캠 정리
        if (webCamTexture != null)
        {
            if (webCamTexture.isPlaying)
            {
                webCamTexture.Stop();
            }
            Destroy(webCamTexture);
        }
    }

    // 외부에서 접근할 수 있는 프로퍼티들
    public bool IsGameActive => isGameActive;
    public float GameTimer => gameTimer;
    public int CurrentScore => currentScore;
    public bool IsWebcamActive => webCamTexture != null && webCamTexture.isPlaying;
    public int ChuruCount => churuObjects.Count;

    // 웹캠 토글 (디버그용)
    [ContextMenu("Toggle Webcam")]
    public void ToggleWebcam()
    {
        useWebcam = !useWebcam;

        if (useWebcam && !isWebcamInitialized)
        {
            InitializeWebcam();
        }

        Debug.Log($"웹캠 사용: {(useWebcam ? "ON" : "OFF (마우스 모드)")}");
    }

    // 게임 강제 종료 (디버그용)
    [ContextMenu("Force End Game")]
    public void ForceEndGame()
    {
        if (isGameActive)
        {
            EndEyesTrackingGame();
        }
    }

    // 츄르 강제 생성 (디버그용)
    [ContextMenu("Spawn Churu")]
    public void ForceSpawnChuru()
    {
        if (isGameActive && churuObjects.Count < maxChuruCount)
        {
            SpawnChuru();
        }
    }

    // 모든 츄르 제거 (디버그용)
    [ContextMenu("Clear All Churu")]
    public void ForceClearChuru()
    {
        ClearAllChuru();
    }
}