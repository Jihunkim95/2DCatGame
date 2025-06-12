using UnityEngine;
using System.Collections;

/// <summary>
/// 리팩터링된 TestCat 클래스 - 아이템 시스템 통합
/// 각 기능을 별도 컴포넌트로 분리하여 단순화 + 아이템 기능 추가
/// </summary>
public class TestCat : MonoBehaviour
{
    [Header("애니메이션")]
    public CatPlayerAnimator catAnimator;

    [Header("아이템 시스템")]
    public Transform hatPoint; // 모자 착용 위치
    public bool useAnimationEvents = false; // 애니메이션 이벤트 사용 여부 (기본값: false)

    [Header("모자 위치 오프셋")]
    public Vector3 walkHatOffset = new Vector3(0, 0.02f, 0);
    public Vector3 sleepHatOffset = new Vector3(0, -0.35f, 0);

    // 컴포넌트들
    private CatMovementController movementController;
    private CatDirectionTracker directionTracker;
    private CatInteractionHandler interactionHandler;
    private CatSpriteManager spriteManager;

    // 아이템 관련
    private GameObject currentHat; // 현재 착용 중인 모자
    private CatPlayerAnimator.CatDirection lastDirection = CatPlayerAnimator.CatDirection.Left;

    // 싱글톤 (ContextMenuManager에서 접근하기 위해)
    public static TestCat Instance { get; private set; }

