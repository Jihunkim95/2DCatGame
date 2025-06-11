using UnityEngine;

/// <summary>
/// 리팩터링된 TestCat 클래스 - 580줄에서 150줄로 축소
/// 각 기능을 별도 컴포넌트로 분리하여 단순화
/// </summary>
public class TestCat : MonoBehaviour
{
    [Header("애니메이션")]
    public CatPlayerAnimator catAnimator;

    // 컴포넌트들
    private CatMovementController movementController;
    private CatDirectionTracker directionTracker;
    private CatInteractionHandler interactionHandler;
    private CatSpriteManager spriteManager;

    // 싱글톤 (ContextMenuManager에서 접근하기 위해)
    public static TestCat Instance { get; private set; }

    void Awake()
    {
        // 싱글톤 설정
        if (Instance == null)
        {
            Instance = this;
        }
    }

    void Start()
    {
        InitializeComponents();
        SetupEventListeners();

        Debug.Log("리팩터링된 TestCat 초기화 완료 - 컴포넌트 기반 아키텍처");
        DebugLogger.LogToFile("리팩터링된 TestCat 초기화 완료 - 컴포넌트 기반 아키텍처");
    }

    void InitializeComponents()
    {
        // 애니메이터 참조 설정
        if (catAnimator == null)
            catAnimator = GetComponent<CatPlayerAnimator>();

        // 각 컴포넌트들 초기화
        InitializeMovementController();
        InitializeDirectionTracker();
        InitializeInteractionHandler();
        InitializeSpriteManager();
    }

    void InitializeMovementController()
    {
        movementController = GetComponent<CatMovementController>();
        if (movementController == null)
        {
            movementController = gameObject.AddComponent<CatMovementController>();
        }
    }

    void InitializeDirectionTracker()
    {
        directionTracker = GetComponent<CatDirectionTracker>();
        if (directionTracker == null)
        {
            directionTracker = gameObject.AddComponent<CatDirectionTracker>();
        }
    }

    void InitializeInteractionHandler()
    {
        interactionHandler = GetComponent<CatInteractionHandler>();
        if (interactionHandler == null)
        {
            interactionHandler = gameObject.AddComponent<CatInteractionHandler>();
        }
    }

    void InitializeSpriteManager()
    {
        spriteManager = GetComponent<CatSpriteManager>();
        if (spriteManager == null)
        {
            spriteManager = gameObject.AddComponent<CatSpriteManager>();
        }
    }

    void SetupEventListeners()
    {
        // 움직임 상태 변경 이벤트
        if (movementController != null)
        {
            movementController.OnMovementStateChanged += OnMovementStateChanged;
        }

        // 방향 변경 이벤트
        if (directionTracker != null)
        {
            directionTracker.OnDirectionChanged += OnDirectionChanged;
        }

        // 상호작용 이벤트들
        if (interactionHandler != null)
        {
            interactionHandler.OnCatClicked += OnCatClicked;
            interactionHandler.OnCatRightClicked += OnCatRightClicked;
            interactionHandler.OnCatHovered += OnCatHovered;
        }
    }

    // 이벤트 핸들러들
    void OnMovementStateChanged(CatMovementController.MovementState newState)
    {
        Debug.Log($"TestCat: 움직임 상태 변경됨 - {newState}");

        // 애니메이터에 상태 변경 알림 (자동으로 처리됨)
        // CatPlayerAnimator가 movementController의 상태를 자동으로 감지함
    }

    void OnDirectionChanged(CatPlayerAnimator.CatDirection newDirection)
    {
        Debug.Log($"TestCat: 방향 변경됨 - {newDirection}");

        // 애니메이터에 즉시 반영
        if (catAnimator != null)
        {
            // CatPlayerAnimator가 directionTracker의 방향을 자동으로 감지함
        }
    }

    void OnCatClicked()
    {
        // 잠들어 있었다면 깨우기
        if (IsSleeping)
        {
            WakeCatUp();
        }

        // 애니메이션: 잠깐 아이들 상태로 (반응 표현)
        if (catAnimator != null)
        {
            catAnimator.PlayTemporaryAnimationSafe(CatPlayerAnimator.CatBaseState.Idle, 1f);
        }

        // 상호작용 처리
        if (interactionHandler != null)
        {
            interactionHandler.PerformPetting();
        }
    }

