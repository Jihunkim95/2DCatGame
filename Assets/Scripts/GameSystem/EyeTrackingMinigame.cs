using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

#if OPENCV_FOR_UNITY
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UtilsModule;
#endif

/// <summary>
/// 순수 눈 추적 미니게임 - Minigamebtn 클릭 시에만 활성화
/// </summary>
public class EyeTrackingMinigame : MonoBehaviour
{
    [Header("미니게임 설정")]
    public float gameDuration = 30f;
    public float pointFollowSpeed = 5f;
    public float footEffectDuration = 0.5f;
    public float churuSpawnInterval = 5f;
    public int maxChuruCount = 5;

    [Header("웹캠 설정")]
    public int webcamWidth = 640;
    public int webcamHeight = 480;
    public int webcamFPS = 30;

    [Header("시선 추적 설정")]
    public float gazeFixationTime = 1.5f;      // 시선 고정 시간 (클릭)
    public float gazeFixationRadius = 30f;     // 시선 고정 반경
    public float gazeSmoothing = 8f;

    [Header("UI 요소")]
    public Button minigameBtn;
    public Text timerText;
    public Text scoreText;
    public RawImage webcamDisplay;

    [Header("스프라이트")]
    public Sprite catFootSprite;
    public Sprite pointSprite;
    public Sprite churuSprite;

#if OPENCV_FOR_UNITY
    // 웹캠 및 OpenCV
    private WebCamTexture webCamTexture;
    private Mat rgbaMat;
    private Mat grayMat;
    private CascadeClassifier faceCascade;
    private CascadeClassifier eyeCascade;

    // 시선 데이터
    private Vector2 currentGazePoint;
    private Vector2 smoothedGazePoint;
    private bool isGazeValid = false;

    // 보정
    private List<Vector2> calibrationTargets = new List<Vector2>();
    private List<Vector2> calibrationGazes = new List<Vector2>();
    private bool isCalibrating = false;
    private int calibrationIndex = 0;
    private bool isCalibrated = false;

    // 시선 고정 감지
    private Vector2 lastGazePos;
    private float fixationTimer = 0f;
#endif

    // 미니게임 상태
    private bool isGameActive = false;
    private float gameTimer = 0f;
    private int currentScore = 0;

    // 게임 오브젝트들
    private GameObject pointObject;
    private Vector3 targetPosition;
    private bool catIsMovingToTarget = false;
    private List<GameObject> churuObjects = new List<GameObject>();
    private float churuSpawnTimer = 0f;

    private Camera mainCamera;
    private TestCat targetCat;

    public static EyeTrackingMinigame Instance { get; private set; }

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
        InitializeComponents();
        SetupUI();

#if OPENCV_FOR_UNITY
        StartCoroutine(InitializeEyeTracking());
#else
        Debug.LogError("OpenCV for Unity 필요!");
        enabled = false;
#endif
    }

    void InitializeComponents()
    {
        mainCamera = Camera.main;
        targetCat = FindObjectOfType<TestCat>();

        // 스프라이트 로드
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
            catFootSprite = Resources.Load<Sprite>("Image/Minigame/cat_foot");

        if (pointSprite == null)
            pointSprite = Resources.Load<Sprite>("Image/Minigame/point");

        if (churuSprite == null)
            churuSprite = Resources.Load<Sprite>("Image/Minigame/churu");
    }

    void SetupUI()
    {
        // 미니게임 버튼 이벤트 연결
        if (minigameBtn != null)
        {
            minigameBtn.onClick.AddListener(StartEyeTrackingGame);
            Debug.Log("Minigamebtn 이벤트 연결 완료");
        }

        // UI 텍스트 자동 생성
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
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

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
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        RectTransform rect = scoreObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(20, -20);
        rect.sizeDelta = new Vector2(200, 30);

        scoreText = text;
        scoreObj.SetActive(false);
    }

