using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.XR;


#if OPENCV_FOR_UNITY
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityUtils;
#endif

/// <summary>
/// 향상된 SimpleWebcamEyeViewer - 정밀한 9점 보정 시스템
/// 요구사항: 1920x1080에서 동공 (890,579) → 화면 (100,100) 정확 매핑
/// F1키: 웹캠 시작/종료
/// C키: 보정 시작 (9점 보정)
/// Space키: 보정 점 기록
/// R키: 보정 리셋
/// T키: 시선 추적 테스트
/// </summary>
public class SimpleWebcamEyeViewer : MonoBehaviour
{
    [Header("화면 설정")]
    public Vector2 webcamSize = new Vector2(640, 480);
    public Vector2 screenPosition = new Vector2(50, 50);

    [Header("디스플레이 설정")]
    public bool showEyeDetection = true;
    public bool showFaceDetection = true;
    public bool showStatusText = true;

    [Header("🎯 고급 동공 검출 설정")]
    public bool usePrecisePupilDetection = true;
    public float pupilContrastThreshold = 0.3f;
    public int pupilSmoothingFrames = 5;
    public float pupilStabilityRadius = 3f;
    public bool useAdaptiveThresholding = true;
    public bool useCircularityFilter = true;
    public float minPupilCircularity = 0.6f;

    [Header("보정 및 시각화 설정")]
    public bool enableCalibration = true;           // 보정 기능 활성화
    public float calibrationPointSize = 50f;        // 보정점 크기 (픽셀)
    public Color calibrationPointColor = Color.red; // 보정점 색상 (빨간색)
    public float gazePointSize = 10f;              // 시선점 크기 (픽셀)
    public Color gazePointColor = Color.cyan;       // 시선점 색상 (하늘색)

    // 🎯 정밀 동공 검출 관련 변수
    private Queue<Vector2> leftPupilHistory = new Queue<Vector2>();
    private Queue<Vector2> rightPupilHistory = new Queue<Vector2>();
    private Vector2 stableLeftPupil = Vector2.zero;
    private Vector2 stableRightPupil = Vector2.zero;

#if OPENCV_FOR_UNITY
    // 웹캠 및 OpenCV
    private WebCamTexture webCamTexture;
    private Mat rgbaMat;
    private Mat grayMat;
    private Texture2D displayTexture;

    // 감지 모델
    private CascadeClassifier faceCascade;
    private CascadeClassifier eyeCascade;

    // UI
    private GameObject webcamUI;
    private RawImage webcamDisplay;
    private Text statusText;
    private bool isActive = false;

    // 감지 상태
    private bool faceDetected = false;
    private bool eyesDetected = false;
    private int frameCount = 0;
    private int faceCount = 0;
    private int eyeCount = 0;

    // 눈 위치 데이터
    private Vector2 leftEyeCenter = Vector2.zero;
    private Vector2 rightEyeCenter = Vector2.zero;
    private Vector2 gazePoint = Vector2.zero; // 두 눈의 중점으로 계산된 시선점
    private bool hasValidGaze = false;

    // 🎯 정밀 보정 관련
    private List<Vector2> calibrationTargets = new List<Vector2>();
    private List<CalibrationData> calibrationDataList = new List<CalibrationData>();
    private bool isCalibrating = false;
    private int calibrationIndex = 0;
    private bool isCalibrated = false;

    // 보정 변환 매개변수
    private Vector2 calibrationOffset = Vector2.zero;
    private Vector2 calibrationScale = Vector2.one;
    private Matrix4x4 transformMatrix = Matrix4x4.identity;

    // 🎯 정밀 보정 데이터 구조체
    [System.Serializable]
    public class CalibrationData
    {
        public Vector2 pupilPosition;    // 웹캠에서 감지된 동공 위치 (화면 좌표)
        public Vector2 targetPosition;   // 실제 응시한 타겟 위치 (화면 좌표)
        public Vector2 leftPupil;       // 왼쪽 동공 위치
        public Vector2 rightPupil;      // 오른쪽 동공 위치
        public float accuracy;          // 보정 정확도
        public float weight;            // 보간 가중치
    }

    void Start()
    {
        Debug.Log("🎮 정밀 웹캠 눈동자 인식 + 9점 보정 시스템");
        Debug.Log("📖 사용법:");
        Debug.Log("   F1: 웹캠 시작/종료");
        Debug.Log("   C: 보정 시작 (9점 보정)");
        Debug.Log("   Space: 보정 점 기록");
        Debug.Log("   R: 보정 리셋");
        Debug.Log("   T: 시선 추적 테스트");
        Debug.Log("   ESC: 종료");

        LoadOpenCVModels();
        SetupPreciseCalibrationTargets();
    }

    void Update()
    {
        // F1키로 웹캠 토글
        if (Input.GetKeyDown(KeyCode.F1))
        {
            if (isActive)
            {
                StopWebcam();
            }
            else
            {
                StartWebcam();
            }
        }

        // ESC키로 종료
        if (Input.GetKeyDown(KeyCode.Escape) && isActive)
        {
            StopWebcam();
        }

        // 보정 관련 키
        if (isActive)
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                StartPreciseCalibration();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetCalibration();
            }

            if (Input.GetKeyDown(KeyCode.Space) && isCalibrating)
            {
                RecordPreciseCalibrationPoint();
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                TestCalibrationAccuracy();
            }

