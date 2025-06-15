using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System.Threading;

#if OPENCV_FOR_UNITY
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.UtilsModule;
#endif

/// <summary>
/// 실제 웹캠을 사용한 눈 추적 시스템
/// OpenCV for Unity 필수
/// </summary>
public class RealWebcamEyeTracker : MonoBehaviour
{
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

    [Header("시선 추정 설정")]
    public float gazeSmoothing = 8f;
    public Vector2 gazeCalibrationOffset = Vector2.zero;
    public Vector2 gazeScale = new Vector2(1.2f, 1.2f);
    public bool enableGazeSmoothing = true;

    [Header("성능 설정")]
    public int processEveryNthFrame = 3;  // 3프레임마다 한 번 처리
    public bool useThreading = true;       // 멀티스레딩 사용
    public bool enableDebugVisualization = true;

    [Header("UI 요소")]
    public RawImage webcamDisplay;
    public Text debugText;

    [Header("보정 설정")]
    public Texture2D calibrationPointTexture;
    public bool showCalibrationUI = true;

    // 기존 변수들 다음에 추가
    [Header("보정 개선 설정")]
    public bool showDetailedCalibrationInfo = true;
    public float calibrationWaitTime = 2f;
    public int calibrationSamplesPerPoint = 5;
    public bool useAdvancedCalibration = true;

    // 보정 관련 추가 변수들
    private List<List<Vector2>> calibrationSamplesPerTarget = new List<List<Vector2>>();
    private float calibrationPointTimer = 0f;
    private int currentSampleCount = 0;
    private bool isCollectingSamples = false;

    // 캐시된 값들
    private int cachedWebcamWidth;
    private int cachedWebcamHeight;
    private int cachedScreenWidth;
    private int cachedScreenHeight;
    private Vector2 cachedGazeCalibrationOffset;
    private Vector2 cachedGazeScale;

#if OPENCV_FOR_UNITY
    // OpenCV 관련
    private WebCamTexture webCamTexture;
    private Mat rgbaMat;
    private Mat grayMat;
    private Mat faceMat;
    private Texture2D outputTexture;

    // 스레드 간 데이터 교환용 변수들
    private Mat threadRgbaMat;
    private Mat threadGrayMat;
    private bool hasNewFrame = false;
    private readonly object frameDataLock = new object();

    // 얼굴/눈 감지
    private CascadeClassifier faceCascade;
    private CascadeClassifier eyeCascade;
    private OpenCVForUnity.CoreModule.Rect[] faces;
    private OpenCVForUnity.CoreModule.Rect[] eyes;

    // 시선 추적 데이터
    private Vector2 leftEyeCenter;
    private Vector2 rightEyeCenter;
    private Vector2 currentGazePoint;
    private Vector2 smoothedGazePoint;
    private bool isGazeValid = false;
    private bool isFaceDetected = false;
    private bool areEyesDetected = false;

    // 보정 데이터
    private List<Vector2> calibrationTargets = new List<Vector2>();
    private List<Vector2> calibrationGazes = new List<Vector2>();
    private bool isCalibrating = false;
    private int calibrationIndex = 0;
    private bool isCalibrated = false;

    // 성능 최적화
    private int frameCounter = 0;
    private bool isProcessing = false;
    private Thread processingThread;
    private object lockObject = new object();

    // 임시 데이터 (스레드 간 공유)
    private Vector2 tempGazePoint;
    private bool tempGazeValid;
    private bool tempFaceDetected;
    private bool tempEyesDetected;
#endif

    // 싱글톤
    public static RealWebcamEyeTracker Instance { get; private set; }

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
#if OPENCV_FOR_UNITY
        Debug.Log("🎯 실제 웹캠 눈 추적 시스템 초기화 시작");
        StartCoroutine(InitializeSystem());
#else
        Debug.LogError("❌ OpenCV for Unity가 필요합니다! Asset Store에서 다운로드하세요.");
        Debug.LogError("🔗 https://assetstore.unity.com/packages/tools/integration/opencv-for-unity-21088");
        enabled = false;
#endif
    }

