using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;

#if OPENCV_FOR_UNITY
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityUtils;
#endif

/// <summary>
/// 개인 맞춤형 Eye Tracking 시스템
/// - 개인별 눈동자 데이터 수집 및 학습
/// - 실시간 캘리브레이션 및 정확도 개선
/// - 기계학습 기반 예측 모델
/// </summary>
public class PersonalizedEyeTracker : MonoBehaviour
{
    [Header("기본 설정")]
    public Vector2 webcamSize = new Vector2(640, 480);

    [Header("학습 설정")]
    [SerializeField] private int minTrainingSamples = 100;
    [SerializeField] private float learningRate = 0.1f;
    [SerializeField] private bool continuousLearning = true;

    [Header("동공 검출 설정")]
    [SerializeField] private float pupilSmoothingFactor = 0.3f;
    [SerializeField] private int historySize = 10;

    [Header("UI 설정")]
    [SerializeField] private bool showWebcamFeed = true;
    [SerializeField] private bool showEyeDetection = true;

    [Header("검출 파라미터")]
    [SerializeField] private float faceScaleFactor = 1.1f;
    [SerializeField] private int faceMinNeighbors = 2;
    [SerializeField] private float eyeScaleFactor = 1.05f;
    [SerializeField] private int eyeMinNeighbors = 3;

#if OPENCV_FOR_UNITY
    // OpenCV 기본 변수
    private WebCamTexture webCamTexture;
    private Mat rgbaMat;
    private Mat grayMat;
    private Texture2D displayTexture;
    private CascadeClassifier faceCascade;
    private CascadeClassifier eyeCascade;
    private bool isActive = false;

    // UI 요소
    private GameObject webcamUI;
    private UnityEngine.UI.RawImage webcamDisplay;

    // 눈 추적 데이터
    private Vector2 leftPupil = Vector2.zero;
    private Vector2 rightPupil = Vector2.zero;
    private Vector2 currentGaze = Vector2.zero;
    private bool hasValidGaze = false;

    // 개인별 학습 데이터
    private PersonalEyeModel personalModel;
    private List<TrainingData> trainingDataset = new List<TrainingData>();
    private Queue<Vector2> leftPupilHistory = new Queue<Vector2>();
    private Queue<Vector2> rightPupilHistory = new Queue<Vector2>();

    // 캘리브레이션
    private bool isCalibrating = false;
    private int calibrationStep = 0;
    private Vector2[] calibrationPoints = new Vector2[] {
        new Vector2(0.1f, 0.1f), new Vector2(0.5f, 0.1f), new Vector2(0.9f, 0.1f),
        new Vector2(0.1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.9f, 0.5f),
        new Vector2(0.1f, 0.9f), new Vector2(0.5f, 0.9f), new Vector2(0.9f, 0.9f)
    };

    // 실시간 정확도 추적
    private float currentAccuracy = 0f;
    private int frameCount = 0;

    /// <summary>
    /// 학습 데이터 구조체
    /// </summary>
    [Serializable]
    public class TrainingData
    {
        public Vector2 leftPupilPos;
        public Vector2 rightPupilPos;
        public float pupilDistance;
        public Vector2 targetScreenPos;
        public float timestamp;
        public float confidence;

        public TrainingData(Vector2 left, Vector2 right, Vector2 target)
        {
            leftPupilPos = left;
            rightPupilPos = right;
            pupilDistance = Vector2.Distance(left, right);
            targetScreenPos = target;
            timestamp = Time.time;
            confidence = 1f;
        }
    }

    /// <summary>
    /// 개인별 눈 모델 (간단한 신경망 구조)
    /// </summary>
    [Serializable]
    public class PersonalEyeModel
    {
        // 가중치 행렬 (입력: 4개 [leftX, leftY, rightX, rightY], 출력: 2개 [screenX, screenY])
        public float[,] weightsInputHidden = new float[4, 8];
        public float[,] weightsHiddenOutput = new float[8, 2];
        public float[] biasHidden = new float[8];
        public float[] biasOutput = new float[2];

        // 정규화 파라미터
        public Vector2 inputMean = Vector2.zero;
        public Vector2 inputStd = Vector2.one;
        public Vector2 outputMean = Vector2.zero;
        public Vector2 outputStd = Vector2.one;

        // 개인별 특성
        public float avgPupilDistance = 60f;
        public float eyeAspectRatio = 1f;
        public int trainingSamples = 0;

        public PersonalEyeModel()
        {
            InitializeWeights();
        }