#if OPENCV_FOR_UNITY
    IEnumerator InitializeEyeTracking()
    {
        // 웹캠 초기화
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("웹캠을 찾을 수 없습니다!");
            yield break;
        }

        webCamTexture = new WebCamTexture(devices[0].name, webcamWidth, webcamHeight, webcamFPS);
        webCamTexture.Play();

        while (!webCamTexture.isPlaying)
            yield return null;

        // OpenCV 초기화
        rgbaMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4);
        grayMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC1);

        // 얼굴/눈 감지 모델 로드
        string facePath = Utils.getFilePath("haarcascade_frontalface_alt.xml");
        string eyePath = Utils.getFilePath("haarcascade_eye.xml");

        if (string.IsNullOrEmpty(facePath))
            facePath = Utils.getFilePath("haarcascade_frontalface_default.xml");

        faceCascade = new CascadeClassifier(facePath);
        eyeCascade = new CascadeClassifier(eyePath);

        if (faceCascade.empty())
        {
            Debug.LogError("얼굴 감지 모델 로드 실패!");
            yield break;
        }

        // 보정 좌표 설정
        SetupCalibration();

        // UI 설정
        if (webcamDisplay != null)
            webcamDisplay.texture = webCamTexture;

        Debug.Log("눈 추적 시스템 초기화 완료!");
        Debug.Log("C키: 보정 시작, Space: 보정 점 기록");
    }

    void SetupCalibration()
    {
        calibrationTargets.Clear();
        float margin = 120f;
        float w = Screen.width;
        float h = Screen.height;

        // 9점 보정
        calibrationTargets.Add(new Vector2(margin, margin));
        calibrationTargets.Add(new Vector2(w * 0.5f, margin));
        calibrationTargets.Add(new Vector2(w - margin, margin));
        calibrationTargets.Add(new Vector2(margin, h * 0.5f));
        calibrationTargets.Add(new Vector2(w * 0.5f, h * 0.5f));
        calibrationTargets.Add(new Vector2(w - margin, h * 0.5f));
        calibrationTargets.Add(new Vector2(margin, h - margin));
        calibrationTargets.Add(new Vector2(w * 0.5f, h - margin));
        calibrationTargets.Add(new Vector2(w - margin, h - margin));
    }
#endif

    void Update()
    {
        if (!isGameActive) return;

#if OPENCV_FOR_UNITY
        HandleCalibrationInput();
        ProcessEyeTracking();
        UpdatePointMovement();
#endif

        UpdateGameTimer();
        UpdateChuruSpawning();
        CheckCatChuruCollision();
        UpdateCatMovement();
    }