#if OPENCV_FOR_UNITY
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

    IEnumerator LoadHaarCascades()
    {
        // 얼굴 감지 모델 로드 - 여러 경로 시도
        string faceCascadePath = GetHaarCascadePath("haarcascade_frontalface_alt.xml");
        if (string.IsNullOrEmpty(faceCascadePath))
        {
            faceCascadePath = GetHaarCascadePath("haarcascade_frontalface_default.xml");
        }

        if (string.IsNullOrEmpty(faceCascadePath))
        {
            Debug.LogError("❌ 얼굴 감지 모델을 찾을 수 없습니다!");
            Debug.LogError("💡 해결 방법:");
            Debug.LogError("1. OpenCV for Unity가 올바르게 설치되었는지 확인");
            Debug.LogError("2. Tools → OpenCV for Unity → Move StreamingAssets Files 실행");
            Debug.LogError("3. Assets/StreamingAssets/opencvforunity/ 폴더에 .xml 파일들이 있는지 확인");

            // 폴백: 얼굴 중심 기반 추적으로 전환
            Debug.LogWarning("🔄 폴백 모드: 얼굴 감지 없이 화면 중앙 기반으로 동작합니다.");
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
        if (string.IsNullOrEmpty(eyeCascadePath))
        {
            Debug.LogWarning("⚠️ 눈 감지 모델을 찾을 수 없습니다. 얼굴 중심을 사용합니다.");
        }
        else
        {
            eyeCascade = new CascadeClassifier(eyeCascadePath);
            if (eyeCascade.empty())
            {
                Debug.LogWarning("⚠️ 눈 감지 모델 로드 실패. 얼굴 중심을 사용합니다.");
                eyeCascade = null;
            }
            else
            {
                Debug.Log("✅ 눈 감지 모델 로드 성공");
            }
        }

        Debug.Log("✅ 얼굴 감지 모델 로드 성공");
        yield return null;
    }

    string GetHaarCascadePath(string fileName)
    {
        // 1. OpenCV for Unity의 기본 방법
        string path = Utils.getFilePath(fileName);
        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
        {
            Debug.Log($"✅ Haar Cascade 파일 찾음 (기본 방법): {path}");
            return path;
        }

        // 2. StreamingAssets 직접 경로
        string streamingPath = System.IO.Path.Combine(Application.streamingAssetsPath, "opencvforunity", fileName);
        if (System.IO.File.Exists(streamingPath))
        {
            Debug.Log($"✅ Haar Cascade 파일 찾음 (StreamingAssets): {streamingPath}");
            return streamingPath;
        }

        // 3. 다른 가능한 경로들
        string[] possiblePaths = {
            System.IO.Path.Combine(Application.streamingAssetsPath, fileName),
            System.IO.Path.Combine(Application.dataPath, "StreamingAssets", "opencvforunity", fileName),
            System.IO.Path.Combine(Application.dataPath, "StreamingAssets", fileName),
#if UNITY_EDITOR
            System.IO.Path.Combine(Application.dataPath, "OpenCV+Unity", "Assets", "StreamingAssets", "opencvforunity", fileName),
#endif
        };

        foreach (string possiblePath in possiblePaths)
        {
            if (System.IO.File.Exists(possiblePath))
            {
                Debug.Log($"✅ Haar Cascade 파일 찾음 (대체 경로): {possiblePath}");
                return possiblePath;
            }
        }

        Debug.LogWarning($"⚠️ Haar Cascade 파일을 찾을 수 없습니다: {fileName}");
        Debug.LogWarning($"다음 경로들을 확인했습니다:");
        Debug.LogWarning($"- {Utils.getFilePath(fileName)}");
        Debug.LogWarning($"- {streamingPath}");

        return null;
    }

    // 디버그용 - 사용 가능한 모든 파일 확인
    [ContextMenu("Debug Haar Cascade Files")]
    void DebugHaarCascadeFiles()
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

        // StreamingAssets 폴더 구조 확인
        string streamingAssetsPath = Application.streamingAssetsPath;
        Debug.Log($"\nStreamingAssets 경로: {streamingAssetsPath}");

        if (System.IO.Directory.Exists(streamingAssetsPath))
        {
            Debug.Log("StreamingAssets 폴더 내용:");
            string[] files = System.IO.Directory.GetFiles(streamingAssetsPath, "*.xml", System.IO.SearchOption.AllDirectories);
            foreach (string file in files)
            {
                Debug.Log($"  - {file}");
            }
        }
        else
        {
            Debug.LogError("StreamingAssets 폴더가 존재하지 않습니다!");
        }
    }

    void SetupCalibration()
    {
        calibrationTargets.Clear();
        float margin = 150f; // 기존 100f에서 150f로 변경
        float w = Screen.width;
        float h = Screen.height;

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
        // 캐시된 값들 업데이트 (메인 스레드에서)
        if (frameCounter % (processEveryNthFrame * 10) == 0) // 가끔씩만 업데이트
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

        if (isCalibrating)
        {
            if (Input.GetKeyDown(KeyCode.Space))
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
        }

        if (Input.GetKeyDown(KeyCode.V))
        {
            enableDebugVisualization = !enableDebugVisualization;
        }

        if (Input.GetKeyDown(KeyCode.Q) && isCalibrated)
        {
            TestCalibrationQuality();
        }
    }

    void ProcessFrameThreaded()
    {
        if (isProcessing) return;

        // 멀티스레딩에서 문제가 발생하면 메인 스레드로 폴백
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
            ProcessFrame(); // 메인 스레드에서 처리
        }
    }

    void ProcessFrameInThread()
    {
        if (isProcessing) return;
        isProcessing = true;

        try
        {
            bool frameAvailable = false;

            // 새 프레임 데이터 확인
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
                // RGB로 변환
                Imgproc.cvtColor(threadRgbaMat, threadGrayMat, Imgproc.COLOR_RGBA2GRAY);

                // 얼굴 감지 (스레드 안전)
                DetectFaceInThread();

                if (tempFaceDetected)
                {
                    // 눈 감지 (스레드 안전)
                    DetectEyesInThread();

                    if (tempEyesDetected)
                    {
                        // 시선 추정 (스레드 안전)
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
                // 메인 스레드에서 사용할 데이터 저장
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
                // 얼굴 영역에서 눈 감지
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

                        // 두 눈의 중심점 계산
                        var eye1 = eyes[0];
                        var eye2 = eyes[1];

                        // 얼굴 좌표계에서 전체 이미지 좌표계로 변환
                        leftEyeCenter = new Vector2(
                            face.x + eye1.x + eye1.width * 0.5f,
                            face.y + eye1.y + eye1.height * 0.5f
                        );

                        rightEyeCenter = new Vector2(
                            face.x + eye2.x + eye2.width * 0.5f,
                            face.y + eye2.y + eye2.height * 0.5f
                        );

                        // 왼쪽/오른쪽 눈 정렬
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
                // 눈 감지 모델이 없으면 얼굴 중심 사용
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

    void DetectEyes()
    {
        if (!isFaceDetected || faces.Length == 0) return;

        OpenCVForUnity.CoreModule.Rect face = faces[0];

        if (eyeCascade != null)
        {
            // 얼굴 영역에서 눈 감지
            Mat faceROI = new Mat(grayMat, face);
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

            eyes = eyeDetections.toArray();
            areEyesDetected = eyes.Length >= 2;

            if (areEyesDetected)
            {
                // 두 눈의 중심점 계산
                var eye1 = eyes[0];
                var eye2 = eyes[1];

                // 얼굴 좌표계에서 전체 이미지 좌표계로 변환
                leftEyeCenter = new Vector2(
                    face.x + eye1.x + eye1.width * 0.5f,
                    face.y + eye1.y + eye1.height * 0.5f
                );

                rightEyeCenter = new Vector2(
                    face.x + eye2.x + eye2.width * 0.5f,
                    face.y + eye2.y + eye2.height * 0.5f
                );

                // 왼쪽/오른쪽 눈 정렬
                if (leftEyeCenter.x > rightEyeCenter.x)
                {
                    Vector2 temp = leftEyeCenter;
                    leftEyeCenter = rightEyeCenter;
                    rightEyeCenter = temp;
                }
            }

            faceROI.Dispose();
            eyeDetections.Dispose();
        }
        else
        {
            // 눈 감지 모델이 없으면 얼굴 중심 사용
            Vector2 faceCenter = new Vector2(
                face.x + face.width * 0.5f,
                face.y + face.height * 0.3f  // 눈 위치는 얼굴 상단 30% 지점
            );

            leftEyeCenter = faceCenter + new Vector2(-face.width * 0.2f, 0);
            rightEyeCenter = faceCenter + new Vector2(face.width * 0.2f, 0);
            areEyesDetected = true;
        }
    }

    void EstimateGaze()
    {
        if (!areEyesDetected) return;

        // 두 눈의 중심점 계산
        Vector2 eyesCenter = (leftEyeCenter + rightEyeCenter) * 0.5f;

        // 이미지 좌표를 화면 좌표로 변환
        float normalizedX = eyesCenter.x / webCamTexture.width;
        float normalizedY = 1f - (eyesCenter.y / webCamTexture.height); // Y축 뒤집기

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

    void DrawDebugVisualization()
    {
        if (!enableDebugVisualization) return;

        // 얼굴 영역 표시
        if (isFaceDetected && faces.Length > 0)
        {
            OpenCVForUnity.CoreModule.Rect face = faces[0];
            Imgproc.rectangle(rgbaMat, new Point(face.x, face.y),
                new Point(face.x + face.width, face.y + face.height),
                new Scalar(0, 255, 0, 255), 2);
        }

        // 눈 위치 표시
        if (areEyesDetected)
        {
            Imgproc.circle(rgbaMat, new Point(leftEyeCenter.x, leftEyeCenter.y), 5, new Scalar(255, 0, 0, 255), -1);
            Imgproc.circle(rgbaMat, new Point(rightEyeCenter.x, rightEyeCenter.y), 5, new Scalar(0, 0, 255, 255), -1);
        }

        // 화면에 표시
        Utils.matToTexture2D(rgbaMat, outputTexture);
        if (webcamDisplay != null)
        {
            webcamDisplay.texture = outputTexture;
        }
    }

    void UpdateDebugUI()
    {
        if (debugText == null) return;

        string status = "=== 실제 웹캠 눈 추적 ===\n";
        status += $"웹캠: {(webCamTexture != null && webCamTexture.isPlaying ? "✅" : "❌")}\n";
        status += $"얼굴 감지: {(isFaceDetected ? "✅" : "❌")}\n";
        status += $"눈 감지: {(areEyesDetected ? "✅" : "❌")}\n";
        status += $"시선 추적: {(isGazeValid ? "✅" : "❌")}\n";
        status += $"보정 완료: {(isCalibrated ? "✅" : "❌")}\n";

        if (isGazeValid)
        {
            status += $"시선 위치: ({smoothedGazePoint.x:F0}, {smoothedGazePoint.y:F0})\n";
        }

        status += "\n⌨️ 단축키:\n";
        status += "C: 보정 시작\n";
        status += "R: 보정 리셋\n";
        status += "Space: 보정 점 기록\n";
        status += "V: 시각화 토글";

        debugText.text = status;
    }

    void StartCalibration()
    {
        isCalibrating = true;
        calibrationIndex = 0;
        calibrationGazes.Clear();

        // 보정 시작 시 캐시 업데이트
        UpdateCachedValues();

        // 보정 중 click-through 비활성화 (중요!)
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

        // 보정 완료 후 click-through 상태 복원
        RestoreClickThroughStateAfterCalibration();
    }


    void RestoreClickThroughStateAfterCalibration()
    {
        if (CompatibilityWindowManager.Instance == null) return;

        // 현재 마우스 위치 확인하여 적절한 click-through 상태 설정
        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

            // 상호작용 가능한 오브젝트 확인
            Collider2D catCollider = Physics2D.OverlapPoint(mouseWorldPos, 1 << 8);
            Collider2D towerCollider = Physics2D.OverlapPoint(mouseWorldPos, 1 << 9);

            bool isOverInteractableObject = (catCollider != null || towerCollider != null);

            if (isOverInteractableObject)
            {
                CompatibilityWindowManager.Instance.DisableClickThrough();
                Debug.Log("🔓 보정 완료 - 상호작용 오브젝트 위에 있어서 click-through 비활성화 유지");
            }
            else
            {
                CompatibilityWindowManager.Instance.EnableClickThrough();
                Debug.Log("🔓 보정 완료 - 빈 공간에 있어서 click-through 활성화");
            }
        }
    }

    void ResetCalibration()
    {
        isCalibrated = false;
        isCalibrating = false;
        gazeCalibrationOffset = Vector2.zero;
        gazeScale = new Vector2(1.2f, 1.2f);
        calibrationGazes.Clear();
        calibrationIndex = 0;

        // 고급 보정 관련 리셋
        if (useAdvancedCalibration)
        {
            calibrationSamplesPerTarget.Clear();
            isCollectingSamples = false;
            currentSampleCount = 0;
            calibrationPointTimer = 0f;
        }

        // 보정 리셋 후 캐시 업데이트
        UpdateCachedValues();

        // click-through 상태 복원
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

        // 보정 취소 후 click-through 상태 복원
        RestoreClickThroughStateAfterCalibration();

        Debug.Log("❌ 웹캠 보정이 취소되었습니다.");
    }


    void StartSampleCollection()
    {
        if (!isCalibrating || !isGazeValid)
        {
            Debug.LogWarning("⚠️ 시선이 감지되지 않습니다. 얼굴이 웹캠에 잘 보이는지 확인하세요.");
            return;
        }

        isCollectingSamples = true;
        currentSampleCount = 0;
        calibrationPointTimer = 0f;

        Debug.Log($"📍 보정 점 {calibrationIndex + 1}/9 - 샘플 수집 시작");
    }
    void UpdateSampleCollection()
    {
        calibrationPointTimer += Time.deltaTime;

        // 일정 간격으로 샘플 수집
        if (calibrationPointTimer >= (calibrationWaitTime / calibrationSamplesPerPoint))
        {
            if (isGazeValid && currentSampleCount < calibrationSamplesPerPoint)
            {
                calibrationSamplesPerTarget[calibrationIndex].Add(currentGazePoint);
                currentSampleCount++;
                calibrationPointTimer = 0f;

                Debug.Log($"샘플 {currentSampleCount}/{calibrationSamplesPerPoint} 수집됨 - 시선: {currentGazePoint}");

                if (currentSampleCount >= calibrationSamplesPerPoint)
                {
                    ProcessAdvancedCalibrationPoint();
                }
            }
            else if (!isGazeValid)
            {
                Debug.LogWarning("⚠️ 시선 감지 실패. 얼굴을 웹캠 쪽으로 향하세요.");
                calibrationPointTimer = 0f; // 타이머 리셋
            }
        }
    }

    // 보정 품질 개선을 위한 추가 메서드
    void ProcessCalibrationPoint()
    {
        if (!isCalibrating || !isGazeValid)
        {
            Debug.LogWarning("⚠️ 시선이 감지되지 않습니다. 얼굴이 웹캠에 잘 보이는지 확인하세요.");
            return;
        }

        // 현재 시선 위치와 타겟 위치의 차이 확인
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


    void ProcessAdvancedCalibrationPoint()
    {
        if (!isCalibrating) return;

        isCollectingSamples = false;

        // 수집된 샘플들의 평균 계산
        List<Vector2> samples = calibrationSamplesPerTarget[calibrationIndex];
        Vector2 averageGaze = Vector2.zero;

        foreach (Vector2 sample in samples)
        {
            averageGaze += sample;
        }
        averageGaze /= samples.Count;

        // 샘플의 분산 계산 (정확도 체크)
        float variance = 0f;
        foreach (Vector2 sample in samples)
        {
            variance += Vector2.Distance(sample, averageGaze);
        }
        variance /= samples.Count;

        calibrationGazes.Add(averageGaze);
        calibrationIndex++;

        Debug.Log($"✅ 보정 점 {calibrationIndex}/9 완료");
        Debug.Log($"📊 평균 시선: {averageGaze}, 분산: {variance:F1}px");

        if (variance > 50f)
        {
            Debug.LogWarning("⚠️ 이 점의 정확도가 낮습니다. 다음 점에서는 더 정확히 바라보세요.");
        }

        if (calibrationIndex >= calibrationTargets.Count)
        {
            CompleteAdvancedCalibration();
        }
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

            // 보정 품질 자동 테스트
            TestCalibrationQuality();
        }
        else
        {
            Debug.LogWarning("⚠️ 보정 데이터가 부족합니다.");
        }
    }

    // 개선된 보정 계산
    void CalculateAdvancedCalibration()
    {
        Vector2 totalOffset = Vector2.zero;
        Vector2 totalScale = Vector2.zero;
        int validPoints = 0;

        for (int i = 0; i < calibrationTargets.Count && i < calibrationGazes.Count; i++)
        {
            Vector2 target = calibrationTargets[i];
            Vector2 gaze = calibrationGazes[i];

            // 정규화된 좌표로 변환
            Vector2 normalizedTarget = new Vector2(target.x / Screen.width, target.y / Screen.height);
            Vector2 normalizedGaze = new Vector2(gaze.x / Screen.width, gaze.y / Screen.height);

            // 오프셋 계산
            Vector2 offset = normalizedTarget - normalizedGaze;
            totalOffset += offset;

            // 스케일 계산
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

            // 스케일 범위 제한 (너무 극단적인 값 방지)
            gazeScale.x = Mathf.Clamp(gazeScale.x, 0.5f, 2.5f);
            gazeScale.y = Mathf.Clamp(gazeScale.y, 0.5f, 2.5f);

            // 보정 완료 후 캐시 업데이트
            UpdateCachedValues();

            // 보정 품질 평가
            EvaluateCalibrationQuality();
        }
        else
        {
            Debug.LogError("❌ 유효한 보정 데이터가 없습니다!");
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
            Debug.Log("💡 개선 팁: 조명을 밝게 하고, 웹캠과 30-50cm 거리 유지");
        }
        else
        {
            Debug.Log("❌ 웹캠 보정 품질 불량 - 재보정 권장");
            Debug.Log("💡 개선 방법:");
            Debug.Log("   1. 조명 환경 개선 (얼굴이 잘 보이도록)");
            Debug.Log("   2. 웹캠 해상도 및 프레임레이트 확인");
            Debug.Log("   3. 머리를 최대한 고정하고 눈만 움직이기");
            Debug.Log("   4. 각 보정점을 정확히 바라보기");
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

            // 보정 완료 후 캐시 업데이트
            UpdateCachedValues();
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
        Debug.Log("👁️ 화면 중앙을 5초간 바라보세요...");

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
            // 평균 위치 계산
            Vector2 averageGaze = Vector2.zero;
            foreach (Vector2 sample in testSamples)
            {
                averageGaze += sample;
            }
            averageGaze /= testSamples.Count;

            // 중앙에서의 오차 계산
            float error = Vector2.Distance(averageGaze, centerTarget);

            // 분산 계산
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
                Debug.Log("💡 개선 방법:");
                Debug.Log("   1. 조명 환경 개선");
                Debug.Log("   2. 웹캠과의 거리 조정 (30-50cm)");
                Debug.Log("   3. 머리를 최대한 고정하고 눈만 움직이기");
            }
        }
        else
        {
            Debug.LogError("❌ 테스트 중 시선이 감지되지 않았습니다.");
        }
    }


    // 런타임 디버그용 메서드들
    [ContextMenu("Force Disable Click Through")]
    public void ForceDisableClickThrough()
    {
        if (CompatibilityWindowManager.Instance != null)
        {
            CompatibilityWindowManager.Instance.DisableClickThrough();
            Debug.Log("🔒 Click-through 강제 비활성화");
        }
    }

    [ContextMenu("Force Enable Click Through")]
    public void ForceEnableClickThrough()
    {
        if (CompatibilityWindowManager.Instance != null)
        {
            CompatibilityWindowManager.Instance.EnableClickThrough();
            Debug.Log("🔓 Click-through 강제 활성화");
        }
    }

    [ContextMenu("Debug Click Through State")]
    public void DebugClickThroughState()
    {
        if (CompatibilityWindowManager.Instance != null)
        {
            bool isClickThrough = CompatibilityWindowManager.Instance.IsClickThrough;
            Debug.Log($"현재 Click-through 상태: {(isClickThrough ? "활성화" : "비활성화")}");
        }
        else
        {
            Debug.Log("CompatibilityWindowManager가 없습니다.");
        }
    }
    // 런타임 설정 변경
    [ContextMenu("Quick Perfect Calibration")]
    public void QuickPerfectCalibration()
    {
        // 화면 중앙 기준으로 완벽한 보정 (테스트용)
        gazeCalibrationOffset = Vector2.zero;
        gazeScale = Vector2.one;
        isCalibrated = true;
        UpdateCachedValues();
        Debug.Log("⚡ 완벽한 보정 설정 (테스트용)");
    }

    [ContextMenu("Reset All Calibration")]
    public void ResetAllCalibration()
    {
        isCalibrated = false;
        isCalibrating = false;
        isCollectingSamples = false;
        gazeCalibrationOffset = Vector2.zero;
        gazeScale = Vector2.one;
        calibrationGazes.Clear();
        calibrationSamplesPerTarget.Clear();
        calibrationIndex = 0;
        UpdateCachedValues();
        Debug.Log("🔄 모든 보정 데이터 초기화 완료");
    }
    void OnGUI()
    {
        if (!showCalibrationUI) return;

        // 보정 모드 UI
        if (isCalibrating && calibrationIndex < calibrationTargets.Count)
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
                // 기본 점 표시
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

        // 시선 커서 표시
        if (isGazeValid)
        {
            Vector2 gazePos = enableGazeSmoothing ? smoothedGazePoint : currentGazePoint;

            GUI.color = Color.cyan;
            GUI.Box(new UnityEngine.Rect(gazePos.x - 10, gazePos.y - 1, 20, 2), "");
            GUI.Box(new UnityEngine.Rect(gazePos.x - 1, gazePos.y - 10, 2, 20), "");
            GUI.color = Color.white;
        }
    }

    void OnDestroy()
    {
        // 스레드 정리
        if (processingThread != null && processingThread.IsAlive)
        {
            try
            {
                processingThread.Join(1000); // 1초 대기
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

    // EyesTrackingManager 호환 메서드들
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

    // 프로퍼티들
    public bool IsGazeValid => isGazeValid;
    public Vector2 GazePosition => GetGazeScreenPosition();
    public bool IsFaceDetected => isFaceDetected;
    public bool AreEyesDetected => areEyesDetected;
    public bool IsCalibrated => isCalibrated;

    // 런타임 설정 변경
    [ContextMenu("Quick Calibration")]
    public void QuickCalibration()
    {
        // 화면 중앙 기준으로 빠른 보정
        Vector2 centerPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        gazeCalibrationOffset = Vector2.zero;
        gazeScale = Vector2.one;
        isCalibrated = true;
        Debug.Log("⚡ 빠른 보정 완료 (화면 중앙 기준)");
    }

    [ContextMenu("Reset Settings")]
    public void ResetSettings()
    {
        gazeCalibrationOffset = Vector2.zero;
        gazeScale = new Vector2(1.2f, 1.2f);
        gazeSmoothing = 8f;
        processEveryNthFrame = 3;
        Debug.Log("🔄 설정 초기화 완료");
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
#endif
}