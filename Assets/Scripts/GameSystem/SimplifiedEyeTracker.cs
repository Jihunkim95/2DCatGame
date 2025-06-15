using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 간단한 시선 추적 시뮬레이터
/// CompatibilityWindowManager와의 호환성 개선 버전
/// </summary>
public class SimplifiedEyeTracker : MonoBehaviour
{
    [Header("시뮬레이션 설정")]
    public bool useMouseAsGaze = true;          // 마우스를 시선으로 사용
    public bool addGazeNoise = true;            // 시선에 노이즈 추가 (현실적으로)
    public float gazeNoiseAmount = 10f;         // 노이즈 강도
    public float gazeUpdateRate = 30f;          // 시선 업데이트 주기 (Hz)

    [Header("보정 설정")]
    public bool isCalibrated = false;           // 보정 완료 여부
    public Vector2 gazeOffset = Vector2.zero;   // 보정 오프셋
    public Vector2 gazeScale = Vector2.one;     // 보정 스케일

    [Header("시선 스무딩")]
    public float smoothingFactor = 5f;          // 스무딩 강도
    public bool enableSmoothing = true;         // 스무딩 활성화

    [Header("호환성 설정")]
    public bool useCompatibilityWindowManager = true; // CompatibilityWindowManager 사용
    public bool forceClickThroughDisable = true;      // 보정 중 click-through 비활성화

    [Header("디버그")]
    public bool showDebugInfo = true;           // 디버그 정보 표시
    public bool showGazeCursor = true;          // 시선 커서 표시

    // 시선 데이터
    private Vector2 currentGazePosition;        // 현재 시선 위치 (스크린 좌표)
    private Vector2 smoothedGazePosition;       // 스무딩된 시선 위치
    private Vector2 rawGazePosition;            // 원시 시선 위치
    private bool isGazeValid = true;            // 시선 추적 유효성

    // 보정 데이터
    private List<Vector2> calibrationTargets = new List<Vector2>();
    private List<Vector2> calibrationGazes = new List<Vector2>();
    private bool isCalibrating = false;
    private int calibrationIndex = 0;

    // 노이즈 및 업데이트
    private float lastUpdateTime;
    private Vector2 noiseOffset;

    // 호환성 관련
    private bool wasClickThroughEnabled = false;

    // 싱글톤
    public static SimplifiedEyeTracker Instance { get; private set; }

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
        SetupCalibrationPoints();
        currentGazePosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        smoothedGazePosition = currentGazePosition;