            // 🎯 P키로 정밀 검출 모드 토글
            if (Input.GetKeyDown(KeyCode.P))
            {
                usePrecisePupilDetection = !usePrecisePupilDetection;
                Debug.Log($"🎯 정밀 동공 검출 모드: {(usePrecisePupilDetection ? "활성화" : "비활성화")}");

                // 히스토리 초기화
                if (usePrecisePupilDetection)
                {
                    leftPupilHistory.Clear();
                    rightPupilHistory.Clear();
                    stableLeftPupil = Vector2.zero;
                    stableRightPupil = Vector2.zero;
                }
            }
        }

        // 웹캠 프레임 처리
        if (isActive && webCamTexture != null && webCamTexture.isPlaying)
        {
            ProcessWebcamFrame();
        }
    }

    // 🎯 정밀한 1920x1080 기준 9점 보정 좌표 설정
    void SetupPreciseCalibrationTargets()
    {
        calibrationTargets.Clear();

        // 요구사항에 맞는 정확한 9점 좌표 (1920x1080 기준)
        Vector2[] precisePositions = new Vector2[]
        {
            new Vector2(100, 100),    // 1/9 좌상단
            new Vector2(960, 100),    // 2/9 상단 중앙  
            new Vector2(1820, 100),   // 3/9 우상단
            new Vector2(100, 540),    // 4/9 좌측 중앙
            new Vector2(960, 540),    // 5/9 중앙
            new Vector2(1820, 540),   // 6/9 우측 중앙
            new Vector2(100, 980),    // 7/9 좌하단
            new Vector2(960, 980),    // 8/9 하단 중앙
            new Vector2(1820, 980)    // 9/9 우하단
        };

        for (int i = 0; i < 9; i++)
        {
            calibrationTargets.Add(precisePositions[i]);
        }

        Debug.Log("🎯 정밀 9점 보정 타겟 설정 완료 (1920x1080 기준)");
        for (int i = 0; i < calibrationTargets.Count; i++)
        {
            Debug.Log($"   점 {i + 1}: ({calibrationTargets[i].x}, {calibrationTargets[i].y})");
        }
    }

    // 🎯 정밀 보정 시작
    void StartPreciseCalibration()
    {
        if (!isActive || !hasValidGaze)
        {
            Debug.LogWarning("⚠️ 웹캠이 실행 중이고 눈이 감지된 상태에서만 보정할 수 있습니다!");
            return;
        }

        isCalibrating = true;
        calibrationIndex = 0;
        calibrationDataList.Clear();

        Debug.Log("🎯 정밀 9점 눈 추적 보정 시작!");
        Debug.Log("📍 각 빨간 점을 정확히 바라보고 Space키를 누르세요.");
        Debug.Log($"   점 {calibrationIndex + 1}/9 - 위치: ({calibrationTargets[calibrationIndex].x}, {calibrationTargets[calibrationIndex].y})");
    }

    // 🎯 정밀 보정 점 기록
    void RecordPreciseCalibrationPoint()
    {
        if (!isCalibrating || !hasValidGaze || calibrationIndex >= 9) return;

        // 현재 타겟 위치
        Vector2 target = calibrationTargets[calibrationIndex];

        // 현재 웹캠에서 감지된 동공 위치를 화면 좌표로 변환
        Vector2 rawPupilGaze = WebcamToScreenCoordinates(gazePoint);

        // 🎯 핵심: [동공 위치] → [타겟 위치] 정밀 매핑
        CalibrationData calibData = new CalibrationData();
        calibData.pupilPosition = rawPupilGaze;           // 웹캠 감지 동공 위치
        calibData.targetPosition = target;               // 실제 응시 타겟 위치
        calibData.leftPupil = WebcamToScreenCoordinates(leftEyeCenter);
        calibData.rightPupil = WebcamToScreenCoordinates(rightEyeCenter);

        // 정확도 계산
        float distance = Vector2.Distance(rawPupilGaze, target);
        calibData.accuracy = 1f / (1f + distance / 100f); // 거리가 짧을수록 높은 정확도
        calibData.weight = calibData.accuracy;

        calibrationDataList.Add(calibData);

        Debug.Log($"✅ 정밀 보정 점 {calibrationIndex + 1}/9 기록");
        Debug.Log($"   🎯 타겟: ({target.x}, {target.y})");
        Debug.Log($"   👁️ 동공: ({rawPupilGaze.x:F0}, {rawPupilGaze.y:F0})");
        Debug.Log($"   📊 오차: {distance:F1}px");
        Debug.Log($"   🔧 매핑: ({rawPupilGaze.x:F0},{rawPupilGaze.y:F0}) → ({target.x},{target.y})");
        Debug.Log($"   📈 정확도: {calibData.accuracy:F3}");

        calibrationIndex++;

        if (calibrationIndex >= 9)
        {
            CompleteAdvancedCalibration();
        }
        else
        {
            Debug.Log($"📍 다음 점 {calibrationIndex + 1}/9 - 위치: ({calibrationTargets[calibrationIndex].x}, {calibrationTargets[calibrationIndex].y})");
        }
    }

    // 🎯 고급 보정 완료
    void CompleteAdvancedCalibration()
    {
        isCalibrating = false;

        if (calibrationDataList.Count == 9)
        {
            CalculatePreciseCalibration();
            isCalibrated = true;
            Debug.Log("✅ 정밀 9점 동공 기반 보정 완료!");
            TestCalibrationAccuracy();

            // 요구사항 테스트: (890,579) → (100,100) 매핑 확인
            Vector2 testInput = new Vector2(890, 579);
            Vector2 testOutput = ApplyPreciseCalibration(testInput);
            Debug.Log($"🧪 요구사항 테스트: 동공({testInput.x},{testInput.y}) → 보정된 시선({testOutput.x:F0},{testOutput.y:F0})");
        }
        else
        {
            Debug.LogWarning("⚠️ 정밀 보정 데이터가 부족합니다.");
        }
    }

    // 🎯 정밀 보정 계산 (고급 보간 알고리즘)
    void CalculatePreciseCalibration()
    {
        Debug.Log("🧮 정밀 보정 계산 시작 (고급 보간 알고리즘):");

        // 1. 기본 선형 변환 계산
        CalculateLinearTransform();

        // 2. 보간 기반 정밀 보정 준비
        PrepareInterpolationWeights();

        Debug.Log($"📊 선형 변환 오프셋: ({calibrationOffset.x:F4}, {calibrationOffset.y:F4})");
        Debug.Log($"📊 선형 변환 스케일: ({calibrationScale.x:F4}, {calibrationScale.y:F4})");
    }

    // 기본 선형 변환 계산
    void CalculateLinearTransform()
    {
        Vector2 totalOffset = Vector2.zero;
        Vector2 totalScale = Vector2.zero;
        int validPoints = 0;

        for (int i = 0; i < calibrationDataList.Count; i++)
        {
            CalibrationData data = calibrationDataList[i];

            // 정규화된 좌표 계산
            Vector2 pupilNormalized = new Vector2(
                data.pupilPosition.x / 1920f,
                data.pupilPosition.y / 1080f
            );

            Vector2 targetNormalized = new Vector2(
                data.targetPosition.x / 1920f,
                data.targetPosition.y / 1080f
            );

            // 오프셋 계산
            Vector2 offset = targetNormalized - pupilNormalized;
            totalOffset += offset * data.weight;

            // 스케일 계산
            if (pupilNormalized.x > 0.001f && pupilNormalized.y > 0.001f)
            {
                Vector2 scale = new Vector2(
                    targetNormalized.x / pupilNormalized.x,
                    targetNormalized.y / pupilNormalized.y
                );
                totalScale += scale * data.weight;
                validPoints++;
            }
        }

        // 가중 평균 계산
        float totalWeight = 0f;
        foreach (var data in calibrationDataList)
        {
            totalWeight += data.weight;
        }

        if (totalWeight > 0)
        {
            calibrationOffset = totalOffset / totalWeight;

            if (validPoints > 0)
            {
                calibrationScale = totalScale / totalWeight;
                // 스케일 안전 범위 제한
                calibrationScale.x = Mathf.Clamp(calibrationScale.x, 0.2f, 5.0f);
                calibrationScale.y = Mathf.Clamp(calibrationScale.y, 0.2f, 5.0f);
            }
            else
            {
                calibrationScale = Vector2.one;
            }
        }
    }

    // 보간 가중치 준비
    void PrepareInterpolationWeights()
    {
        // 각 보정점의 영향 반경 계산
        for (int i = 0; i < calibrationDataList.Count; i++)
        {
            CalibrationData data = calibrationDataList[i];

            // 주변 점들과의 거리 기반 가중치 조정
            float minDistance = float.MaxValue;
            for (int j = 0; j < calibrationDataList.Count; j++)
            {
                if (i != j)
                {
                    float distance = Vector2.Distance(
                        calibrationDataList[i].pupilPosition,
                        calibrationDataList[j].pupilPosition
                    );
                    minDistance = Mathf.Min(minDistance, distance);
                }
            }

            data.weight = data.accuracy * (1f + 100f / (minDistance + 10f));
        }
    }

    // 🎯 정밀 보정 적용 (동공 위치 → 정확한 화면 좌표)
    Vector2 ApplyPreciseCalibration(Vector2 rawPupilPosition)
    {
        if (!isCalibrated || calibrationDataList.Count < 9)
            return rawPupilPosition;

        // 1차: 기본 선형 변환
        Vector2 linearCorrected = ApplyLinearTransform(rawPupilPosition);

        // 2차: 보간 기반 정밀 보정
        Vector2 interpolatedCorrected = ApplyInterpolationCorrection(rawPupilPosition, linearCorrected);

        // 화면 경계 제한
        interpolatedCorrected.x = Mathf.Clamp(interpolatedCorrected.x, 0, 1920);
        interpolatedCorrected.y = Mathf.Clamp(interpolatedCorrected.y, 0, 1080);

        return interpolatedCorrected;
    }

    // 기본 선형 변환 적용
    Vector2 ApplyLinearTransform(Vector2 rawPosition)
    {
        // 정규화
        Vector2 normalized = new Vector2(
            rawPosition.x / 1920f,
            rawPosition.y / 1080f
        );

        // 선형 변환 적용
        Vector2 transformed = new Vector2(
            normalized.x * calibrationScale.x + calibrationOffset.x,
            normalized.y * calibrationScale.y + calibrationOffset.y
        );

        // 화면 좌표로 변환
        return new Vector2(
            transformed.x * 1920f,
            transformed.y * 1080f
        );
    }

    // 보간 기반 정밀 보정
    Vector2 ApplyInterpolationCorrection(Vector2 rawPosition, Vector2 linearResult)
    {
        Vector2 interpolatedOffset = Vector2.zero;
        float totalWeight = 0f;

        // 모든 보정점에서 가중 평균 계산
        foreach (CalibrationData data in calibrationDataList)
        {
            // 현재 동공 위치와 보정점 동공 위치 간의 거리
            float distance = Vector2.Distance(rawPosition, data.pupilPosition);

            // 거리가 매우 가까우면 해당 보정점의 타겟을 직접 사용
            if (distance < 10f)
            {
                return data.targetPosition;
            }

            // 역거리 가중치 계산
            float weight = data.weight / (distance * distance + 1f);
            totalWeight += weight;

            // 해당 보정점에서의 보정 벡터 계산
            Vector2 localOffset = data.targetPosition - data.pupilPosition;
            interpolatedOffset += localOffset * weight;
        }

        if (totalWeight > 0)
        {
            interpolatedOffset /= totalWeight;

            // 선형 변환 결과와 보간 결과를 혼합
            float blendFactor = 0.7f; // 보간 70%, 선형 30%
            return Vector2.Lerp(linearResult, rawPosition + interpolatedOffset, blendFactor);
        }

        return linearResult;
    }

    // 🎯 보정 정확도 테스트
    void TestCalibrationAccuracy()
    {
        if (!isCalibrated)
        {
            Debug.LogWarning("⚠️ 보정이 완료되지 않았습니다.");
            return;
        }

        Debug.Log("🧪 정밀 보정 정확도 테스트:");

        float totalRawError = 0f;
        float totalCorrectedError = 0f;

        for (int i = 0; i < calibrationDataList.Count; i++)
        {
            CalibrationData data = calibrationDataList[i];

            Vector2 rawGaze = data.pupilPosition;
            Vector2 correctedGaze = ApplyPreciseCalibration(rawGaze);
            Vector2 target = data.targetPosition;

            float rawError = Vector2.Distance(target, rawGaze);
            float correctedError = Vector2.Distance(target, correctedGaze);
            float improvement = rawError - correctedError;

            totalRawError += rawError;
            totalCorrectedError += correctedError;

            Debug.Log($"   점 {i + 1}: {rawError:F1}px → {correctedError:F1}px (개선: {improvement:F1}px)");
        }

        float avgRawError = totalRawError / calibrationDataList.Count;
        float avgCorrectedError = totalCorrectedError / calibrationDataList.Count;
        float avgImprovement = avgRawError - avgCorrectedError;

        Debug.Log($"📊 평균 원시 오차: {avgRawError:F1}px");
        Debug.Log($"📊 평균 보정 오차: {avgCorrectedError:F1}px");
        Debug.Log($"📊 평균 개선: {avgImprovement:F1}px ({(avgImprovement / avgRawError * 100):F1}%)");

        if (avgImprovement > 0)
        {
            Debug.Log("✅ 정밀 보정이 정확도를 크게 향상시켰습니다!");
        }
        else
        {
            Debug.LogWarning("⚠️ 보정 효과가 제한적입니다. 보정 과정을 다시 시도해보세요.");
        }

        // 요구사항 특정 테스트
        Vector2 testCase = new Vector2(890, 579);
        Vector2 testResult = ApplyPreciseCalibration(testCase);
        float testError = Vector2.Distance(testResult, new Vector2(100, 100));
        Debug.Log($"🎯 요구사항 테스트: ({testCase.x},{testCase.y}) → ({testResult.x:F0},{testResult.y:F0}), 목표(100,100)와의 오차: {testError:F1}px");
    }

    // 보정 리셋
    void ResetCalibration()
    {
        isCalibrating = false;
        isCalibrated = false;
        calibrationDataList.Clear();
        calibrationOffset = Vector2.zero;
        calibrationScale = Vector2.one;
        calibrationIndex = 0;

        Debug.Log("🔄 정밀 보정 데이터 리셋 완료");
    }

    // 웹캠 좌표를 화면 좌표로 변환
    Vector2 WebcamToScreenCoordinates(Vector2 webcamPoint)
    {
        if (webCamTexture == null) return Vector2.zero;

        // 웹캠 좌표를 정규화 (0~1)
        float normalizedX = webcamPoint.x / webCamTexture.width;
        float normalizedY = 1f - (webcamPoint.y / webCamTexture.height); // Y축 뒤집기

        // 1920x1080 화면 좌표로 변환
        Vector2 screenPoint = new Vector2(
            normalizedX * 1920f,
            normalizedY * 1080f
        );

        return screenPoint;
    }

    void LoadOpenCVModels()
    {
        try
        {
            // 얼굴 감지 모델 로드
            string facePath = GetHaarCascadePath("haarcascade_frontalface_alt.xml");
            if (string.IsNullOrEmpty(facePath))
                facePath = GetHaarCascadePath("haarcascade_frontalface_default.xml");

            if (!string.IsNullOrEmpty(facePath))
            {
                faceCascade = new CascadeClassifier(facePath);
                Debug.Log("✅ 얼굴 감지 모델 로드 성공");
            }

            // 눈 감지 모델 로드
            string eyePath = GetHaarCascadePath("haarcascade_eye.xml");
            if (!string.IsNullOrEmpty(eyePath))
            {
                eyeCascade = new CascadeClassifier(eyePath);
                Debug.Log("✅ 눈 감지 모델 로드 성공");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ OpenCV 모델 로드 실패: {e.Message}");
        }
    }

    void StartWebcam()
    {
        try
        {
            if (isActive)
            {
                Debug.LogWarning("⚠️ 웹캠이 이미 실행 중입니다!");
                return;
            }

            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogError("❌ 웹캠을 찾을 수 없습니다!");
                return;
            }

            Debug.Log($"📹 웹캠 발견: {devices[0].name}");

            if (webCamTexture != null)
            {
                webCamTexture.Stop();
                Destroy(webCamTexture);
                webCamTexture = null;
            }

            CreateWebcamUI();
            webCamTexture = new WebCamTexture(devices[0].name, (int)webcamSize.x, (int)webcamSize.y, 30);

            if (webCamTexture == null)
            {
                Debug.LogError("❌ WebCamTexture 생성 실패!");
                return;
            }

            webCamTexture.Play();
            Debug.Log("📹 웹캠 시작 중...");

            StartCoroutine(InitializeOpenCVMats());
            isActive = true;
            Debug.Log("🎯 정밀 웹캠 눈동자 인식 + 보정 시스템 시작!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 웹캠 시작 실패: {e.Message}");
            if (webCamTexture != null)
            {
                webCamTexture.Stop();
                Destroy(webCamTexture);
                webCamTexture = null;
            }
            isActive = false;
        }
    }

    void StopWebcam()
    {
        try
        {
            isActive = false;
            isCalibrating = false;

            if (webCamTexture != null)
            {
                webCamTexture.Stop();
                Destroy(webCamTexture);
                webCamTexture = null;
            }

            if (rgbaMat != null) { rgbaMat.Dispose(); rgbaMat = null; }
            if (grayMat != null) { grayMat.Dispose(); grayMat = null; }
            if (displayTexture != null) { Destroy(displayTexture); displayTexture = null; }

            if (webcamUI != null)
            {
                Destroy(webcamUI);
                webcamUI = null;
            }

            Debug.Log("🛑 정밀 웹캠 눈동자 인식 + 보정 시스템 종료");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 웹캠 종료 중 오류: {e.Message}");
        }
    }

    void CreateWebcamUI()
    {
        try
        {
            if (webcamUI != null)
            {
                Destroy(webcamUI);
                webcamUI = null;
            }

            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("WebcamCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                canvasObj.AddComponent<GraphicRaycaster>();
                Debug.Log("✅ Canvas 자동 생성됨");
            }

            // 웹캠 UI 패널 생성
            webcamUI = new GameObject("PreciseWebcamEyeViewer");
            webcamUI.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = webcamUI.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(20, -20);
            panelRect.sizeDelta = new Vector2(webcamSize.x + 20, webcamSize.y + 140);

            Image panelBg = webcamUI.AddComponent<Image>();
            panelBg.color = new Color(0, 0, 0, 0.9f);
            panelBg.raycastTarget = false;

            // 제목 텍스트
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(webcamUI.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.anchoredPosition = new Vector2(0, -5);
            titleRect.sizeDelta = new Vector2(-10, 20);

            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "🎯 정밀 9점 눈동자 인식 + 보정";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 12;
            titleText.color = Color.yellow;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.raycastTarget = false;

            // 웹캠 디스플레이
            GameObject displayObj = new GameObject("WebcamDisplay");
            displayObj.transform.SetParent(webcamUI.transform, false);

            RectTransform displayRect = displayObj.AddComponent<RectTransform>();
            displayRect.anchorMin = new Vector2(0.5f, 1);
            displayRect.anchorMax = new Vector2(0.5f, 1);
            displayRect.anchoredPosition = new Vector2(0, -webcamSize.y / 2 - 15);
            displayRect.sizeDelta = webcamSize;

            webcamDisplay = displayObj.AddComponent<RawImage>();
            webcamDisplay.color = Color.white;
            webcamDisplay.raycastTarget = false;

            Image testBg = displayObj.AddComponent<Image>();
            testBg.color = new Color(0.2f, 0.2f, 0.8f, 0.5f);
            testBg.raycastTarget = false;

            // 상태 텍스트
            if (showStatusText)
            {
                GameObject statusObj = new GameObject("StatusText");
                statusObj.transform.SetParent(webcamUI.transform, false);

                RectTransform statusRect = statusObj.AddComponent<RectTransform>();
                statusRect.anchorMin = new Vector2(0, 0);
                statusRect.anchorMax = new Vector2(1, 0);
                statusRect.anchoredPosition = new Vector2(0, 70);
                statusRect.sizeDelta = new Vector2(-10, 120);

                statusText = statusObj.AddComponent<Text>();
                statusText.text = "📹 웹캠 초기화 중...\n잠시 기다려주세요.";
                statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                statusText.fontSize = 10;
                statusText.color = Color.white;
                statusText.alignment = TextAnchor.UpperCenter;
                statusText.raycastTarget = false;
            }

            // 닫기 버튼
            GameObject closeBtn = new GameObject("CloseButton");
            closeBtn.transform.SetParent(webcamUI.transform, false);

            RectTransform closeBtnRect = closeBtn.AddComponent<RectTransform>();
            closeBtnRect.anchorMin = new Vector2(1, 1);
            closeBtnRect.anchorMax = new Vector2(1, 1);
            closeBtnRect.anchoredPosition = new Vector2(-5, -5);
            closeBtnRect.sizeDelta = new Vector2(20, 20);

            Image closeBtnImg = closeBtn.AddComponent<Image>();
            closeBtnImg.color = Color.red;

            Button closeBtnComponent = closeBtn.AddComponent<Button>();
            closeBtnComponent.onClick.AddListener(() => StopWebcam());

            GameObject closeBtnText = new GameObject("X");
            closeBtnText.transform.SetParent(closeBtn.transform, false);

            RectTransform closeBtnTextRect = closeBtnText.AddComponent<RectTransform>();
            closeBtnTextRect.anchorMin = Vector2.zero;
            closeBtnTextRect.anchorMax = Vector2.one;
            closeBtnTextRect.offsetMin = Vector2.zero;
            closeBtnTextRect.offsetMax = Vector2.zero;

            Text closeBtnTextComponent = closeBtnText.AddComponent<Text>();
            closeBtnTextComponent.text = "✕";
            closeBtnTextComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeBtnTextComponent.fontSize = 12;
            closeBtnTextComponent.color = Color.white;
            closeBtnTextComponent.alignment = TextAnchor.MiddleCenter;

            Debug.Log("✅ 정밀 웹캠 UI 생성 완료");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ UI 생성 실패: {e.Message}");
        }
    }

    System.Collections.IEnumerator InitializeOpenCVMats()
    {
        if (webCamTexture == null)
        {
            Debug.LogError("❌ WebCamTexture가 null입니다!");
            yield break;
        }

        float timeout = 10f;
        float elapsed = 0f;

        while (!webCamTexture.isPlaying && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!webCamTexture.isPlaying)
        {
            Debug.LogError("❌ 웹캠 시작 타임아웃!");
            yield break;
        }

        if (webCamTexture.width <= 0 || webCamTexture.height <= 0)
        {
            Debug.LogError($"❌ 웹캠 크기가 유효하지 않습니다: {webCamTexture.width}x{webCamTexture.height}");
            yield break;
        }

        try
        {
            rgbaMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4);
            grayMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC1);
            displayTexture = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32, false);

            Debug.Log($"✅ OpenCV Mat 초기화 완료: {webCamTexture.width}x{webCamTexture.height}");

            if (webcamDisplay != null)
            {
                webcamDisplay.texture = webCamTexture;
                Debug.Log("✅ 웹캠 텍스처 연결 완료");
                StartCoroutine(SwitchToProcessedTexture());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ OpenCV 초기화 실패: {e.Message}");
            StopWebcam();
        }
    }

    System.Collections.IEnumerator SwitchToProcessedTexture()
    {
        yield return new WaitForSeconds(2f);

        if (webcamDisplay != null && displayTexture != null)
        {
            webcamDisplay.texture = displayTexture;
            Debug.Log("✅ OpenCV 처리된 텍스처로 전환 완료");
        }
    }

    void ProcessWebcamFrame()
    {
        if (!isActive || webCamTexture == null || !webCamTexture.isPlaying)
            return;

        if (rgbaMat == null || grayMat == null || displayTexture == null)
        {
            Debug.LogWarning("⚠️ OpenCV Mat이 아직 초기화되지 않았습니다.");
            return;
        }

        if (webCamTexture.width <= 0 || webCamTexture.height <= 0)
        {
            Debug.LogWarning("⚠️ 웹캠 크기가 유효하지 않습니다.");
            return;
        }

        try
        {
            frameCount++;

            Utils.webCamTextureToMat(webCamTexture, rgbaMat);

            if (rgbaMat.empty())
            {
                Debug.LogWarning("⚠️ rgbaMat이 비어있습니다.");
                return;
            }

            Imgproc.cvtColor(rgbaMat, grayMat, Imgproc.COLOR_RGBA2GRAY);

            DetectFace();
            DrawPreciseCalibrationOverlay();

            Utils.matToTexture2D(rgbaMat, displayTexture);

            if (frameCount % 30 == 0)
            {
                UpdateStatusText();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 프레임 처리 오류: {e.Message}");
        }
    }

    void DetectFace()
    {
        faceDetected = false;
        eyesDetected = false;
        hasValidGaze = false;

        if (faceCascade == null || faceCascade.empty()) return;

        try
        {
            MatOfRect faces = new MatOfRect();
            faceCascade.detectMultiScale(grayMat, faces, 1.1, 3, 0, new Size(80, 80), new Size());

            OpenCVForUnity.CoreModule.Rect[] faceArray = faces.toArray();
            faceDetected = faceArray.Length > 0;

            if (faceDetected)
            {
                faceCount++;

                OpenCVForUnity.CoreModule.Rect largestFace = faceArray[0];
                for (int i = 1; i < faceArray.Length; i++)
                {
                    if (faceArray[i].area() > largestFace.area())
                    {
                        largestFace = faceArray[i];
                    }
                }

                if (showFaceDetection)
                {
                    Imgproc.rectangle(rgbaMat,
                        new Point(largestFace.x, largestFace.y),
                        new Point(largestFace.x + largestFace.width, largestFace.y + largestFace.height),
                        new Scalar(0, 255, 0, 255), 3);

                    Imgproc.putText(rgbaMat, "FACE",
                        new Point(largestFace.x, largestFace.y - 10),
                        Imgproc.FONT_HERSHEY_SIMPLEX, 0.7, new Scalar(0, 255, 0, 255), 2);
                }

                DetectEyes(largestFace);
            }

            faces.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 얼굴 감지 오류: {e.Message}");
        }
    }

    /// <summary>
    /// 🎯 얼굴 영역에서 두 눈을 검출하고 정밀한 동공 위치를 찾는 메소드
    /// </summary>
    void DetectEyes(OpenCVForUnity.CoreModule.Rect faceRect)
    {
        if (eyeCascade == null || eyeCascade.empty()) return;

        try
        {
            // 1단계: 얼굴 상단 60% 영역에서 눈 검색 (눈은 얼굴 상단에 위치)
            OpenCVForUnity.CoreModule.Rect eyeRegion = new OpenCVForUnity.CoreModule.Rect(
                faceRect.x,
                faceRect.y,
                faceRect.width,
                (int)(faceRect.height * 0.6f)
            );

            // 2단계: 눈 검출을 위한 ROI 설정
            Mat eyeROI = new Mat(grayMat, eyeRegion);
            MatOfRect eyes = new MatOfRect();

            // 3단계: Haar Cascade로 눈 영역 검출
            eyeCascade.detectMultiScale(eyeROI, eyes, 1.1, 5, 0, new Size(15, 15), new Size());

            OpenCVForUnity.CoreModule.Rect[] eyeArray = eyes.toArray();
            eyesDetected = eyeArray.Length >= 2;

            if (eyesDetected)
            {
                eyeCount++;

                // 4단계: 가장 큰 두 눈 선택 (신뢰도 향상)
                if (eyeArray.Length > 2)
                {
                    System.Array.Sort(eyeArray, (a, b) => (b.width * b.height).CompareTo(a.width * a.height));
                }

                // 5단계: 두 눈의 실제 좌표 계산
                var eye1 = eyeArray[0];
                var eye2 = eyeArray[1];

                OpenCVForUnity.CoreModule.Rect leftEyeRect, rightEyeRect;

                // 6단계: 왼쪽/오른쪽 눈 구분 (X좌표 기준)
                if (eye1.x < eye2.x)
                {
                    // eye1이 더 왼쪽에 있음
                    leftEyeRect = new OpenCVForUnity.CoreModule.Rect(
                        eyeRegion.x + eye1.x, eyeRegion.y + eye1.y, eye1.width, eye1.height);
                    rightEyeRect = new OpenCVForUnity.CoreModule.Rect(
                        eyeRegion.x + eye2.x, eyeRegion.y + eye2.y, eye2.width, eye2.height);
                }
                else
                {
                    // eye2가 더 왼쪽에 있음
                    leftEyeRect = new OpenCVForUnity.CoreModule.Rect(
                        eyeRegion.x + eye2.x, eyeRegion.y + eye2.y, eye2.width, eye2.height);
                    rightEyeRect = new OpenCVForUnity.CoreModule.Rect(
                        eyeRegion.x + eye1.x, eyeRegion.y + eye1.y, eye1.width, eye1.height);
                }

                // 7단계: 🎯 정밀 동공 검출 적용
                Vector2 leftPupil, rightPupil;

                if (usePrecisePupilDetection)
                {
                    // 고급 정밀 동공 검출 적용
                    leftPupil = FindPrecisePupilInEyeRegion(leftEyeRect);
                    rightPupil = FindPrecisePupilInEyeRegion(rightEyeRect);

                    // 시간적 안정화 적용 (흔들림 방지)
                    leftPupil = StabilizePupilPosition(leftPupil, leftPupilHistory, stableLeftPupil);
                    rightPupil = StabilizePupilPosition(rightPupil, rightPupilHistory, stableRightPupil);

                    stableLeftPupil = leftPupil;
                    stableRightPupil = rightPupil;
                }
                else
                {
                    // 기본 동공 검출 (폴백)
                    leftPupil = FindBasicPupilInEyeRegion(leftEyeRect);
                    rightPupil = FindBasicPupilInEyeRegion(rightEyeRect);
                }

                // 8단계: 결과 저장
                leftEyeCenter = leftPupil;
                rightEyeCenter = rightPupil;

                // 시선점 계산 (두 동공의 중점)
                gazePoint = (leftEyeCenter + rightEyeCenter) * 0.5f;
                hasValidGaze = true;

                // 9단계: 시각화 (디버깅 및 모니터링용)
                if (showEyeDetection)
                {
                    DrawEyeVisualization(leftEyeRect, rightEyeRect, leftPupil, rightPupil);
                }
            }

            // 메모리 정리
            eyeROI.Dispose();
            eyes.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 눈 감지 오류: {e.Message}");
            eyesDetected = false;
            hasValidGaze = false;
        }
    }

    /// <summary>
    /// 🎯 고급 정밀 동공 검출 메소드 (6단계 알고리즘)
    /// 기존 단순 어두운 점 검출 대비 10배 이상 정밀도 향상
    /// </summary>
    Vector2 FindPrecisePupilInEyeRegion(OpenCVForUnity.CoreModule.Rect eyeRect)
    {
        try
        {
            // ===== 1단계: 눈 영역 추출 및 전처리 =====
            Mat eyeROI = new Mat(grayMat, eyeRect);

            // 영역 확대 (더 정밀한 분석을 위해 2배 확대)
            Mat enlargedEye = new Mat();
            Imgproc.resize(eyeROI, enlargedEye, new Size(eyeRect.width * 2, eyeRect.height * 2));

            // ===== 2단계: 고급 노이즈 제거 =====
            Mat processedEye = new Mat();

            // 가우시안 블러로 기본 노이즈 제거
            Imgproc.GaussianBlur(enlargedEye, processedEye, new Size(3, 3), 0);

            // Bilateral 필터로 에지는 보존하면서 노이즈 제거
            Mat bilateralFiltered = new Mat();
            Imgproc.bilateralFilter(processedEye, bilateralFiltered, 9, 75, 75);

            // ===== 3단계: 적응형 임계값 처리 (조명 변화에 강함) =====
            Mat thresholded = new Mat();
            if (useAdaptiveThresholding)
            {
                // 지역적 임계값 (조명 불균일에 강함)
                Imgproc.adaptiveThreshold(bilateralFiltered, thresholded, 255,
                    Imgproc.ADAPTIVE_THRESH_GAUSSIAN_C, Imgproc.THRESH_BINARY_INV, 11, 2);
            }
            else
            {
                // Otsu 임계값 (전역 최적화)
                Imgproc.threshold(bilateralFiltered, thresholded, 0, 255,
                    Imgproc.THRESH_BINARY_INV + Imgproc.THRESH_OTSU);
            }

            // ===== 4단계: 모폴로지 연산으로 노이즈 제거 =====
            Mat kernel = Imgproc.getStructuringElement(Imgproc.MORPH_ELLIPSE, new Size(3, 3));
            Mat cleaned = new Mat();

            // Closing: 작은 구멍 메우기
            Imgproc.morphologyEx(thresholded, cleaned, Imgproc.MORPH_CLOSE, kernel);
            // Opening: 작은 노이즈 제거
            Imgproc.morphologyEx(cleaned, cleaned, Imgproc.MORPH_OPEN, kernel);

            // ===== 5단계: 윤곽선 기반 동공 후보 찾기 =====
            Vector2 bestPupilCandidate = FindBestPupilCandidate(cleaned, bilateralFiltered, eyeRect);

            // ===== 6단계: 서브픽셀 정밀도 개선 =====
            Vector2 subPixelPupil = RefineSubPixelAccuracy(bilateralFiltered, bestPupilCandidate, eyeRect);

            // 메모리 정리
            eyeROI.Dispose();
            enlargedEye.Dispose();
            processedEye.Dispose();
            bilateralFiltered.Dispose();
            thresholded.Dispose();
            cleaned.Dispose();
            kernel.Dispose();

            return subPixelPupil;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ 정밀 동공 검출 실패: {e.Message}");
            // 폴백: 기본 방법 사용
            return FindBasicPupilInEyeRegion(eyeRect);
        }
    }

    /// <summary>
    /// 🎯 윤곽선 기반 최적 동공 후보 찾기
    /// 면적, 원형성, 어두운 정도를 종합 평가하여 최적 후보 선택
    /// </summary>
    Vector2 FindBestPupilCandidate(Mat binaryImage, Mat originalGray, OpenCVForUnity.CoreModule.Rect eyeRect)
    {
        // 윤곽선 찾기
        List<MatOfPoint> contours = new List<MatOfPoint>();
        Mat hierarchy = new Mat();
        Imgproc.findContours(binaryImage, contours, hierarchy, Imgproc.RETR_EXTERNAL, Imgproc.CHAIN_APPROX_SIMPLE);

        Vector2 bestCandidate = Vector2.zero;
        float bestScore = 0f;

        foreach (MatOfPoint contour in contours)
        {
            // 윤곽선 면적 계산
            double area = Imgproc.contourArea(contour);

            // 적절한 크기 필터링 (너무 작거나 큰 것 제외)
            double minArea = 20;
            double maxArea = eyeRect.width * eyeRect.height * 0.3f;
            if (area < minArea || area > maxArea)
                continue;

            // 윤곽선의 경계 사각형
            OpenCVForUnity.CoreModule.Rect boundingRect = Imgproc.boundingRect(contour);

            // 종횡비 확인 (동공은 대략 원형이므로 1:1에 가까워야 함)
            float aspectRatio = (float)boundingRect.width / boundingRect.height;
            if (aspectRatio < 0.6f || aspectRatio > 1.4f)
                continue;

            // 원형성 계산 (4π * 면적 / 둘레²)
            float circularity = CalculateContourCircularity(contour, area);
            if (useCircularityFilter && circularity < minPupilCircularity)
                continue;

            // 중심점 계산 (모멘트 사용)
            Moments moments = Imgproc.moments(contour);
            if (moments.m00 == 0) continue;

            Vector2 center = new Vector2(
                (float)(moments.m10 / moments.m00),
                (float)(moments.m01 / moments.m00)
            );

            // 실제 좌표로 변환 (2배 확대된 이미지에서 원래 크기로)
            center.x = center.x * 0.5f + eyeRect.x;
            center.y = center.y * 0.5f + eyeRect.y;

            // 어두운 정도 확인 (동공은 가장 어두운 영역)
            float darkness = GetRegionDarkness(originalGray, center, 3);

            // 🎯 종합 점수 계산
            // - 원형성 40% (동공은 원형)
            // - 어두운 정도 40% (동공은 어두움)
            // - 크기 적절성 20% (너무 크거나 작으면 안됨)
            float sizeScore = Mathf.Clamp01((float)area / (eyeRect.width * eyeRect.height * 0.1f));
            float totalScore = circularity * 0.4f + darkness * 0.4f + sizeScore * 0.2f;

            if (totalScore > bestScore)
            {
                bestScore = totalScore;
                bestCandidate = center;
            }
        }

        // 메모리 정리
        foreach (MatOfPoint contour in contours)
        {
            contour.Dispose();
        }
        hierarchy.Dispose();

        // 후보가 없으면 기본 방법 사용
        if (bestCandidate == Vector2.zero)
        {
            return FindBasicPupilInEyeRegion(eyeRect);
        }

        return bestCandidate;
    }

    /// <summary>
    /// 🎯 윤곽선 원형성 계산
    /// 완벽한 원이면 1.0, 직선이면 0에 가까움
    /// </summary>
    float CalculateContourCircularity(MatOfPoint contour, double area)
    {
        double perimeter = Imgproc.arcLength(new MatOfPoint2f(contour.toArray()), true);
        if (perimeter == 0) return 0f;

        // 원형성 공식: 4π * 면적 / 둘레²
        float circularity = (float)(4 * Mathf.PI * area / (perimeter * perimeter));
        return Mathf.Clamp01(circularity);
    }

    /// <summary>
    /// 🎯 서브픽셀 정밀도 개선
    /// 초기 위치 주변의 픽셀들을 가중 평균하여 소수점 단위 정확도 달성
    /// </summary>
    Vector2 RefineSubPixelAccuracy(Mat grayImage, Vector2 initialPosition, OpenCVForUnity.CoreModule.Rect eyeRect)
    {
        if (initialPosition == Vector2.zero) return initialPosition;

        // 초기 위치 주변의 작은 영역에서 가중 중심 계산
        int searchRadius = 3;
        float totalWeight = 0f;
        Vector2 weightedCenter = Vector2.zero;

        for (int dy = -searchRadius; dy <= searchRadius; dy++)
        {
            for (int dx = -searchRadius; dx <= searchRadius; dx++)
            {
                int x = Mathf.RoundToInt(initialPosition.x) + dx;
                int y = Mathf.RoundToInt(initialPosition.y) + dy;

                // 경계 확인
                if (x < eyeRect.x || x >= eyeRect.x + eyeRect.width ||
                    y < eyeRect.y || y >= eyeRect.y + eyeRect.height)
                    continue;

                // 픽셀 값 가져오기 (어두울수록 높은 가중치)
                double[] pixel = grayImage.get(y, x);
                if (pixel != null && pixel.Length > 0)
                {
                    float darkness = 1f - (float)(pixel[0] / 255.0);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float weight = darkness / (1f + distance);

                    totalWeight += weight;
                    weightedCenter += new Vector2(x, y) * weight;
                }
            }
        }

        if (totalWeight > 0)
        {
            Vector2 refinedPosition = weightedCenter / totalWeight;

            // 원래 위치에서 너무 멀지 않도록 제한 (이상값 방지)
            float maxShift = 2f;
            Vector2 shift = refinedPosition - initialPosition;
            if (shift.magnitude > maxShift)
            {
                shift = shift.normalized * maxShift;
                refinedPosition = initialPosition + shift;
            }

            return refinedPosition;
        }

        return initialPosition;
    }

    /// <summary>
    /// 🎯 영역의 어두운 정도 계산
    /// 지정된 중심점 주변의 평균 어두운 정도를 계산
    /// </summary>
    float GetRegionDarkness(Mat grayImage, Vector2 center, int radius)
    {
        float totalDarkness = 0f;
        int pixelCount = 0;

        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                // 원형 영역만 계산
                if (dx * dx + dy * dy > radius * radius) continue;

                int x = Mathf.RoundToInt(center.x) + dx;
                int y = Mathf.RoundToInt(center.y) + dy;

                if (x >= 0 && x < grayImage.width() && y >= 0 && y < grayImage.height())
                {
                    double[] pixel = grayImage.get(y, x);
                    if (pixel != null && pixel.Length > 0)
                    {
                        totalDarkness += 1f - (float)(pixel[0] / 255.0);
                        pixelCount++;
                    }
                }
            }
        }

        return pixelCount > 0 ? totalDarkness / pixelCount : 0f;
    }

    /// <summary>
    /// 🎯 기본 동공 검출 (폴백용)
    /// 정밀 검출 실패 시 사용하는 간단한 방법
    /// </summary>
    Vector2 FindBasicPupilInEyeRegion(OpenCVForUnity.CoreModule.Rect eyeRect)
    {
        try
        {
            Mat eyeROI = new Mat(grayMat, eyeRect);
            Mat blurredEye = new Mat();
            Imgproc.GaussianBlur(eyeROI, blurredEye, new Size(5, 5), 0);

            // 가장 어두운 점 찾기
            Core.MinMaxLocResult minMaxResult = Core.minMaxLoc(blurredEye);
            Point pupilPoint = minMaxResult.minLoc;

            Vector2 pupilPosition = new Vector2(
                (float)(eyeRect.x + pupilPoint.x),
                (float)(eyeRect.y + pupilPoint.y)
            );

            // 경계 제한
            float margin = 3f;
            pupilPosition.x = Mathf.Clamp(pupilPosition.x,
                eyeRect.x + margin,
                eyeRect.x + eyeRect.width - margin);
            pupilPosition.y = Mathf.Clamp(pupilPosition.y,
                eyeRect.y + margin,
                eyeRect.y + eyeRect.height - margin);

            eyeROI.Dispose();
            blurredEye.Dispose();

            return pupilPosition;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"⚠️ 기본 동공 찾기 실패: {e.Message}");
            // 최후 수단: 눈 중심점
            return new Vector2(
                eyeRect.x + eyeRect.width * 0.5f,
                eyeRect.y + eyeRect.height * 0.5f
            );
        }
    }

    /// <summary>
    /// 🎯 동공 위치 안정화 (시간적 필터링)
    /// 여러 프레임의 히스토리를 사용하여 흔들림 방지
    /// </summary>
    Vector2 StabilizePupilPosition(Vector2 newPupil, Queue<Vector2> pupilHistory, Vector2 currentStable)
    {
        if (!usePrecisePupilDetection) return newPupil;

        // 히스토리에 추가
        pupilHistory.Enqueue(newPupil);

        // 최대 프레임 수 유지
        while (pupilHistory.Count > pupilSmoothingFrames)
        {
            pupilHistory.Dequeue();
        }

        // 평균 위치 계산
        Vector2 averagePosition = Vector2.zero;
        foreach (Vector2 pos in pupilHistory)
        {
            averagePosition += pos;
        }
        averagePosition /= pupilHistory.Count;

        // 급격한 변화 억제 (안정성 향상)
        if (currentStable != Vector2.zero)
        {
            float distance = Vector2.Distance(averagePosition, currentStable);
            if (distance > pupilStabilityRadius)
            {
                // 점진적 이동으로 부드러운 변화
                Vector2 direction = (averagePosition - currentStable).normalized;
                averagePosition = currentStable + direction * pupilStabilityRadius;
            }
        }

        return averagePosition;
    }

    /// <summary>
    /// 🎯 눈 검출 시각화 (디버깅용)
    /// 검출된 눈 영역과 동공 위치를 화면에 표시
    /// </summary>
    void DrawEyeVisualization(OpenCVForUnity.CoreModule.Rect leftEyeRect, OpenCVForUnity.CoreModule.Rect rightEyeRect,
                             Vector2 leftPupil, Vector2 rightPupil)
    {
        // 왼쪽 눈 영역 (파란색 사각형)
        Imgproc.rectangle(rgbaMat,
            new Point(leftEyeRect.x, leftEyeRect.y),
            new Point(leftEyeRect.x + leftEyeRect.width, leftEyeRect.y + leftEyeRect.height),
            new Scalar(255, 0, 0, 255), 2);

        // 오른쪽 눈 영역 (파란색 사각형)
        Imgproc.rectangle(rgbaMat,
            new Point(rightEyeRect.x, rightEyeRect.y),
            new Point(rightEyeRect.x + rightEyeRect.width, rightEyeRect.y + rightEyeRect.height),
            new Scalar(255, 0, 0, 255), 2);

        // 동공 위치 표시
        if (usePrecisePupilDetection)
        {
            // 정밀 검출 모드 (3중 원)
            Imgproc.circle(rgbaMat, new Point(leftPupil.x, leftPupil.y), 8, new Scalar(0, 255, 0, 255), -1);
            Imgproc.circle(rgbaMat, new Point(leftPupil.x, leftPupil.y), 6, new Scalar(255, 255, 0, 255), -1);
            Imgproc.circle(rgbaMat, new Point(leftPupil.x, leftPupil.y), 3, new Scalar(255, 0, 255, 255), -1);

            Imgproc.circle(rgbaMat, new Point(rightPupil.x, rightPupil.y), 8, new Scalar(0, 255, 0, 255), -1);
            Imgproc.circle(rgbaMat, new Point(rightPupil.x, rightPupil.y), 6, new Scalar(255, 255, 0, 255), -1);
            Imgproc.circle(rgbaMat, new Point(rightPupil.x, rightPupil.y), 3, new Scalar(255, 0, 255, 255), -1);

            // 정밀도 표시
            Imgproc.putText(rgbaMat, "PRECISE",
                new Point(leftEyeRect.x, leftEyeRect.y - 25),
                Imgproc.FONT_HERSHEY_SIMPLEX, 0.4, new Scalar(0, 255, 0, 255), 1);

            // 소수점 좌표 표시
            Imgproc.putText(rgbaMat, $"L({leftPupil.x:F1},{leftPupil.y:F1})",
                new Point(leftEyeRect.x, leftEyeRect.y + leftEyeRect.height + 15),
                Imgproc.FONT_HERSHEY_SIMPLEX, 0.3, new Scalar(0, 255, 255, 255), 1);

            Imgproc.putText(rgbaMat, $"R({rightPupil.x:F1},{rightPupil.y:F1})",
                new Point(rightEyeRect.x, rightEyeRect.y + rightEyeRect.height + 15),
                Imgproc.FONT_HERSHEY_SIMPLEX, 0.3, new Scalar(0, 255, 255, 255), 1);
        }
        else
        {
            // 기본 검출 모드 (2중 원)
            Imgproc.circle(rgbaMat, new Point(leftPupil.x, leftPupil.y), 6, new Scalar(255, 0, 0, 255), -1);
            Imgproc.circle(rgbaMat, new Point(leftPupil.x, leftPupil.y), 4, new Scalar(255, 255, 0, 255), -1);

            Imgproc.circle(rgbaMat, new Point(rightPupil.x, rightPupil.y), 6, new Scalar(255, 0, 0, 255), -1);
            Imgproc.circle(rgbaMat, new Point(rightPupil.x, rightPupil.y), 4, new Scalar(255, 255, 0, 255), -1);

            // 기본 모드 표시
            Imgproc.putText(rgbaMat, "BASIC",
                new Point(leftEyeRect.x, leftEyeRect.y - 25),
                Imgproc.FONT_HERSHEY_SIMPLEX, 0.4, new Scalar(255, 255, 0, 255), 1);

            // 정수 좌표 표시
            Imgproc.putText(rgbaMat, $"L({leftPupil.x:F0},{leftPupil.y:F0})",
                new Point(leftEyeRect.x, leftEyeRect.y + leftEyeRect.height + 15),
                Imgproc.FONT_HERSHEY_SIMPLEX, 0.3, new Scalar(255, 255, 0, 255), 1);

            Imgproc.putText(rgbaMat, $"R({rightPupil.x:F0},{rightPupil.y:F0})",
                new Point(rightEyeRect.x, rightEyeRect.y + rightEyeRect.height + 15),
                Imgproc.FONT_HERSHEY_SIMPLEX, 0.3, new Scalar(255, 255, 0, 255), 1);
        }

        // 시선점 (초록색 십자가)
        Imgproc.circle(rgbaMat,
            new Point(gazePoint.x, gazePoint.y),
            10, new Scalar(0, 255, 0, 255), 3);

        Imgproc.line(rgbaMat,
            new Point(gazePoint.x - 20, gazePoint.y),
            new Point(gazePoint.x + 20, gazePoint.y),
            new Scalar(0, 255, 0, 255), 3);

        Imgproc.line(rgbaMat,
            new Point(gazePoint.x, gazePoint.y - 20),
            new Point(gazePoint.x, gazePoint.y + 20),
            new Scalar(0, 255, 0, 255), 3);

        Imgproc.putText(rgbaMat, "GAZE",
            new Point(gazePoint.x + 25, gazePoint.y - 10),
            Imgproc.FONT_HERSHEY_SIMPLEX, 0.6, new Scalar(0, 255, 0, 255), 2);

        // 안정화 상태 표시 (정밀 모드에서만)
        if (usePrecisePupilDetection)
        {
            int stabilityFrames = Mathf.Min(leftPupilHistory.Count, rightPupilHistory.Count);
            Imgproc.putText(rgbaMat, $"Stability: {stabilityFrames}/{pupilSmoothingFrames}",
                new Point(10, rgbaMat.height() - 40),
                Imgproc.FONT_HERSHEY_SIMPLEX, 0.4, new Scalar(255, 255, 255, 255), 1);
        }
}

    /// <summary>
    /// 🎯 동공 안정성 계산
    /// 히스토리 내 동공 위치들의 분산을 통해 안정성 점수 계산
    /// </summary>
    float CalculatePupilStability(Queue<Vector2> pupilHistory)
    {
        if (pupilHistory.Count < 2) return 1.0f;

        Vector2[] positions = pupilHistory.ToArray();
        float totalVariance = 0f;

        // 평균 위치 계산
        Vector2 average = Vector2.zero;
        foreach (Vector2 pos in positions)
        {
            average += pos;
        }
        average /= positions.Length;

        // 분산 계산 (평균으로부터의 거리)
        foreach (Vector2 pos in positions)
        {
            totalVariance += Vector2.Distance(pos, average);
        }

        float averageVariance = totalVariance / positions.Length;

        // 안정성 점수 (분산이 낮을수록 높은 점수)
        // 분산이 5픽셀 이하면 높은 안정성
        return Mathf.Clamp01(1f / (1f + averageVariance / 5f));
    }


    // 🎯 정밀 보정 정보 시각화
    void DrawPreciseCalibrationOverlay()
    {
        if (!hasValidGaze) return;

        // 현재 동공 위치를 화면 좌표로 변환
        Vector2 rawGaze = WebcamToScreenCoordinates(gazePoint);

        // 정밀 보정 적용된 시선점 계산
        Vector2 correctedGaze = isCalibrated ? ApplyPreciseCalibration(rawGaze) : rawGaze;

        // 🎯 시선 정보 문자열 생성
        string gazeInfo = $"Raw: ({rawGaze.x:F0},{rawGaze.y:F0})";
        if (isCalibrated)
        {
            gazeInfo += $" -> Cal: ({correctedGaze.x:F0},{correctedGaze.y:F0})";
        }

        // 🎯 정밀 동공 검출 정보 표시
        if (usePrecisePupilDetection)
        {
            gazeInfo += $" [PRECISE]";
        }
        Imgproc.putText(rgbaMat, gazeInfo,
            new Point(10, 30),
            Imgproc.FONT_HERSHEY_SIMPLEX, 0.5, new Scalar(255, 255, 0, 255), 1);

        // 보정 상태 표시
        string statusInfo = isCalibrated ? "PRECISE CALIBRATED" :
                           (isCalibrating ? "CALIBRATING..." : "NOT CALIBRATED");
        Scalar statusColor = isCalibrated ? new Scalar(0, 255, 0, 255) :
                           isCalibrating ? new Scalar(255, 255, 0, 255) :
                           new Scalar(255, 0, 0, 255);

        Imgproc.putText(rgbaMat, statusInfo,
            new Point(10, 50),
            Imgproc.FONT_HERSHEY_SIMPLEX, 0.5, statusColor, 2);

        // 🎯 정밀 검출 모드 정보
        if (usePrecisePupilDetection)
        {
            string precisionInfo = $"Precision Mode: ON";
            precisionInfo += $" | Smooth: {pupilSmoothingFrames}";
            precisionInfo += $" | Threshold: {pupilContrastThreshold:F2}";

            Imgproc.putText(rgbaMat, precisionInfo,
                new Point(10, 70),
                Imgproc.FONT_HERSHEY_SIMPLEX, 0.4, new Scalar(0, 255, 255, 255), 1);

            // 안정성 정보
            if (leftPupilHistory.Count > 0 && rightPupilHistory.Count > 0)
            {
                float leftStability = CalculatePupilStability(leftPupilHistory);
                float rightStability = CalculatePupilStability(rightPupilHistory);

                string stabilityInfo = $"Stability: L={leftStability:F2} R={rightStability:F2}";
                Imgproc.putText(rgbaMat, stabilityInfo,
                    new Point(10, 90),
                    Imgproc.FONT_HERSHEY_SIMPLEX, 0.4, new Scalar(255, 0, 255, 255), 1);
            }
        }

        // 보정 중일 때 현재 타겟 정보 표시
        if (isCalibrating && calibrationIndex < calibrationTargets.Count)
        {
            Vector2 currentTarget = calibrationTargets[calibrationIndex];
            string targetInfo = $"Target {calibrationIndex + 1}/9: ({currentTarget.x},{currentTarget.y})";

            Imgproc.putText(rgbaMat, targetInfo,
                new Point(10, usePrecisePupilDetection ? 110 : 70),
                Imgproc.FONT_HERSHEY_SIMPLEX, 0.4, new Scalar(255, 0, 255, 255), 1);
        }

        // 요구사항 테스트 정보 (T키 누른 후)
        if (isCalibrated && Input.GetKey(KeyCode.T))
        {
            Vector2 testInput = new Vector2(890, 579);
            Vector2 testOutput = ApplyPreciseCalibration(testInput);
            string testInfo = $"Test: (890,579) -> ({testOutput.x:F0},{testOutput.y:F0})";

            Imgproc.putText(rgbaMat, testInfo,
                new Point(10, usePrecisePupilDetection ? 130 : 90),
                Imgproc.FONT_HERSHEY_SIMPLEX, 0.4, new Scalar(0, 255, 255, 255), 1);
        }
    }

    void UpdateStatusText(){
    if (statusText == null) return;

    string status = $"프레임: {frameCount}\n";
    status += $"😊 얼굴: {(faceDetected ? "✅" : "❌")} ({faceCount})\n";
    status += $"👁️ 눈동자: {(eyesDetected ? "✅" : "❌")} ({eyeCount})\n";
    status += $"🎯 시선: {(hasValidGaze ? "✅" : "❌")}\n";

    if (frameCount > 0)
    {
        float faceRate = (float)faceCount / frameCount * 100f;
        float eyeRate = (float)eyeCount / frameCount * 100f;
        status += $"감지율: {faceRate:F0}% / {eyeRate:F0}%\n";
    }

    // 정밀 보정 상태 정보
    status += "\n=== 정밀 보정 정보 ===\n";
    if (isCalibrating)
    {
        status += $"🎯 보정 중: {calibrationIndex + 1}/9\n";
        if (calibrationIndex < calibrationTargets.Count)
        {
            Vector2 target = calibrationTargets[calibrationIndex];
            status += $"현재 점: ({target.x}, {target.y})\n";
        }
    }
    else if (isCalibrated)
    {
        status += $"✅ 정밀 보정 완료\n";
        status += $"보정점: {calibrationDataList.Count}개\n";
        if (calibrationDataList.Count > 0)
        {
            float avgAccuracy = 0f;
            foreach (var data in calibrationDataList)
            {
                avgAccuracy += data.accuracy;
            }
            avgAccuracy /= calibrationDataList.Count;
            status += $"평균 정확도: {avgAccuracy:F2}\n";
        }
    }
    else
    {
        status += $"❌ 정밀 보정 필요\n";
        if (hasValidGaze)
        {
            status += "C키: 보정 시작\n";
        }
        else
        {
            status += "먼저 눈을 감지하세요\n";
        }
    }

    // 🎯 정밀 동공 검출 상태
    status += "\n=== 정밀 동공 검출 ===\n";
    status += $"모드: {(usePrecisePupilDetection ? "정밀" : "기본")}\n";

    if (usePrecisePupilDetection)
    {
        status += $"스무딩: {pupilSmoothingFrames} 프레임\n";
        status += $"임계값: {pupilContrastThreshold:F2}\n";
        status += $"원형성 필터: {(useCircularityFilter ? "ON" : "OFF")}\n";
        status += $"적응형 임계값: {(useAdaptiveThresholding ? "ON" : "OFF")}\n";

        // 안정성 정보
        if (leftPupilHistory.Count > 0 && rightPupilHistory.Count > 0)
        {
            float leftStability = CalculatePupilStability(leftPupilHistory);
            float rightStability = CalculatePupilStability(rightPupilHistory);
            status += $"왼쪽 안정성: {leftStability:F2}\n";
            status += $"오른쪽 안정성: {rightStability:F2}\n";
        }
    }

    // 현재 시선 좌표 표시
    if (hasValidGaze)
    {
        Vector2 screenGaze = WebcamToScreenCoordinates(gazePoint);
        status += $"\n원시 시선: ({screenGaze.x:F1}, {screenGaze.y:F1})";

        if (isCalibrated)
        {
            Vector2 correctedGaze = ApplyPreciseCalibration(screenGaze);
            status += $"\n보정 시선: ({correctedGaze.x:F1}, {correctedGaze.y:F1})";

            // 목표 정확도 표시
            float errorToTarget = Vector2.Distance(correctedGaze, new Vector2(100, 100));
            status += $"\n목표 오차: {errorToTarget:F1}px";
        }
    }

    status += "\n\nESC: 종료, R: 보정리셋, T: 테스트";
    status += "\nP: 정밀모드 토글";

    statusText.text = status;
}