        void InitializeWeights()
        {
            System.Random rand = new System.Random();

            // Xavier 초기화
            float inputSize = 4f;
            float hiddenSize = 8f;

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 8; j++)
                {
                    weightsInputHidden[i, j] = (float)(rand.NextDouble() * 2 - 1) * Mathf.Sqrt(2f / inputSize);
                }
            }

            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    weightsHiddenOutput[i, j] = (float)(rand.NextDouble() * 2 - 1) * Mathf.Sqrt(2f / hiddenSize);
                }
            }
        }

        public Vector2 Predict(Vector2 leftPupil, Vector2 rightPupil)
        {
            // 입력 정규화
            float[] input = new float[] {
                (leftPupil.x - inputMean.x) / inputStd.x,
                (leftPupil.y - inputMean.y) / inputStd.y,
                (rightPupil.x - inputMean.x) / inputStd.x,
                (rightPupil.y - inputMean.y) / inputStd.y
            };

            // Hidden Layer
            float[] hidden = new float[8];
            for (int j = 0; j < 8; j++)
            {
                hidden[j] = biasHidden[j];
                for (int i = 0; i < 4; i++)
                {
                    hidden[j] += input[i] * weightsInputHidden[i, j];
                }
                hidden[j] = Mathf.Max(0, hidden[j]); // ReLU
            }

            // Output Layer
            float[] output = new float[2];
            for (int j = 0; j < 2; j++)
            {
                output[j] = biasOutput[j];
                for (int i = 0; i < 8; i++)
                {
                    output[j] += hidden[i] * weightsHiddenOutput[i, j];
                }
            }

            // 역정규화
            return new Vector2(
                output[0] * outputStd.x + outputMean.x,
                output[1] * outputStd.y + outputMean.y
            );
        }

        public void UpdateWeights(Vector2 leftPupil, Vector2 rightPupil, Vector2 target, float learningRate)
        {
            // 간단한 역전파 구현
            Vector2 prediction = Predict(leftPupil, rightPupil);
            Vector2 error = target - prediction;

            // 그래디언트 계산 및 가중치 업데이트 (단순화된 버전)
            float errorMagnitude = error.magnitude;
            if (errorMagnitude > 0.01f)
            {
                // 출력층 가중치 조정
                for (int i = 0; i < 8; i++)
                {
                    weightsHiddenOutput[i, 0] += learningRate * error.x * 0.01f;
                    weightsHiddenOutput[i, 1] += learningRate * error.y * 0.01f;
                }

                // 바이어스 조정
                biasOutput[0] += learningRate * error.x * 0.001f;
                biasOutput[1] += learningRate * error.y * 0.001f;
            }

            trainingSamples++;
        }
    }

    void Start()
    {
        Debug.Log("🎯 개인 맞춤형 Eye Tracking 시스템 시작");
        Debug.Log("📖 사용법:");
        Debug.Log("   Space: 웹캠 시작/정지");
        Debug.Log("   C: 캘리브레이션 시작");
        Debug.Log("   S: 학습 데이터 저장");
        Debug.Log("   L: 학습 데이터 로드");
        Debug.Log("   R: 모델 리셋");

        personalModel = new PersonalEyeModel();
        LoadOpenCVModels();

        // 저장된 개인 데이터 로드 시도
        LoadPersonalData();
    }

    void Update()
    {
        // 키 입력 처리
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isActive) StopTracking();
            else StartTracking();
        }

        if (isActive)
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                StartCalibration();
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                SavePersonalData();
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
                LoadPersonalData();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetModel();
            }

            // 캘리브레이션 중 클릭으로 포인트 기록
            if (isCalibrating && Input.GetMouseButtonDown(0))
            {
                RecordCalibrationPoint();
            }
        }

        // 웹캠 프레임 처리
        if (isActive && webCamTexture != null && webCamTexture.isPlaying)
        {
            ProcessFrame();
        }
    }

    void LoadOpenCVModels()
    {
        try
        {
            // 다양한 경로와 방법 시도
            string[] possiblePaths = {
                Application.streamingAssetsPath,
                Path.Combine(Application.streamingAssetsPath, "OpenCVForUnity"),
                Path.Combine(Application.streamingAssetsPath, "OpenCVForUnity", "objdetect"),
                Path.Combine(Application.streamingAssetsPath, "haarcascades"),
                Path.Combine(Application.dataPath, "StreamingAssets"),
                Path.Combine(Application.dataPath, "StreamingAssets", "OpenCVForUnity"),
                Path.Combine(Application.dataPath, "StreamingAssets", "haarcascades")
            };

            string[] faceModelNames = {
                "haarcascade_frontalface_alt.xml",
                "haarcascade_frontalface_default.xml",
                "haarcascade_frontalface_alt2.xml",
                "lbpcascade_frontalface.xml"
            };

            string[] eyeModelNames = {
                "haarcascade_eye.xml",
                "haarcascade_eye_tree_eyeglasses.xml",
                "haarcascade_lefteye_2splits.xml",
                "haarcascade_righteye_2splits.xml"
            };

            // 가능한 모든 경로 출력
            Debug.Log("🔍 Haar Cascade 파일 검색 중...");
            Debug.Log($"StreamingAssets 경로: {Application.streamingAssetsPath}");

            // 얼굴 모델 찾기
            bool faceModelFound = false;
            foreach (string basePath in possiblePaths)
            {
                foreach (string modelName in faceModelNames)
                {
                    string fullPath = Path.Combine(basePath, modelName);

                    if (File.Exists(fullPath))
                    {
                        Debug.Log($"📁 파일 발견: {fullPath}");
                        faceCascade = new CascadeClassifier(fullPath);

                        if (!faceCascade.empty())
                        {
                            Debug.Log($"✅ 얼굴 감지 모델 로드 성공: {modelName}");
                            faceModelFound = true;
                            break;
                        }
                        else
                        {
                            faceCascade.Dispose();
                            faceCascade = null;
                        }
                    }
                }
                if (faceModelFound) break;
            }

            // Utils.getFilePath 시도 (OpenCV for Unity 방식)
            if (!faceModelFound)
            {
                foreach (string modelName in faceModelNames)
                {
                    try
                    {
                        string path = Utils.getFilePath("OpenCVForUnity/objdetect/haarcascades/" + modelName);
                        if (!string.IsNullOrEmpty(path))
                        {
                            faceCascade = new CascadeClassifier(path);
                            if (!faceCascade.empty())
                            {
                                Debug.Log($"✅ 얼굴 감지 모델 로드 성공 (Utils): {modelName}");
                                faceModelFound = true;
                                break;
                            }
                        }
                    }
                    catch { }

                    try
                    {
                        string path = Utils.getFilePath(modelName);
                        if (!string.IsNullOrEmpty(path))
                        {
                            faceCascade = new CascadeClassifier(path);
                            if (!faceCascade.empty())
                            {
                                Debug.Log($"✅ 얼굴 감지 모델 로드 성공 (Utils): {modelName}");
                                faceModelFound = true;
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }

            // 눈 모델 찾기
            bool eyeModelFound = false;
            foreach (string basePath in possiblePaths)
            {
                foreach (string modelName in eyeModelNames)
                {
                    string fullPath = Path.Combine(basePath, modelName);

                    if (File.Exists(fullPath))
                    {
                        Debug.Log($"📁 파일 발견: {fullPath}");
                        eyeCascade = new CascadeClassifier(fullPath);

                        if (!eyeCascade.empty())
                        {
                            Debug.Log($"✅ 눈 감지 모델 로드 성공: {modelName}");
                            eyeModelFound = true;
                            break;
                        }
                        else
                        {
                            eyeCascade.Dispose();
                            eyeCascade = null;
                        }
                    }
                }
                if (eyeModelFound) break;
            }

            // Utils.getFilePath 시도 (눈 모델)
            if (!eyeModelFound)
            {
                foreach (string modelName in eyeModelNames)
                {
                    try
                    {
                        string path = Utils.getFilePath("OpenCVForUnity/objdetect/haarcascades/" + modelName);
                        if (!string.IsNullOrEmpty(path))
                        {
                            eyeCascade = new CascadeClassifier(path);
                            if (!eyeCascade.empty())
                            {
                                Debug.Log($"✅ 눈 감지 모델 로드 성공 (Utils): {modelName}");
                                eyeModelFound = true;
                                break;
                            }
                        }
                    }
                    catch { }

                    try
                    {
                        string path = Utils.getFilePath(modelName);
                        if (!string.IsNullOrEmpty(path))
                        {
                            eyeCascade = new CascadeClassifier(path);
                            if (!eyeCascade.empty())
                            {
                                Debug.Log($"✅ 눈 감지 모델 로드 성공 (Utils): {modelName}");
                                eyeModelFound = true;
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }

            // 최종 확인
            if (!faceModelFound || faceCascade == null || faceCascade.empty())
            {
                Debug.LogError("❌ 얼굴 감지 모델을 찾을 수 없습니다!");
                Debug.LogError("다음 위치 중 하나에 haarcascade_frontalface_default.xml 파일을 넣어주세요:");
                foreach (string path in possiblePaths)
                {
                    Debug.LogError($"  - {path}");
                }
            }

            if (!eyeModelFound || eyeCascade == null || eyeCascade.empty())
            {
                Debug.LogError("❌ 눈 감지 모델을 찾을 수 없습니다!");
                Debug.LogError("다음 위치 중 하나에 haarcascade_eye.xml 파일을 넣어주세요:");
                foreach (string path in possiblePaths)
                {
                    Debug.LogError($"  - {path}");
                }
            }

            // StreamingAssets 폴더의 파일 목록 출력 (디버깅용)
            if (Directory.Exists(Application.streamingAssetsPath))
            {
                Debug.Log($"📂 StreamingAssets 폴더 내용:");
                string[] files = Directory.GetFiles(Application.streamingAssetsPath, "*.xml", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    Debug.Log($"  - {file}");
                }
            }

        }
        catch (Exception e)
        {
            Debug.LogError($"❌ OpenCV 모델 로드 실패: {e.Message}");
            Debug.LogError($"스택 트레이스: {e.StackTrace}");
        }
    }

    void StartTracking()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            Debug.LogError("❌ 웹캠을 찾을 수 없습니다!");
            return;
        }

        webCamTexture = new WebCamTexture(devices[0].name, (int)webcamSize.x, (int)webcamSize.y, 30);
        webCamTexture.Play();

        CreateWebcamUI();
        StartCoroutine(InitializeMats());
        isActive = true;
        Debug.Log("📹 웹캠 시작");
    }

    void StopTracking()
    {
        isActive = false;

        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            Destroy(webCamTexture);
            webCamTexture = null;
        }

        if (rgbaMat != null) rgbaMat.Dispose();
        if (grayMat != null) grayMat.Dispose();
        if (displayTexture != null)
        {
            Destroy(displayTexture);
            displayTexture = null;
        }

        if (webcamUI != null)
        {
            Destroy(webcamUI);
            webcamUI = null;
        }

        Debug.Log("🛑 웹캠 정지");
    }

    void CreateWebcamUI()
    {
        // Canvas 찾기 또는 생성
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        // 웹캠 UI 패널 생성
        webcamUI = new GameObject("WebcamPanel");
        webcamUI.transform.SetParent(canvas.transform, false);

        // 배경 패널
        UnityEngine.UI.Image panelBg = webcamUI.AddComponent<UnityEngine.UI.Image>();
        panelBg.color = new Color(0, 0, 0, 0.8f);

        RectTransform panelRect = webcamUI.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(10, -10);
        panelRect.sizeDelta = new Vector2(webcamSize.x + 20, webcamSize.y + 20);

        // 웹캠 디스플레이
        GameObject displayObj = new GameObject("WebcamDisplay");
        displayObj.transform.SetParent(webcamUI.transform, false);

        webcamDisplay = displayObj.AddComponent<UnityEngine.UI.RawImage>();
        RectTransform displayRect = displayObj.GetComponent<RectTransform>();
        displayRect.anchorMin = new Vector2(0.5f, 0.5f);
        displayRect.anchorMax = new Vector2(0.5f, 0.5f);
        displayRect.pivot = new Vector2(0.5f, 0.5f);
        displayRect.anchoredPosition = Vector2.zero;
        displayRect.sizeDelta = webcamSize;

        Debug.Log("✅ 웹캠 UI 생성 완료");
    }

    System.Collections.IEnumerator InitializeMats()
    {
        while (!webCamTexture.isPlaying)
        {
            yield return null;
        }

        rgbaMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4);
        grayMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC1);
        displayTexture = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32, false);

        // 웹캠 텍스처를 UI에 연결
        if (webcamDisplay != null)
        {
            webcamDisplay.texture = displayTexture;
        }

        Debug.Log($"✅ OpenCV Mat 초기화: {webCamTexture.width}x{webCamTexture.height}");
    }

    void ProcessFrame()
    {
        if (rgbaMat == null || grayMat == null) return;

        frameCount++;

        // 웹캠 텍스처를 Mat으로 변환
        Utils.webCamTextureToMat(webCamTexture, rgbaMat);
        Imgproc.cvtColor(rgbaMat, grayMat, Imgproc.COLOR_RGBA2GRAY);

        // 얼굴 및 눈 검출
        DetectEyes();

        // 검출 결과 시각화
        if (showEyeDetection && hasValidGaze)
        {
            DrawEyeDetection();
        }

        // Mat을 텍스처로 변환하여 화면에 표시
        if (displayTexture != null && showWebcamFeed)
        {
            Utils.matToTexture2D(rgbaMat, displayTexture);
        }

        // 개인 모델로 시선 예측
        if (hasValidGaze && personalModel.trainingSamples > minTrainingSamples)
        {
            Vector2 predictedGaze = personalModel.Predict(leftPupil, rightPupil);
            currentGaze = predictedGaze;

            // 연속 학습
            if (continuousLearning && !isCalibrating)
            {
                ContinuousLearningUpdate();
            }
        }

        // 디버그 정보 업데이트
        if (frameCount % 30 == 0)
        {
            UpdateAccuracy();
        }
    }

    void DrawEyeDetection()
    {
        if (!hasValidGaze) return;

        // 왼쪽 눈 동공 표시 (녹색 원)
        Imgproc.circle(rgbaMat, new Point(leftPupil.x, leftPupil.y), 5, new Scalar(0, 255, 0, 255), -1);
        Imgproc.circle(rgbaMat, new Point(leftPupil.x, leftPupil.y), 7, new Scalar(0, 255, 0, 255), 2);

        // 오른쪽 눈 동공 표시 (녹색 원)
        Imgproc.circle(rgbaMat, new Point(rightPupil.x, rightPupil.y), 5, new Scalar(0, 255, 0, 255), -1);
        Imgproc.circle(rgbaMat, new Point(rightPupil.x, rightPupil.y), 7, new Scalar(0, 255, 0, 255), 2);

        // 두 눈 중심점 표시 (노란색)
        Vector2 center = (leftPupil + rightPupil) * 0.5f;
        Imgproc.circle(rgbaMat, new Point(center.x, center.y), 3, new Scalar(255, 255, 0, 255), -1);

        // 연결선 그리기
        Imgproc.line(rgbaMat, new Point(leftPupil.x, leftPupil.y),
                    new Point(rightPupil.x, rightPupil.y), new Scalar(0, 255, 255, 255), 1);

        // 상태 텍스트
        string status = $"Samples: {personalModel.trainingSamples}";
        Imgproc.putText(rgbaMat, status, new Point(10, 30),
                       Imgproc.FONT_HERSHEY_SIMPLEX, 0.7, new Scalar(255, 255, 0, 255), 2);

        if (personalModel.trainingSamples > minTrainingSamples)
        {
            string accuracy = $"Accuracy: {(currentAccuracy * 100):F1}%";
            Imgproc.putText(rgbaMat, accuracy, new Point(10, 60),
                           Imgproc.FONT_HERSHEY_SIMPLEX, 0.7, new Scalar(0, 255, 0, 255), 2);
        }
    }

    void DetectEyes()
    {
        hasValidGaze = false;

        if (faceCascade == null || eyeCascade == null)
        {
            Debug.LogWarning("⚠️ Cascade 분류기가 로드되지 않았습니다!");
            return;
        }

        // 얼굴 검출 - 파라미터 조정
        MatOfRect faces = new MatOfRect();
        faceCascade.detectMultiScale(
            grayMat,
            faces,
            1.1,     // scaleFactor - 더 작은 값으로 더 정밀한 검색
            2,       // minNeighbors - 더 작은 값으로 더 많은 검출
            0,       // flags
            new Size(grayMat.cols() / 8, grayMat.rows() / 8),  // 최소 크기를 더 작게
            new Size(grayMat.cols() / 2, grayMat.rows() / 2)   // 최대 크기
        );

        OpenCVForUnity.CoreModule.Rect[] faceArray = faces.toArray();
        Debug.Log($"검출된 얼굴 수: {faceArray.Length}");

        if (faceArray.Length > 0)
        {
            // 가장 큰 얼굴 선택
            OpenCVForUnity.CoreModule.Rect face = faceArray[0];
            for (int i = 1; i < faceArray.Length; i++)
            {
                if (faceArray[i].area() > face.area())
                {
                    face = faceArray[i];
                }
            }

            // 얼굴 영역 시각화 (디버깅용)
            if (showEyeDetection)
            {
                Imgproc.rectangle(rgbaMat,
                    new Point(face.x, face.y),
                    new Point(face.x + face.width, face.y + face.height),
                    new Scalar(0, 255, 0, 255), 2);
            }

            // 눈 영역에서 검출 - 얼굴 상단 50%에서 검색
            OpenCVForUnity.CoreModule.Rect eyeRegion = new OpenCVForUnity.CoreModule.Rect(
                face.x,
                face.y + (int)(face.height * 0.15f),  // 약간 아래에서 시작
                face.width,
                (int)(face.height * 0.5f)  // 얼굴 높이의 50%
            );

            Mat eyeROI = new Mat(grayMat, eyeRegion);
            MatOfRect eyes = new MatOfRect();

            // 눈 검출 파라미터 조정
            eyeCascade.detectMultiScale(
                eyeROI,
                eyes,
                1.05,    // 더 정밀한 검색
                3,       // minNeighbors
                0,
                new Size(eyeRegion.width / 10, eyeRegion.height / 5),   // 최소 크기
                new Size(eyeRegion.width / 3, eyeRegion.height / 2)     // 최대 크기
            );

            OpenCVForUnity.CoreModule.Rect[] eyeArray = eyes.toArray();
            Debug.Log($"검출된 눈 수: {eyeArray.Length}");

            if (eyeArray.Length >= 2)
            {
                // 눈 정렬 및 정렬
                System.Array.Sort(eyeArray, (a, b) => a.x.CompareTo(b.x));

                OpenCVForUnity.CoreModule.Rect leftEye = eyeArray[0];
                OpenCVForUnity.CoreModule.Rect rightEye = eyeArray[1];

                // 절대 좌표로 변환
                leftEye.x += eyeRegion.x;
                leftEye.y += eyeRegion.y;
                rightEye.x += eyeRegion.x;
                rightEye.y += eyeRegion.y;

                // 눈 영역 시각화
                if (showEyeDetection)
                {
                    Imgproc.rectangle(rgbaMat,
                        new Point(leftEye.x, leftEye.y),
                        new Point(leftEye.x + leftEye.width, leftEye.y + leftEye.height),
                        new Scalar(255, 0, 0, 255), 2);

                    Imgproc.rectangle(rgbaMat,
                        new Point(rightEye.x, rightEye.y),
                        new Point(rightEye.x + rightEye.width, rightEye.y + rightEye.height),
                        new Scalar(255, 0, 0, 255), 2);
                }

                // 동공 위치 찾기
                leftPupil = FindPupilCenter(leftEye);
                rightPupil = FindPupilCenter(rightEye);

                // 시간적 스무딩 적용
                leftPupil = SmoothPupilPosition(leftPupil, leftPupilHistory);
                rightPupil = SmoothPupilPosition(rightPupil, rightPupilHistory);

                hasValidGaze = true;
                Debug.Log($"✅ 눈 감지 성공! 왼쪽: ({leftPupil.x:F1}, {leftPupil.y:F1}), 오른쪽: ({rightPupil.x:F1}, {rightPupil.y:F1})");

                // 개인별 특성 업데이트
                UpdatePersonalCharacteristics();
            }
            else if (eyeArray.Length == 1)
            {
                Debug.Log("⚠️ 한쪽 눈만 감지됨");
            }

            eyeROI.Dispose();
            eyes.Dispose();
        }
        else
        {
            Debug.Log("⚠️ 얼굴이 감지되지 않음");
        }

        faces.Dispose();
    }

    Vector2 FindPupilCenter(OpenCVForUnity.CoreModule.Rect eyeRect)
    {
        // 더 안전한 경계 체크
        int x = Mathf.Max(0, eyeRect.x);
        int y = Mathf.Max(0, eyeRect.y);
        int width = Mathf.Min(eyeRect.width, grayMat.cols() - x);
        int height = Mathf.Min(eyeRect.height, grayMat.rows() - y);

        if (width <= 0 || height <= 0)
        {
            Debug.LogWarning("⚠️ 유효하지 않은 눈 영역");
            return new Vector2(eyeRect.x + eyeRect.width / 2, eyeRect.y + eyeRect.height / 2);
        }

        OpenCVForUnity.CoreModule.Rect safeEyeRect = new OpenCVForUnity.CoreModule.Rect(x, y, width, height);
        Mat eyeROI = new Mat(grayMat, safeEyeRect);
        Mat blurred = new Mat();
        Imgproc.GaussianBlur(eyeROI, blurred, new Size(5, 5), 0);

        // 가장 어두운 점 찾기
        Core.MinMaxLocResult minMax = Core.minMaxLoc(blurred);

        Vector2 pupilPos = new Vector2(
            (float)(safeEyeRect.x + minMax.minLoc.x),
            (float)(safeEyeRect.y + minMax.minLoc.y)
        );

        eyeROI.Dispose();
        blurred.Dispose();

        return pupilPos;
    }

    Vector2 SmoothPupilPosition(Vector2 newPos, Queue<Vector2> history)
    {
        history.Enqueue(newPos);

        while (history.Count > historySize)
        {
            history.Dequeue();
        }

        // 지수 이동 평균
        Vector2 smoothed = Vector2.zero;
        float weight = 1f;
        float totalWeight = 0f;

        foreach (Vector2 pos in history.Reverse())
        {
            smoothed += pos * weight;
            totalWeight += weight;
            weight *= (1f - pupilSmoothingFactor);
        }

        return smoothed / totalWeight;
    }

    void UpdatePersonalCharacteristics()
    {
        if (!hasValidGaze) return;

        // 동공 간 거리 업데이트
        float currentDistance = Vector2.Distance(leftPupil, rightPupil);
        personalModel.avgPupilDistance = Mathf.Lerp(
            personalModel.avgPupilDistance,
            currentDistance,
            0.01f
        );
    }

    void StartCalibration()
    {
        if (!hasValidGaze)
        {
            Debug.LogWarning("⚠️ 눈이 감지되지 않습니다!");
            return;
        }

        isCalibrating = true;
        calibrationStep = 0;
        trainingDataset.Clear();

        Debug.Log("🎯 캘리브레이션 시작 - 화면의 점을 클릭하세요");
    }

    void RecordCalibrationPoint()
    {
        if (!isCalibrating || !hasValidGaze) return;

        Vector2 screenPoint = new Vector2(
            calibrationPoints[calibrationStep].x * Screen.width,
            calibrationPoints[calibrationStep].y * Screen.height
        );

        // 학습 데이터 추가
        TrainingData data = new TrainingData(leftPupil, rightPupil, screenPoint);
        trainingDataset.Add(data);

        // 모델 업데이트
        personalModel.UpdateWeights(leftPupil, rightPupil, screenPoint, learningRate);

        Debug.Log($"✅ 캘리브레이션 포인트 {calibrationStep + 1}/9 기록됨");

        calibrationStep++;

        if (calibrationStep >= 9)
        {
            CompleteCalibration();
        }
    }

    void CompleteCalibration()
    {
        isCalibrating = false;

        // 정규화 파라미터 계산
        CalculateNormalizationParameters();

        Debug.Log($"✅ 캘리브레이션 완료! 학습 샘플: {personalModel.trainingSamples}");

        // 자동 저장
        SavePersonalData();
    }

    void CalculateNormalizationParameters()
    {
        if (trainingDataset.Count == 0) return;

        // 입력 통계 계산
        Vector2 sumInput = Vector2.zero;
        Vector2 sumOutput = Vector2.zero;

        foreach (var data in trainingDataset)
        {
            sumInput += (data.leftPupilPos + data.rightPupilPos) * 0.5f;
            sumOutput += data.targetScreenPos;
        }

        personalModel.inputMean = sumInput / trainingDataset.Count;
        personalModel.outputMean = sumOutput / trainingDataset.Count;

        // 표준편차 계산
        Vector2 varInput = Vector2.zero;
        Vector2 varOutput = Vector2.zero;

        foreach (var data in trainingDataset)
        {
            Vector2 inputCenter = (data.leftPupilPos + data.rightPupilPos) * 0.5f;
            varInput += Vector2.Scale(inputCenter - personalModel.inputMean, inputCenter - personalModel.inputMean);
            varOutput += Vector2.Scale(data.targetScreenPos - personalModel.outputMean, data.targetScreenPos - personalModel.outputMean);
        }

        personalModel.inputStd = new Vector2(
            Mathf.Sqrt(varInput.x / trainingDataset.Count),
            Mathf.Sqrt(varInput.y / trainingDataset.Count)
        );

        personalModel.outputStd = new Vector2(
            Mathf.Sqrt(varOutput.x / trainingDataset.Count),
            Mathf.Sqrt(varOutput.y / trainingDataset.Count)
        );

        // 0으로 나누기 방지
        personalModel.inputStd.x = Mathf.Max(personalModel.inputStd.x, 0.1f);
        personalModel.inputStd.y = Mathf.Max(personalModel.inputStd.y, 0.1f);
        personalModel.outputStd.x = Mathf.Max(personalModel.outputStd.x, 1f);
        personalModel.outputStd.y = Mathf.Max(personalModel.outputStd.y, 1f);
    }

    void ContinuousLearningUpdate()
    {
        // 사용자의 마우스 클릭을 암묵적 피드백으로 사용
        if (Input.GetMouseButton(0))
        {
            Vector3 mousePos = Input.mousePosition;
            Vector2 targetPos = new Vector2(mousePos.x, mousePos.y);

            // 예측과 실제 클릭 위치의 차이가 작으면 학습
            Vector2 prediction = personalModel.Predict(leftPupil, rightPupil);
            float error = Vector2.Distance(prediction, targetPos);

            if (error < 100f) // 100픽셀 이내면 유효한 샘플로 간주
            {
                personalModel.UpdateWeights(leftPupil, rightPupil, targetPos, learningRate * 0.1f);

                // 학습 데이터셋에도 추가
                TrainingData data = new TrainingData(leftPupil, rightPupil, targetPos);
                data.confidence = 1f - (error / 100f);
                trainingDataset.Add(data);

                // 메모리 관리 - 최대 1000개 샘플 유지
                if (trainingDataset.Count > 1000)
                {
                    trainingDataset.RemoveAt(0);
                }
            }
        }
    }

    void UpdateAccuracy()
    {
        if (trainingDataset.Count < 10) return;

        // 최근 10개 샘플의 정확도 계산
        float totalError = 0f;
        int sampleCount = Mathf.Min(10, trainingDataset.Count);

        for (int i = trainingDataset.Count - sampleCount; i < trainingDataset.Count; i++)
        {
            var data = trainingDataset[i];
            Vector2 prediction = personalModel.Predict(data.leftPupilPos, data.rightPupilPos);
            totalError += Vector2.Distance(prediction, data.targetScreenPos);
        }

        currentAccuracy = 1f - (totalError / (sampleCount * Screen.width));
        currentAccuracy = Mathf.Clamp01(currentAccuracy);
    }

    void SavePersonalData()
    {
        try
        {
            string json = JsonUtility.ToJson(personalModel);
            string path = Path.Combine(Application.persistentDataPath, "personal_eye_model.json");
            File.WriteAllText(path, json);

            Debug.Log($"✅ 개인 모델 저장 완료: {path}");
            Debug.Log($"   학습 샘플: {personalModel.trainingSamples}");
            Debug.Log($"   평균 동공 거리: {personalModel.avgPupilDistance:F1}");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 저장 실패: {e.Message}");
        }
    }

    void LoadPersonalData()
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "personal_eye_model.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                personalModel = JsonUtility.FromJson<PersonalEyeModel>(json);

                Debug.Log($"✅ 개인 모델 로드 완료");
                Debug.Log($"   학습 샘플: {personalModel.trainingSamples}");
                Debug.Log($"   평균 동공 거리: {personalModel.avgPupilDistance:F1}");
            }
            else
            {
                Debug.Log("📁 저장된 개인 모델이 없습니다");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 로드 실패: {e.Message}");
        }
    }

    void ResetModel()
    {
        personalModel = new PersonalEyeModel();
        trainingDataset.Clear();
        leftPupilHistory.Clear();
        rightPupilHistory.Clear();
        currentAccuracy = 0f;

        Debug.Log("🔄 모델 리셋 완료");
    }

    void OnGUI()
    {
        // 상태 표시
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.normal.textColor = Color.white;

        string status = "=== 개인 맞춤형 Eye Tracker ===\n";
        status += $"상태: {(isActive ? "활성" : "비활성")}\n";
        status += $"눈 감지: {(hasValidGaze ? "✅" : "❌")}\n";
        status += $"학습 샘플: {personalModel.trainingSamples}\n";
        status += $"정확도: {(currentAccuracy * 100):F1}%\n";
        status += $"FPS: {(1f / Time.deltaTime):F0}\n\n";

        status += "조작법:\n";
        status += "Space: 시작/정지\n";
        status += "C: 캘리브레이션\n";
        status += "S: 저장 / L: 로드\n";
        status += "R: 리셋\n";

        GUI.Label(new UnityEngine.Rect(10, 10, 300, 300), status, style);

        // 캘리브레이션 UI
        if (isCalibrating && calibrationStep < 9)
        {
            Vector2 point = calibrationPoints[calibrationStep];
            Vector2 screenPos = new Vector2(point.x * Screen.width, point.y * Screen.height);

            GUI.color = Color.red;
            GUI.Box(new UnityEngine.Rect(screenPos.x - 25, screenPos.y - 25, 50, 50), "");

            GUI.color = Color.white;
            string msg = $"점 {calibrationStep + 1}/9를 보고 클릭하세요";
            GUI.Label(new UnityEngine.Rect(Screen.width / 2 - 150, Screen.height - 50, 300, 30), msg, style);
        }

        // 시선 표시
        if (hasValidGaze && personalModel.trainingSamples > minTrainingSamples)
        {
            Vector2 gazePos = personalModel.Predict(leftPupil, rightPupil);

            GUI.color = new Color(0, 1, 1, 0.5f);
            GUI.Box(new UnityEngine.Rect(gazePos.x - 10, gazePos.y - 10, 20, 20), "");

            GUI.color = Color.white;
        }

        // 웹캠 표시 토글 버튼
        if (isActive)
        {
            if (GUI.Button(new UnityEngine.Rect(Screen.width - 150, 10, 140, 30),
                showWebcamFeed ? "웹캠 숨기기" : "웹캠 보이기"))
            {
                showWebcamFeed = !showWebcamFeed;
                if (webcamUI != null)
                {
                    webcamUI.SetActive(showWebcamFeed);
                }
            }
        }
    }

    void OnDestroy()
    {
        if (isActive)
        {
            StopTracking();
        }
    }
#else
    void Start()
    {
        Debug.LogError("❌ OpenCV for Unity가 설치되지 않았습니다!");
    }
    
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.normal.textColor = Color.red;
        style.alignment = TextAnchor.MiddleCenter;
        
        GUI.Label(new UnityEngine.Rect(0, Screen.height/2 - 50, Screen.width, 100),
                  "OpenCV for Unity 패키지가 필요합니다!", style);
    }
#endif
}