        Debug.Log("✅ SimplifiedEyeTracker 초기화 완료 (CompatibilityWindowManager 호환)");
        Debug.Log("🖱️ 마우스로 시선 시뮬레이션");
        Debug.Log("⌨️ C키: 보정 시작, R키: 보정 리셋, Space: 보정 점 기록");
    }

    void SetupCalibrationPoints()
    {
        // 9점 보정 좌표 설정
        float margin = 100f;
        float w = Screen.width;
        float h = Screen.height;

        calibrationTargets.Clear();
        calibrationTargets.Add(new Vector2(margin, margin));                    // 좌상
        calibrationTargets.Add(new Vector2(w * 0.5f, margin));                 // 상중
        calibrationTargets.Add(new Vector2(w - margin, margin));               // 우상
        calibrationTargets.Add(new Vector2(margin, h * 0.5f));                 // 좌중
        calibrationTargets.Add(new Vector2(w * 0.5f, h * 0.5f));               // 중앙
        calibrationTargets.Add(new Vector2(w - margin, h * 0.5f));             // 우중
        calibrationTargets.Add(new Vector2(margin, h - margin));               // 좌하
        calibrationTargets.Add(new Vector2(w * 0.5f, h - margin));             // 하중
        calibrationTargets.Add(new Vector2(w - margin, h - margin));           // 우하
    }

    void Update()
    {
        HandleInput();
        UpdateGazePosition();

        if (enableSmoothing)
        {
            UpdateSmoothing();
        }
        else
        {
            smoothedGazePosition = currentGazePosition;
        }
    }

    void HandleInput()
    {
        // 보정 시작
        if (Input.GetKeyDown(KeyCode.C))
        {
            StartCalibration();
        }

        // 보정 리셋
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetCalibration();
        }

        // 보정 점 기록
        if (Input.GetKeyDown(KeyCode.Space) && isCalibrating)
        {
            ProcessCalibrationPoint();
        }

        // 시선 추적 토글
        if (Input.GetKeyDown(KeyCode.T))
        {
            isGazeValid = !isGazeValid;
            Debug.Log($"시선 추적: {(isGazeValid ? "활성화" : "비활성화")}");
        }

        // 노이즈 토글
        if (Input.GetKeyDown(KeyCode.N))
        {
            addGazeNoise = !addGazeNoise;
            Debug.Log($"시선 노이즈: {(addGazeNoise ? "활성화" : "비활성화")}");
        }
    }

    Vector2 GetMousePosition()
    {
        // CompatibilityWindowManager와의 호환성을 위한 마우스 위치 획득
        if (useCompatibilityWindowManager && CompatibilityWindowManager.Instance != null)
        {
            // CompatibilityWindowManager의 마우스 위치 사용
            return CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        }
        else
        {
            // 기본 Unity 마우스 위치 사용
            return Input.mousePosition;
        }
    }

    void UpdateGazePosition()
    {
        if (!isGazeValid) return;

        // 업데이트 주기 체크
        if (Time.time - lastUpdateTime < 1f / gazeUpdateRate) return;
        lastUpdateTime = Time.time;

        if (useMouseAsGaze)
        {
            // 호환성을 고려한 마우스 위치 사용
            rawGazePosition = GetMousePosition();
        }
        else
        {
            // 다른 입력 방식이 필요하면 여기에 추가
            rawGazePosition = currentGazePosition;
        }

        // 노이즈 추가
        if (addGazeNoise)
        {
            AddGazeNoise();
        }

        // 보정 적용
        Vector2 calibratedGaze = ApplyCalibration(rawGazePosition + noiseOffset);

        // 화면 경계 제한
        currentGazePosition = new Vector2(
            Mathf.Clamp(calibratedGaze.x, 0, Screen.width),
            Mathf.Clamp(calibratedGaze.y, 0, Screen.height)
        );
    }

    void AddGazeNoise()
    {
        // 현실적인 시선 떨림 시뮬레이션
        float noiseX = Mathf.PerlinNoise(Time.time * 2f, 0) - 0.5f;
        float noiseY = Mathf.PerlinNoise(0, Time.time * 2f) - 0.5f;

        noiseOffset = new Vector2(
            noiseX * gazeNoiseAmount,
            noiseY * gazeNoiseAmount
        );
    }

    Vector2 ApplyCalibration(Vector2 rawGaze)
    {
        if (!isCalibrated) return rawGaze;

        // 정규화
        Vector2 normalized = new Vector2(
            rawGaze.x / Screen.width,
            rawGaze.y / Screen.height
        );

        // 오프셋과 스케일 적용
        normalized = (normalized + gazeOffset) * gazeScale;

        // 다시 스크린 좌표로 변환
        return new Vector2(
            normalized.x * Screen.width,
            normalized.y * Screen.height
        );
    }

    void UpdateSmoothing()
    {
        smoothedGazePosition = Vector2.Lerp(
            smoothedGazePosition,
            currentGazePosition,
            smoothingFactor * Time.deltaTime
        );
    }

    void StartCalibration()
    {
        isCalibrating = true;
        calibrationIndex = 0;
        calibrationGazes.Clear();

        // 보정 중 click-through 비활성화
        if (forceClickThroughDisable && CompatibilityWindowManager.Instance != null)
        {
            wasClickThroughEnabled = CompatibilityWindowManager.Instance.IsClickThrough;
            CompatibilityWindowManager.Instance.DisableClickThrough();
            Debug.Log("🔒 보정 중 click-through 비활성화");
        }

        Debug.Log("🎯 보정 시작! 각 점을 바라보고 스페이스 키를 누르세요.");
        Debug.Log($"🖱️ 마우스 입력 모드: {(useCompatibilityWindowManager ? "CompatibilityWindowManager" : "Unity Input")}");
    }

    void ProcessCalibrationPoint()
    {
        if (!isCalibrating) return;

        // 현재 시선 위치 기록 (원시 데이터 사용)
        Vector2 currentMousePos = GetMousePosition();
        calibrationGazes.Add(currentMousePos);
        calibrationIndex++;

        Debug.Log($"보정 점 {calibrationIndex}/9 완료");
        Debug.Log($"  타겟: {calibrationTargets[calibrationIndex - 1]}");
        Debug.Log($"  실제: {currentMousePos}");
        Debug.Log($"  오차: {Vector2.Distance(calibrationTargets[calibrationIndex - 1], currentMousePos):F1}px");

        if (calibrationIndex >= calibrationTargets.Count)
        {
            CompleteCalibration();
        }
    }

    void CompleteCalibration()
    {
        isCalibrating = false;

        if (calibrationGazes.Count == calibrationTargets.Count)
        {
            CalculateCalibration();
            isCalibrated = true;
            Debug.Log($"✅ 보정 완료! 오프셋: {gazeOffset}, 스케일: {gazeScale}");
        }
        else
        {
            Debug.LogWarning("⚠️ 보정 데이터가 부족합니다.");
        }

        // 보정 완료 후 click-through 상태 복원
        RestoreClickThroughState();
    }

    void CalculateCalibration()
    {
        Vector2 totalOffset = Vector2.zero;
        Vector2 totalScale = Vector2.zero;
        int validPoints = 0;

        for (int i = 0; i < calibrationTargets.Count && i < calibrationGazes.Count; i++)
        {
            Vector2 target = calibrationTargets[i];
            Vector2 gaze = calibrationGazes[i];

            // 오프셋 계산
            Vector2 offset = target - gaze;
            totalOffset += offset;

            // 스케일 계산 (간단히 비율로)
            Vector2 targetNorm = new Vector2(target.x / Screen.width, target.y / Screen.height);
            Vector2 gazeNorm = new Vector2(gaze.x / Screen.width, gaze.y / Screen.height);

            if (gazeNorm.x != 0 && gazeNorm.y != 0)
            {
                Vector2 scale = new Vector2(targetNorm.x / gazeNorm.x, targetNorm.y / gazeNorm.y);
                totalScale += scale;
                validPoints++;
            }
        }

        if (calibrationGazes.Count > 0)
        {
            gazeOffset = totalOffset / (calibrationGazes.Count * Screen.width);

            if (validPoints > 0)
            {
                gazeScale = totalScale / validPoints;
                // 스케일 범위 제한
                gazeScale.x = Mathf.Clamp(gazeScale.x, 0.5f, 2f);
                gazeScale.y = Mathf.Clamp(gazeScale.y, 0.5f, 2f);
            }
        }

        // 보정 품질 평가
        EvaluateCalibrationQuality();
    }

    void EvaluateCalibrationQuality()
    {
        if (calibrationTargets.Count != calibrationGazes.Count) return;

        float totalError = 0f;
        float maxError = 0f;

        for (int i = 0; i < calibrationTargets.Count; i++)
        {
            Vector2 target = calibrationTargets[i];
            Vector2 gaze = calibrationGazes[i];
            float error = Vector2.Distance(target, gaze);

            totalError += error;
            maxError = Mathf.Max(maxError, error);
        }

        float avgError = totalError / calibrationTargets.Count;

        Debug.Log($"📊 보정 품질 평가:");
        Debug.Log($"  평균 오차: {avgError:F1}px");
        Debug.Log($"  최대 오차: {maxError:F1}px");

        if (avgError < 50f)
        {
            Debug.Log("✅ 보정 품질 우수");
        }
        else if (avgError < 100f)
        {
            Debug.Log("⚠️ 보정 품질 보통");
        }
        else
        {
            Debug.Log("❌ 보정 품질 불량 - 재보정 권장");
        }
    }

    void ResetCalibration()
    {
        isCalibrated = false;
        isCalibrating = false;
        gazeOffset = Vector2.zero;
        gazeScale = Vector2.one;
        calibrationGazes.Clear();
        calibrationIndex = 0;

        // click-through 상태 복원
        RestoreClickThroughState();

        Debug.Log("🔄 보정 리셋 완료");
    }

    void RestoreClickThroughState()
    {
        if (forceClickThroughDisable && CompatibilityWindowManager.Instance != null)
        {
            if (wasClickThroughEnabled)
            {
                CompatibilityWindowManager.Instance.EnableClickThrough();
                Debug.Log("🔓 보정 완료 - click-through 상태 복원");
            }
            else
            {
                Debug.Log("🔓 보정 완료 - click-through 비활성화 유지");
            }
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        // 디버그 정보
        GUILayout.BeginArea(new Rect(10, 10, 400, 300));
        GUILayout.Label("=== Simplified Eye Tracker (호환성 개선) ===");
        GUILayout.Label($"시선 추적 활성: {(isGazeValid ? "✅" : "❌")}");
        GUILayout.Label($"보정 완료: {(isCalibrated ? "✅" : "❌")}");
        GUILayout.Label($"노이즈 추가: {(addGazeNoise ? "✅" : "❌")}");
        GUILayout.Label($"스무딩: {(enableSmoothing ? "✅" : "❌")}");
        GUILayout.Label($"CompatibilityWindowManager: {(useCompatibilityWindowManager ? "✅" : "❌")}");

        // 마우스 위치 정보
        Vector2 unityMouse = Input.mousePosition;
        Vector2 compatMouse = Vector2.zero;
        if (CompatibilityWindowManager.Instance != null)
        {
            compatMouse = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        }

        GUILayout.Space(5);
        GUILayout.Label($"Unity 마우스: ({unityMouse.x:F0}, {unityMouse.y:F0})");
        GUILayout.Label($"Compat 마우스: ({compatMouse.x:F0}, {compatMouse.y:F0})");
        GUILayout.Label($"좌표 차이: {Vector2.Distance(unityMouse, compatMouse):F1}px");

        if (isGazeValid)
        {
            GUILayout.Label($"원시 시선: ({rawGazePosition.x:F0}, {rawGazePosition.y:F0})");
            GUILayout.Label($"현재 시선: ({currentGazePosition.x:F0}, {currentGazePosition.y:F0})");
            GUILayout.Label($"스무딩 시선: ({smoothedGazePosition.x:F0}, {smoothedGazePosition.y:F0})");
        }

        GUILayout.Space(10);
        GUILayout.Label("⌨️ 단축키:");
        GUILayout.Label("C: 보정 시작 | R: 보정 리셋");
        GUILayout.Label("Space: 보정 점 기록");
        GUILayout.Label("T: 시선 추적 토글");
        GUILayout.Label("N: 노이즈 토글");
        GUILayout.EndArea();

        // 시선 커서 표시
        if (showGazeCursor && isGazeValid)
        {
            Vector2 gazePos = enableSmoothing ? smoothedGazePosition : currentGazePosition;

            // 시선 커서 (십자가)
            GUI.color = Color.cyan;
            GUI.Box(new Rect(gazePos.x - 10, gazePos.y - 1, 20, 2), "");
            GUI.Box(new Rect(gazePos.x - 1, gazePos.y - 10, 2, 20), "");

            // 시선 원
            GUI.color = new Color(0, 1, 1, 0.3f);
            GUI.Box(new Rect(gazePos.x - 15, gazePos.y - 15, 30, 30), "");

            GUI.color = Color.white;
        }

        // 보정 모드 UI
        if (isCalibrating && calibrationIndex < calibrationTargets.Count)
        {
            Vector2 target = calibrationTargets[calibrationIndex];

            // 보정 점 표시
            GUI.color = Color.red;
            GUI.Box(new Rect(target.x - 25, target.y - 25, 50, 50), "");
            GUI.color = Color.yellow;
            GUI.Box(new Rect(target.x - 15, target.y - 15, 30, 30), "");
            GUI.color = Color.white;

            // 숫자 표시
            GUIStyle numberStyle = new GUIStyle(GUI.skin.label);
            numberStyle.normal.textColor = Color.black;
            numberStyle.fontSize = 20;
            numberStyle.fontStyle = FontStyle.Bold;
            numberStyle.alignment = TextAnchor.MiddleCenter;

            GUI.Label(new Rect(target.x - 15, target.y - 10, 30, 20), $"{calibrationIndex + 1}", numberStyle);

            // 현재 마우스 위치 표시
            Vector2 currentMouse = GetMousePosition();
            GUI.color = Color.green;
            GUI.Box(new Rect(currentMouse.x - 5, currentMouse.y - 5, 10, 10), "");
            GUI.color = Color.white;

            // 안내 메시지
            GUI.color = Color.black;
            GUI.Box(new Rect(Screen.width * 0.5f - 250, Screen.height - 100, 500, 70), "");
            GUI.color = Color.yellow;

            GUIStyle messageStyle = new GUIStyle(GUI.skin.label);
            messageStyle.normal.textColor = Color.yellow;
            messageStyle.fontSize = 16;
            messageStyle.fontStyle = FontStyle.Bold;
            messageStyle.alignment = TextAnchor.MiddleCenter;

            string message = $"보정 점 {calibrationIndex + 1}/9를 바라보고 스페이스 키를 누르세요\n";
            message += $"현재 마우스: ({currentMouse.x:F0}, {currentMouse.y:F0})\n";
            message += $"타겟과의 거리: {Vector2.Distance(currentMouse, target):F1}px";

            GUI.Label(new Rect(Screen.width * 0.5f - 250, Screen.height - 95, 500, 60), message, messageStyle);

            GUI.color = Color.white;
        }
    }

    // EyesTrackingManager 호환 메서드들
    public Vector3 GetGazeWorldPosition(Camera camera)
    {
        if (!isGazeValid) return Vector3.zero;

        Vector2 gazePos = enableSmoothing ? smoothedGazePosition : currentGazePosition;
        Vector3 screenPos = new Vector3(gazePos.x, gazePos.y, 10f);
        return camera.ScreenToWorldPoint(screenPos);
    }

    public Vector2 GetGazeScreenPosition()
    {
        if (!isGazeValid) return Vector2.zero;
        return enableSmoothing ? smoothedGazePosition : currentGazePosition;
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
    public bool IsFaceDetected => isGazeValid; // 간소화
    public bool AreEyesDetected => isGazeValid; // 간소화
    public bool IsCalibrated => isCalibrated;

    // 런타임 설정 변경
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

        CalculateCalibration();
        isCalibrated = true;
        Debug.Log("⚡ 빠른 보정 완료!");
    }

    [ContextMenu("Perfect Calibration")]
    public void PerfectCalibration()
    {
        // 완벽한 보정 (오차 없음)
        isCalibrated = true;
        gazeOffset = Vector2.zero;
        gazeScale = Vector2.one;
        Debug.Log("💯 완벽한 보정 설정!");
    }

    [ContextMenu("Toggle Compatibility Mode")]
    public void ToggleCompatibilityMode()
    {
        useCompatibilityWindowManager = !useCompatibilityWindowManager;
        Debug.Log($"CompatibilityWindowManager 사용: {(useCompatibilityWindowManager ? "ON" : "OFF")}");
    }

    [ContextMenu("Test Mouse Position Difference")]
    public void TestMousePositionDifference()
    {
        Vector2 unityMouse = Input.mousePosition;
        Vector2 compatMouse = Vector2.zero;

        if (CompatibilityWindowManager.Instance != null)
        {
            compatMouse = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        }

        float distance = Vector2.Distance(unityMouse, compatMouse);

        Debug.Log("=== 마우스 위치 비교 ===");
        Debug.Log($"Unity: {unityMouse}");
        Debug.Log($"Compatibility: {compatMouse}");
        Debug.Log($"차이: {distance:F1}px");

        if (distance > 5f)
        {
            Debug.LogWarning("⚠️ 마우스 위치 차이가 큽니다. 보정에 영향을 줄 수 있습니다.");
        }
    }
}