#if OPENCV_FOR_UNITY
    void HandleCalibrationInput()
    {
        if (Input.GetKeyDown(KeyCode.C))
            StartCalibration();

        if (Input.GetKeyDown(KeyCode.R))
            ResetCalibration();

        if (Input.GetKeyDown(KeyCode.Space) && isCalibrating)
            ProcessCalibrationPoint();
    }

    void ProcessEyeTracking()
    {
        if (webCamTexture == null || !webCamTexture.isPlaying) return;

        // 웹캠에서 프레임 가져오기
        Utils.webCamTextureToMat(webCamTexture, rgbaMat);
        Imgproc.cvtColor(rgbaMat, grayMat, Imgproc.COLOR_RGBA2GRAY);

        // 얼굴 감지
        MatOfRect faces = new MatOfRect();
        faceCascade.detectMultiScale(grayMat, faces, 1.1, 5, 0, new Size(80, 80), new Size());

        OpenCVForUnity.CoreModule.Rect[] faceArray = faces.toArray();

        if (faceArray.Length > 0)
        {
            OpenCVForUnity.CoreModule.Rect face = faceArray[0];

            if (eyeCascade != null && !eyeCascade.empty())
            {
                // 얼굴 영역에서 눈 감지
                Mat faceROI = new Mat(grayMat, face);
                MatOfRect eyes = new MatOfRect();
                eyeCascade.detectMultiScale(faceROI, eyes, 1.1, 3, 0, new Size(20, 20), new Size());

                OpenCVForUnity.CoreModule.Rect[] eyeArray = eyes.toArray();

                if (eyeArray.Length >= 2)
                {
                    // 두 눈의 중심점 계산
                    var eye1 = eyeArray[0];
                    var eye2 = eyeArray[1];

                    Vector2 leftEye = new Vector2(
                        face.x + eye1.x + eye1.width * 0.5f,
                        face.y + eye1.y + eye1.height * 0.5f
                    );

                    Vector2 rightEye = new Vector2(
                        face.x + eye2.x + eye2.width * 0.5f,
                        face.y + eye2.y + eye2.height * 0.5f
                    );

                    // 시선 계산
                    Vector2 eyeCenter = (leftEye + rightEye) * 0.5f;
                    EstimateGaze(eyeCenter);
                    isGazeValid = true;
                }
                else
                {
                    isGazeValid = false;
                }

                faceROI.Dispose();
                eyes.Dispose();
            }
            else
            {
                // 눈 감지가 없으면 얼굴 중심 사용
                Vector2 faceCenter = new Vector2(
                    face.x + face.width * 0.5f,
                    face.y + face.height * 0.3f
                );
                EstimateGaze(faceCenter);
                isGazeValid = true;
            }
        }
        else
        {
            isGazeValid = false;
        }

        faces.Dispose();

        // 시선 스무딩
        if (isGazeValid)
        {
            smoothedGazePoint = Vector2.Lerp(smoothedGazePoint, currentGazePoint, gazeSmoothing * Time.deltaTime);
        }
    }

    void EstimateGaze(Vector2 eyeCenter)
    {
        // 웹캠 좌표를 화면 좌표로 변환
        float normalizedX = eyeCenter.x / webCamTexture.width;
        float normalizedY = 1f - (eyeCenter.y / webCamTexture.height);

        // 보정 적용
        if (isCalibrated && calibrationGazes.Count > 0)
        {
            Vector2 averageOffset = Vector2.zero;
            for (int i = 0; i < calibrationTargets.Count && i < calibrationGazes.Count; i++)
            {
                averageOffset += calibrationTargets[i] - calibrationGazes[i];
            }
            averageOffset /= calibrationGazes.Count;

            currentGazePoint = new Vector2(
                normalizedX * Screen.width,
                normalizedY * Screen.height
            ) + averageOffset * 0.2f;
        }
        else
        {
            currentGazePoint = new Vector2(
                normalizedX * Screen.width,
                normalizedY * Screen.height
            );
        }

        // 화면 경계 제한
        currentGazePoint.x = Mathf.Clamp(currentGazePoint.x, 0, Screen.width);
        currentGazePoint.y = Mathf.Clamp(currentGazePoint.y, 0, Screen.height);
    }

    void UpdatePointMovement()
    {
        if (pointObject == null || !isGazeValid) return;

        // point 스프라이트를 시선 위치로 이동
        Vector3 gazeWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(smoothedGazePoint.x, smoothedGazePoint.y, 10f));

        pointObject.transform.position = Vector3.Lerp(
            pointObject.transform.position,
            gazeWorldPos,
            pointFollowSpeed * Time.deltaTime
        );

        // 시선 고정 감지 (클릭 대신)
        CheckGazeFixation(gazeWorldPos);
    }

    void CheckGazeFixation(Vector3 gazeWorldPos)
    {
        float distance = Vector2.Distance(smoothedGazePoint, lastGazePos);

        if (distance < gazeFixationRadius)
        {
            fixationTimer += Time.deltaTime;

            if (fixationTimer >= gazeFixationTime)
            {
                // 시선 "클릭" 발생 - 고양이 목표 설정
                SetCatTarget(gazeWorldPos);
                fixationTimer = 0f;
            }
        }
        else
        {
            fixationTimer = 0f;
            lastGazePos = smoothedGazePoint;
        }
    }

    void StartCalibration()
    {
        isCalibrating = true;
        calibrationIndex = 0;
        calibrationGazes.Clear();
        Debug.Log("🎯 보정 시작! 각 점을 바라보고 스페이스 키를 누르세요.");
    }

    void ProcessCalibrationPoint()
    {
        if (!isCalibrating || !isGazeValid) return;

        calibrationGazes.Add(currentGazePoint);
        calibrationIndex++;

        Debug.Log($"보정 점 {calibrationIndex}/9 완료");

        if (calibrationIndex >= calibrationTargets.Count)
        {
            CompleteCalibration();
        }
    }

    void CompleteCalibration()
    {
        isCalibrating = false;
        isCalibrated = true;
        Debug.Log("✅ 보정 완료!");
    }

    void ResetCalibration()
    {
        isCalibrated = false;
        isCalibrating = false;
        calibrationGazes.Clear();
        calibrationIndex = 0;
        Debug.Log("🔄 보정 리셋");
    }