    void OnCatRightClicked(Vector3 mousePosition)
    {
        // 잠들어 있었다면 깨우기
        if (IsSleeping)
        {
            WakeCatUp();
        }

        // 컨텍스트 메뉴 표시
        if (ContextMenuManager.Instance != null)
        {
            ContextMenuManager.Instance.ShowCatMenu(mousePosition);
        }

        // 애니메이션: 잠깐 아이들 상태로 (관심 표현)
        if (catAnimator != null)
        {
            catAnimator.PlayTemporaryAnimationSafe(CatPlayerAnimator.CatBaseState.Idle, 0.5f);
        }
    }

    void OnCatHovered()
    {
        Debug.Log("고양이에 마우스를 올렸습니다!");
    }

    // 외부에서 호출할 수 있는 메서드들 (컨텍스트 메뉴에서 사용)
    public void FeedCat()
    {
        // 잠들어 있었다면 깨우기
        if (IsSleeping)
        {
            WakeCatUp();
        }

        if (catAnimator != null)
        {
            // 먹이를 먹는 동안 잠깐 아이들 상태로
            catAnimator.PlayTemporaryAnimationSafe(CatPlayerAnimator.CatBaseState.Idle, 2f);
        }

        // 상호작용 처리
        if (interactionHandler != null)
        {
            interactionHandler.PerformFeeding();
        }

        Debug.Log("고양이가 먹이를 먹었습니다!");
    }

    public void PetCat()
    {
        // 잠들어 있었다면 깨우기
        if (IsSleeping)
        {
            WakeCatUp();
        }

        if (catAnimator != null)
        {
            // 쓰다듬기 동안 아이들 상태로
            catAnimator.PlayTemporaryAnimationSafe(CatPlayerAnimator.CatBaseState.Idle, 1.5f);
        }

        // 상호작용 처리
        if (interactionHandler != null)
        {
            interactionHandler.PerformPetting();
        }

        Debug.Log("고양이를 쓰다듬었습니다!");
    }

    // 움직임 상태 제어
    public void MakeCatSleep()
    {
        if (movementController != null)
        {
            movementController.MakeCatSleep();
        }
    }

    public void WakeCatUp()
    {
        if (movementController != null)
        {
            movementController.WakeCatUp();
        }
    }

    // 방향 제어
    public void SetFacingDirection(CatPlayerAnimator.CatDirection direction)
    {
        if (directionTracker != null)
        {
            directionTracker.SetFacingDirection(direction);
        }
    }

    // 스프라이트 관리
    public void UpdateOriginalSpriteInfo()
    {
        if (spriteManager != null)
        {
            spriteManager.UpdateOriginalSpriteInfo();
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

        if (interactionHandler != null)
        {
            interactionHandler.OnCatClicked -= OnCatClicked;
            interactionHandler.OnCatRightClicked -= OnCatRightClicked;
            interactionHandler.OnCatHovered -= OnCatHovered;
        }
    }

    // 외부에서 접근할 수 있는 프로퍼티들 (기존 코드와의 호환성)
    public bool IsMoving => movementController != null ? movementController.IsMoving : false;
    public bool IsSleeping => movementController != null ? movementController.IsSleeping : false;
    public bool IsIdle => movementController != null ? movementController.IsIdle : false;

    public CatMovementController.MovementState CurrentMovementState =>
        movementController != null ? movementController.CurrentMovementState : CatMovementController.MovementState.Idle;

    public CatPlayerAnimator.CatDirection CurrentFacingDirection =>
        directionTracker != null ? directionTracker.CurrentFacingDirection : CatPlayerAnimator.CatDirection.Left;

    public CatPlayerAnimator.CatAnimationState CurrentAnimationState =>
        catAnimator != null ? catAnimator.currentState : CatPlayerAnimator.CatAnimationState.IdleRight;

    // 컴포넌트 접근용 프로퍼티들 (확장성)
    public CatMovementController MovementController => movementController;
    public CatDirectionTracker DirectionTracker => directionTracker;
    public CatInteractionHandler InteractionHandler => interactionHandler;
    public CatSpriteManager SpriteManager => spriteManager;
}