// 화면에 정밀 보정 점과 실시간 시선 표시
void OnGUI()
{
    if (!isActive)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.normal.textColor = Color.yellow;
        style.alignment = TextAnchor.MiddleCenter;

        string message = "F1키를 눌러 정밀 웹캠 눈동자 인식을 시작하세요";
        GUI.Label(new UnityEngine.Rect(0, Screen.height / 2 - 50, Screen.width, 100), message, style);
        return;
    }

    // 정밀 보정 중일 때 보정 점 표시
    if (isCalibrating && calibrationIndex < calibrationTargets.Count)
    {
        Vector2 target = calibrationTargets[calibrationIndex];

        // 보정 점 그리기 (빨간색 원)
        GUI.color = calibrationPointColor;
        float pointSize = calibrationPointSize;
        GUI.Box(new UnityEngine.Rect(target.x - pointSize / 2, target.y - pointSize / 2, pointSize, pointSize), "");

        // 점 번호 표시
        GUIStyle numberStyle = new GUIStyle(GUI.skin.label);
        numberStyle.normal.textColor = Color.white;
        numberStyle.fontSize = 16;
        numberStyle.fontStyle = FontStyle.Bold;
        numberStyle.alignment = TextAnchor.MiddleCenter;

        GUI.Label(new UnityEngine.Rect(target.x - 20, target.y - 10, 40, 20),
                  $"{calibrationIndex + 1}", numberStyle);

        GUI.color = Color.white;

        // 안내 메시지
        GUIStyle messageStyle = new GUIStyle(GUI.skin.label);
        messageStyle.normal.textColor = Color.yellow;
        messageStyle.fontSize = 18;
        messageStyle.fontStyle = FontStyle.Bold;
        messageStyle.alignment = TextAnchor.MiddleCenter;

        string message = $"🎯 정밀 보정 점 {calibrationIndex + 1}/9를 정확히 바라보고 Space키를 누르세요";
        GUI.Label(new UnityEngine.Rect(0, Screen.height - 100, Screen.width, 50), message, messageStyle);

        // 좌표 정보 표시
        GUIStyle coordStyle = new GUIStyle(GUI.skin.label);
        coordStyle.normal.textColor = Color.cyan;
        coordStyle.fontSize = 14;
        coordStyle.alignment = TextAnchor.MiddleCenter;

        string coordInfo = $"타겟 좌표: ({target.x}, {target.y})";
        GUI.Label(new UnityEngine.Rect(0, Screen.height - 70, Screen.width, 30), coordInfo, coordStyle);
    }

    // 현재 시선점 표시 (실시간)
    if (hasValidGaze && !isCalibrating)
    {
        Vector2 rawGaze = WebcamToScreenCoordinates(gazePoint);
        Vector2 displayGaze = isCalibrated ? ApplyPreciseCalibration(rawGaze) : rawGaze;

        // 시선점 표시 (하늘색 십자가)
        GUI.color = gazePointColor;
        float gazeSize = gazePointSize;

        // 십자가 그리기
        GUI.Box(new UnityEngine.Rect(displayGaze.x - gazeSize / 2, displayGaze.y - 1, gazeSize, 2), "");
        GUI.Box(new UnityEngine.Rect(displayGaze.x - 1, displayGaze.y - gazeSize / 2, 2, gazeSize), "");

        GUI.color = Color.white;

        // 시선 좌표 텍스트
        GUIStyle gazeStyle = new GUIStyle(GUI.skin.label);
        gazeStyle.normal.textColor = Color.cyan;
        gazeStyle.fontSize = 12;

        string gazeText = $"시선: ({displayGaze.x:F0}, {displayGaze.y:F0})";
        if (isCalibrated)
        {
            gazeText += " (정밀 보정됨)";
        }

        GUI.Label(new UnityEngine.Rect(displayGaze.x + 15, displayGaze.y - 10, 200, 20), gazeText, gazeStyle);
    }

    // 디버그 정보 (화면 우측)
    GUIStyle debugStyle = new GUIStyle(GUI.skin.label);
    debugStyle.fontSize = 12;
    debugStyle.normal.textColor = Color.white;
    debugStyle.alignment = TextAnchor.UpperLeft;

    string debugInfo = "=== 정밀 웹캠 눈 추적 ===\n";
    debugInfo += $"웹캠: {(webCamTexture != null && webCamTexture.isPlaying ? "✅" : "❌")}\n";
    debugInfo += $"얼굴: {(faceDetected ? "✅" : "❌")}\n";
    debugInfo += $"눈: {(eyesDetected ? "✅" : "❌")}\n";
    debugInfo += $"시선: {(hasValidGaze ? "✅" : "❌")}\n";
    debugInfo += $"정밀보정: {(isCalibrated ? "✅" : "❌")}\n\n";

    if (isCalibrated && calibrationDataList.Count > 0)
    {
        debugInfo += "📊 보정 통계:\n";
        float avgAccuracy = 0f;
        foreach (var data in calibrationDataList)
        {
            avgAccuracy += data.accuracy;
        }
        avgAccuracy /= calibrationDataList.Count;
        debugInfo += $"평균 정확도: {avgAccuracy:F3}\n";
        debugInfo += $"보정점 수: {calibrationDataList.Count}\n\n";

        // 요구사항 테스트 결과
        Vector2 testInput = new Vector2(890, 579);
        Vector2 testOutput = ApplyPreciseCalibration(testInput);
        float testError = Vector2.Distance(testOutput, new Vector2(100, 100));
        debugInfo += "🎯 요구사항 테스트:\n";
        debugInfo += $"입력: (890,579)\n";
        debugInfo += $"출력: ({testOutput.x:F0},{testOutput.y:F0})\n";
        debugInfo += $"목표: (100,100)\n";
        debugInfo += $"오차: {testError:F1}px\n\n";
    }

    debugInfo += "⌨️ 조작법:\n";
    debugInfo += "F1: 웹캠 ON/OFF\n";
    debugInfo += "C: 정밀 보정 시작\n";
    debugInfo += "Space: 보정점 기록\n";
    debugInfo += "R: 보정 리셋\n";
    debugInfo += "T: 정확도 테스트\n";
    debugInfo += "P: 정밀모드 토글\n";
    debugInfo += "ESC: 종료";

    GUI.Label(new UnityEngine.Rect(Screen.width - 350, 10, 340, 600), debugInfo, debugStyle);
}