#endif

    public void StartEyeTrackingGame()
    {
        if (isGameActive)
        {
            Debug.Log("이미 게임이 진행 중입니다!");
            return;
        }

        Debug.Log("👁️ 눈 추적 미니게임 시작!");

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

        if (CompatibilityWindowManager.Instance != null)
        {
            CompatibilityWindowManager.Instance.DisableClickThrough();
        }

#if OPENCV_FOR_UNITY
        if (!isCalibrated)
        {
            Debug.LogWarning("⚠️ 눈 추적이 보정되지 않았습니다. C키로 보정하세요.");
        }
#endif
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
            animator.SetBool("IsFacingRight", direction.x > 0);
        }

        Debug.Log($"👁️ 시선으로 고양이 목표 설정: {worldPosition}");
    }

    void DisableCatAutoMovement()
    {
        if (targetCat == null) return;

        if (targetCat.MovementController != null)
            targetCat.MovementController.enabled = false;

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
    }

    void EnableCatAutoMovement()
    {
        if (targetCat == null) return;

        if (targetCat.MovementController != null)
            targetCat.MovementController.enabled = true;

        if (targetCat.catAnimator != null)
            targetCat.catAnimator.enabled = true;
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
            EndEyeTrackingGame();
        }
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

        Debug.Log($"👁️ 고양이가 츄르를 먹었습니다! 점수: {currentScore}");
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
    }

    void AddChur(int amount)
    {
        if (CatTower.Instance != null)
        {
            CatTower.Instance.churCount += amount;
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

    void EndEyeTrackingGame()
    {
        Debug.Log($"👁️ 눈 추적 미니게임 종료! 최종 점수: {currentScore}");

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
            CompatibilityWindowManager.Instance.EnableClickThrough();
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
            resultText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

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
        if (!isGameActive) return;

        DrawGameUI();

#if OPENCV_FOR_UNITY
        DrawEyeTrackingUI();
#endif
    }

    void DrawGameUI()
    {
        // 게임 상태 표시

        // Fix for CS0104: Explicitly specify the namespace for 'Rect' to resolve ambiguity.
        GUILayout.BeginArea(new UnityEngine.Rect(10, 100, 300, 150));
        GUILayout.Label("=== 👁️ 눈 추적 미니게임 ===");
        GUILayout.Label($"점수: {currentScore}");
        GUILayout.Label($"남은 시간: {gameTimer:F1}초");
        GUILayout.Label($"츄르 개수: {churuObjects.Count}/{maxChuruCount}");

#if OPENCV_FOR_UNITY
        GUILayout.Label($"시선 감지: {(isGazeValid ? "✅" : "❌")}");
        GUILayout.Label($"보정 완료: {(isCalibrated ? "✅" : "❌")}");

        if (fixationTimer > 0)
        {
            float progress = fixationTimer / gazeFixationTime;
            GUILayout.Label($"시선 고정: {progress * 100:F0}%");
        }
#endif

        GUILayout.Space(10);
        GUILayout.Label("⌨️ ESC: 게임 종료");

#if OPENCV_FOR_UNITY
        if (!isCalibrated)
        {
            GUILayout.Label("⚠️ C키로 보정 필요!");
        }
#endif

        GUILayout.EndArea();
    }

#if OPENCV_FOR_UNITY
    void DrawEyeTrackingUI()
    {
        // 시선 커서 표시
        if (isGazeValid)
        {
            Vector2 gazePos = smoothedGazePoint;

            // 시선 십자가
            GUI.color = Color.cyan;
            GUI.Box(new UnityEngine.Rect(gazePos.x - 15, gazePos.y - 1, 30, 2), "");
            GUI.Box(new UnityEngine.Rect(gazePos.x - 1, gazePos.y - 15, 2, 30), "");

            // 고정 진행도 원
            if (fixationTimer > 0)
            {
                float progress = fixationTimer / gazeFixationTime;
                GUI.color = Color.Lerp(Color.yellow, Color.green, progress);
                float size = 50f * progress;
                GUI.Box(new UnityEngine.Rect(gazePos.x - size / 2, gazePos.y - size / 2, size, size), "");
            }

            GUI.color = Color.white;
        }

        // 보정 모드 UI
        if (isCalibrating && calibrationIndex < calibrationTargets.Count)
        {
            Vector2 target = calibrationTargets[calibrationIndex];

            // 보정 점
            GUI.color = Color.red;
            GUI.Box(new UnityEngine.Rect(target.x - 30, target.y - 30, 60, 60), "");
            GUI.color = Color.yellow;
            GUI.Box(new UnityEngine.Rect(target.x - 20, target.y - 20, 40, 40), "");
            GUI.color = Color.white;

            // 번호
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 24;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.black;

            GUI.Label(new UnityEngine.Rect(target.x - 20, target.y - 12, 40, 24), $"{calibrationIndex + 1}", style);

            // 안내 메시지
            GUI.Box(new UnityEngine.Rect(Screen.width / 2 - 200, Screen.height - 80, 400, 50), "");

            style.normal.textColor = Color.yellow;
            style.fontSize = 18;
            GUI.Label(new UnityEngine.Rect(Screen.width / 2 - 200, Screen.height - 75, 400, 40),
                $"점 {calibrationIndex + 1}/9를 바라보고 스페이스 키를 누르세요", style);
        }
    }
#endif

    void LateUpdate()
    {
        // ESC 키로 미니게임 강제 종료
        if (Input.GetKeyDown(KeyCode.Escape) && isGameActive)
        {
            Debug.Log("ESC 키로 미니게임 강제 종료");
            EndEyeTrackingGame();
        }
    }

    void OnDestroy()
    {
#if OPENCV_FOR_UNITY
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            Destroy(webCamTexture);
        }

        if (rgbaMat != null) rgbaMat.Dispose();
        if (grayMat != null) grayMat.Dispose();
        if (faceCascade != null) faceCascade.Dispose();
        if (eyeCascade != null) eyeCascade.Dispose();
#endif
    }

    // 외부 접근용 프로퍼티
    public bool IsGameActive => isGameActive;
    public float GameTimer => gameTimer;
    public int CurrentScore => currentScore;
    public int ChuruCount => churuObjects.Count;

#if OPENCV_FOR_UNITY
    public bool IsGazeValid => isGazeValid;
    public Vector2 GazePosition => smoothedGazePoint;
    public bool IsCalibrated => isCalibrated;
    public float FixationProgress => fixationTimer / gazeFixationTime;
#endif

    // 디버그용 메서드들
    [ContextMenu("Force Start Game")]
    public void ForceStartGame()
    {
        if (!isGameActive)
        {
            StartEyeTrackingGame();
            Debug.Log("강제로 게임 시작");
        }
    }

    [ContextMenu("Force End Game")]
    public void ForceEndGame()
    {
        if (isGameActive)
        {
            EndEyeTrackingGame();
            Debug.Log("강제로 게임 종료");
        }
    }

#if OPENCV_FOR_UNITY
    [ContextMenu("Quick Calibration")]
    public void QuickCalibration()
    {
        // 빠른 보정 (자동으로 9점 모두 기록)
        calibrationGazes.Clear();
        for (int i = 0; i < calibrationTargets.Count; i++)
        {
            // 각 타겟 위치에 약간의 오차를 추가하여 시뮬레이션
            Vector2 simulatedGaze = calibrationTargets[i] + Random.insideUnitCircle * 20f;
            calibrationGazes.Add(simulatedGaze);
        }

        isCalibrated = true;
        Debug.Log("⚡ 빠른 보정 완료!");
    }

    [ContextMenu("Perfect Calibration")]
    public void PerfectCalibration()
    {
        // 완벽한 보정 (오차 없음)
        calibrationGazes.Clear();
        for (int i = 0; i < calibrationTargets.Count; i++)
        {
            calibrationGazes.Add(calibrationTargets[i]);
        }

        isCalibrated = true;
        Debug.Log("💯 완벽한 보정 설정!");
    }

    [ContextMenu("Test Eye Tracking")]
    public void TestEyeTracking()
    {
        Debug.Log("=== 눈 추적 시스템 테스트 ===");
        Debug.Log($"웹캠 상태: {(webCamTexture != null && webCamTexture.isPlaying ? "✅ 작동 중" : "❌ 문제 있음")}");
        Debug.Log($"얼굴 감지 모델: {(faceCascade != null && !faceCascade.empty() ? "✅ 로드됨" : "❌ 로드 실패")}");
        Debug.Log($"눈 감지 모델: {(eyeCascade != null && !eyeCascade.empty() ? "✅ 로드됨" : "❌ 로드 실패")}");
        Debug.Log($"시선 감지: {(isGazeValid ? "✅ 정상" : "❌ 감지 안됨")}");
        Debug.Log($"보정 상태: {(isCalibrated ? "✅ 완료" : "❌ 필요")}");

        if (webCamTexture != null)
        {
            Debug.Log($"웹캠 해상도: {webCamTexture.width}x{webCamTexture.height}");
        }

        if (isGazeValid)
        {
            Debug.Log($"현재 시선 위치: ({smoothedGazePoint.x:F0}, {smoothedGazePoint.y:F0})");
        }
    }

    [ContextMenu("Toggle Webcam Display")]
    public void ToggleWebcamDisplay()
    {
        if (webcamDisplay != null)
        {
            webcamDisplay.gameObject.SetActive(!webcamDisplay.gameObject.activeSelf);
            Debug.Log($"웹캠 화면: {(webcamDisplay.gameObject.activeSelf ? "표시" : "숨김")}");
        }
    }

    // 런타임 설정 변경 메서드들
    public void SetGazeFixationTime(float time)
    {
        gazeFixationTime = Mathf.Clamp(time, 0.5f, 5f);
        Debug.Log($"시선 고정 시간 변경: {gazeFixationTime}초");
    }

    public void SetGazeFixationRadius(float radius)
    {
        gazeFixationRadius = Mathf.Clamp(radius, 10f, 100f);
        Debug.Log($"시선 고정 반경 변경: {gazeFixationRadius}px");
    }

    public void SetGameDuration(float duration)
    {
        if (!isGameActive)
        {
            gameDuration = Mathf.Clamp(duration, 10f, 120f);
            Debug.Log($"게임 지속 시간 변경: {gameDuration}초");
        }
        else
        {
            Debug.LogWarning("게임 진행 중에는 시간을 변경할 수 없습니다!");
        }
    }

    public void SetChuruSpawnInterval(float interval)
    {
        churuSpawnInterval = Mathf.Clamp(interval, 2f, 10f);
        Debug.Log($"츄르 생성 간격 변경: {churuSpawnInterval}초");
    }

    public void SetMaxChuruCount(int count)
    {
        maxChuruCount = Mathf.Clamp(count, 1, 10);
        Debug.Log($"최대 츄르 개수 변경: {maxChuruCount}개");
    }

    // 게임 상태 확인 메서드들
    public string GetEyeTrackingStatus()
    {
        if (webCamTexture == null || !webCamTexture.isPlaying)
            return "❌ 웹캠 연결 안됨";

        if (faceCascade == null || faceCascade.empty())
            return "❌ 얼굴 감지 모델 없음";

        if (!isGazeValid)
            return "❌ 시선 감지 실패";

        if (!isCalibrated)
            return "⚠️ 보정 필요";

        return "✅ 눈 추적 정상 작동";
    }

    public string GetGameStatus()
    {
        if (!isGameActive)
            return "게임 대기 중";

        return $"게임 진행 중 - 점수: {currentScore}, 남은시간: {gameTimer:F1}초";
    }

    // 에러 핸들링 및 복구 메서드들
    public void RestartWebcam()
    {
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            Destroy(webCamTexture);
        }

        StartCoroutine(InitializeEyeTracking());
        Debug.Log("웹캠 재시작 시도");
    }

    public void ResetEyeTracking()
    {
        isGazeValid = false;
        isCalibrated = false;
        isCalibrating = false;
        calibrationGazes.Clear();
        calibrationIndex = 0;
        fixationTimer = 0f;

        Debug.Log("눈 추적 시스템 리셋 완료");
    }

    // 성능 모니터링
    public void LogPerformanceInfo()
    {
        Debug.Log("=== 성능 정보 ===");
        Debug.Log($"현재 FPS: {1f / Time.deltaTime:F1}");
        Debug.Log($"웹캠 FPS: {webcamFPS}");
        Debug.Log($"활성 츄르 개수: {churuObjects.Count}");
        Debug.Log($"시선 스무딩 값: {gazeSmoothing}");
        Debug.Log($"Point 따라가기 속도: {pointFollowSpeed}");
    }

    // 접근성 설정
    public void SetEasyMode()
    {
        gazeFixationTime = 1f;      // 더 빠른 고정
        gazeFixationRadius = 50f;   // 더 큰 반경
        churuSpawnInterval = 6f;    // 더 느린 생성
        maxChuruCount = 3;          // 더 적은 츄르

        Debug.Log("쉬운 모드로 설정됨");
    }

    public void SetHardMode()
    {
        gazeFixationTime = 2f;      // 더 느린 고정
        gazeFixationRadius = 20f;   // 더 작은 반경
        churuSpawnInterval = 3f;    // 더 빠른 생성
        maxChuruCount = 7;          // 더 많은 츄르

        Debug.Log("어려운 모드로 설정됨");
    }

    // 이벤트 시스템 (다른 스크립트에서 구독 가능)
    public System.Action<int> OnScoreChanged;
    public System.Action<float> OnGameTimeChanged;
    public System.Action OnGameStarted;
    public System.Action<int> OnGameEnded;
    public System.Action OnChuruEaten;

    // 이벤트 발생 메서드들 (기존 메서드들을 수정해서 이벤트 호출)
    private void FireScoreChanged()
    {
        OnScoreChanged?.Invoke(currentScore);
    }

    private void FireGameTimeChanged()
    {
        OnGameTimeChanged?.Invoke(gameTimer);
    }

    private void FireGameStarted()
    {
        OnGameStarted?.Invoke();
    }

    private void FireGameEnded()
    {
        OnGameEnded?.Invoke(currentScore);
    }

    private void FireChuruEaten()
    {
        OnChuruEaten?.Invoke();
    }

    // 기존 메서드들에 이벤트 호출 추가하려면:
    // OnCatEatChuru에서 FireChuruEaten(); FireScoreChanged(); 호출
    // StartEyeTrackingGame에서 FireGameStarted(); 호출
    // EndEyeTrackingGame에서 FireGameEnded(); 호출
    // UpdateGameTimer에서 FireGameTimeChanged(); 호출
#endif
}
