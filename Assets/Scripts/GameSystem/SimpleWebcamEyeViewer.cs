using UnityEngine;
using UnityEngine.UI;

#if OPENCV_FOR_UNITY
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.ObjdetectModule;
using OpenCVForUnity.UnityUtils;
#endif

/// <summary>
/// F1키 하나로 웹캠 화면과 눈동자 인식을 바로 볼 수 있는 간단한 뷰어
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

    void Start()
    {
        Debug.Log("🎮 F1키를 눌러 웹캠 눈동자 인식을 시작하세요!");
        LoadOpenCVModels();
    }

    void Update()
    {
        // F1키로 토글
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

        // 웹캠 프레임 처리
        if (isActive && webCamTexture != null && webCamTexture.isPlaying)
        {
            ProcessWebcamFrame();
        }
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
            // 이미 실행 중이면 무시
            if (isActive)
            {
                Debug.LogWarning("⚠️ 웹캠이 이미 실행 중입니다!");
                return;
            }

            // 웹캠 디바이스 확인
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogError("❌ 웹캠을 찾을 수 없습니다!");
                return;
            }

            Debug.Log($"📹 웹캠 발견: {devices[0].name}");

            // 기존 텍스처 정리
            if (webCamTexture != null)
            {
                webCamTexture.Stop();
                Destroy(webCamTexture);
                webCamTexture = null;
            }

            // UI 먼저 생성
            CreateWebcamUI();

            // 웹캠 시작
            webCamTexture = new WebCamTexture(devices[0].name, (int)webcamSize.x, (int)webcamSize.y, 30);

            if (webCamTexture == null)
            {
                Debug.LogError("❌ WebCamTexture 생성 실패!");
                return;
            }

            webCamTexture.Play();
            Debug.Log("📹 웹캠 시작 중...");

            // OpenCV Mat 초기화 (코루틴으로)
            StartCoroutine(InitializeOpenCVMats());

            isActive = true;
            Debug.Log("🎯 웹캠 눈동자 인식 시작! (ESC로 종료)");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 웹캠 시작 실패: {e.Message}");
            Debug.LogError($"스택 트레이스: {e.StackTrace}");

            // 실패 시 정리
            if (webCamTexture != null)
            {
                webCamTexture.Stop();
                Destroy(webCamTexture);
                webCamTexture = null;
            }
            isActive = false;
        }
    }

    System.Collections.IEnumerator InitializeOpenCVMats()
    {
        // 웹캠 텍스처가 null인지 확인
        if (webCamTexture == null)
        {
            Debug.LogError("❌ WebCamTexture가 null입니다!");
            yield break;
        }

        // 웹캠이 시작될 때까지 대기 (최대 10초)
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

        // 웹캠 크기가 유효한지 확인
        if (webCamTexture.width <= 0 || webCamTexture.height <= 0)
        {
            Debug.LogError($"❌ 웹캠 크기가 유효하지 않습니다: {webCamTexture.width}x{webCamTexture.height}");
            yield break;
        }

        try
        {
            // OpenCV Mat 초기화
            rgbaMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC4);
            grayMat = new Mat(webCamTexture.height, webCamTexture.width, CvType.CV_8UC1);
            displayTexture = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGBA32, false);

            Debug.Log($"✅ OpenCV Mat 초기화 완료: {webCamTexture.width}x{webCamTexture.height}");

            // UI에 텍스처 연결 (웹캠 직접 연결도 시도)
            if (webcamDisplay != null)
            {
                // 먼저 웹캠 텍스처를 직접 연결해서 테스트
                webcamDisplay.texture = webCamTexture;
                Debug.Log("✅ 웹캠 텍스처 직접 연결 완료");

                // 2초 후 OpenCV 처리된 텍스처로 전환
                StartCoroutine(SwitchToProcessedTexture());
            }
            else
            {
                Debug.LogWarning("⚠️ webcamDisplay가 null입니다!");
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

    void StopWebcam()
    {
        try
        {
            isActive = false;

            // 웹캠 정지
            if (webCamTexture != null)
            {
                webCamTexture.Stop();
                Destroy(webCamTexture);
                webCamTexture = null;
            }

            // OpenCV Mat 정리
            if (rgbaMat != null) { rgbaMat.Dispose(); rgbaMat = null; }
            if (grayMat != null) { grayMat.Dispose(); grayMat = null; }
            if (displayTexture != null) { Destroy(displayTexture); displayTexture = null; }

            // UI 제거
            if (webcamUI != null)
            {
                Destroy(webcamUI);
                webcamUI = null;
            }

            Debug.Log("🛑 웹캠 눈동자 인식 종료");
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
            // 기존 UI가 있으면 제거
            if (webcamUI != null)
            {
                Destroy(webcamUI);
                webcamUI = null;
            }

            // Canvas 찾기 또는 생성
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("WebcamCanvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000; // 최상위 표시

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);

                canvasObj.AddComponent<GraphicRaycaster>();
                Debug.Log("✅ Canvas 자동 생성됨 (sortingOrder: 1000)");
            }
            else
            {
                // 기존 Canvas의 sortingOrder 확인/설정
                if (canvas.sortingOrder < 100)
                {
                    canvas.sortingOrder = 1000;
                    Debug.Log($"✅ Canvas sortingOrder를 {canvas.sortingOrder}로 변경");
                }
            }

            // 웹캠 UI 패널 생성 (화면 왼쪽 상단에 고정)
            webcamUI = new GameObject("WebcamEyeViewer");
            webcamUI.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = webcamUI.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(20, -20); // 좌상단에서 20px 떨어진 위치
            panelRect.sizeDelta = new Vector2(webcamSize.x + 20, webcamSize.y + 100);

            // 배경 패널 (더 진한 색으로)
            Image panelBg = webcamUI.AddComponent<Image>();
            panelBg.color = new Color(0, 0, 0, 0.9f);
            panelBg.raycastTarget = false; // 클릭 방해 방지

            // 제목 텍스트 추가
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(webcamUI.transform, false);

            RectTransform titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.anchoredPosition = new Vector2(0, -5);
            titleRect.sizeDelta = new Vector2(-10, 20);

            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = "👁️ 웹캠 눈동자 인식";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 14;
            titleText.color = Color.yellow;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.raycastTarget = false;

            // 웹캠 디스플레이
            GameObject displayObj = new GameObject("WebcamDisplay");
            displayObj.transform.SetParent(webcamUI.transform, false);

            RectTransform displayRect = displayObj.AddComponent<RectTransform>();
            displayRect.anchorMin = new Vector2(0.5f, 1);
            displayRect.anchorMax = new Vector2(0.5f, 1);
            displayRect.anchoredPosition = new Vector2(0, -webcamSize.y/2);
            displayRect.sizeDelta = webcamSize;

            webcamDisplay = displayObj.AddComponent<RawImage>();
            webcamDisplay.color = Color.white;
            webcamDisplay.raycastTarget = false;

            // 테스트용 배경색 (웹캠이 로드되기 전)
            Image testBg = displayObj.AddComponent<Image>();
            testBg.color = new Color(0.2f, 0.2f, 0.8f, 0.5f); // 파란색 배경
            testBg.raycastTarget = false;

            // 상태 텍스트
            if (showStatusText)
            {
                GameObject statusObj = new GameObject("StatusText");
                statusObj.transform.SetParent(webcamUI.transform, false);

                RectTransform statusRect = statusObj.AddComponent<RectTransform>();
                statusRect.anchorMin = new Vector2(0, 0);
                statusRect.anchorMax = new Vector2(1, 0);
                statusRect.anchoredPosition = new Vector2(0, 45);
                statusRect.sizeDelta = new Vector2(-10, 80);

                statusText = statusObj.AddComponent<Text>();
                statusText.text = "📹 웹캠 초기화 중...\n잠시 기다려주세요.";
                statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                statusText.fontSize = 11;
                statusText.color = Color.white;
                statusText.alignment = TextAnchor.UpperCenter;
                statusText.raycastTarget = false;
            }

            // 닫기 버튼 추가
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

            // X 텍스트
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

            Debug.Log("✅ 웹캠 UI 생성 완료 (향상된 버전)");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ UI 생성 실패: {e.Message}");
        }
    }

    void ProcessWebcamFrame()
    {
        // 안전성 체크
        if (!isActive || webCamTexture == null || !webCamTexture.isPlaying)
            return;

        if (rgbaMat == null || grayMat == null || displayTexture == null)
        {
            Debug.LogWarning("⚠️ OpenCV Mat이 아직 초기화되지 않았습니다.");
            return;
        }

        // 웹캠 크기 재확인
        if (webCamTexture.width <= 0 || webCamTexture.height <= 0)
        {
            Debug.LogWarning("⚠️ 웹캠 크기가 유효하지 않습니다.");
            return;
        }

        try
        {
            frameCount++;

            // 웹캠에서 OpenCV Mat으로 변환
            Utils.webCamTextureToMat(webCamTexture, rgbaMat);

            // Mat이 올바르게 생성되었는지 확인
            if (rgbaMat.empty())
            {
                Debug.LogWarning("⚠️ rgbaMat이 비어있습니다.");
                return;
            }

            Imgproc.cvtColor(rgbaMat, grayMat, Imgproc.COLOR_RGBA2GRAY);

            // 얼굴 감지
            DetectFace();

            // 화면에 표시
            Utils.matToTexture2D(rgbaMat, displayTexture);

            // 상태 업데이트 (1초에 한 번만)
            if (frameCount % 30 == 0)
            {
                UpdateStatusText();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 프레임 처리 오류: {e.Message}");
            Debug.LogError($"스택 트레이스: {e.StackTrace}");

            // 오류 발생 시 잠시 중단
            StopCoroutine(nameof(ProcessWebcamFrame));
        }
    }

    void DetectFace()
    {
        faceDetected = false;
        eyesDetected = false;

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

                // 가장 큰 얼굴 선택
                OpenCVForUnity.CoreModule.Rect largestFace = faceArray[0];
                for (int i = 1; i < faceArray.Length; i++)
                {
                    if (faceArray[i].area() > largestFace.area())
                    {
                        largestFace = faceArray[i];
                    }
                }

                // 얼굴 경계 그리기
                if (showFaceDetection)
                {
                    Imgproc.rectangle(rgbaMat,
                        new Point(largestFace.x, largestFace.y),
                        new Point(largestFace.x + largestFace.width, largestFace.y + largestFace.height),
                        new Scalar(0, 255, 0, 255), 3);

                    // 얼굴 라벨
                    Imgproc.putText(rgbaMat, "FACE",
                        new Point(largestFace.x, largestFace.y - 10),
                        Imgproc.FONT_HERSHEY_SIMPLEX, 0.7, new Scalar(0, 255, 0, 255), 2);
                }

                // 눈 감지
                DetectEyes(largestFace);
            }

            faces.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 얼굴 감지 오류: {e.Message}");
        }
    }

    void DetectEyes(OpenCVForUnity.CoreModule.Rect faceRect)
    {
        if (eyeCascade == null || eyeCascade.empty()) return;

        try
        {
            // 얼굴 상단 60% 영역에서 눈 검색
            OpenCVForUnity.CoreModule.Rect eyeRegion = new OpenCVForUnity.CoreModule.Rect(
                faceRect.x,
                faceRect.y,
                faceRect.width,
                (int)(faceRect.height * 0.6f)
            );

            Mat eyeROI = new Mat(grayMat, eyeRegion);
            MatOfRect eyes = new MatOfRect();

            eyeCascade.detectMultiScale(eyeROI, eyes, 1.1, 5, 0, new Size(15, 15), new Size());

            OpenCVForUnity.CoreModule.Rect[] eyeArray = eyes.toArray();
            eyesDetected = eyeArray.Length >= 2;

            if (eyesDetected)
            {
                eyeCount++;

                if (showEyeDetection)
                {
                    // 눈 경계 그리기 (최대 2개)
                    for (int i = 0; i < Mathf.Min(2, eyeArray.Length); i++)
                    {
                        OpenCVForUnity.CoreModule.Rect eye = eyeArray[i];
                        OpenCVForUnity.CoreModule.Rect actualEye = new OpenCVForUnity.CoreModule.Rect(
                            eyeRegion.x + eye.x,
                            eyeRegion.y + eye.y,
                            eye.width,
                            eye.height
                        );

                        // 눈 사각형
                        Imgproc.rectangle(rgbaMat,
                            new Point(actualEye.x, actualEye.y),
                            new Point(actualEye.x + actualEye.width, actualEye.y + actualEye.height),
                            new Scalar(255, 0, 0, 255), 2);

                        // 눈 중심점
                        Point eyeCenter = new Point(
                            actualEye.x + actualEye.width / 2,
                            actualEye.y + actualEye.height / 2
                        );
                        Imgproc.circle(rgbaMat, eyeCenter, 5, new Scalar(0, 0, 255, 255), -1);

                        // 눈 라벨
                        Imgproc.putText(rgbaMat, $"EYE{i + 1}",
                            new Point(actualEye.x, actualEye.y - 5),
                            Imgproc.FONT_HERSHEY_SIMPLEX, 0.4, new Scalar(255, 0, 0, 255), 1);
                    }
                }
            }

            eyeROI.Dispose();
            eyes.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 눈 감지 오류: {e.Message}");
        }
    }

    void UpdateStatusText()
    {
        if (statusText == null) return;

        string status = $"프레임: {frameCount}\n";
        status += $"😊 얼굴: {(faceDetected ? "✅" : "❌")} ({faceCount})\n";
        status += $"👁️ 눈동자: {(eyesDetected ? "✅" : "❌")} ({eyeCount})\n";

        if (frameCount > 0)
        {
            float faceRate = (float)faceCount / frameCount * 100f;
            float eyeRate = (float)eyeCount / frameCount * 100f;
            status += $"감지율: {faceRate:F0}% / {eyeRate:F0}%\n";
        }

        status += "\nESC: 종료";

        statusText.text = status;
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

    // 화면에 간단한 안내 표시
    void OnGUI()
    {
        // F1키 안내 (웹캠이 꺼져있을 때만)
        if (!isActive)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 20;
            style.normal.textColor = Color.yellow;
            style.alignment = TextAnchor.MiddleCenter;

            string message = "F1키를 눌러 웹캠 눈동자 인식을 시작하세요";
            GUI.Label(new UnityEngine.Rect(0, Screen.height / 2 - 50, Screen.width, 100), message, style);
        }
        else
        {
            // 웹캠이 켜져있을 때 상태 정보 표시
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label);
            statusStyle.fontSize = 14;
            statusStyle.normal.textColor = Color.white;
            statusStyle.alignment = TextAnchor.UpperLeft;

            string debugInfo = "=== 웹캠 디버그 정보 ===\n";
            debugInfo += $"웹캠 상태: {(webCamTexture != null && webCamTexture.isPlaying ? "✅ 작동중" : "❌ 정지")}\n";

            if (webCamTexture != null)
            {
                debugInfo += $"웹캠 크기: {webCamTexture.width}x{webCamTexture.height}\n";
                debugInfo += $"웹캠 FPS: {webCamTexture.requestedFPS}\n";
            }

            debugInfo += $"OpenCV Mat: {(rgbaMat != null && !rgbaMat.empty() ? "✅" : "❌")}\n";
            debugInfo += $"Display Texture: {(displayTexture != null ? "✅" : "❌")}\n";
            debugInfo += $"UI Display: {(webcamDisplay != null ? "✅" : "❌")}\n";
            debugInfo += $"프레임 카운트: {frameCount}\n";
            debugInfo += $"얼굴 감지: {(faceDetected ? "✅" : "❌")} ({faceCount})\n";
            debugInfo += $"눈 감지: {(eyesDetected ? "✅" : "❌")} ({eyeCount})\n";
            debugInfo += "\nESC: 종료";

            // 화면 우측에 디버그 정보 표시
            GUI.Label(new UnityEngine.Rect(Screen.width - 300, 10, 290, 300), debugInfo, statusStyle);

            // 웹캠 화면이 보이지 않을 때 추가 안내
            if (webcamDisplay != null && webcamDisplay.texture == null)
            {
                GUIStyle warningStyle = new GUIStyle(GUI.skin.label);
                warningStyle.fontSize = 16;
                warningStyle.normal.textColor = Color.red;
                warningStyle.alignment = TextAnchor.MiddleCenter;

                string warning = "⚠️ 웹캠 화면이 표시되지 않습니다!\n잠시 기다리거나 F1키를 다시 눌러보세요.";
                GUI.Label(new UnityEngine.Rect(0, Screen.height / 2 + 100, Screen.width, 100), warning, warningStyle);
            }
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