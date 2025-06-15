using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Linq;

#if OPENCV_FOR_UNITY
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UtilsModule;
#endif

/// <summary>
/// 실제 웹캠을 사용한 눈 추적 시스템 (정리된 버전)
/// OpenCV for Unity 필수
/// </summary>
public class RealWebcamEyeTracker : MonoBehaviour
{
    #region 싱글톤
    public static RealWebcamEyeTracker Instance { get; private set; }
    #endregion

    #region Inspector 설정값들
    [Header("웹캠 설정")]
    public int webcamWidth = 640;
    public int webcamHeight = 480;
    public int webcamFPS = 30;
    public int webcamIndex = 0;

    [Header("얼굴/눈 감지 설정")]
    public float faceDetectionScale = 1.2f;
    public int minNeighbors = 3;
    public int minFaceSize = 60;
    public float eyeRegionScale = 0.3f;
    public float eyeRegionMultiplier = 0.6f;

    [Header("시선 추정 설정")]
    public float gazeSmoothing = 8f;
    public Vector2 gazeCalibrationOffset = Vector2.zero;
    public Vector2 gazeScale = new Vector2(1.2f, 1.2f);
    public bool enableGazeSmoothing = true;

    [Header("성능 설정")]
    public int processEveryNthFrame = 3;
    public bool useThreading = true;
    public bool enableDebugVisualization = true;

    [Header("UI 요소")]
    public RawImage webcamDisplay;
    public Text debugText;

    [Header("보정 설정")]
    public Texture2D calibrationPointTexture;
    public bool showCalibrationUI = true;
    public bool showDetailedCalibrationInfo = true;
    public float calibrationWaitTime = 2f;
    public int calibrationSamplesPerPoint = 5;
    public bool useAdvancedCalibration = true;

    [Header("좌우 반전 및 눈 감지 수정")]
    public bool flipHorizontal = true;
    public bool useEyeTracking = true;
    public bool useFaceCenterFallback = true;
    public Vector2 eyePositionOffset = new Vector2(0f, -0.1f);

    [Header("디버그 시각화")]
    public bool showEyeDetectionDebug = true;
    public bool showGazeDebugInfo = true;

    [Header("시선 안정화 설정")]
    public float gazeStabilityThreshold = 30f;
    public int minStableSamples = 10;
    public float outlierThreshold = 100f;
    public bool useAdvancedFiltering = true;
    public float calibrationStabilityWait = 1f;
    public int calibrationSamplesRequired = 15;
    public float maxCalibrationVariance = 40f;
    #endregion

#if OPENCV_FOR_UNITY
    #region OpenCV 관련 변수들
    // 웹캠 및 OpenCV
    private WebCamTexture webCamTexture;
    private Mat rgbaMat;
    private Mat grayMat;
    private Mat faceMat;
    private Texture2D outputTexture;

    // 스레드 간 데이터 교환용
    private Mat threadRgbaMat;
    private Mat threadGrayMat;
    private bool hasNewFrame = false;
    private readonly object frameDataLock = new object();

    // 얼굴/눈 감지
    private CascadeClassifier faceCascade;
    private CascadeClassifier eyeCascade;
    private OpenCVForUnity.CoreModule.Rect[] faces;
    private OpenCVForUnity.CoreModule.Rect[] eyes;
    #endregion

    #region 시선 추적 관련 변수들
    // 시선 데이터
    private Vector2 leftEyeCenter;
    private Vector2 rightEyeCenter;
    private Vector2 currentGazePoint;
    private Vector2 smoothedGazePoint;
    private bool isGazeValid = false;
    private bool isFaceDetected = false;
    private bool areEyesDetected = false;

    // 시선 안정화
    private Queue<Vector2> gazeHistory = new Queue<Vector2>();
    private Vector2 filteredGazePosition;
    private float lastStableTime;
    private bool isGazeStable = false;
    #endregion

    #region 보정 관련 변수들
    // 기본 보정 데이터
    private List<Vector2> calibrationTargets = new List<Vector2>();
    private List<Vector2> calibrationGazes = new List<Vector2>();
    private bool isCalibrating = false;
    private int calibrationIndex = 0;
    private bool isCalibrated = false;

    // 고급 보정 데이터
    private List<List<Vector2>> calibrationSamplesPerTarget = new List<List<Vector2>>();
    private float calibrationPointTimer = 0f;
    private int currentSampleCount = 0;
    private bool isCollectingSamples = false;
    private List<Vector2> currentCalibrationSamples = new List<Vector2>();
    private float calibrationPointStartTime;
    private bool isWaitingForStability = false;
    #endregion

    #region 성능 최적화 관련 변수들
    // 성능 최적화
    private int frameCounter = 0;
    private bool isProcessing = false;
    private Thread processingThread;
    private object lockObject = new object();

    // 스레드 간 임시 데이터
    private Vector2 tempGazePoint;
    private bool tempGazeValid;
    private bool tempFaceDetected;
    private bool tempEyesDetected;

    // 캐시된 값들
    private int cachedWebcamWidth;
    private int cachedWebcamHeight;
    private int cachedScreenWidth;
    private int cachedScreenHeight;
    private Vector2 cachedGazeCalibrationOffset;
    private Vector2 cachedGazeScale;
    #endregion