    void Awake()
    {
        // 싱글톤 설정
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
        InitializeComponents();
        SetupEventListeners();
        SetupItemSystem();

        Debug.Log("리팩터링된 TestCat 초기화 완료 - 컴포넌트 기반 아키텍처 + 아이템 시스템");
        DebugLogger.LogToFile("리팩터링된 TestCat 초기화 완료 - 컴포넌트 기반 아키텍처 + 아이템 시스템");
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

    void SetupItemSystem()
    {
        // 모자 착용 포인트 설정
        SetupHatPoint();

        // 저장된 모자 복원 (1초 후 - ItemManager 로드 대기)
        Invoke(nameof(RestoreEquippedHat), 1f);
    }

    void SetupHatPoint()
    {
        // 모자 착용 포인트가 없으면 자동 생성
        if (hatPoint == null)
        {
            GameObject hatPointObj = new GameObject("HatPoint");
            hatPointObj.transform.SetParent(transform);
            hatPointObj.transform.localPosition = new Vector3(0.0f, 0.3f, 0);
            hatPoint = hatPointObj.transform;
            Debug.Log("모자 착용 포인트 자동 생성");
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

    void Update()
    {
        // 방향 변경 감지 (모자 업데이트용)
        CheckDirectionChange();
    }

    void CheckDirectionChange()
    {
        if (catAnimator != null && catAnimator.CurrentDirection != lastDirection)
        {
            lastDirection = catAnimator.CurrentDirection;
            UpdateHatDirection();
            Debug.Log($"고양이 방향 변경 감지: {lastDirection}");
        }

        // 애니메이션 이벤트를 사용하지 않는 경우에만 자동 조정
        if (!useAnimationEvents && catAnimator != null)
        {
            UpdateHatPositionForFallback(catAnimator.currentState);
        }

        // 추가: 더 정밀한 애니메이션 기반 조정이 필요한 경우
        if (useAnimationEvents && catAnimator != null && catAnimator.animator != null)
        {
            UpdateHatPositionForAnimation();
        }
    }

    // 추가: 애니메이션 진행도 기반 정밀 조정
    void UpdateHatPositionForAnimation()
    {
        if (currentHat == null || hatPoint == null) return;

        AnimatorStateInfo stateInfo = catAnimator.animator.GetCurrentAnimatorStateInfo(0);

        // 걷기 애니메이션의 경우 진행도에 따른 미세 조정
        if (stateInfo.IsName("Walk") || stateInfo.IsName("WalkLeft") || stateInfo.IsName("WalkRight"))
        {
            float normalizedTime = stateInfo.normalizedTime % 1f;
            Vector3 walkOffset = CalculateWalkOffset(normalizedTime);

            Vector3 basePosition = new Vector3(0, 0.3f, 0);
            hatPoint.localPosition = basePosition + walkOffset;
        }
    }

    Vector3 CalculateWalkOffset(float normalizedTime)
    {
        // 걷기 애니메이션 진행도에 따른 오프셋 계산
        Vector3 offset = Vector3.zero;

        if (normalizedTime < 0.25f) // 첫 번째 스텝
        {
            float t = normalizedTime / 0.25f;
            offset.y = Mathf.Lerp(0f, 0.02f, t);
            offset.x = Mathf.Lerp(0f, 0.01f, t);
        }
        else if (normalizedTime < 0.5f) // 두 번째 스텝
        {
            float t = (normalizedTime - 0.25f) / 0.25f;
            offset.y = Mathf.Lerp(0.02f, -0.01f, t);
            offset.x = Mathf.Lerp(0.01f, 0f, t);
        }
        else if (normalizedTime < 0.75f) // 세 번째 스텝
        {
            float t = (normalizedTime - 0.5f) / 0.25f;
            offset.y = Mathf.Lerp(-0.01f, 0.02f, t);
            offset.x = Mathf.Lerp(0f, -0.01f, t);
        }
        else // 네 번째 스텝
        {
            float t = (normalizedTime - 0.75f) / 0.25f;
            offset.y = Mathf.Lerp(0.02f, 0f, t);
            offset.x = Mathf.Lerp(-0.01f, 0f, t);
        }

        return offset;
    }

    // 애니메이션 이벤트가 없을 때의 백업 메서드
    void UpdateHatPositionForFallback(CatPlayerAnimator.CatAnimationState animState)
    {
        if (currentHat == null || hatPoint == null) return;

        Vector3 basePosition = new Vector3(0, 0.3f, 0);
        Vector3 targetPosition = basePosition;

        switch (animState)
        {
            case CatPlayerAnimator.CatAnimationState.SleepLeft:
            case CatPlayerAnimator.CatAnimationState.SleepRight:
                targetPosition = basePosition + sleepHatOffset;
                break;

            case CatPlayerAnimator.CatAnimationState.WalkLeft:
            case CatPlayerAnimator.CatAnimationState.WalkRight:
                targetPosition = basePosition + walkHatOffset;
                break;

            default:
                targetPosition = basePosition;
                break;
        }

        hatPoint.localPosition = Vector3.Lerp(hatPoint.localPosition, targetPosition, Time.deltaTime * 5f);
    }

    // 애니메이션 이벤트에서 호출할 메서드들
    public void OnWalkFrame1()
    {
        Debug.Log("🚶 애니메이션 이벤트 호출: OnWalkFrame1");
        if (useAnimationEvents) SetHatOffset(new Vector3(0.01f, 0.02f, 0));
    }

    public void OnWalkFrame2()
    {
        Debug.Log("🚶 애니메이션 이벤트 호출: OnWalkFrame2");
        if (useAnimationEvents) SetHatOffset(new Vector3(0f, -0.01f, 0));
    }

    public void OnWalkFrame3()
    {
        Debug.Log("🚶 애니메이션 이벤트 호출: OnWalkFrame3");
        if (useAnimationEvents) SetHatOffset(new Vector3(-0.01f, 0.02f, 0));
    }

    public void OnWalkFrame4()
    {
        Debug.Log("🚶 애니메이션 이벤트 호출: OnWalkFrame4");
        if (useAnimationEvents) SetHatOffset(new Vector3(0f, 0f, 0));
    }

    public void OnSleepStart()
    {
        Debug.Log($" 애니메이션 이벤트 호출: OnSleepStart{sleepHatOffset}, useAnimationEvents:{useAnimationEvents}");
        if (useAnimationEvents) SetHatOffset(sleepHatOffset);
    }

    public void OnWakeUp()
    {
        Debug.Log("😊 애니메이션 이벤트 호출: OnWakeUp");
        if (useAnimationEvents) SetHatOffset(Vector3.zero);
    }

    public void OnIdleState()
    {
        Debug.Log("😐 애니메이션 이벤트 호출: OnIdleState");
        if (useAnimationEvents) SetHatOffset(Vector3.zero);
    }

    // 테스트용 간단한 함수
    public void TestAnimationEvent()
    {
        Debug.Log("🎯 테스트 애니메이션 이벤트 성공!");
    }

    void SetHatOffset(Vector3 offset)
    {
        if (hatPoint != null && useAnimationEvents)
        {
            Vector3 basePosition = new Vector3(0.2f, -0.5f, 0);
            Vector3 targetPosition = basePosition + offset;
            // 부드러운 전환
            StartCoroutine(SmoothMoveHat(targetPosition, 0.1f));
        }
    }

    IEnumerator SmoothMoveHat(Vector3 targetPosition, float duration)
    {
        if (hatPoint == null) yield break;

        Vector3 startPosition = hatPoint.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            hatPoint.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        hatPoint.localPosition = targetPosition;
    }

    void UpdateHatDirection()
    {
        if (currentHat != null)
        {
            SpriteRenderer hatRenderer = currentHat.GetComponent<SpriteRenderer>();
            if (hatRenderer != null)
            {
                bool facingRight = (lastDirection == CatPlayerAnimator.CatDirection.Right);
                hatRenderer.flipX = facingRight;
                Debug.Log($"모자 방향 업데이트: {(facingRight ? "오른쪽" : "왼쪽")}");
            }
        }
    }

    // 이벤트 핸들러들
    void OnMovementStateChanged(CatMovementController.MovementState newState)
    {
        Debug.Log($"TestCat: 움직임 상태 변경됨 - {newState}");
        // CatPlayerAnimator가 movementController의 상태를 자동으로 감지함
    }

    void OnDirectionChanged(CatPlayerAnimator.CatDirection newDirection)
    {
        Debug.Log($"TestCat: 방향 변경됨 - {newDirection}");
        // 모자 방향도 업데이트 (CheckDirectionChange에서 처리)
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

        // 모자 착용 시 추가 효과
        ApplyHatInteractionBonus("쓰다듬기");
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

    // 아이템 시스템 메서드들
    public void EquipHat(ItemData hatItem)
    {
        if (hatItem == null) return;

        // 기존 모자 제거
        UnequipHat();

        // 새 모자 생성
        GameObject hatObj = new GameObject($"Hat_{hatItem.itemName}");
        SpriteRenderer spriteRenderer = hatObj.AddComponent<SpriteRenderer>();

        // 스프라이트 설정
        spriteRenderer.sprite = hatItem.itemSprite;
        spriteRenderer.sortingOrder = hatItem.sortingOrder;

        // 위치 설정
        hatObj.transform.SetParent(hatPoint);
        hatObj.transform.localPosition = hatItem.positionOffset;
        hatObj.transform.localRotation = Quaternion.Euler(hatItem.rotationOffset);
        hatObj.transform.localScale = hatItem.scaleMultiplier;

        currentHat = hatObj;

        // 현재 방향에 맞춰 모자 뒤집기
        UpdateHatDirection();

        // 현재 애니메이션 상태에 맞춰 모자 위치 조정
        if (catAnimator != null)
        {
            UpdateHatPositionForFallback(catAnimator.currentState);
        }

        Debug.Log($"모자 착용 완료: {hatItem.itemName}");
        DebugLogger.LogToFile($"모자 착용 완료: {hatItem.itemName}");
    }

    public void UnequipHat()
    {
        if (currentHat != null)
        {
            string hatName = currentHat.name;
            Destroy(currentHat);
            currentHat = null;

            Debug.Log($"모자 해제 완료: {hatName}");
            DebugLogger.LogToFile($"모자 해제 완료: {hatName}");
        }
    }

    void RestoreEquippedHat()
    {
        if (ItemManager.Instance != null && ItemManager.Instance.EquippedItem != null)
        {
            EquipHat(ItemManager.Instance.EquippedItem);
            Debug.Log("저장된 모자 복원 완료");
        }
    }

    void ApplyHatInteractionBonus(string interactionType)
    {
        if (currentHat == null || ItemManager.Instance == null || ItemManager.Instance.EquippedItem == null)
            return;

        var equippedHat = ItemManager.Instance.EquippedItem;
        Debug.Log($"모자 착용 중 {interactionType}: {equippedHat.itemName} - 특별 효과!");

        // 모자별 특별 효과
        ApplyHatSpecialEffect(equippedHat, interactionType);
    }

    void ApplyHatSpecialEffect(ItemData hat, string interactionType)
    {
        if (GameDataManager.Instance == null) return;

        float bonusHappiness = 0f;
        int bonusChur = 0;

        switch (hat.itemName)
        {
            case "왕관":
                bonusHappiness = 3f;
                Debug.Log("👑 왕관 효과: 고급스러운 행복도 증가!");
                break;

            case "마법사 모자":
                bonusHappiness = 1f;
                bonusChur = 1;
                Debug.Log("🎩 마법사 모자 효과: 마법으로 츄르 1개 생성!");
                break;

            case "해적 모자":
                bonusHappiness = 2f;
                Debug.Log("🏴‍☠️ 해적 모자 효과: 모험심 자극으로 행복도 증가!");
                break;

            case "빨간 모자":
            case "파란 모자":
                bonusHappiness = 1f;
                Debug.Log($"🧢 {hat.itemName} 효과: 활기찬 에너지!");
                break;

            default:
                bonusHappiness = 0.5f;
                Debug.Log($"✨ {hat.itemName} 효과: 패션센스로 인한 기분 좋음!");
                break;
        }

        // 효과 적용
        if (bonusHappiness > 0)
        {
            GameDataManager.Instance.happiness += bonusHappiness;
            GameDataManager.Instance.happiness = Mathf.Clamp(GameDataManager.Instance.happiness, 0f, 100f);
        }

        if (bonusChur > 0 && CatTower.Instance != null)
        {
            CatTower.Instance.churCount += bonusChur;
        }
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

        // 모자 착용 시 추가 효과
        ApplyHatInteractionBonus("먹이주기");

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

        // 모자 착용 시 추가 효과
        ApplyHatInteractionBonus("쓰다듬기");

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

    // 아이템 관련 프로퍼티들
    public bool HasHatEquipped => currentHat != null;
    public string CurrentHatName => currentHat != null ? currentHat.name : "없음";

    // 컴포넌트 접근용 프로퍼티들 (확장성)
    public CatMovementController MovementController => movementController;
    public CatDirectionTracker DirectionTracker => directionTracker;
    public CatInteractionHandler InteractionHandler => interactionHandler;
    public CatSpriteManager SpriteManager => spriteManager;
}