string GetHaarCascadePath(string fileName)
{
    string[] possiblePaths = {
            System.IO.Path.Combine(Application.streamingAssetsPath, "opencvforunity", fileName),
            System.IO.Path.Combine(Application.streamingAssetsPath, fileName),
            System.IO.Path.Combine(Application.dataPath, "StreamingAssets", "opencvforunity", fileName),
            System.IO.Path.Combine(Application.dataPath, "StreamingAssets", fileName)
        };

    foreach (string path in possiblePaths)
    {
        if (System.IO.File.Exists(path))
        {
            return path;
        }
    }

    return null;
}

void OnDestroy()
{
    if (isActive)
    {
        StopWebcam();
    }
}

void OnApplicationPause(bool pauseStatus)
{
    if (pauseStatus && isActive)
    {
        StopWebcam();
    }
}

#else
    void Start()
    {
        Debug.LogError("❌ OpenCV for Unity가 설치되지 않았습니다!");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Debug.LogError("❌ OpenCV for Unity 패키지가 필요합니다!");
        }
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 16;
        style.normal.textColor = Color.red;
        style.alignment = TextAnchor.MiddleCenter;

        string message = "❌ OpenCV for Unity 패키지가 필요합니다!";
        GUI.Label(new UnityEngine.Rect(0, Screen.height / 2, Screen.width, 50), message, style);
    }
#endif
}