    #region Unity 생명주기 메서드들
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
        Debug.Log("🎯 실제 웹캠 눈 추적 시스템 초기화 시작");
        StartCoroutine(InitializeSystem());
    }

    void Update()
    {
        if (webCamTexture == null || !webCamTexture.isPlaying) return;

        HandleInput();

        frameCounter++;

        // 보정 중 샘플 수집 처리
        if (isCollectingSamples && isCalibrating)
        {
            UpdateSampleCollection();
        }

        // 캐시된 값들 업데이트
        if (frameCounter % (processEveryNthFrame * 10) == 0)
        {
            UpdateCachedValues();
        }

        // N프레임마다 한 번씩 처리
        if (frameCounter % processEveryNthFrame == 0)
        {
            if (useThreading)
            {
                ProcessFrameThreaded();
            }
            else
            {
                ProcessFrame();
            }
        }

        // 스무딩 적용
        if (enableGazeSmoothing && isGazeValid)
        {
            smoothedGazePoint = Vector2.Lerp(smoothedGazePoint, currentGazePoint, gazeSmoothing * Time.deltaTime);
        }
        else
        {
            smoothedGazePoint = currentGazePoint;
        }

        UpdateDebugUI();
    }

    void OnGUI()
    {
        if (!showCalibrationUI) return;

        // 보정 모드 UI
        if (isCalibrating && calibrationIndex < calibrationTargets.Count)
        {
            DrawCalibrationUI();
        }

        // 시선 커서 표시
        if (isGazeValid)
        {
            DrawGazeCursor();
        }
    }

    void OnDestroy()
    {
        // 스레드 정리
        if (processingThread != null && processingThread.IsAlive)
        {
            try
            {
                processingThread.Join(1000);
                if (processingThread.IsAlive)
                {
                    processingThread.Abort();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"스레드 종료 중 오류: {e.Message}");
            }
        }

        // Unity 리소스 정리
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            Destroy(webCamTexture);
        }

        // OpenCV Mat 정리
        if (rgbaMat != null) rgbaMat.Dispose();
        if (grayMat != null) grayMat.Dispose();
        if (threadRgbaMat != null) threadRgbaMat.Dispose();
        if (threadGrayMat != null) threadGrayMat.Dispose();
        if (faceMat != null) faceMat.Dispose();
        if (outputTexture != null) Destroy(outputTexture);
        if (faceCascade != null) faceCascade.Dispose();
        if (eyeCascade != null) eyeCascade.Dispose();
    }
    #endregion

    #region 초기화 메서드들
    IEnumerator InitializeSystem()
    {
        // 웹캠 초기화
        yield return StartCoroutine(InitializeWebcam());

        // OpenCV 초기화
        yield return StartCoroutine(InitializeOpenCV());

        // 보정 설정
        SetupCalibration();

        Debug.Log("✅ 실제 웹캠 눈 추적 시스템 초기화 완료!");
        Debug.Log("📹 웹캠 기반 실시간 얼굴/눈 감지");
        Debug.Log("⌨️ C키: 보정 시작, R키: 보정 리셋, Space: 보정 점 기록");
    }

    IEnumerator InitializeWebcam()
    {
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            Debug.LogError("❌ 웹캠을 찾을 수 없습니다!");
            yield break;
        }

        Debug.Log($"📹 발견된 웹캠: {devices.Length}개");
        for (int i = 0; i < devices.Length; i++)
        {
            Debug.Log($"  {i}: {devices[i].name}");
        }

        if (webcamIndex >= devices.Length)
        {
            webcamIndex = 0;
        }

        webCamTexture = new WebCamTexture(devices[webcamIndex].name, webcamWidth, webcamHeight, webcamFPS);
        webCamTexture.Play();

        // 웹캠이 시작될 때까지 대기
        while (!webCamTexture.isPlaying)
        {
            yield return null;
        }

        Debug.Log($"✅ 웹캠 초기화 완료: {devices[webcamIndex].name} ({webCamTexture.width}x{webCamTexture.height})");

        // UI 설정
        if (webcamDisplay != null)
        {
            webcamDisplay.texture = webCamTexture;
        }
    }

    IEnumerator InitializeOpenCV()
    {
        // Mat 초기화
        rgbaMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4);
        grayMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC1);

        // 스레드용 Mat 초기화
        threadRgbaMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4);
        threadGrayMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC1);

        // Texture2D 초기화
        outputTexture = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32, false);

        // 스레드에서 사용할 값들 캐시
        UpdateCachedValues();

        // Haar Cascade 로드
        yield return StartCoroutine(LoadHaarCascades());

        Debug.Log("✅ OpenCV 초기화 완료");
    }

    IEnumerator LoadHaarCascades()
    {
        // 얼굴 감지 모델 로드
        string faceCascadePath = GetHaarCascadePath("haarcascade_frontalface_alt.xml");
        if (string.IsNullOrEmpty(faceCascadePath))
        {
            faceCascadePath = GetHaarCascadePath("haarcascade_frontalface_default.xml");
        }

        if (string.IsNullOrEmpty(faceCascadePath))
        {
            Debug.LogError("❌ 얼굴 감지 모델을 찾을 수 없습니다!");
            yield break;
        }

        faceCascade = new CascadeClassifier(faceCascadePath);
        if (faceCascade.empty())
        {
            Debug.LogError("❌ 얼굴 감지 모델 로드 실패!");
            yield break;
        }

        // 눈 감지 모델 로드
        string eyeCascadePath = GetHaarCascadePath("haarcascade_eye.xml");
        if (!string.IsNullOrEmpty(eyeCascadePath))
        {
            eyeCascade = new CascadeClassifier(eyeCascadePath);
            if (eyeCascade.empty())
            {
                eyeCascade = null;
            }
        }

        Debug.Log("✅ 얼굴 감지 모델 로드 성공");
        yield return null;
    }

    void SetupCalibration()
    {
        calibrationTargets.Clear();
        float margin = 150f;
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

        Debug.Log($"보정 좌표 설정 완료 - 여백: {margin}px, 화면 크기: {w}x{h}");
    }
    #endregion

    #region 입력 처리
    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            StartCalibration();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetCalibration();
        }

        if (Input.GetKeyDown(KeyCode.Space) && isCalibrating)
        {
            if (useAdvancedCalibration)
            {
                StartSampleCollection();
            }
            else
            {
                ProcessCalibrationPoint();
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelCalibration();
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            enableDebugVisualization = !enableDebugVisualization;
            Debug.Log($"🎨 시각화: {(enableDebugVisualization ? "ON" : "OFF")}");
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleEyeTrackingMode();
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleHorizontalFlip();
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            StartCoroutine(GazeDirectionTest());
        }
    }
    #endregion

    #region 프레임 처리 메서드들
    void ProcessFrameThreaded()
    {
        if (isProcessing) return;

        try
        {
            // 메인 스레드에서 웹캠 데이터 복사
            Utils.webCamTextureToMat(webCamTexture, rgbaMat);

            lock (frameDataLock)
            {
                rgbaMat.copyTo(threadRgbaMat);
                hasNewFrame = true;
            }

            if (processingThread == null || !processingThread.IsAlive)
            {
                processingThread = new Thread(ProcessFrameInThread);
                processingThread.Start();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ 멀티스레딩 오류 발생, 메인 스레드로 폴백: {e.Message}");
            useThreading = false;
            ProcessFrame();
        }
    }

    void ProcessFrameInThread()
    {
        if (isProcessing) return;
        isProcessing = true;

        try
        {
            bool frameAvailable = false;

            lock (frameDataLock)
            {
                frameAvailable = hasNewFrame;
                if (hasNewFrame)
                {
                    hasNewFrame = false;
                }
            }

            if (!frameAvailable)
            {
                isProcessing = false;
                return;
            }

            // 스레드에서 안전한 OpenCV 처리
            lock (frameDataLock)
            {
                Imgproc.cvtColor(threadRgbaMat, threadGrayMat, Imgproc.COLOR_RGBA2GRAY);
                DetectFaceInThread();

                if (tempFaceDetected)
                {
                    DetectEyesInThread();

                    if (tempEyesDetected)
                    {
                        EstimateGazeInThread();
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 스레드 프레임 처리 오류: {e.Message}");
        }

        isProcessing = false;
    }

    void ProcessFrame()
    {
        // 메인 스레드에서 직접 처리
        Utils.webCamTextureToMat(webCamTexture, rgbaMat);
        Imgproc.cvtColor(rgbaMat, grayMat, Imgproc.COLOR_RGBA2GRAY);

        DetectFace();

        if (isFaceDetected)
        {
            DetectEyes();

            if (areEyesDetected)
            {
                EstimateGaze();
            }
        }

        if (enableDebugVisualization)
        {
            DrawDebugVisualization();
        }
    }
    #endregion

    #region 얼굴/눈 감지 메서드들
    void DetectFaceInThread()
    {
        if (faceCascade == null)
        {
            tempFaceDetected = false;
            return;
        }

        try
        {
            MatOfRect faceDetections = new MatOfRect();

            faceCascade.detectMultiScale(
                threadGrayMat,
                faceDetections,
                faceDetectionScale,
                minNeighbors,
                0,
                new Size(minFaceSize, minFaceSize),
                new Size()
            );

            OpenCVForUnity.CoreModule.Rect[] detectedFaces = faceDetections.toArray();
            tempFaceDetected = detectedFaces.Length > 0;

            if (tempFaceDetected)
            {
                lock (lockObject)
                {
                    faces = detectedFaces;
                    isFaceDetected = tempFaceDetected;

                    // 가장 큰 얼굴 선택
                    if (faces.Length > 1)
                    {
                        OpenCVForUnity.CoreModule.Rect largestFace = faces[0];
                        for (int i = 1; i < faces.Length; i++)
                        {
                            if (faces[i].area() > largestFace.area())
                            {
                                largestFace = faces[i];
                            }
                        }
                        faces = new OpenCVForUnity.CoreModule.Rect[] { largestFace };
                    }
                }
            }

            faceDetections.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 스레드 얼굴 감지 오류: {e.Message}");
            tempFaceDetected = false;
        }
    }

    void DetectFace()
    {
        if (faceCascade == null) return;

        MatOfRect faceDetections = new MatOfRect();

        faceCascade.detectMultiScale(
            grayMat,
            faceDetections,
            faceDetectionScale,
            minNeighbors,
            0,
            new Size(minFaceSize, minFaceSize),
            new Size()
        );

        faces = faceDetections.toArray();
        isFaceDetected = faces.Length > 0;

        if (isFaceDetected)
        {
            // 가장 큰 얼굴 선택
            OpenCVForUnity.CoreModule.Rect largestFace = faces[0];
            for (int i = 1; i < faces.Length; i++)
            {
                if (faces[i].area() > largestFace.area())
                {
                    largestFace = faces[i];
                }
            }
            faces = new OpenCVForUnity.CoreModule.Rect[] { largestFace };
        }

        faceDetections.Dispose();
    }

    void DetectEyesInThread()
    {
        if (!tempFaceDetected)
        {
            tempEyesDetected = false;
            return;
        }

        try
        {
            OpenCVForUnity.CoreModule.Rect face;
            lock (lockObject)
            {
                if (faces == null || faces.Length == 0)
                {
                    tempEyesDetected = false;
                    return;
                }
                face = faces[0];
            }

            if (eyeCascade != null)
            {
                Mat faceROI = new Mat(threadGrayMat, face);
                MatOfRect eyeDetections = new MatOfRect();

                eyeCascade.detectMultiScale(
                    faceROI,
                    eyeDetections,
                    1.1,
                    5,
                    0,
                    new Size(20, 20),
                    new Size()
                );

                OpenCVForUnity.CoreModule.Rect[] detectedEyes = eyeDetections.toArray();
                tempEyesDetected = detectedEyes.Length >= 2;

                if (tempEyesDetected)
                {
                    lock (lockObject)
                    {
                        eyes = detectedEyes;
                        areEyesDetected = tempEyesDetected;

                        var eye1 = eyes[0];
                        var eye2 = eyes[1];

                        leftEyeCenter = new Vector2(
                            face.x + eye1.x + eye1.width * 0.5f,
                            face.y + eye1.y + eye1.height * 0.5f
                        );

                        rightEyeCenter = new Vector2(
                            face.x + eye2.x + eye2.width * 0.5f,
                            face.y + eye2.y + eye2.height * 0.5f
                        );

                        if (leftEyeCenter.x > rightEyeCenter.x)
                        {
                            Vector2 temp = leftEyeCenter;
                            leftEyeCenter = rightEyeCenter;
                            rightEyeCenter = temp;
                        }
                    }
                }

                faceROI.Dispose();
                eyeDetections.Dispose();
            }
            else
            {
                Vector2 faceCenter = new Vector2(
                    face.x + face.width * 0.5f,
                    face.y + face.height * 0.3f
                );

                lock (lockObject)
                {
                    leftEyeCenter = faceCenter + new Vector2(-face.width * 0.2f, 0);
                    rightEyeCenter = faceCenter + new Vector2(face.width * 0.2f, 0);
                    areEyesDetected = true;
                    tempEyesDetected = true;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 스레드 눈 감지 오류: {e.Message}");
            tempEyesDetected = false;
        }
    }

    void DetectEyes()
    {
        if (!isFaceDetected || faces.Length == 0)
        {
            areEyesDetected = false;
            return;
        }

        OpenCVForUnity.CoreModule.Rect face = faces[0];

        if (eyeCascade != null && !eyeCascade.empty())
        {
            int eyeRegionHeight = (int)(face.height * eyeRegionMultiplier);
            OpenCVForUnity.CoreModule.Rect eyeRegion = new OpenCVForUnity.CoreModule.Rect(
                face.x,
                face.y,
                face.width,
                eyeRegionHeight
            );

            Mat eyeROI = new Mat(grayMat, eyeRegion);
            MatOfRect eyeDetections = new MatOfRect();

            eyeCascade.detectMultiScale(
                eyeROI,
                eyeDetections,
                1.05,
                3,
                0,
                new Size(15, 15),
                new Size(face.width / 3, face.height / 4)
            );

            eyes = eyeDetections.toArray();
            areEyesDetected = eyes.Length >= 2;

            if (areEyesDetected)
            {
                if (eyes.Length > 2)
                {
                    System.Array.Sort(eyes, (a, b) => (b.width * b.height).CompareTo(a.width * a.height));
                }

                var eye1 = eyes[0];
                var eye2 = eyes[1];

                leftEyeCenter = new Vector2(
                    eyeRegion.x + eye1.x + eye1.width * 0.5f,
                    eyeRegion.y + eye1.y + eye1.height * 0.5f
                );

                rightEyeCenter = new Vector2(
                    eyeRegion.x + eye2.x + eye2.width * 0.5f,
                    eyeRegion.y + eye2.y + eye2.height * 0.5f
                );

                if (leftEyeCenter.x > rightEyeCenter.x)
                {
                    Vector2 temp = leftEyeCenter;
                    leftEyeCenter = rightEyeCenter;
                    rightEyeCenter = temp;
                }
            }

            eyeROI.Dispose();
            eyeDetections.Dispose();
        }

        if (!areEyesDetected && useFaceCenterFallback)
        {
            Vector2 faceCenter = new Vector2(
                face.x + face.width * 0.5f,
                face.y + face.height * 0.3f
            );

            leftEyeCenter = faceCenter + new Vector2(-face.width * 0.2f, 0);
            rightEyeCenter = faceCenter + new Vector2(face.width * 0.2f, 0);
            areEyesDetected = true;
        }
    }
    #endregion

    #region 시선 추정 메서드들
    void EstimateGazeInThread()
    {
        if (!tempEyesDetected)
        {
            tempGazeValid = false;
            return;
        }

        try
        {
            Vector2 eyesCenter;
            lock (lockObject)
            {
                eyesCenter = (leftEyeCenter + rightEyeCenter) * 0.5f;
            }

            if (cachedWebcamWidth <= 0 || cachedWebcamHeight <= 0 || cachedScreenWidth <= 0 || cachedScreenHeight <= 0)
            {
                tempGazeValid = false;
                return;
            }

            float normalizedX = eyesCenter.x / cachedWebcamWidth;
            float normalizedY = 1f - (eyesCenter.y / cachedWebcamHeight);

            normalizedX = (normalizedX + cachedGazeCalibrationOffset.x) * cachedGazeScale.x;
            normalizedY = (normalizedY + cachedGazeCalibrationOffset.y) * cachedGazeScale.y;

            Vector2 calculatedGaze = new Vector2(
                normalizedX * cachedScreenWidth,
                normalizedY * cachedScreenHeight
            );

            calculatedGaze.x = Mathf.Clamp(calculatedGaze.x, 0, cachedScreenWidth);
            calculatedGaze.y = Mathf.Clamp(calculatedGaze.y, 0, cachedScreenHeight);

            lock (lockObject)
            {
                currentGazePoint = calculatedGaze;
                isGazeValid = true;
                tempGazeValid = true;
                tempGazePoint = calculatedGaze;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 스레드 시선 추정 오류: {e.Message}");
            tempGazeValid = false;
        }
    }
    void EstimateGaze()
    {
        // 👁️ 먼저 눈동자 기반 추적 시도
        TrackPupilBasedGaze();

        // 눈동자 감지 실패시 기존 방법 사용
        if (!isGazeValid)
        {
            // 기존 코드 (얼굴 중심 기반)
            if (!areEyesDetected && !isFaceDetected)
            {
                isGazeValid = false;
                return;
            }

            Vector2 gazePoint;

            if (areEyesDetected && useEyeTracking)
            {
                gazePoint = (leftEyeCenter + rightEyeCenter) * 0.5f;
            }
            else if (isFaceDetected && useFaceCenterFallback)
            {
                OpenCVForUnity.CoreModule.Rect face = faces[0];
                Vector2 faceCenter = new Vector2(
                    face.x + face.width * 0.5f,
                    face.y + face.height * (0.3f + eyePositionOffset.y)
                );
                gazePoint = faceCenter;
            }
            else
            {
                isGazeValid = false;
                return;
            }

            currentGazePoint = gazePoint;
            isGazeValid = true;
        }

        // 🔄 좌우 반전 처리 (핵심 수정!)
        if (flipHorizontal)
        {
            currentGazePoint.x = webCamTexture.width - currentGazePoint.x;
        }

        // 웹캠 좌표를 화면 좌표로 변환
        float normalizedX = currentGazePoint.x / webCamTexture.width;
        float normalizedY = 1f - (currentGazePoint.y / webCamTexture.height);

        // 보정 적용
        normalizedX = (normalizedX + gazeCalibrationOffset.x) * gazeScale.x;
        normalizedY = (normalizedY + gazeCalibrationOffset.y) * gazeScale.y;

        // 화면 좌표로 변환
        currentGazePoint = new Vector2(
            normalizedX * Screen.width,
            normalizedY * Screen.height
        );

        // 화면 경계 제한
        currentGazePoint.x = Mathf.Clamp(currentGazePoint.x, 0, Screen.width);
        currentGazePoint.y = Mathf.Clamp(currentGazePoint.y, 0, Screen.height);

        isGazeValid = true;
    }

    #endregion
    #region 눈동자 중심 감지 (새로 추가)
    Vector2 DetectPupilInEye(OpenCVForUnity.CoreModule.Rect eyeRect)
    {
        if (eyeRect.width <= 0 || eyeRect.height <= 0) return Vector2.zero;

        try
        {
            Mat eyeROI = new Mat(grayMat, eyeRect);

            // 가우시안 블러 적용
            Imgproc.GaussianBlur(eyeROI, eyeROI, new Size(5, 5), 0);

            // 임계값 처리로 어두운 부분(눈동자) 추출
            Mat threshold = new Mat();
            Imgproc.threshold(eyeROI, threshold, 30f, 255, Imgproc.THRESH_BINARY_INV);

            // 컨투어 찾기
            List<MatOfPoint> contours = new List<MatOfPoint>();
            Mat hierarchy = new Mat();
            Imgproc.findContours(threshold, contours, hierarchy, Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_SIMPLE);

            Vector2 bestPupilCenter = Vector2.zero;
            float bestScore = 0f;

            foreach (var contour in contours)
            {
                double area = Imgproc.contourArea(contour);

                // 크기 필터링 (Inspector에서 설정 가능하도록)
                if (area < 5 || area > 50) continue;

                // 원형도 계산
                double perimeter = Imgproc.arcLength(new MatOfPoint2f(contour.toArray()), true);
                double circularity = 4 * Mathf.PI * area / (perimeter * perimeter);

                if (circularity < 0.7f) continue;

                // 중심점 계산
                Moments moments = Imgproc.moments(contour);
                if (moments.m00 == 0) continue;

                float cx = (float)(moments.m10 / moments.m00);
                float cy = (float)(moments.m01 / moments.m00);

                // 점수 계산 (크기와 원형도 조합)
                float score = (float)(area * circularity);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPupilCenter = new Vector2(eyeRect.x + cx, eyeRect.y + cy);
                }
            }

            eyeROI.Dispose();
            threshold.Dispose();
            hierarchy.Dispose();
            foreach (var contour in contours) contour.Dispose();

            return bestPupilCenter;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"눈동자 감지 오류: {e.Message}");
            return Vector2.zero;
        }
    }

    // 눈동자 기반 시선 추적 메서드
    void TrackPupilBasedGaze()
    {
        if (faces.Length == 0 || eyes.Length < 2)
        {
            isGazeValid = false;
            return;
        }

        var face = faces[0];

        // 왼쪽 눈과 오른쪽 눈 영역 설정
        var leftEyeRect = new OpenCVForUnity.CoreModule.Rect(
            face.x + eyes[0].x, face.y + eyes[0].y, eyes[0].width, eyes[0].height);
        var rightEyeRect = new OpenCVForUnity.CoreModule.Rect(
            face.x + eyes[1].x, face.y + eyes[1].y, eyes[1].width, eyes[1].height);

        // 눈 순서 확인 (왼쪽 눈이 더 왼쪽에 있어야 함)
        if (leftEyeRect.x > rightEyeRect.x)
        {
            var temp = leftEyeRect;
            leftEyeRect = rightEyeRect;
            rightEyeRect = temp;
        }

        Vector2 leftPupil = DetectPupilInEye(leftEyeRect);
        Vector2 rightPupil = DetectPupilInEye(rightEyeRect);

        bool leftValid = leftPupil != Vector2.zero;
        bool rightValid = rightPupil != Vector2.zero;

        if (leftValid && rightValid)
        {
            // 양쪽 눈동자 모두 감지됨 - 평균 사용
            currentGazePoint = (leftPupil + rightPupil) * 0.5f;
            isGazeValid = true;

            Debug.Log($"👁️ 양쪽 눈동자 감지: L{leftPupil}, R{rightPupil}");
        }
        else if (leftValid || rightValid)
        {
            // 한쪽 눈동자만 감지됨
            currentGazePoint = leftValid ? leftPupil : rightPupil;
            isGazeValid = true;

            Debug.Log($"👁️ 한쪽 눈동자 감지: {currentGazePoint}");
        }
        else
        {
            // 눈동자 감지 실패 - 기존 방법으로 폴백
            isGazeValid = false;
            Debug.LogWarning("⚠️ 눈동자 감지 실패 - 기존 방법 사용");
        }
    }
    #endregion
    #region 보정 관련 메서드들
    void StartCalibration()
    {
        isCalibrating = true;
        calibrationIndex = 0;
        calibrationGazes.Clear();

        UpdateCachedValues();

        // 보정 중 click-through 비활성화
        if (CompatibilityWindowManager.Instance != null)
        {
            CompatibilityWindowManager.Instance.DisableClickThrough();
            Debug.Log("🔒 웹캠 보정 중 click-through 비활성화");
        }

        // 고급 보정 모드라면 샘플 목록 초기화
        if (useAdvancedCalibration)
        {
            calibrationSamplesPerTarget.Clear();
            for (int i = 0; i < calibrationTargets.Count; i++)
            {
                calibrationSamplesPerTarget.Add(new List<Vector2>());
            }
        }

        Debug.Log("🎯 실제 웹캠 보정 시작! 각 점을 바라보고 스페이스 키를 누르세요.");
        Debug.Log("👀 얼굴이 웹캠에 잘 보이는지 확인하세요.");
    }

    void ResetCalibration()
    {
        isCalibrated = false;
        isCalibrating = false;
        gazeCalibrationOffset = Vector2.zero;
        gazeScale = new Vector2(1.2f, 1.2f);
        calibrationGazes.Clear();
        calibrationIndex = 0;

        if (useAdvancedCalibration)
        {
            calibrationSamplesPerTarget.Clear();
            isCollectingSamples = false;
            currentSampleCount = 0;
            calibrationPointTimer = 0f;
        }

        UpdateCachedValues();
        RestoreClickThroughStateAfterCalibration();

        Debug.Log("🔄 웹캠 보정 리셋 완료");
    }

    void CancelCalibration()
    {
        isCalibrating = false;
        isCollectingSamples = false;
        calibrationIndex = 0;
        calibrationGazes.Clear();
        calibrationSamplesPerTarget.Clear();

        RestoreClickThroughStateAfterCalibration();

        Debug.Log("❌ 웹캠 보정이 취소되었습니다.");
    }

    void ProcessCalibrationPoint()
    {
        if (!isCalibrating || !isGazeValid)
        {
            Debug.LogWarning("⚠️ 시선이 감지되지 않습니다. 얼굴이 웹캠에 잘 보이는지 확인하세요.");
            return;
        }

        Vector2 target = calibrationTargets[calibrationIndex];
        float distance = Vector2.Distance(currentGazePoint, target);

        if (distance > 200f)
        {
            Debug.LogWarning($"⚠️ 시선과 타겟의 거리가 큽니다 ({distance:F1}px). 정확히 타겟을 바라보고 있는지 확인하세요.");
        }

        calibrationGazes.Add(currentGazePoint);
        calibrationIndex++;

        Debug.Log($"보정 점 {calibrationIndex}/9 완료 - 위치: {currentGazePoint}, 오차: {distance:F1}px");

        if (calibrationIndex >= calibrationTargets.Count)
        {
            CompleteCalibration();
        }
    }

    void StartSampleCollection()
    {
        if (!isCalibrating || !isGazeValid)
        {
            Debug.LogWarning("⚠️ 시선이 감지되지 않습니다. 얼굴이 웹캠에 잘 보이는지 확인하세요.");
            return;
        }

        isCollectingSamples = true;
        isWaitingForStability = true;
        currentSampleCount = 0;
        calibrationPointTimer = 0f;
        currentCalibrationSamples.Clear();

        Debug.Log($"📍 보정 점 {calibrationIndex + 1}/9 - 안정화 대기 중...");
        Debug.Log($"💡 점을 정확히 바라보고 머리를 고정하세요.");
    }

    void UpdateSampleCollection()
    {
        if (!isCollectingSamples || !isCalibrating) return;

        calibrationPointTimer += Time.deltaTime;

        // 1단계: 안정화 대기
        if (isWaitingForStability)
        {
            if (IsGazeStableForCalibration())
            {
                isWaitingForStability = false;
                currentCalibrationSamples.Clear();
                calibrationPointStartTime = Time.time;
                Debug.Log($"✅ 시선 안정화 완료 - 샘플 수집 시작");
            }
            else if (calibrationPointTimer > 10f)
            {
                Debug.LogWarning("⚠️ 시선 안정화 타임아웃 - 강제 진행");
                isWaitingForStability = false;
                currentCalibrationSamples.Clear();
                calibrationPointStartTime = Time.time;
            }
            return;
        }

        // 2단계: 안정된 샘플 수집
        if (isGazeValid && IsGazeStableForCalibration())
        {
            float sampleInterval = calibrationWaitTime / calibrationSamplesRequired;

            if (Time.time - calibrationPointStartTime > currentCalibrationSamples.Count * sampleInterval)
            {
                Vector2 currentGaze = GetStabilizedGazePosition();

                if (currentCalibrationSamples.Count == 0 || IsConsistentWithPreviousSamples(currentGaze))
                {
                    currentCalibrationSamples.Add(currentGaze);
                    currentSampleCount = currentCalibrationSamples.Count;

                    Debug.Log($"📍 안정 샘플 {currentSampleCount}/{calibrationSamplesRequired} 수집됨 - 시선: {currentGaze}");

                    if (currentSampleCount >= calibrationSamplesRequired)
                    {
                        ProcessImprovedCalibrationPoint();
                    }
                }
                else
                {
                    Debug.LogWarning("⚠️ 시선이 불안정합니다. 점을 정확히 바라보세요.");
                }
            }
        }
        else
        {
            if (calibrationPointTimer > 15f)
            {
                Debug.LogWarning("⚠️ 보정 점 타임아웃 - 수집된 샘플로 진행");
                if (currentCalibrationSamples.Count >= 5)
                {
                    ProcessImprovedCalibrationPoint();
                }
                else
                {
                    Debug.LogError("❌ 충분한 샘플을 수집하지 못했습니다. 다음 점으로 건너뜁니다.");
                    SkipCalibrationPoint();
                }
            }
        }
    }

    void CompleteCalibration()
    {
        isCalibrating = false;

        if (calibrationGazes.Count == calibrationTargets.Count)
        {
            CalculateCalibration();
            isCalibrated = true;
            Debug.Log($"✅ 웹캠 보정 완료! 오프셋: {gazeCalibrationOffset}, 스케일: {gazeScale}");
        }
        else
        {
            Debug.LogWarning("⚠️ 보정 데이터가 부족합니다.");
        }

        RestoreClickThroughStateAfterCalibration();
    }

    void CompleteAdvancedCalibration()
    {
        isCalibrating = false;

        if (calibrationGazes.Count == calibrationTargets.Count)
        {
            CalculateAdvancedCalibration();
            isCalibrated = true;
            Debug.Log($"✅ 고급 보정 완료!");
            Debug.Log($"📊 오프셋: {gazeCalibrationOffset}");
            Debug.Log($"📊 스케일: {gazeScale}");

            TestCalibrationQuality();
        }
        else
        {
            Debug.LogWarning("⚠️ 보정 데이터가 부족합니다.");
        }
    }

    void CalculateCalibration()
    {
        Vector2 totalOffset = Vector2.zero;

        for (int i = 0; i < calibrationTargets.Count && i < calibrationGazes.Count; i++)
        {
            Vector2 target = calibrationTargets[i];
            Vector2 gaze = calibrationGazes[i];

            Vector2 offset = target - gaze;
            totalOffset += offset;
        }

        if (calibrationGazes.Count > 0)
        {
            gazeCalibrationOffset = totalOffset / (calibrationGazes.Count * Screen.width);
            gazeScale = new Vector2(1.2f, 1.2f);
            UpdateCachedValues();
        }
    }

    void CalculateAdvancedCalibration()
    {
        Vector2 totalOffset = Vector2.zero;
        Vector2 totalScale = Vector2.zero;
        int validPoints = 0;

        for (int i = 0; i < calibrationTargets.Count && i < calibrationGazes.Count; i++)
        {
            Vector2 target = calibrationTargets[i];
            Vector2 gaze = calibrationGazes[i];

            Vector2 normalizedTarget = new Vector2(target.x / Screen.width, target.y / Screen.height);
            Vector2 normalizedGaze = new Vector2(gaze.x / Screen.width, gaze.y / Screen.height);

            Vector2 offset = normalizedTarget - normalizedGaze;
            totalOffset += offset;

            if (normalizedGaze.x != 0 && normalizedGaze.y != 0)
            {
                Vector2 scale = new Vector2(
                    normalizedTarget.x / normalizedGaze.x,
                    normalizedTarget.y / normalizedGaze.y
                );
                totalScale += scale;
                validPoints++;
            }
        }

        if (validPoints > 0)
        {
            gazeCalibrationOffset = totalOffset / calibrationGazes.Count;
            gazeScale = totalScale / validPoints;

            gazeScale.x = Mathf.Clamp(gazeScale.x, 0.5f, 2.5f);
            gazeScale.y = Mathf.Clamp(gazeScale.y, 0.5f, 2.5f);

            UpdateCachedValues();
            EvaluateCalibrationQuality();
        }
        else
        {
            Debug.LogError("❌ 유효한 보정 데이터가 없습니다!");
        }
    }
    #endregion

    #region 시선 안정화 메서드들
    bool IsGazeStableForCalibration()
    {
        if (!isGazeValid) return false;

        Vector2 currentGaze = smoothedGazePoint;

        gazeHistory.Enqueue(currentGaze);
        if (gazeHistory.Count > 30)
        {
            gazeHistory.Dequeue();
        }

        if (gazeHistory.Count < minStableSamples) return false;

        Vector2 average = Vector2.zero;
        foreach (Vector2 gaze in gazeHistory)
        {
            average += gaze;
        }
        average /= gazeHistory.Count;

        float variance = 0f;
        foreach (Vector2 gaze in gazeHistory)
        {
            variance += Vector2.Distance(gaze, average);
        }
        variance /= gazeHistory.Count;

        bool isStable = variance < gazeStabilityThreshold;

        if (isStable && !isGazeStable)
        {
            lastStableTime = Time.time;
            isGazeStable = true;
        }
        else if (!isStable)
        {
            isGazeStable = false;
        }

        return isStable && (Time.time - lastStableTime > 0.5f);
    }

    Vector2 GetStabilizedGazePosition()
    {
        if (gazeHistory.Count == 0) return currentGazePoint;

        List<Vector2> validSamples = new List<Vector2>();
        Vector2 roughAverage = Vector2.zero;

        foreach (Vector2 gaze in gazeHistory)
        {
            roughAverage += gaze;
        }
        roughAverage /= gazeHistory.Count;

        foreach (Vector2 gaze in gazeHistory)
        {
            if (Vector2.Distance(gaze, roughAverage) < outlierThreshold)
            {
                validSamples.Add(gaze);
            }
        }

        if (validSamples.Count == 0) return currentGazePoint;

        Vector2 stableAverage = Vector2.zero;
        foreach (Vector2 sample in validSamples)
        {
            stableAverage += sample;
        }
        stableAverage /= validSamples.Count;

        return stableAverage;
    }

    bool IsConsistentWithPreviousSamples(Vector2 newSample)
    {
        if (currentCalibrationSamples.Count == 0) return true;

        Vector2 average = Vector2.zero;
        foreach (Vector2 sample in currentCalibrationSamples)
        {
            average += sample;
        }
        average /= currentCalibrationSamples.Count;

        float distance = Vector2.Distance(newSample, average);
        return distance < gazeStabilityThreshold * 1.5f;
    }

    void ProcessImprovedCalibrationPoint()
    {
        if (!isCalibrating) return;

        isCollectingSamples = false;

        Vector2 finalGaze = CalculateRobustAverage(currentCalibrationSamples);

        float variance = 0f;
        foreach (Vector2 sample in currentCalibrationSamples)
        {
            variance += Vector2.Distance(sample, finalGaze);
        }
        variance /= currentCalibrationSamples.Count;

        calibrationGazes.Add(finalGaze);
        calibrationIndex++;

        Debug.Log($"✅ 개선된 보정 점 {calibrationIndex}/9 완료");
        Debug.Log($"📊 최종 시선: {finalGaze}, 분산: {variance:F1}px ({currentCalibrationSamples.Count}개 샘플)");

        if (variance < 20f)
        {
            Debug.Log($"🏆 우수한 품질의 보정 점!");
        }
        else if (variance < maxCalibrationVariance)
        {
            Debug.Log($"✅ 양호한 품질의 보정 점");
        }
        else
        {
            Debug.LogWarning($"⚠️ 품질이 낮은 보정 점 (분산: {variance:F1}px > {maxCalibrationVariance}px)");
        }

        if (calibrationIndex >= calibrationTargets.Count)
        {
            CompleteAdvancedCalibration();
        }
        else
        {
            isWaitingForStability = true;
            calibrationPointTimer = 0f;
            currentCalibrationSamples.Clear();
        }
    }

    Vector2 CalculateRobustAverage(List<Vector2> samples)
    {
        if (samples.Count <= 3)
        {
            Vector2 simpleAverage = Vector2.zero;
            foreach (Vector2 sample in samples)
            {
                simpleAverage += sample;
            }
            return simpleAverage / samples.Count;
        }

        Vector2 roughAverage = Vector2.zero;
        foreach (Vector2 sample in samples)
        {
            roughAverage += sample;
        }
        roughAverage /= samples.Count;

        List<Vector2> sortedSamples = new List<Vector2>(samples);
        sortedSamples.Sort((a, b) => Vector2.Distance(a, roughAverage).CompareTo(Vector2.Distance(b, roughAverage)));

        int validCount = Mathf.Max(3, (int)(sortedSamples.Count * 0.7f));

        Vector2 robustAverage = Vector2.zero;
        for (int i = 0; i < validCount; i++)
        {
            robustAverage += sortedSamples[i];
        }
        robustAverage /= validCount;

        return robustAverage;
    }

    void SkipCalibrationPoint()
    {
        Vector2 fallbackGaze = gazeHistory.Count > 0 ? GetStabilizedGazePosition() : currentGazePoint;

        calibrationGazes.Add(fallbackGaze);
        calibrationIndex++;

        Debug.LogWarning($"⚠️ 보정 점 {calibrationIndex}/9 건너뜀 (대체값 사용: {fallbackGaze})");

        if (calibrationIndex >= calibrationTargets.Count)
        {
            CompleteAdvancedCalibration();
        }
        else
        {
            isWaitingForStability = true;
            calibrationPointTimer = 0f;
            currentCalibrationSamples.Clear();
        }
    }
    #endregion

    #region UI 및 시각화 메서드들
    void UpdateDebugUI()
    {
        if (debugText == null) return;

        string status = "=== 웹캠 눈 추적 상태 ===\n";
        status += $"📹 웹캠: {(webCamTexture != null && webCamTexture.isPlaying ? "✅" : "❌")}\n";
        status += $"😊 얼굴 감지: {(isFaceDetected ? "✅" : "❌")}\n";
        status += $"👁️ 눈 감지: {(areEyesDetected ? (useEyeTracking ? "✅ 실제" : "🤖 추정") : "❌")}\n";
        status += $"🎯 시선 추적: {(isGazeValid ? "✅" : "❌")}\n";
        status += $"🔧 보정 완료: {(isCalibrated ? "✅" : "❌")}\n";
        status += $"🔄 좌우 반전: {(flipHorizontal ? "ON" : "OFF")}\n";

        if (isGazeValid)
        {
            status += $"📍 시선 위치: ({smoothedGazePoint.x:F0}, {smoothedGazePoint.y:F0})\n";
        }

        if (isFaceDetected && faces.Length > 0)
        {
            var face = faces[0];
            status += $"📏 얼굴 크기: {face.width}x{face.height}\n";
        }

        status += "\n⌨️ 단축키:\n";
        status += "C: 보정 시작 | R: 보정 리셋\n";
        status += "V: 시각화 토글 | T: 눈 추적 모드\n";
        status += "F: 좌우 반전 토글\n";

        debugText.text = status;
    }

    void DrawCalibrationUI()
    {
        Vector2 target = calibrationTargets[calibrationIndex];

        // 보정 점 표시
        if (calibrationPointTexture != null)
        {
            float imageSize = 64f;
            UnityEngine.Rect imageRect = new UnityEngine.Rect(target.x - imageSize * 0.5f, target.y - imageSize * 0.5f, imageSize, imageSize);
            GUI.DrawTexture(imageRect, calibrationPointTexture);
        }
        else
        {
            GUI.color = Color.red;
            GUI.Box(new UnityEngine.Rect(target.x - 25, target.y - 25, 50, 50), "");
            GUI.color = Color.white;
        }

        // 숫자 표시
        GUIStyle numberStyle = new GUIStyle(GUI.skin.label);
        numberStyle.normal.textColor = Color.white;
        numberStyle.fontSize = 20;
        numberStyle.fontStyle = FontStyle.Bold;
        numberStyle.alignment = TextAnchor.MiddleCenter;

        GUI.Label(new UnityEngine.Rect(target.x - 15, target.y - 10, 30, 20), $"{calibrationIndex + 1}", numberStyle);

        // 안내 메시지
        GUI.Box(new UnityEngine.Rect(Screen.width * 0.5f - 200, Screen.height - 80, 400, 50), "");

        GUIStyle messageStyle = new GUIStyle(GUI.skin.label);
        messageStyle.normal.textColor = Color.yellow;
        messageStyle.fontSize = 18;
        messageStyle.fontStyle = FontStyle.Bold;
        messageStyle.alignment = TextAnchor.MiddleCenter;

        GUI.Label(new UnityEngine.Rect(Screen.width * 0.5f - 200, Screen.height - 75, 400, 40),
            $"보정 점 {calibrationIndex + 1}/9를 바라보고 스페이스 키를 누르세요", messageStyle);
    }

    void DrawGazeCursor()
    {
        Vector2 gazePos = enableGazeSmoothing ? smoothedGazePoint : currentGazePoint;

        GUI.color = Color.cyan;
        GUI.Box(new UnityEngine.Rect(gazePos.x - 10, gazePos.y - 1, 20, 2), "");
        GUI.Box(new UnityEngine.Rect(gazePos.x - 1, gazePos.y - 10, 2, 20), "");
        GUI.color = Color.white;
    }

    void DrawDebugVisualization()
    {
        if (!enableDebugVisualization) return;

        // 얼굴 영역 표시
        if (isFaceDetected && faces.Length > 0)
        {
            OpenCVForUnity.CoreModule.Rect face = faces[0];
            Imgproc.rectangle(rgbaMat,
                new Point(face.x, face.y),
                new Point(face.x + face.width, face.y + face.height),
                new Scalar(0, 255, 0, 255), 3);

            Vector2 faceCenter = new Vector2(face.x + face.width * 0.5f, face.y + face.height * 0.5f);
            Imgproc.circle(rgbaMat, new Point(faceCenter.x, faceCenter.y), 5, new Scalar(0, 255, 255, 255), -1);
        }

        // 눈 위치 표시
        if (areEyesDetected)
        {
            if (useEyeTracking && eyes != null && eyes.Length >= 2)
            {
                Imgproc.circle(rgbaMat, new Point(leftEyeCenter.x, leftEyeCenter.y), 8, new Scalar(255, 0, 0, 255), -1);
                Imgproc.circle(rgbaMat, new Point(rightEyeCenter.x, rightEyeCenter.y), 8, new Scalar(0, 0, 255, 255), -1);
            }
            else
            {
                Imgproc.circle(rgbaMat, new Point(leftEyeCenter.x, leftEyeCenter.y), 6, new Scalar(255, 0, 255, 255), -1);
                Imgproc.circle(rgbaMat, new Point(rightEyeCenter.x, rightEyeCenter.y), 6, new Scalar(255, 0, 255, 255), -1);
            }

            Vector2 gazeCenter = (leftEyeCenter + rightEyeCenter) * 0.5f;

            if (flipHorizontal)
            {
                gazeCenter.x = webCamTexture.width - gazeCenter.x;
            }

            Imgproc.circle(rgbaMat, new Point(gazeCenter.x, gazeCenter.y), 10, new Scalar(0, 255, 0, 255), 3);
        }

        // 화면에 표시
        Utils.matToTexture2D(rgbaMat, outputTexture);
        if (webcamDisplay != null)
        {
            webcamDisplay.texture = outputTexture;
        }
    }
    #endregion

    #region 유틸리티 메서드들
    void UpdateCachedValues()
    {
        if (webCamTexture != null)
        {
            cachedWebcamWidth = webCamTexture.width;
            cachedWebcamHeight = webCamTexture.height;
        }

        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;
        cachedGazeCalibrationOffset = gazeCalibrationOffset;
        cachedGazeScale = gazeScale;
    }

    string GetHaarCascadePath(string fileName)
    {
        // 1. OpenCV for Unity의 기본 방법
        string path = Utils.getFilePath(fileName);
        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
        {
            return path;
        }

        // 2. StreamingAssets 직접 경로
        string streamingPath = System.IO.Path.Combine(Application.streamingAssetsPath, "opencvforunity", fileName);
        if (System.IO.File.Exists(streamingPath))
        {
            return streamingPath;
        }

        // 3. 다른 가능한 경로들
        string[] possiblePaths = {
            System.IO.Path.Combine(Application.streamingAssetsPath, fileName),
            System.IO.Path.Combine(Application.dataPath, "StreamingAssets", "opencvforunity", fileName),
            System.IO.Path.Combine(Application.dataPath, "StreamingAssets", fileName),
        };

        foreach (string possiblePath in possiblePaths)
        {
            if (System.IO.File.Exists(possiblePath))
            {
                return possiblePath;
            }
        }

        return null;
    }

    void RestoreClickThroughStateAfterCalibration()
    {
        if (CompatibilityWindowManager.Instance == null) return;

        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
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

    void EvaluateCalibrationQuality()
    {
        if (calibrationTargets.Count != calibrationGazes.Count) return;

        float totalError = 0f;
        float maxError = 0f;
        int goodPoints = 0;

        for (int i = 0; i < calibrationTargets.Count; i++)
        {
            Vector2 target = calibrationTargets[i];
            Vector2 gaze = calibrationGazes[i];
            float error = Vector2.Distance(target, gaze);

            totalError += error;
            maxError = Mathf.Max(maxError, error);

            if (error < 100f) goodPoints++;
        }

        float avgError = totalError / calibrationTargets.Count;
        float accuracy = (float)goodPoints / calibrationTargets.Count * 100f;

        Debug.Log($"📊 웹캠 보정 품질 평가:");
        Debug.Log($"  평균 오차: {avgError:F1}px");
        Debug.Log($"  최대 오차: {maxError:F1}px");
        Debug.Log($"  정확도: {accuracy:F0}% ({goodPoints}/{calibrationTargets.Count}점)");

        if (avgError < 80f && accuracy > 80f)
        {
            Debug.Log("✅ 웹캠 보정 품질 우수");
        }
        else if (avgError < 150f && accuracy > 60f)
        {
            Debug.Log("⚠️ 웹캠 보정 품질 보통");
        }
        else
        {
            Debug.Log("❌ 웹캠 보정 품질 불량 - 재보정 권장");
        }
    }

    void TestCalibrationQuality()
    {
        if (!isCalibrated)
        {
            Debug.LogWarning("⚠️ 보정을 먼저 완료하세요.");
            return;
        }

        Debug.Log("🧪 보정 품질 테스트 시작");
        StartCoroutine(CalibrationQualityTest());
    }

    System.Collections.IEnumerator CalibrationQualityTest()
    {
        Vector2 centerTarget = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        List<Vector2> testSamples = new List<Vector2>();

        float testDuration = 5f;
        float elapsed = 0f;

        while (elapsed < testDuration)
        {
            if (isGazeValid)
            {
                testSamples.Add(smoothedGazePoint);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (testSamples.Count > 0)
        {
            Vector2 averageGaze = Vector2.zero;
            foreach (Vector2 sample in testSamples)
            {
                averageGaze += sample;
            }
            averageGaze /= testSamples.Count;

            float error = Vector2.Distance(averageGaze, centerTarget);

            float variance = 0f;
            foreach (Vector2 sample in testSamples)
            {
                variance += Vector2.Distance(sample, averageGaze);
            }
            variance /= testSamples.Count;

            Debug.Log($"📊 보정 품질 결과:");
            Debug.Log($"   중앙 오차: {error:F1}px");
            Debug.Log($"   시선 안정성: {variance:F1}px");

            if (error < 100f && variance < 30f)
            {
                Debug.Log("✅ 보정 품질 양호");
            }
            else if (error < 200f && variance < 60f)
            {
                Debug.Log("⚠️ 보정 품질 보통 - 재보정 권장");
            }
            else
            {
                Debug.Log("❌ 보정 품질 불량 - 재보정 필요");
            }
        }
        else
        {
            Debug.LogError("❌ 테스트 중 시선이 감지되지 않았습니다.");
        }
    }
    #endregion

    #region 설정 토글 메서드들
    public void ToggleEyeTrackingMode()
    {
        useEyeTracking = !useEyeTracking;
        Debug.Log($"👁️ 눈 추적 모드: {(useEyeTracking ? "실제 눈 감지" : "얼굴 중심 추정")}");
    }

    public void ToggleHorizontalFlip()
    {
        flipHorizontal = !flipHorizontal;
        Debug.Log($"🔄 좌우 반전: {(flipHorizontal ? "활성화" : "비활성화")}");
    }

    System.Collections.IEnumerator GazeDirectionTest()
    {
        Debug.Log("🧪 시선 방향 테스트 시작");
        Debug.Log("📍 화면 왼쪽을 바라보세요...");

        yield return new WaitForSeconds(3f);

        Vector2 leftGaze = Vector2.zero;
        if (isGazeValid)
        {
            leftGaze = currentGazePoint;
            Debug.Log($"👈 왼쪽 시선: {leftGaze}");
        }

        Debug.Log("📍 화면 오른쪽을 바라보세요...");

        yield return new WaitForSeconds(3f);

        if (isGazeValid)
        {
            Vector2 rightGaze = currentGazePoint;
            Debug.Log($"👉 오른쪽 시선: {rightGaze}");

            if (rightGaze.x > leftGaze.x)
            {
                Debug.Log("✅ 시선 방향이 올바릅니다!");
            }
            else
            {
                Debug.LogWarning("⚠️ 시선 방향이 반전되어 있습니다!");
                Debug.Log("💡 'Toggle Horizontal Flip'을 실행해보세요.");
            }
        }
    }
    #endregion

    #region 진단 및 자동 최적화 메서드들
    public void AutoDiagnoseEyeTracking()
    {
        StartCoroutine(AutoDiagnoseEyeTrackingCoroutine());
    }

    System.Collections.IEnumerator AutoDiagnoseEyeTrackingCoroutine()
    {
        Debug.Log("🔍 자동 눈 추적 진단 시작");

        bool webcamOK = webCamTexture != null && webCamTexture.isPlaying;
        bool faceOK = isFaceDetected;
        bool eyesOK = areEyesDetected;

        Debug.Log($"📊 현재 상태: 웹캠({(webcamOK ? "✅" : "❌")}) 얼굴({(faceOK ? "✅" : "❌")}) 눈({(eyesOK ? "✅" : "❌")})");

        // 5초간 안정성 테스트
        int totalFrames = 0;
        int faceDetectedFrames = 0;
        int eyesDetectedFrames = 0;
        float testDuration = 5f;
        float elapsed = 0f;

        while (elapsed < testDuration)
        {
            if (isFaceDetected) faceDetectedFrames++;
            if (areEyesDetected) eyesDetectedFrames++;
            totalFrames++;

            elapsed += Time.deltaTime;
            yield return new WaitForSeconds(0.1f);
        }

        float faceRate = (float)faceDetectedFrames / totalFrames;
        float eyeRate = (float)eyesDetectedFrames / totalFrames;

        Debug.Log($"📊 결과: 얼굴 감지율 {faceRate * 100:F1}%, 눈 감지율 {eyeRate * 100:F1}%");

        if (faceRate < 0.7f)
        {
            Debug.LogWarning("❌ 얼굴 감지 불량");
            Debug.Log("💡 해결책: 조명 개선, 카메라 정면 응시, 배경 단순화");
        }
        else if (eyeRate < 0.5f)
        {
            Debug.LogWarning("❌ 눈 감지 불량");
            Debug.Log("💡 자동 해결 시도: 얼굴 중심 기반 추정 모드로 전환");

            useEyeTracking = false;
            useFaceCenterFallback = true;

            Debug.Log("🔧 얼굴 중심 기반 시선 추정 모드 활성화");
        }
        else
        {
            Debug.Log("✅ 눈 추적 상태 양호");
        }

        if (faceRate > 0.7f)
        {
            Debug.Log("🧪 좌우 반전 테스트를 시작합니다...");
            yield return StartCoroutine(GazeDirectionTest());
        }

        Debug.Log("🎯 진단 완료! 이제 C키로 보정을 시작하세요.");
    }

    public void StartInteractiveCalibrationGuide()
    {
        StartCoroutine(InteractiveCalibrationGuide());
    }

    System.Collections.IEnumerator InteractiveCalibrationGuide()
    {
        Debug.Log("🎯 대화형 보정 가이드 시작");

        Debug.Log("📋 보정 준비 체크리스트:");
        Debug.Log("   □ 조명이 충분히 밝은가요?");
        Debug.Log("   □ 웹캠과 30-50cm 거리인가요?");
        Debug.Log("   □ 배경이 단순한가요?");
        Debug.Log("   □ 안경 착용 시 반사광은 없나요?");

        yield return new WaitForSeconds(5f);

        Debug.Log("🔍 얼굴 감지 상태 확인 중...");

        float checkTime = 3f;
        float elapsed = 0f;
        int faceDetectedCount = 0;
        int totalChecks = 0;

        while (elapsed < checkTime)
        {
            if (isFaceDetected) faceDetectedCount++;
            totalChecks++;

            elapsed += Time.deltaTime;
            yield return new WaitForSeconds(0.1f);
        }

        float faceDetectionRate = (float)faceDetectedCount / totalChecks;

        if (faceDetectionRate > 0.8f)
        {
            Debug.Log("✅ 얼굴 감지 양호 - 보정 시작 가능");
        }
        else
        {
            Debug.LogWarning("⚠️ 얼굴 감지 불안정 - 환경 개선 후 다시 시도");
            yield break;
        }

        Debug.Log("🎯 보정 시작 안내:");
        Debug.Log("   1. 각 점을 차례로 정확히 바라보세요");
        Debug.Log("   2. 머리는 움직이지 말고 눈만 움직이세요");
        Debug.Log("   3. 각 점에서 스페이스 키를 눌러주세요");
        Debug.Log("   4. 점이 안정화될 때까지 기다리세요");

        yield return new WaitForSeconds(3f);

        Debug.Log("🚀 이제 C키를 눌러 보정을 시작하세요!");
    }
    #endregion

    #region 컨텍스트 메뉴 메서드들 (디버그용)
    [ContextMenu("Quick Perfect Calibration")]
    public void QuickPerfectCalibration()
    {
        gazeCalibrationOffset = Vector2.zero;
        gazeScale = Vector2.one;
        isCalibrated = true;
        UpdateCachedValues();
        Debug.Log("⚡ 완벽한 보정 설정 (테스트용)");
    }

    [ContextMenu("Reset All Settings")]
    public void ResetAllSettings()
    {
        isCalibrated = false;
        isCalibrating = false;
        isCollectingSamples = false;
        gazeCalibrationOffset = Vector2.zero;
        gazeScale = Vector2.one;
        gazeSmoothing = 8f;
        processEveryNthFrame = 3;
        calibrationGazes.Clear();
        calibrationSamplesPerTarget.Clear();
        calibrationIndex = 0;
        UpdateCachedValues();
        Debug.Log("🔄 모든 설정 초기화 완료");
    }

    [ContextMenu("Toggle Threading")]
    public void ToggleThreading()
    {
        useThreading = !useThreading;
        Debug.Log($"멀티스레딩: {(useThreading ? "ON" : "OFF")}");
    }

    [ContextMenu("Toggle Visualization")]
    public void ToggleVisualization()
    {
        enableDebugVisualization = !enableDebugVisualization;
        Debug.Log($"디버그 시각화: {(enableDebugVisualization ? "ON" : "OFF")}");
    }

    [ContextMenu("Test All Systems")]
    public void TestAllSystems()
    {
        Debug.Log("=== 모든 시스템 테스트 ===");
        Debug.Log($"웹캠 상태: {(webCamTexture != null && webCamTexture.isPlaying ? "✅" : "❌")}");
        Debug.Log($"얼굴 감지: {(faceCascade != null && !faceCascade.empty() ? "✅" : "❌")}");
        Debug.Log($"눈 감지: {(eyeCascade != null && !eyeCascade.empty() ? "✅" : "❌")}");
        Debug.Log($"시선 추적: {(isGazeValid ? "✅" : "❌")}");
        Debug.Log($"보정 상태: {(isCalibrated ? "✅" : "❌")}");

        if (webCamTexture != null)
        {
            Debug.Log($"웹캠 해상도: {webCamTexture.width}x{webCamTexture.height}");
        }

        if (isGazeValid)
        {
            Debug.Log($"현재 시선: ({smoothedGazePoint.x:F0}, {smoothedGazePoint.y:F0})");
        }
    }

    [ContextMenu("Debug Haar Cascade Files")]
    public void DebugHaarCascadeFiles()
    {
        Debug.Log("=== Haar Cascade 파일 디버깅 ===");

        string[] fileNames = {
            "haarcascade_frontalface_alt.xml",
            "haarcascade_frontalface_default.xml",
            "haarcascade_eye.xml"
        };

        foreach (string fileName in fileNames)
        {
            Debug.Log($"\n--- {fileName} ---");
            string path = GetHaarCascadePath(fileName);
            if (!string.IsNullOrEmpty(path))
            {
                Debug.Log($"✅ 파일 발견: {path}");
                Debug.Log($"파일 크기: {new System.IO.FileInfo(path).Length} bytes");
            }
            else
            {
                Debug.LogError($"❌ 파일 없음: {fileName}");
            }
        }
    }
    #endregion

    #region EyesTrackingManager 호환 메서드들 (외부 접근용)
    public Vector3 GetGazeWorldPosition(Camera camera)
    {
        if (!isGazeValid) return Vector3.zero;

        Vector2 gazePos = enableGazeSmoothing ? smoothedGazePoint : currentGazePoint;
        Vector3 screenPos = new Vector3(gazePos.x, gazePos.y, 10f);
        return camera.ScreenToWorldPoint(screenPos);
    }

    public Vector2 GetGazeScreenPosition()
    {
        if (!isGazeValid) return Vector2.zero;
        return enableGazeSmoothing ? smoothedGazePoint : currentGazePoint;
    }

    public bool IsEyeDetected()
    {
        return isGazeValid;
    }

    public Vector2 GetCurrentGazePoint()
    {
        return GetGazeScreenPosition();
    }
    #endregion

    #region 프로퍼티들 (외부 접근용)
    public bool IsGazeValid => isGazeValid;
    public Vector2 GazePosition => GetGazeScreenPosition();
    public bool IsFaceDetected => isFaceDetected;
    public bool AreEyesDetected => areEyesDetected;
    public bool IsCalibrated => isCalibrated;
    public bool IsCalibrating => isCalibrating;
    public float CalibrationProgress => isCalibrating ? (float)calibrationIndex / calibrationTargets.Count : 0f;
    public string CurrentTrackingMode => useEyeTracking ? "실제 눈 추적" : "얼굴 중심 추정";
    public string SystemStatus
    {
        get
        {
            if (!isGazeValid) return "시선 감지 실패";
            if (!isCalibrated) return "보정 필요";
            return "정상 작동";
        }
    }
    #endregion
    // RealWebcamEyeTracker.cs에 추가할 진단 및 해결 메서드

    #region 웹캠 눈 추적 문제 진단

    [ContextMenu("Diagnose Eye Tracking Issues")]
    public void DiagnoseEyeTrackingIssues()
    {
        Debug.Log("=== 🔍 웹캠 눈 추적 문제 진단 ===");

        // 1. 기본 하드웨어 체크
        CheckWebcamHardware();

        // 2. OpenCV 모델 상태 체크
        CheckOpenCVModels();

        // 3. 실제 얼굴/눈 감지 상태 체크
        CheckDetectionQuality();

        // 4. 좌표 변환 문제 체크
        CheckCoordinateMapping();

        // 5. 해결책 제시
        SuggestSolutions();
    }

    void CheckWebcamHardware()
    {
        Debug.Log("📹 웹캠 하드웨어 상태:");

        if (webCamTexture == null)
        {
            Debug.LogError("❌ WebCamTexture가 null입니다!");
            return;
        }

        Debug.Log($"  웹캠 상태: {(webCamTexture.isPlaying ? "✅ 작동중" : "❌ 정지")}");
        Debug.Log($"  해상도: {webCamTexture.width}x{webCamTexture.height}");
        Debug.Log($"  설정 FPS: {webcamFPS}");
        Debug.Log($"  실제 FPS: {webCamTexture.requestedFPS}");

        // 웹캠 품질 체크
        if (webCamTexture.width < 640 || webCamTexture.height < 480)
        {
            Debug.LogWarning("⚠️ 웹캠 해상도가 너무 낮습니다! (최소 640x480 권장)");
        }
    }

    void CheckOpenCVModels()
    {
        Debug.Log("🤖 OpenCV 모델 상태:");

        if (faceCascade == null || faceCascade.empty())
        {
            Debug.LogError("❌ 얼굴 감지 모델이 로드되지 않았습니다!");
            Debug.LogError("💡 해결책: OpenCV for Unity > Tools > Move StreamingAssets Files 실행");
            return;
        }

        Debug.Log("✅ 얼굴 감지 모델 정상");

        if (eyeCascade == null || eyeCascade.empty())
        {
            Debug.LogWarning("⚠️ 눈 감지 모델이 로드되지 않았습니다!");
            Debug.LogWarning("💡 얼굴 중심 기반 추정으로 전환 권장");

            // 자동으로 얼굴 중심 모드로 전환
            useEyeTracking = false;
            useFaceCenterFallback = true;
            Debug.Log("🔧 자동으로 얼굴 중심 모드로 전환했습니다.");
        }
        else
        {
            Debug.Log("✅ 눈 감지 모델 정상");
        }
    }

    void CheckDetectionQuality()
    {
        Debug.Log("👁️ 실시간 감지 품질:");

        Debug.Log($"  얼굴 감지: {(isFaceDetected ? "✅" : "❌")}");
        Debug.Log($"  눈 감지: {(areEyesDetected ? "✅" : "❌")}");
        Debug.Log($"  시선 유효성: {(isGazeValid ? "✅" : "❌")}");

        if (isFaceDetected && faces.Length > 0)
        {
            var face = faces[0];
            Debug.Log($"  얼굴 크기: {face.width}x{face.height}px");
            Debug.Log($"  얼굴 위치: ({face.x}, {face.y})");

            // 얼굴 크기가 너무 작은지 체크
            if (face.width < 80 || face.height < 80)
            {
                Debug.LogWarning("⚠️ 얼굴이 너무 작게 감지됩니다!");
                Debug.LogWarning("💡 웹캠에 더 가까이 앉으세요.");
            }

            // 얼굴이 화면 중앙에 있는지 체크
            float centerX = webCamTexture.width * 0.5f;
            float centerY = webCamTexture.height * 0.5f;
            float faceX = face.x + face.width * 0.5f;
            float faceY = face.y + face.height * 0.5f;

            float distanceFromCenter = Vector2.Distance(new Vector2(faceX, faceY), new Vector2(centerX, centerY));

            if (distanceFromCenter > Mathf.Min(webCamTexture.width, webCamTexture.height) * 0.3f)
            {
                Debug.LogWarning("⚠️ 얼굴이 화면 중앙에서 벗어나 있습니다!");
                Debug.LogWarning("💡 웹캠 정면으로 앉으세요.");
            }
        }

        if (areEyesDetected)
        {
            Debug.Log($"  왼쪽 눈: ({leftEyeCenter.x:F0}, {leftEyeCenter.y:F0})");
            Debug.Log($"  오른쪽 눈: ({rightEyeCenter.x:F0}, {rightEyeCenter.y:F0})");

            float eyeDistance = Vector2.Distance(leftEyeCenter, rightEyeCenter);
            Debug.Log($"  눈 간격: {eyeDistance:F1}px");

            if (eyeDistance < 20)
            {
                Debug.LogWarning("⚠️ 감지된 눈 간격이 너무 좁습니다!");
                Debug.LogWarning("💡 눈 감지 품질이 낮을 수 있습니다.");
            }
        }
    }

    void CheckCoordinateMapping()
    {
        Debug.Log("🗺️ 좌표 변환 체크:");

        if (!isGazeValid)
        {
            Debug.LogError("❌ 시선이 감지되지 않아 좌표 변환을 체크할 수 없습니다!");
            return;
        }

        Vector2 currentGaze = smoothedGazePoint;
        Debug.Log($"  현재 시선 (화면좌표): ({currentGaze.x:F0}, {currentGaze.y:F0})");
        Debug.Log($"  화면 크기: {Screen.width}x{Screen.height}");

        // 시선이 화면 범위를 벗어나는지 체크
        bool outOfBounds = currentGaze.x < 0 || currentGaze.x > Screen.width ||
                          currentGaze.y < 0 || currentGaze.y > Screen.height;

        if (outOfBounds)
        {
            Debug.LogWarning("⚠️ 시선이 화면 범위를 벗어납니다!");
            Debug.LogWarning("💡 좌표 변환에 문제가 있을 수 있습니다.");
        }

        // 시선이 특정 영역에만 몰리는지 체크
        float normalizedX = currentGaze.x / Screen.width;
        float normalizedY = currentGaze.y / Screen.height;

        Debug.Log($"  정규화된 시선: ({normalizedX:F2}, {normalizedY:F2})");

        if (normalizedX > 0.7f || normalizedX < 0.3f)
        {
            Debug.LogWarning("⚠️ 시선이 화면 한쪽으로 치우쳐 있습니다!");
            Debug.LogWarning("💡 좌우 반전 설정을 확인하세요.");
        }

        // 보정값 체크
        Debug.Log($"  보정 오프셋: ({gazeCalibrationOffset.x:F3}, {gazeCalibrationOffset.y:F3})");
        Debug.Log($"  보정 스케일: ({gazeScale.x:F2}, {gazeScale.y:F2})");

        if (Mathf.Abs(gazeCalibrationOffset.x) > 0.5f || Mathf.Abs(gazeCalibrationOffset.y) > 0.5f)
        {
            Debug.LogWarning("⚠️ 보정 오프셋이 비정상적으로 큽니다!");
        }

        if (gazeScale.x < 0.5f || gazeScale.x > 2.0f || gazeScale.y < 0.5f || gazeScale.y > 2.0f)
        {
            Debug.LogWarning("⚠️ 보정 스케일이 비정상적입니다!");
        }
    }

    void SuggestSolutions()
    {
        Debug.Log("💡 해결책 제안:");

        if (!isFaceDetected)
        {
            Debug.Log("🔧 얼굴 감지 개선:");
            Debug.Log("  1. 조명을 더 밝게 하세요");
            Debug.Log("  2. 배경을 단순하게 하세요");
            Debug.Log("  3. 웹캠을 정면으로 향하게 하세요");
            Debug.Log("  4. 웹캠과 30-50cm 거리를 유지하세요");
        }
        else if (!areEyesDetected)
        {
            Debug.Log("🔧 눈 감지 개선:");
            Debug.Log("  1. 안경 반사광을 제거하세요");
            Debug.Log("  2. 눈을 크게 뜨세요");
            Debug.Log("  3. 얼굴 중심 모드로 전환하세요 (T키)");
            Debug.Log("  4. 웹캠 해상도를 높이세요");
        }
        else if (isGazeValid)
        {
            Debug.Log("🔧 시선 추적 정확도 개선:");
            Debug.Log("  1. 좌우 반전을 토글해보세요 (F키)");
            Debug.Log("  2. 보정을 다시 시도하세요 (R키 후 C키)");
            Debug.Log("  3. 더 정확히 보정점을 바라보세요");
            Debug.Log("  4. 머리를 최대한 고정하세요");
        }

        // 자동 해결 시도
        Debug.Log("🤖 자동 해결 시도:");

        if (!areEyesDetected && isFaceDetected)
        {
            useEyeTracking = false;
            useFaceCenterFallback = true;
            Debug.Log("✅ 얼굴 중심 모드로 자동 전환");
        }

        if (isGazeValid && !isCalibrated)
        {
            Debug.Log("💡 보정을 시작하려면 C키를 누르세요");
        }
    }

    // 실시간 감지 품질 모니터링
    [ContextMenu("Start Detection Quality Monitor")]
    public void StartDetectionQualityMonitor()
    {
        StartCoroutine(DetectionQualityMonitor());
    }

    System.Collections.IEnumerator DetectionQualityMonitor()
    {
        Debug.Log("🔍 실시간 감지 품질 모니터링 시작 (10초)");

        int totalFrames = 0;
        int faceDetectedFrames = 0;
        int eyesDetectedFrames = 0;
        int gazeValidFrames = 0;

        float monitorDuration = 10f;
        float elapsed = 0f;

        while (elapsed < monitorDuration)
        {
            if (isFaceDetected) faceDetectedFrames++;
            if (areEyesDetected) eyesDetectedFrames++;
            if (isGazeValid) gazeValidFrames++;
            totalFrames++;

            elapsed += Time.deltaTime;
            yield return new WaitForSeconds(0.1f);
        }

        float faceRate = (float)faceDetectedFrames / totalFrames * 100f;
        float eyeRate = (float)eyesDetectedFrames / totalFrames * 100f;
        float gazeRate = (float)gazeValidFrames / totalFrames * 100f;

        Debug.Log("📊 10초간 감지 품질 결과:");
        Debug.Log($"  얼굴 감지율: {faceRate:F1}%");
        Debug.Log($"  눈 감지율: {eyeRate:F1}%");
        Debug.Log($"  시선 유효율: {gazeRate:F1}%");

        // 품질 평가
        if (faceRate < 70f)
        {
            Debug.LogError("❌ 얼굴 감지 품질 불량!");
            Debug.LogError("💡 조명, 각도, 거리를 조정하세요.");
        }
        else if (eyeRate < 50f)
        {
            Debug.LogWarning("⚠️ 눈 감지 품질 불량!");
            Debug.LogWarning("💡 얼굴 중심 모드 사용을 권장합니다.");

            useEyeTracking = false;
            useFaceCenterFallback = true;
            Debug.Log("🔧 자동으로 얼굴 중심 모드로 전환했습니다.");
        }
        else if (gazeRate < 80f)
        {
            Debug.LogWarning("⚠️ 시선 추적 안정성 부족!");
            Debug.LogWarning("💡 환경을 개선하고 재보정하세요.");
        }
        else
        {
            Debug.Log("✅ 감지 품질 양호!");
        }
    }

    // 간단한 보정 테스트
    [ContextMenu("Quick Calibration Test")]
    public void QuickCalibrationTest()
    {
        if (!isGazeValid)
        {
            Debug.LogError("❌ 시선이 감지되지 않습니다. 먼저 감지 품질을 확인하세요.");
            return;
        }

        Debug.Log("🎯 화면 중앙을 5초간 바라보세요...");
        StartCoroutine(QuickCalibrationTestCoroutine());
    }

    System.Collections.IEnumerator QuickCalibrationTestCoroutine()
    {
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        List<Vector2> gazeSamples = new List<Vector2>();

        float testDuration = 5f;
        float elapsed = 0f;

        while (elapsed < testDuration)
        {
            if (isGazeValid)
            {
                gazeSamples.Add(smoothedGazePoint);
            }

            elapsed += Time.deltaTime;
            yield return new WaitForSeconds(0.1f);
        }



        // 평균 시선 위치 계산
        Vector2 averageGaze = Vector2.zero;
        foreach (Vector2 sample in gazeSamples)
        {
            averageGaze += sample;
        }
        averageGaze /= gazeSamples.Count;

        float errorDistance = Vector2.Distance(averageGaze, screenCenter);

        Debug.Log($"📊 빠른 보정 테스트 결과:");
        Debug.Log($"  화면 중앙: ({screenCenter.x:F0}, {screenCenter.y:F0})");
        Debug.Log($"  평균 시선: ({averageGaze.x:F0}, {averageGaze.y:F0})");
        Debug.Log($"  오차 거리: {errorDistance:F1}px");

        if (errorDistance < 100f)
        {
            Debug.Log("✅ 시선 추적 정확도 양호!");
        }
        else if (errorDistance < 300f)
        {
            Debug.LogWarning("⚠️ 시선 추적 정확도 보통 - 보정 권장");
        }
        else
        {
            Debug.LogError("❌ 시선 추적 정확도 불량 - 환경 개선 및 재보정 필요");

            // 자동 해결책 제시
            Vector2 offset = screenCenter - averageGaze;
            Debug.Log($"💡 권장 보정 오프셋: ({offset.x / Screen.width:F3}, {offset.y / Screen.height:F3})");
        }
    }

    #endregion
#else
    // OpenCV가 없는 경우 더미 구현
    void Start()
    {
        Debug.LogError("❌ OpenCV for Unity가 필요합니다!");
        enabled = false;
    }

    public bool IsGazeValid => false;
    public Vector2 GazePosition => Vector2.zero;
    public bool IsFaceDetected => false;
    public bool AreEyesDetected => false;
    public bool IsCalibrated => false;
    public Vector3 GetGazeWorldPosition(Camera camera) => Vector3.zero;
    public Vector2 GetGazeScreenPosition() => Vector2.zero;
    public bool IsEyeDetected() => false;
    public Vector2 GetCurrentGazePoint() => Vector2.zero;
#endif
}