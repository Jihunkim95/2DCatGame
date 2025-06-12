using UnityEngine;

public class CatPlayerAnimator : MonoBehaviour
{
    [Header("애니메이션 설정")]
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    [Header("상태 변경 조건")]
    public float idleTime = 3f;
    public float sleepTime = 8f;
    public float moveThreshold = 0.15f;

    [Header("애니메이션 상태")]
    public CatAnimationState currentState = CatAnimationState.IdleLeft;
    public CatDirection currentDirection = CatDirection.Left;
    public bool isMoving = false;

    // 애니메이션 상태 enum - 방향별로 분리
    public enum CatAnimationState
    {
        IdleLeft,
        IdleRight,
        WalkLeft,
        WalkRight,
        SleepLeft,
        SleepRight
    }

    // 고양이 방향 enum
    public enum CatDirection
    {
        Left,
        Right
    }

    // 기본 상태 enum (방향 무관)
    public enum CatBaseState
    {
        Idle,
        Walk,
        Sleep
    }

    // 애니메이션 파라미터 이름
    private static readonly string PARAM_IS_WALKING = "IsWalking";
    private static readonly string PARAM_IS_SLEEPING = "IsSleeping";
    private static readonly string PARAM_IS_FACING_RIGHT = "IsFacingRight";
    private static readonly string PARAM_SPEED = "Speed";

    // 상태 추적 변수들
    private Vector3 lastPosition;
    private float stateTimer = 0f;
    private float currentSpeed = 0f;
    private bool forceStateActive = false;
    private CatAnimationState forcedState;
    private float forceStateTimer = 0f;
    private float forceStateDuration = 0f;

    // 컴포넌트 참조들 (리팩터링된 구조에 맞게 변경)
    private CatMovementController movementController;
    private CatDirectionTracker directionTracker;
    private TestCat testCat; // TestCat 참조 추가

    void Start()
    {
        // 컴포넌트 참조 설정
        if (animator == null)
            animator = GetComponent<Animator>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // 새로운 컴포넌트들 참조
        movementController = GetComponent<CatMovementController>();
        directionTracker = GetComponent<CatDirectionTracker>();
        testCat = GetComponent<TestCat>(); // TestCat 참조 추가

        // 초기 위치 저장
        lastPosition = transform.position;

        // 초기 상태 설정
        SetAnimationState(CatAnimationState.IdleLeft);

        // 이벤트 리스너 등록
        SetupEventListeners();

        Debug.Log("CatPlayerAnimator 초기화 완료 - 리팩터링된 컴포넌트 구조 적용");
        DebugLogger.LogToFile("CatPlayerAnimator 초기화 완료 - 리팩터링된 컴포넌트 구조 적용");
    }

    void SetupEventListeners()
    {
        // MovementController의 상태 변경 이벤트 구독
        if (movementController != null)
        {
            movementController.OnMovementStateChanged += OnMovementStateChanged;
        }

        // DirectionTracker의 방향 변경 이벤트 구독
        if (directionTracker != null)
        {
            directionTracker.OnDirectionChanged += OnDirectionChanged;
        }
    }

