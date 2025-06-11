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

    // 애니메이션 파라미터 이름 (Animator Controller와 일치해야 함)
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

        // 초기 상태 설정 (Animator Controller의 기본 상태와 일치시키기)
        SetAnimationState(CatAnimationState.IdleLeft);

        Debug.Log("CatPlayerAnimator 초기화 완료 - 방향별 애니메이션 시스템");
        DebugLogger.LogToFile("CatPlayerAnimator 초기화 완료 - 방향별 애니메이션 시스템");
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
                CatBaseState baseState = GetBaseStateFromTestCat();
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
        Vector3 movement = currentPosition - lastPosition;
        float distance = Vector3.Distance(currentPosition, lastPosition);
        currentSpeed = distance / Time.deltaTime;

        // 움직임 감지 (더 정확한 임계값 적용)
        isMoving = currentSpeed > moveThreshold;

        // TestCat에서 방향 정보를 가져오기 (TestCat이 방향 감지의 주체)
        if (testCat != null)
        {
            // TestCat의 현재 방향을 사용
            CatDirection testCatDirection = testCat.CurrentFacingDirection;

            // 방향이 바뀌었을 때만 업데이트
            if (testCatDirection != currentDirection)
            {
                currentDirection = testCatDirection;
                Debug.Log($"애니메이터: TestCat으로부터 방향 업데이트 - {currentDirection}");

                // 즉시 Animator 파라미터 업데이트
                if (animator != null)
                {
                    animator.SetBool(PARAM_IS_FACING_RIGHT, currentDirection == CatDirection.Right);
                    Debug.Log($"Animator 파라미터 즉시 업데이트: IsFacingRight = {currentDirection == CatDirection.Right}");
                }

                // 현재 기본 상태 유지하면서 방향만 변경
                CatBaseState baseState = GetBaseState(currentState);
                SetAnimationStateWithDirection(baseState, currentDirection);
            }

            // TestCat의 실제 움직임 상태 확인
            bool testCatIsMoving = testCat.IsMoving;

            // TestCat이 실제로 움직이고 있고, 속도도 임계값을 넘을 때만 움직임으로 판정
            isMoving = testCatIsMoving && currentSpeed > moveThreshold;

            //Debug.Log($"Movement Detection - TestCat Moving: {testCatIsMoving}, Speed: {currentSpeed:F3}, IsMoving: {isMoving}, Direction: {currentDirection}, IsFacingRight: {currentDirection == CatDirection.Right}");
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
            CatBaseState targetBaseState = CatBaseState.Idle;

            // TestCat의 MovementState에 따라 기본 애니메이션 상태 결정
            switch (testCat.CurrentMovementState)
            {
                case TestCat.MovementState.Walking:
                    targetBaseState = CatBaseState.Walk;
                    break;

                case TestCat.MovementState.Idle:
                    targetBaseState = CatBaseState.Idle;
                    break;

                case TestCat.MovementState.Sleeping:
                    targetBaseState = CatBaseState.Sleep;
                    break;
            }

            // 현재 기본 상태와 다르면 방향을 포함한 새 상태로 변경
            CatBaseState currentBaseState = GetBaseState(currentState);
            if (targetBaseState != currentBaseState)
            {
                SetAnimationStateWithDirection(targetBaseState, currentDirection);
                Debug.Log($"Animation State Update - TestCat State: {testCat.CurrentMovementState}, Target Animation: {targetBaseState} {currentDirection}");
            }
        }
        else
        {
            // TestCat 참조가 없을 때의 기본 로직 (속도 기반)
            CatBaseState currentBaseState = GetBaseState(currentState);
            CatBaseState newBaseState = currentBaseState;

            switch (currentBaseState)
            {
                case CatBaseState.Idle:
                    if (isMoving)
                    {
                        newBaseState = CatBaseState.Walk;
                    }
                    else if (stateTimer >= sleepTime)
                    {
                        newBaseState = CatBaseState.Sleep;
                    }
                    break;

                case CatBaseState.Walk:
                    if (!isMoving)
                    {
                        newBaseState = CatBaseState.Idle;
                    }
                    break;

                case CatBaseState.Sleep:
                    if (isMoving)
                    {
                        newBaseState = CatBaseState.Walk;
                    }
                    else if (currentSpeed > moveThreshold * 2f)
                    {
                        newBaseState = CatBaseState.Idle;
                    }
                    break;
            }

            // 상태 변경
            if (newBaseState != currentBaseState)
            {
                SetAnimationStateWithDirection(newBaseState, currentDirection);
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
            Debug.Log($"SetAnimationState에서 IsFacingRight 파라미터 업데이트: {currentDirection == CatDirection.Right}");
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

        // Float 파라미터 업데이트 (애니메이션 속도 조절용)
        animator.SetFloat(PARAM_SPEED, currentSpeed);

        // 디버그 정보 (너무 많이 출력되지 않도록 조건부)
        if (Time.frameCount % 60 == 0) // 1초마다 한 번씩만 출력
        {
            Debug.Log($"Animator Parameters - Walking: {baseState == CatBaseState.Walk}, Sleeping: {baseState == CatBaseState.Sleep}, FacingRight: {currentDirection == CatDirection.Right}, Speed: {currentSpeed:F2}");
        }

        // 스프라이트 뒤집기는 더 이상 사용하지 않음 (애니메이션 클립으로 처리)
        // if (spriteRenderer != null)
        // {
        //     spriteRenderer.flipX = currentDirection == CatDirection.Left;
        // }
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

    CatBaseState GetBaseStateFromTestCat()
    {
        if (testCat == null) return CatBaseState.Idle;

        switch (testCat.CurrentMovementState)
        {
            case TestCat.MovementState.Walking:
                return CatBaseState.Walk;
            case TestCat.MovementState.Sleeping:
                return CatBaseState.Sleep;
            default:
                return CatBaseState.Idle;
        }
    }

    // 외부에서 강제로 상태 변경 (방향 고려)
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

    // 특정 상태로 일시적 변경 (예: 먹이를 먹을 때)
    public void PlayTemporaryAnimation(CatBaseState temporaryBaseState, float duration)
    {
        StartCoroutine(TemporaryStateCoroutine(temporaryBaseState, duration));
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

    private System.Collections.IEnumerator TemporaryStateCoroutine(CatBaseState tempBaseState, float duration)
    {
        CatAnimationState originalState = currentState;
        SetAnimationStateWithDirection(tempBaseState, currentDirection);

        yield return new WaitForSeconds(duration);

        // TestCat의 현재 상태에 맞춰 복원
        if (testCat != null)
        {
            CatBaseState restoreState = GetBaseStateFromTestCat();
            SetAnimationStateWithDirection(restoreState, currentDirection);
        }
        else
        {
            SetAnimationState(originalState);
        }
    }

    // 강제로 잠들게 하기 (TestCat과 연동)
    public void MakeSleep()
    {
        ForceState(CatBaseState.Sleep);
        if (testCat != null)
        {
            testCat.MakeCatSleep();
        }
    }

    // 강제로 깨우기 (TestCat과 연동)
    public void WakeUp()
    {
        ForceState(CatBaseState.Idle);
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
            GUILayout.BeginArea(new Rect(10, 10, 400, 250));
            GUILayout.Label($"=== Cat Animation Debug (Directional) ===");
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

            if (testCat != null)
            {
                GUILayout.Label($"TestCat State: {testCat.CurrentMovementState}");
                GUILayout.Label($"TestCat Moving: {testCat.IsMoving}");
                GUILayout.Label($"TestCat Sleeping: {testCat.IsSleeping}");
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

    // 기존 코드와의 호환성을 위한 메서드들
    public void PlayTemporaryAnimation(CatPlayerAnimator.CatAnimationState oldState, float duration)
    {
        // 기존 enum을 새 시스템으로 변환
        CatBaseState baseState = CatBaseState.Idle;

        // 기존 enum에서 기본 상태 추출 (방향 정보는 무시)
        string stateName = oldState.ToString();
        if (stateName.Contains("Walk"))
            baseState = CatBaseState.Walk;
        else if (stateName.Contains("Sleep"))
            baseState = CatBaseState.Sleep;
        else
            baseState = CatBaseState.Idle;

        PlayTemporaryAnimation(baseState, duration);
    }

    // 기존 enum과의 호환성을 위한 정의 (기존 코드에서 사용하는 경우)
    public enum OldCatAnimationState
    {
        Idle,
        Walk,
        Sleep
    }
}