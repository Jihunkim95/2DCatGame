using UnityEngine;

public class CatPlayerAnimator : MonoBehaviour
{
    [Header("애니메이션 설정")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    [Header("상태 변경 조건")]
    public float idleTime = 3f; // idle 상태 유지 시간
    public float sleepTime = 8f; // sleep으로 전환되는 시간 (15초 → 8초로 단축)
    public float moveThreshold = 0.15f; // 움직임 감지 임계값 (약간 증가)

    [Header("애니메이션 상태")]
    public CatAnimationState currentState = CatAnimationState.Idle;
    public bool isMoving = false;

    // 애니메이션 상태 enum
    public enum CatAnimationState
    {
        Idle,
        Walk,
        Sleep
    }

    // 애니메이션 파라미터 이름 (Animator Controller와 일치해야 함)
    private static readonly string PARAM_IS_WALKING = "IsWalking";
    private static readonly string PARAM_IS_SLEEPING = "IsSleeping";
    private static readonly string PARAM_SPEED = "Speed";

    // 상태 추적 변수들
    private Vector3 lastPosition;
    private float stateTimer = 0f;
    private float currentSpeed = 0f;
    private bool forceStateActive = false;
    private CatAnimationState forcedState;
    private float forceStateTimer = 0f;
    private float forceStateDuration = 0f;

    // 참조
    private TestCat testCat;

    void Start()
    {
        // 컴포넌트 참조 설정
        if (animator == null)
            animator = GetComponent<Animator>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (testCat == null)
            testCat = GetComponent<TestCat>();

        // 초기 위치 저장
        lastPosition = transform.position;

        // 초기 상태 설정
        SetAnimationState(CatAnimationState.Idle);

        Debug.Log("CatPlayerAnimator 초기화 완료");
        DebugLogger.LogToFile("CatPlayerAnimator 초기화 완료");
    }

    void Update()
    {
        // 강제 상태가 활성화되어 있다면 처리
        if (forceStateActive)
        {
            UpdateForceState();
            return;
        }

        UpdateMovementDetection();
        UpdateAnimationState();
        UpdateAnimatorParameters();
    }

    void UpdateForceState()
    {
        forceStateTimer += Time.deltaTime;

        if (forceStateTimer >= forceStateDuration)
        {
            // 강제 상태 종료
            forceStateActive = false;
            forceStateTimer = 0f;

            // TestCat의 현재 움직임 상태에 맞춰 애니메이션 상태 복원
            if (testCat != null)
            {
                if (testCat.IsMoving)
                {
                    SetAnimationState(CatAnimationState.Walk);
                }
                else if (testCat.IsSleeping)
                {
                    SetAnimationState(CatAnimationState.Sleep);
                }
                else
                {
                    SetAnimationState(CatAnimationState.Idle);
                }
            }

            Debug.Log("강제 애니메이션 상태 종료, 자동 상태로 복귀");
        }
        else
        {
            // 강제 상태 유지
            if (currentState != forcedState)
            {
                SetAnimationState(forcedState);
            }
        }

        UpdateAnimatorParameters();
    }

    void UpdateMovementDetection()
    {
        // 현재 속도 계산
        Vector3 currentPosition = transform.position;
        float distance = Vector3.Distance(currentPosition, lastPosition);
        currentSpeed = distance / Time.deltaTime;

        // 움직임 감지 (더 정확한 임계값 적용)
        isMoving = currentSpeed > moveThreshold;

        // TestCat의 움직임 상태와 직접 동기화
        if (testCat != null)
        {
            // TestCat의 실제 움직임 상태 확인
            bool testCatIsMoving = testCat.IsMoving;

            // TestCat이 실제로 움직이고 있고, 속도도 임계값을 넘을 때만 움직임으로 판정
            isMoving = testCatIsMoving && currentSpeed > moveThreshold;

            Debug.Log($"Movement Detection - TestCat Moving: {testCatIsMoving}, Speed: {currentSpeed:F3}, IsMoving: {isMoving}");
        }

        // 위치 업데이트
        lastPosition = currentPosition;

        // 상태 타이머 업데이트
        stateTimer += Time.deltaTime;
    }

    void UpdateAnimationState()
    {
        // TestCat의 움직임 상태와 동기화
        if (testCat != null)
        {
            CatAnimationState targetState = CatAnimationState.Idle;

            // TestCat의 MovementState에 따라 애니메이션 상태 결정
            switch (testCat.CurrentMovementState)
            {
                case TestCat.MovementState.Walking:
                    targetState = CatAnimationState.Walk;
                    break;

                case TestCat.MovementState.Idle:
                    targetState = CatAnimationState.Idle;
                    break;

                case TestCat.MovementState.Sleeping:
                    targetState = CatAnimationState.Sleep;
                    break;
            }

            Debug.Log($"Animation State Update - TestCat State: {testCat.CurrentMovementState}, Target Animation: {targetState}, Current: {currentState}");

            // 상태 변경
            if (targetState != currentState)
            {
                SetAnimationState(targetState);
            }
        }
        else
        {
            // TestCat 참조가 없을 때의 기본 로직 (속도 기반)
            CatAnimationState newState = currentState;

            switch (currentState)
            {
                case CatAnimationState.Idle:
                    if (isMoving)
                    {
                        newState = CatAnimationState.Walk;
                    }
                    else if (stateTimer >= sleepTime)
                    {
                        newState = CatAnimationState.Sleep;
                    }
                    break;

                case CatAnimationState.Walk:
                    if (!isMoving)
                    {
                        newState = CatAnimationState.Idle;
                    }
                    break;

                case CatAnimationState.Sleep:
                    if (isMoving)
                    {
                        newState = CatAnimationState.Walk;
                    }
                    else if (currentSpeed > moveThreshold * 2f)
                    {
                        newState = CatAnimationState.Idle;
                    }
                    break;
            }

            // 상태 변경
            if (newState != currentState)
            {
                SetAnimationState(newState);
            }
        }
    }

    void SetAnimationState(CatAnimationState newState)
    {
        Debug.Log($"고양이 애니메이션 상태 변경: {currentState} → {newState}");
        DebugLogger.LogToFile($"고양이 애니메이션 상태 변경: {currentState} → {newState}");

        currentState = newState;
        stateTimer = 0f;

        // 상태별 특별한 처리
        switch (newState)
        {
            case CatAnimationState.Sleep:
                // 잠들 때 행복도 약간 회복
                if (GameDataManager.Instance != null)
                {
                    GameDataManager.Instance.happiness += 1f;
                    GameDataManager.Instance.happiness = Mathf.Clamp(GameDataManager.Instance.happiness, 0f, 100f);
                }
                Debug.Log("고양이가 잠들었습니다 (+1 행복도)");
                break;

            case CatAnimationState.Walk:
                Debug.Log("고양이가 걷기 시작했습니다");
                break;

            case CatAnimationState.Idle:
                Debug.Log("고양이가 가만히 있습니다");
                break;
        }
    }

    void UpdateAnimatorParameters()
    {
        if (animator == null) return;

        // Bool 파라미터 업데이트
        animator.SetBool(PARAM_IS_WALKING, currentState == CatAnimationState.Walk);
        animator.SetBool(PARAM_IS_SLEEPING, currentState == CatAnimationState.Sleep);

        // Float 파라미터 업데이트 (애니메이션 속도 조절용)
        animator.SetFloat(PARAM_SPEED, currentSpeed);

        // 움직임 방향에 따른 스프라이트 뒤집기
        if (isMoving && spriteRenderer != null)
        {
            Vector3 movement = transform.position - lastPosition;
            if (Mathf.Abs(movement.x) > 0.01f)
            {
                spriteRenderer.flipX = movement.x < 0f;
            }
        }
    }

    // 외부에서 강제로 상태 변경
    public void ForceState(CatAnimationState state)
    {
        forceStateActive = false; // 기존 강제 상태 해제
        SetAnimationState(state);
        Debug.Log($"애니메이션 상태 강제 변경: {state}");
    }

    // 특정 상태로 일시적 변경 (예: 먹이를 먹을 때)
    public void PlayTemporaryAnimation(CatAnimationState temporaryState, float duration)
    {
        StartCoroutine(TemporaryStateCoroutine(temporaryState, duration));
    }

    // 일시적 애니메이션 재생 (더 안전한 방식)
    public void PlayTemporaryAnimationSafe(CatAnimationState temporaryState, float duration)
    {
        // 기존 강제 상태가 있다면 중단
        forceStateActive = false;

        // 새로운 강제 상태 설정
        forcedState = temporaryState;
        forceStateDuration = duration;
        forceStateTimer = 0f;
        forceStateActive = true;

        SetAnimationState(temporaryState);

        Debug.Log($"일시적 애니메이션 재생: {temporaryState} ({duration}초)");
        DebugLogger.LogToFile($"일시적 애니메이션 재생: {temporaryState} ({duration}초)");
    }

    private System.Collections.IEnumerator TemporaryStateCoroutine(CatAnimationState tempState, float duration)
    {
        CatAnimationState originalState = currentState;
        SetAnimationState(tempState);

        yield return new WaitForSeconds(duration);

        // TestCat의 현재 상태에 맞춰 복원
        if (testCat != null)
        {
            if (testCat.IsMoving)
            {
                SetAnimationState(CatAnimationState.Walk);
            }
            else if (testCat.IsSleeping)
            {
                SetAnimationState(CatAnimationState.Sleep);
            }
            else
            {
                SetAnimationState(CatAnimationState.Idle);
            }
        }
        else
        {
            SetAnimationState(originalState);
        }
    }

    // 강제로 잠들게 하기 (TestCat과 연동)
    public void MakeSleep()
    {
        ForceState(CatAnimationState.Sleep);
        if (testCat != null)
        {
            testCat.MakeCatSleep();
        }
    }

    // 강제로 깨우기 (TestCat과 연동)
    public void WakeUp()
    {
        ForceState(CatAnimationState.Idle);
        if (testCat != null)
        {
            testCat.WakeCatUp();
        }
    }

    // 디버그 정보 표시
    void OnGUI()
    {
        if (Debug.isDebugBuild)
        {
            GUILayout.BeginArea(new Rect(10, 10, 350, 200));
            GUILayout.Label($"=== Cat Animation Debug ===");
            GUILayout.Label($"Current State: {currentState}");
            GUILayout.Label($"Is Moving: {isMoving}");
            GUILayout.Label($"Speed: {currentSpeed:F2}");
            GUILayout.Label($"State Timer: {stateTimer:F1}s");
            GUILayout.Label($"Force State Active: {forceStateActive}");

            if (forceStateActive)
            {
                GUILayout.Label($"Forced State: {forcedState} ({forceStateTimer:F1}/{forceStateDuration:F1}s)");
            }

            if (testCat != null)
            {
                GUILayout.Label($"TestCat State: {testCat.CurrentMovementState}");
                GUILayout.Label($"TestCat Moving: {testCat.IsMoving}");
                GUILayout.Label($"TestCat Sleeping: {testCat.IsSleeping}");
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Force Idle"))
                ForceState(CatAnimationState.Idle);
            if (GUILayout.Button("Force Walk"))
                ForceState(CatAnimationState.Walk);
            if (GUILayout.Button("Force Sleep"))
                ForceState(CatAnimationState.Sleep);
            if (GUILayout.Button("Play Temp Idle (2s)"))
                PlayTemporaryAnimationSafe(CatAnimationState.Idle, 2f);

            GUILayout.EndArea();
        }
    }

    // 외부에서 접근할 수 있는 프로퍼티들
    public bool IsWalking => currentState == CatAnimationState.Walk;
    public bool IsSleeping => currentState == CatAnimationState.Sleep;
    public bool IsIdle => currentState == CatAnimationState.Idle;
    public float CurrentSpeed => currentSpeed;
    public bool IsForceStateActive => forceStateActive;
}