    void OnDestroy()
    {
        // 이벤트 리스너 해제
        if (movementController != null)
        {
            movementController.OnMovementStateChanged -= OnMovementStateChanged;
        }

        if (directionTracker != null)
        {
            directionTracker.OnDirectionChanged -= OnDirectionChanged;
        }
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

    // 🎬 Animation Event 함수들 - TestCat에 위임
    public void OnWalkFrame1()
    {
        Debug.Log("🚶 CatPlayerAnimator에서 Animation Event 수신: OnWalkFrame1");
        if (testCat != null)
        {
            testCat.OnWalkFrame1();
        }
    }

    public void OnWalkFrame2()
    {
        Debug.Log("🚶 CatPlayerAnimator에서 Animation Event 수신: OnWalkFrame2");
        if (testCat != null)
        {
            testCat.OnWalkFrame2();
        }
    }

    public void OnWalkFrame3()
    {
        Debug.Log("🚶 CatPlayerAnimator에서 Animation Event 수신: OnWalkFrame3");
        if (testCat != null)
        {
            testCat.OnWalkFrame3();
        }
    }

    public void OnWalkFrame4()
    {
        Debug.Log("🚶 CatPlayerAnimator에서 Animation Event 수신: OnWalkFrame4");
        if (testCat != null)
        {
            testCat.OnWalkFrame4();
        }
    }

    public void OnSleepStart()
    {
        Debug.Log("😴 CatPlayerAnimator에서 Animation Event 수신: OnSleepStart");
        if (testCat != null)
        {
            testCat.OnSleepStart();
        }
    }

    public void OnWakeUp()
    {
        Debug.Log("😊 CatPlayerAnimator에서 Animation Event 수신: OnWakeUp");
        if (testCat != null)
        {
            testCat.OnWakeUp();
        }
    }

    public void OnIdleState()
    {
        Debug.Log("😐 CatPlayerAnimator에서 Animation Event 수신: OnIdleState");
        if (testCat != null)
        {
            testCat.OnIdleState();
        }
    }

    public void TestAnimationEvent()
    {
        Debug.Log("🎯 CatPlayerAnimator 테스트 애니메이션 이벤트 성공!");
        if (testCat != null)
        {
            testCat.TestAnimationEvent();
        }
    }

    // 이벤트 핸들러들
    void OnMovementStateChanged(CatMovementController.MovementState newMovementState)
    {
        if (forceStateActive) return; // 강제 상태 중에는 무시

        CatBaseState targetBaseState = ConvertMovementStateToBaseState(newMovementState);
        SetAnimationStateWithDirection(targetBaseState, currentDirection);

        Debug.Log($"Movement State 변경에 따른 Animation 업데이트: {newMovementState} → {targetBaseState} {currentDirection}");
    }

    void OnDirectionChanged(CatDirection newDirection)
    {
        if (currentDirection != newDirection)
        {
            currentDirection = newDirection;
            Debug.Log($"Direction 변경에 따른 Animation 업데이트: {currentDirection}");

            // 즉시 Animator 파라미터 업데이트
            if (animator != null)
            {
                animator.SetBool(PARAM_IS_FACING_RIGHT, currentDirection == CatDirection.Right);
            }

            // 현재 기본 상태 유지하면서 방향만 변경
            CatBaseState baseState = GetBaseState(currentState);
            SetAnimationStateWithDirection(baseState, currentDirection);
        }
    }

    CatBaseState ConvertMovementStateToBaseState(CatMovementController.MovementState movementState)
    {
        switch (movementState)
        {
            case CatMovementController.MovementState.Walking:
                return CatBaseState.Walk;
            case CatMovementController.MovementState.Sleeping:
                return CatBaseState.Sleep;
            case CatMovementController.MovementState.Idle:
            default:
                return CatBaseState.Idle;
        }
    }

    void UpdateForceState()
    {
        forceStateTimer += Time.deltaTime;

        if (forceStateTimer >= forceStateDuration)
        {
            // 강제 상태 종료
            forceStateActive = false;
            forceStateTimer = 0f;

            // 현재 MovementController의 상태에 맞춰 애니메이션 상태 복원
            if (movementController != null)
            {
                CatBaseState baseState = ConvertMovementStateToBaseState(movementController.CurrentMovementState);
                SetAnimationStateWithDirection(baseState, currentDirection);
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

        // 움직임 감지 (MovementController와 동기화)
        if (movementController != null)
        {
            isMoving = movementController.IsMoving && currentSpeed > moveThreshold;
        }
        else
        {
            // fallback: 속도 기반 감지
            isMoving = currentSpeed > moveThreshold;
        }

        // 위치 업데이트
        lastPosition = currentPosition;

        // 상태 타이머 업데이트
        stateTimer += Time.deltaTime;
    }

    void UpdateAnimationState()
    {
        // MovementController의 상태와 동기화 (이벤트로 이미 처리되지만 안전장치)
        if (movementController != null && !forceStateActive)
        {
            CatBaseState targetBaseState = ConvertMovementStateToBaseState(movementController.CurrentMovementState);
            CatBaseState currentBaseState = GetBaseState(currentState);

            if (targetBaseState != currentBaseState)
            {
                SetAnimationStateWithDirection(targetBaseState, currentDirection);
                Debug.Log($"Animation State 동기화: {targetBaseState} {currentDirection}");
            }
        }
    }

    // 기본 상태와 방향을 조합하여 최종 애니메이션 상태 설정
    void SetAnimationStateWithDirection(CatBaseState baseState, CatDirection direction)
    {
        CatAnimationState newState = CatAnimationState.IdleLeft;

        switch (baseState)
        {
            case CatBaseState.Idle:
                newState = direction == CatDirection.Left ? CatAnimationState.IdleLeft : CatAnimationState.IdleRight;
                break;
            case CatBaseState.Walk:
                newState = direction == CatDirection.Left ? CatAnimationState.WalkLeft : CatAnimationState.WalkRight;
                break;
            case CatBaseState.Sleep:
                newState = direction == CatDirection.Left ? CatAnimationState.SleepLeft : CatAnimationState.SleepRight;
                break;
        }

        SetAnimationState(newState);
    }

    void SetAnimationState(CatAnimationState newState)
    {
        Debug.Log($"고양이 애니메이션 상태 변경: {currentState} → {newState}");
        DebugLogger.LogToFile($"고양이 애니메이션 상태 변경: {currentState} → {newState}");

        currentState = newState;
        stateTimer = 0f;

        // 방향 업데이트
        currentDirection = GetDirection(newState);

        // 방향 변경 시 즉시 Animator 파라미터 업데이트
        if (animator != null)
        {
            animator.SetBool(PARAM_IS_FACING_RIGHT, currentDirection == CatDirection.Right);
        }

        // 상태별 특별한 처리
        CatBaseState baseState = GetBaseState(newState);
        switch (baseState)
        {
            case CatBaseState.Sleep:
                // 잠들 때 행복도 약간 회복
                if (GameDataManager.Instance != null)
                {
                    GameDataManager.Instance.happiness += 1f;
                    GameDataManager.Instance.happiness = Mathf.Clamp(GameDataManager.Instance.happiness, 0f, 100f);
                }
                Debug.Log($"고양이가 잠들었습니다 (+1 행복도) - 방향: {currentDirection}");
                break;

            case CatBaseState.Walk:
                Debug.Log($"고양이가 걷기 시작했습니다 - 방향: {currentDirection}");
                break;

            case CatBaseState.Idle:
                Debug.Log($"고양이가 가만히 있습니다 - 방향: {currentDirection}");
                break;
        }
    }

    void UpdateAnimatorParameters()
    {
        if (animator == null) return;

        CatBaseState baseState = GetBaseState(currentState);

        // Bool 파라미터 업데이트
        animator.SetBool(PARAM_IS_WALKING, baseState == CatBaseState.Walk);
        animator.SetBool(PARAM_IS_SLEEPING, baseState == CatBaseState.Sleep);
        animator.SetBool(PARAM_IS_FACING_RIGHT, currentDirection == CatDirection.Right);

        // Float 파라미터 업데이트
        animator.SetFloat(PARAM_SPEED, currentSpeed);

        // 디버그 정보 (60프레임마다 한 번씩만 출력)
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"Animator Parameters - Walking: {baseState == CatBaseState.Walk}, Sleeping: {baseState == CatBaseState.Sleep}, FacingRight: {currentDirection == CatDirection.Right}, Speed: {currentSpeed:F2}");
        }
    }

    // 유틸리티 메서드들
    CatBaseState GetBaseState(CatAnimationState animState)
    {
        switch (animState)
        {
            case CatAnimationState.IdleLeft:
            case CatAnimationState.IdleRight:
                return CatBaseState.Idle;
            case CatAnimationState.WalkLeft:
            case CatAnimationState.WalkRight:
                return CatBaseState.Walk;
            case CatAnimationState.SleepLeft:
            case CatAnimationState.SleepRight:
                return CatBaseState.Sleep;
            default:
                return CatBaseState.Idle;
        }
    }

    CatDirection GetDirection(CatAnimationState animState)
    {
        switch (animState)
        {
            case CatAnimationState.IdleLeft:
            case CatAnimationState.WalkLeft:
            case CatAnimationState.SleepLeft:
                return CatDirection.Left;
            case CatAnimationState.IdleRight:
            case CatAnimationState.WalkRight:
            case CatAnimationState.SleepRight:
                return CatDirection.Right;
            default:
                return CatDirection.Right;
        }
    }

    // 외부에서 강제로 상태 변경
    public void ForceState(CatBaseState baseState)
    {
        forceStateActive = false; // 기존 강제 상태 해제
        SetAnimationStateWithDirection(baseState, currentDirection);
        Debug.Log($"애니메이션 상태 강제 변경: {baseState} {currentDirection}");
    }

    public void ForceStateWithDirection(CatBaseState baseState, CatDirection direction)
    {
        forceStateActive = false; // 기존 강제 상태 해제
        currentDirection = direction;
        SetAnimationStateWithDirection(baseState, direction);
        Debug.Log($"애니메이션 상태와 방향 강제 변경: {baseState} {direction}");
    }

    // 일시적 애니메이션 재생 (더 안전한 방식)
    public void PlayTemporaryAnimationSafe(CatBaseState temporaryBaseState, float duration)
    {
        // 기존 강제 상태가 있다면 중단
        forceStateActive = false;

        // 방향을 포함한 최종 상태 결정
        CatAnimationState tempState;
        switch (temporaryBaseState)
        {
            case CatBaseState.Idle:
                tempState = currentDirection == CatDirection.Left ? CatAnimationState.IdleLeft : CatAnimationState.IdleRight;
                break;
            case CatBaseState.Walk:
                tempState = currentDirection == CatDirection.Left ? CatAnimationState.WalkLeft : CatAnimationState.WalkRight;
                break;
            case CatBaseState.Sleep:
                tempState = currentDirection == CatDirection.Left ? CatAnimationState.SleepLeft : CatAnimationState.SleepRight;
                break;
            default:
                tempState = currentDirection == CatDirection.Left ? CatAnimationState.IdleLeft : CatAnimationState.IdleRight;
                break;
        }

        // 새로운 강제 상태 설정
        forcedState = tempState;
        forceStateDuration = duration;
        forceStateTimer = 0f;
        forceStateActive = true;

        SetAnimationState(tempState);

        Debug.Log($"일시적 애니메이션 재생: {temporaryBaseState} {currentDirection} ({duration}초)");
        DebugLogger.LogToFile($"일시적 애니메이션 재생: {temporaryBaseState} {currentDirection} ({duration}초)");
    }

    // 강제로 잠들게 하기
    public void MakeSleep()
    {
        ForceState(CatBaseState.Sleep);
        if (movementController != null)
        {
            movementController.MakeCatSleep();
        }
    }

    // 강제로 깨우기
    public void WakeUp()
    {
        ForceState(CatBaseState.Idle);
        if (movementController != null)
        {
            movementController.WakeCatUp();
        }
    }

    // 디버그 정보 표시
    void OnGUI()
    {
        if (Debug.isDebugBuild)
        {
            GUILayout.BeginArea(new Rect(10, 10, 400, 300));
            GUILayout.Label($"=== Cat Animation Debug (리팩터링됨) ===");
            GUILayout.Label($"Current State: {currentState}");
            GUILayout.Label($"Base State: {GetBaseState(currentState)}");
            GUILayout.Label($"Direction: {currentDirection}");
            GUILayout.Label($"Is Moving: {isMoving}");
            GUILayout.Label($"Speed: {currentSpeed:F2}");
            GUILayout.Label($"State Timer: {stateTimer:F1}s");
            GUILayout.Label($"Force State Active: {forceStateActive}");

            if (forceStateActive)
            {
                GUILayout.Label($"Forced State: {forcedState} ({forceStateTimer:F1}/{forceStateDuration:F1}s)");
            }

            // 컴포넌트 상태 표시
            if (movementController != null)
            {
                GUILayout.Label($"MovementController State: {movementController.CurrentMovementState}");
                GUILayout.Label($"MovementController Moving: {movementController.IsMoving}");
                GUILayout.Label($"MovementController Sleeping: {movementController.IsSleeping}");
            }

            if (directionTracker != null)
            {
                GUILayout.Label($"DirectionTracker Direction: {directionTracker.CurrentFacingDirection}");
            }

            GUILayout.Space(10);

            // 방향별 테스트 버튼들
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Idle Left"))
                ForceStateWithDirection(CatBaseState.Idle, CatDirection.Left);
            if (GUILayout.Button("Idle Right"))
                ForceStateWithDirection(CatBaseState.Idle, CatDirection.Right);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Walk Left"))
                ForceStateWithDirection(CatBaseState.Walk, CatDirection.Left);
            if (GUILayout.Button("Walk Right"))
                ForceStateWithDirection(CatBaseState.Walk, CatDirection.Right);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Sleep Left"))
                ForceStateWithDirection(CatBaseState.Sleep, CatDirection.Left);
            if (GUILayout.Button("Sleep Right"))
                ForceStateWithDirection(CatBaseState.Sleep, CatDirection.Right);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Play Temp Idle (2s)"))
                PlayTemporaryAnimationSafe(CatBaseState.Idle, 2f);

            GUILayout.EndArea();
        }
    }

    // 외부에서 접근할 수 있는 프로퍼티들
    public bool IsWalking => GetBaseState(currentState) == CatBaseState.Walk;
    public bool IsSleeping => GetBaseState(currentState) == CatBaseState.Sleep;
    public bool IsIdle => GetBaseState(currentState) == CatBaseState.Idle;
    public bool IsFacingLeft => currentDirection == CatDirection.Left;
    public bool IsFacingRight => currentDirection == CatDirection.Right;
    public float CurrentSpeed => currentSpeed;
    public bool IsForceStateActive => forceStateActive;
    public CatDirection CurrentDirection => currentDirection;
    public CatBaseState CurrentBaseState => GetBaseState(currentState);
}