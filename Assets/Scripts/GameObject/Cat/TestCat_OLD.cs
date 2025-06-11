using UnityEngine;
using static TestCat_OLD;

public class TestCat_OLD : MonoBehaviour
{
    //[Header("고양이 설정")]
    //public SpriteRenderer spriteRenderer;
    //public Color normalColor = Color.cyan; // 흰색과 구별되도록 변경
    //public Color hoverColor = Color.yellow;
    //public Color clickColor = Color.green;

    //[Header("이동 설정")]
    //public float moveSpeed = 1.5f; // 이동 속도 약간 감소
    //public float changeDirectionTime = 2f; // 방향 변경 주기 단축 (더 자주 멈춤)
    //public float pauseTime = 5f; // idle 상태 유지 시간 증가 (2초 → 5초)
    //public float idleChance = 0.7f; // idle 상태가 될 확률 증가 (30% → 70%)

    //[Header("움직임 영역 설정")]
    //[Range(0.1f, 1f)]
    //public float movementAreaHeight = 0.2f; // 화면 하단 1/5 영역 (20%)
    //public float bottomMargin = 0.5f; // 화면 하단에서의 여백

    //[Header("애니메이션")]
    //public CatPlayerAnimator catAnimator; // 애니메이터 참조

    //// 고양이 움직임 상태 enum (public으로 선언)
    //public enum MovementState
    //{
    //    Walking,
    //    Idle,
    //    Sleeping
    //}

    //// 상호작용 상태 enum
    //private enum InteractionState
    //{
    //    Normal,
    //    Hover,
    //    Clicked
    //}

    //// 원본 스프라이트 크기 저장용
    //private Vector3 originalScale;
    //private Sprite originalSprite;

    //// 움직임 관련
    //private Vector2 moveDirection;
    //private float directionTimer;
    //private float pauseTimer;
    //private float stateTimer;

    //// 방향 추적
    //private CatPlayerAnimator.CatDirection currentFacingDirection = CatPlayerAnimator.CatDirection.Left;
    //private Vector3 lastMovementPosition;

    //// 움직임 영역 경계
    //private Vector2 movementAreaMin;
    //private Vector2 movementAreaMax;

    //private Camera mainCamera;

    //// 상호작용 상태
    //private InteractionState currentState = InteractionState.Normal;

    //// 고양이 움직임 상태
    //private MovementState currentMovementState = MovementState.Idle;

    //// 싱글톤 (ContextMenuManager에서 접근하기 위해)
    //public static TestCat Instance { get; private set; }

    //void Start()
    //{
    //    // 싱글톤 설정
    //    if (Instance == null)
    //    {
    //        Instance = this;
    //    }

    //    if (spriteRenderer == null)
    //        spriteRenderer = GetComponent<SpriteRenderer>();

    //    // 애니메이터 참조 설정
    //    if (catAnimator == null)
    //        catAnimator = GetComponent<CatPlayerAnimator>();

    //    // 원본 스케일과 스프라이트 저장
    //    originalScale = transform.localScale;

    //    // 고양이 스프라이트가 없으면 기본 스프라이트 생성
    //    if (spriteRenderer.sprite == null)
    //    {
    //        CreateDefaultCatSprite();
    //    }

    //    // 원본 스프라이트 저장 (생성 후에)
    //    originalSprite = spriteRenderer.sprite;

    //    // 색상 강제 설정
    //    spriteRenderer.color = normalColor;

    //    mainCamera = Camera.main;

    //    // 움직임 영역 경계 계산
    //    CalculateMovementBounds();

    //    // 고양이를 움직임 영역 내의 랜덤 위치에 배치
    //    PlaceCatInMovementArea();

    //    // 초기 상태 설정
    //    SetMovementState(MovementState.Idle);

    //    // 초기 방향 추적 위치 설정
    //    lastMovementPosition = transform.position;

    //    // 고양이 레이어 설정 (Layer 8 = Interactable)
    //    gameObject.layer = 8;

    //    // Collider2D가 없으면 추가
    //    if (GetComponent<Collider2D>() == null)
    //    {
    //        CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
    //        collider.isTrigger = true;
    //    }

    //    Debug.Log($"TestCat 초기화 완료 - 움직임 영역: Y({movementAreaMin.y:F2} ~ {movementAreaMax.y:F2}), 방향별 애니메이션 시스템");
    //    DebugLogger.LogToFile($"TestCat 초기화 완료 - 움직임 영역: Y({movementAreaMin.y:F2} ~ {movementAreaMax.y:F2}), 방향별 애니메이션 시스템");
    //}

    //void Update()
    //{
    //    UpdateMovement();
    //    UpdateDirection(); // 방향 업데이트 추가
    //    CheckInteraction();
    //}

    //void UpdateDirection()
    //{
    //    // 현재 움직임 방향에 따라 고양이가 바라보는 방향 결정
    //    Vector3 currentPosition = transform.position;
    //    Vector3 movement = currentPosition - lastMovementPosition;


    //    CatPlayerAnimator.CatDirection newDirection;

    //    // X값이 감소하면 Left, 증가하면 Right
    //    if (movement.x < 0) // X값이 줄어들면 (왼쪽으로 이동)
    //    {
    //        newDirection = CatPlayerAnimator.CatDirection.Left;
    //    }
    //    else // X값이 늘어나면 (오른쪽으로 이동)
    //    {
    //        newDirection = CatPlayerAnimator.CatDirection.Right;
    //    }
    //    // 방향이 실제로 바뀌었을 때만 로그 출력
    //    if (newDirection != currentFacingDirection)
    //    {
    //        currentFacingDirection = newDirection;
    //        Debug.Log($"🐱 고양이 방향 변경: X={movement.x:F3} → {currentFacingDirection}");
    //        DebugLogger.LogToFile($"고양이 방향 변경: X={movement.x:F3} → {currentFacingDirection}");

    //        // 애니메이터에 방향 변경 알림
    //        if (catAnimator != null)
    //        {
    //            // 애니메이터에서 방향을 자동으로 처리하므로 별도 호출 불필요
    //            // catAnimator의 UpdateMovementDetection에서 자동으로 처리됨
    //            Debug.Log($"  → CatAnimator에 방향 정보 전달: {currentFacingDirection}");
    //        }
    //    }


    //    // 방향 추적을 위한 위치 업데이트
    //    lastMovementPosition = currentPosition;

    //    // spriteRenderer.flipX는 더 이상 사용하지 않음 (애니메이션 클립으로 처리)
    //    // if (spriteRenderer != null)
    //    // {
    //    //     spriteRenderer.flipX = currentFacingDirection == CatPlayerAnimator.CatDirection.Left;
    //    // }
    //}

    //void CalculateMovementBounds()
    //{
    //    // 화면 경계를 월드 좌표로 변환
    //    Vector3 screenMin = mainCamera.ScreenToWorldPoint(new Vector3(0, 0, mainCamera.transform.position.z));
    //    Vector3 screenMax = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, mainCamera.transform.position.z));

    //    // 전체 화면 크기
    //    float screenWidth = screenMax.x - screenMin.x;
    //    float screenHeight = screenMax.y - screenMin.y;

    //    // 움직임 영역 계산 (화면 하단 1/4 영역)
    //    movementAreaMin = new Vector2(
    //        screenMin.x + 1f, // 좌우 여백
    //        screenMin.y + bottomMargin // 하단 여백
    //    );

    //    movementAreaMax = new Vector2(
    //        screenMax.x - 1f, // 좌우 여백
    //        screenMin.y + bottomMargin + (screenHeight * movementAreaHeight) // 하단에서 25% 높이
    //    );

    //    Debug.Log($"움직임 영역 설정: X({movementAreaMin.x:F2} ~ {movementAreaMax.x:F2}), Y({movementAreaMin.y:F2} ~ {movementAreaMax.y:F2})");
    //    DebugLogger.LogToFile($"움직임 영역 설정: X({movementAreaMin.x:F2} ~ {movementAreaMax.x:F2}), Y({movementAreaMin.y:F2} ~ {movementAreaMax.y:F2})");
    //}

    //void PlaceCatInMovementArea()
    //{
    //    // 움직임 영역 내의 랜덤 위치에 고양이 배치
    //    Vector3 randomPosition = new Vector3(
    //        Random.Range(movementAreaMin.x, movementAreaMax.x),
    //        Random.Range(movementAreaMin.y, movementAreaMax.y),
    //        transform.position.z
    //    );

    //    transform.position = randomPosition;
    //    lastMovementPosition = randomPosition; // 방향 추적 위치도 초기화
    //    Debug.Log($"고양이 위치 설정: {randomPosition}");
    //}

    //void UpdateMovement()
    //{
    //    switch (currentMovementState)
    //    {
    //        case MovementState.Walking:
    //            UpdateWalking();
    //            break;

    //        case MovementState.Idle:
    //            UpdateIdle();
    //            break;

    //        case MovementState.Sleeping:
    //            UpdateSleeping();
    //            break;
    //    }
    //}

    //void UpdateWalking()
    //{
    //    // 이동
    //    Vector3 movement = moveDirection * moveSpeed * Time.deltaTime;
    //    Vector3 newPosition = transform.position + movement;

    //    // 움직임 영역 경계 체크
    //    bool hitBoundary = false;

    //    if (newPosition.x <= movementAreaMin.x || newPosition.x >= movementAreaMax.x)
    //    {
    //        moveDirection.x = -moveDirection.x;
    //        hitBoundary = true;
    //        Debug.Log($"좌우 경계 충돌 - 새 방향: {moveDirection}");
    //    }

    //    if (newPosition.y <= movementAreaMin.y || newPosition.y >= movementAreaMax.y)
    //    {
    //        moveDirection.y = -moveDirection.y;
    //        hitBoundary = true;
    //        Debug.Log($"상하 경계 충돌 - 새 방향: {moveDirection}");
    //    }

    //    // 위치를 움직임 영역 내로 제한
    //    newPosition.x = Mathf.Clamp(newPosition.x, movementAreaMin.x, movementAreaMax.x);
    //    newPosition.y = Mathf.Clamp(newPosition.y, movementAreaMin.y, movementAreaMax.y);
    //    transform.position = newPosition;

    //    // 경계 충돌 시 잠시 멈출 확률 추가
    //    if (hitBoundary && Random.value < 0.3f) // 30% 확률로 경계 충돌 시 idle
    //    {
    //        Debug.Log("경계 충돌 후 잠시 멈춤");
    //        SetMovementState(MovementState.Idle);
    //        return;
    //    }

    //    // 타이머 업데이트
    //    directionTimer += Time.deltaTime;

    //    // 주기적으로 방향 변경 또는 상태 변경
    //    if (directionTimer >= changeDirectionTime)
    //    {
    //        // 걷기 상태에서 더 자주 멈추도록 확률 조정
    //        if (Random.value < idleChance) // 70% 확률로 idle
    //        {
    //            Debug.Log("Walking → Idle 전환 (70% 확률)");
    //            SetMovementState(MovementState.Idle);
    //        }
    //        else if (Random.value < 0.2f) // 추가로 20% 확률로 바로 잠들기
    //        {
    //            Debug.Log("Walking → Sleeping 전환 (희귀 케이스)");
    //            SetMovementState(MovementState.Sleeping);
    //        }
    //        else // 10% 확률로만 계속 걷기
    //        {
    //            SetRandomDirection();
    //            changeDirectionTime = Random.Range(1.5f, 3f); // 다음 평가까지 시간 재설정
    //            Debug.Log("Walking 상태 유지, 새로운 방향 설정");
    //        }
    //    }
    //}

    //void UpdateIdle()
    //{
    //    pauseTimer += Time.deltaTime;

    //    if (pauseTimer >= pauseTime)
    //    {
    //        // idle에서 더 다양한 선택지 제공
    //        float randomValue = Random.value;

    //        if (randomValue < 0.4f) // 40% 확률로 계속 걷기
    //        {
    //            Debug.Log("Idle → Walking 전환 (40% 확률)");
    //            SetMovementState(MovementState.Walking);
    //        }
    //        else if (randomValue < 0.7f) // 30% 확률로 더 오래 쉬기
    //        {
    //            Debug.Log("Idle 연장 (30% 확률) - 추가로 3초 더 쉼");
    //            pauseTimer = 0f; // 타이머 리셋하여 더 오래 쉬기
    //            pauseTime = Random.Range(3f, 7f); // 3-7초 랜덤하게 더 쉬기
    //        }
    //        else // 30% 확률로 잠들기
    //        {
    //            Debug.Log("Idle → Sleeping 전환 (30% 확률)");
    //            SetMovementState(MovementState.Sleeping);
    //        }
    //    }
    //}

    //void UpdateSleeping()
    //{
    //    // 잠자는 상태에서 자연스럽게 깨어나는 로직 추가
    //    stateTimer += Time.deltaTime;

    //    // 10-20초 사이에 자연스럽게 깨어날 확률
    //    if (stateTimer > 10f)
    //    {
    //        float wakeUpChance = (stateTimer - 10f) / 30f; // 10초 후부터 서서히 깨어날 확률 증가

    //        if (Random.value < wakeUpChance * 0.01f) // 매우 낮은 확률로 자연스럽게 깨어남
    //        {
    //            Debug.Log($"자연스럽게 잠에서 깨어남 ({stateTimer:F1}초 후)");

    //            // 깨어나서 바로 걷기보다는 idle 상태로
    //            if (Random.value < 0.8f) // 80% 확률로 idle
    //            {
    //                SetMovementState(MovementState.Idle);
    //            }
    //            else // 20% 확률로 바로 walking
    //            {
    //                SetMovementState(MovementState.Walking);
    //            }
    //        }
    //    }
    //}

    //void SetMovementState(MovementState newState)
    //{
    //    if (currentMovementState == newState) return;

    //    Debug.Log($"🐱 고양이 움직임 상태 변경: {currentMovementState} → {newState}");
    //    DebugLogger.LogToFile($"고양이 움직임 상태 변경: {currentMovementState} → {newState}");

    //    MovementState previousState = currentMovementState;
    //    currentMovementState = newState;
    //    directionTimer = 0f;
    //    pauseTimer = 0f;

    //    switch (newState)
    //    {
    //        case MovementState.Walking:
    //            SetRandomDirection();
    //            // Walking 지속 시간을 짧게 설정
    //            changeDirectionTime = Random.Range(1.5f, 3f); // 1.5-3초 사이로 랜덤
    //            Debug.Log($"  → Walking 시작: 방향 {moveDirection}, {changeDirectionTime:F1}초 후 재평가");
    //            break;

    //        case MovementState.Idle:
    //            moveDirection = Vector2.zero;
    //            // Idle 지속 시간을 랜덤하게 설정 (더 오래 쉬기)
    //            pauseTime = Random.Range(4f, 8f); // 4-8초 사이로 랜덤
    //            Debug.Log($"  → Idle 시작: {pauseTime:F1}초 대기 예정");
    //            break;

    //        case MovementState.Sleeping:
    //            moveDirection = Vector2.zero;
    //            stateTimer = 0f; // sleep 타이머 리셋
    //            Debug.Log($"  → Sleeping 시작 (10-40초 후 자연 깨어남 가능)");
    //            break;
    //    }

    //    // 애니메이터에 상태 변경 알림 (있는 경우)
    //    if (catAnimator != null)
    //    {
    //        Debug.Log($"  → CatAnimator에 상태 변경 알림: {newState}");
    //        // CatPlayerAnimator.UpdateAnimationState()에서 자동으로 처리됨
    //    }
    //}

    //void SetRandomDirection()
    //{
    //    // 움직임 영역 내에서 유효한 방향 설정
    //    Vector2 currentPos = transform.position;
    //    Vector2 targetDirection;

    //    // 경계에 너무 가까우면 중앙 방향으로 유도
    //    bool nearLeftEdge = currentPos.x - movementAreaMin.x < 1f;
    //    bool nearRightEdge = movementAreaMax.x - currentPos.x < 1f;
    //    bool nearBottomEdge = currentPos.y - movementAreaMin.y < 1f;
    //    bool nearTopEdge = movementAreaMax.y - currentPos.y < 1f;

    //    if (nearLeftEdge || nearRightEdge || nearBottomEdge || nearTopEdge)
    //    {
    //        // 중앙을 향하는 방향으로 설정
    //        Vector2 center = (movementAreaMin + movementAreaMax) * 0.5f;
    //        targetDirection = (center - currentPos).normalized;
    //        Debug.Log($"경계 근처에서 중앙 방향으로 설정: {targetDirection}");
    //    }
    //    else
    //    {
    //        // 완전 랜덤 방향
    //        targetDirection = new Vector2(
    //            Random.Range(-1f, 1f),
    //            Random.Range(-1f, 1f)
    //        ).normalized;
    //        Debug.Log($"랜덤 방향 설정: {targetDirection}");
    //    }

    //    moveDirection = targetDirection;
    //    directionTimer = 0f;

    //    Debug.Log($"새로운 이동 방향 설정: {moveDirection}");
    //}

    //void CheckInteraction()
    //{
    //    if (CompatibilityWindowManager.Instance == null) return;

    //    // 마우스 위치 가져오기
    //    Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
    //    Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

    //    // 고양이와 마우스 거리 계산
    //    float distance = Vector2.Distance(transform.position, mouseWorldPos);
    //    float interactionRadius = GetComponent<Collider2D>().bounds.size.x / 2f;

    //    // 마우스 클릭 체크
    //    if (Input.GetMouseButtonDown(0) && distance <= interactionRadius) // 좌클릭
    //    {
    //        OnCatClicked();
    //    }
    //    // 우클릭 체크
    //    else if (Input.GetMouseButtonDown(1) && distance <= interactionRadius) // 우클릭
    //    {
    //        OnCatRightClicked(mouseWorldPos);
    //    }
    //    // 마우스 호버 체크
    //    else if (distance <= interactionRadius)
    //    {
    //        if (currentState != InteractionState.Hover)
    //            OnCatHover();
    //    }
    //    // 마우스가 멀어질 때
    //    else
    //    {
    //        if (currentState != InteractionState.Normal)
    //            OnCatNormal();
    //    }
    //}

    //void OnCatClicked()
    //{
    //    currentState = InteractionState.Clicked;
    //    spriteRenderer.color = clickColor;

    //    // 클릭 효과 - 원본 스케일 기준으로 증가
    //    transform.localScale = originalScale * 1.2f;

    //    // 잠들어 있었다면 깨우기
    //    if (currentMovementState == MovementState.Sleeping)
    //    {
    //        SetMovementState(MovementState.Idle);
    //    }

    //    // 애니메이션: 잠깐 아이들 상태로 (반응 표현)
    //    if (catAnimator != null)
    //    {
    //        catAnimator.PlayTemporaryAnimationSafe(CatPlayerAnimator.CatBaseState.Idle, 1f);
    //    }

    //    Debug.Log("고양이를 클릭했습니다! (쓰다듬기)");
    //    DebugLogger.LogToFile("고양이를 클릭했습니다! (쓰다듬기)");

    //    // 행복도 약간 증가
    //    if (GameDataManager.Instance != null)
    //    {
    //        GameDataManager.Instance.happiness += 2f;
    //        GameDataManager.Instance.happiness = Mathf.Clamp(GameDataManager.Instance.happiness, 0f, 100f);
    //    }

    //    // 0.2초 후 원래대로
    //    Invoke(nameof(ResetClickEffect), 0.2f);
    //}

    //void OnCatRightClicked(Vector3 mousePosition)
    //{
    //    currentState = InteractionState.Clicked;
    //    spriteRenderer.color = clickColor;

    //    // 클릭 효과 - 원본 스케일 기준으로 증가
    //    transform.localScale = originalScale * 1.1f;

    //    // 잠들어 있었다면 깨우기
    //    if (currentMovementState == MovementState.Sleeping)
    //    {
    //        SetMovementState(MovementState.Idle);
    //    }

    //    // 컨텍스트 메뉴 표시
    //    if (ContextMenuManager.Instance != null)
    //    {
    //        ContextMenuManager.Instance.ShowCatMenu(mousePosition);
    //    }

    //    // 애니메이션: 잠깐 아이들 상태로 (관심 표현)
    //    if (catAnimator != null)
    //    {
    //        catAnimator.PlayTemporaryAnimationSafe(CatPlayerAnimator.CatBaseState.Idle, 0.5f);
    //    }

    //    Debug.Log("고양이 우클릭! 컨텍스트 메뉴 표시");
    //    DebugLogger.LogToFile("고양이 우클릭! 컨텍스트 메뉴 표시");

    //    // 0.2초 후 원래대로
    //    Invoke(nameof(ResetClickEffect), 0.2f);
    //}

    //void OnCatHover()
    //{
    //    currentState = InteractionState.Hover;
    //    spriteRenderer.color = hoverColor;

    //    Debug.Log("고양이에 마우스를 올렸습니다!");
    //}

    //void OnCatNormal()
    //{
    //    currentState = InteractionState.Normal;
    //    spriteRenderer.color = normalColor;
    //}

    //void ResetClickEffect()
    //{
    //    // 원본 스케일로 복원
    //    transform.localScale = originalScale;
    //    OnCatNormal();

    //    Debug.Log($"클릭 효과 리셋 - 스케일: {transform.localScale}");
    //}

    //void CreateDefaultCatSprite()
    //{
    //    // 기본 원형 스프라이트 생성 (PPU 200으로 설정)
    //    Texture2D texture = new Texture2D(64, 64);
    //    Color[] colors = new Color[64 * 64];

    //    // 원형 모양으로 색칠
    //    Vector2 center = new Vector2(32, 32);
    //    for (int y = 0; y < 64; y++)
    //    {
    //        for (int x = 0; x < 64; x++)
    //        {
    //            float distance = Vector2.Distance(new Vector2(x, y), center);
    //            if (distance <= 30)
    //            {
    //                colors[y * 64 + x] = Color.cyan; // 고양이 색상 (흰색 대신)
    //            }
    //            else
    //            {
    //                colors[y * 64 + x] = Color.clear; // 투명
    //            }
    //        }
    //    }

    //    texture.SetPixels(colors);
    //    texture.Apply();

    //    // PPU 200으로 설정하여 기존 Cat 이미지들과 일치시킴
    //    Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 200f);
    //    spriteRenderer.sprite = sprite;

    //    Debug.Log("기본 고양이 스프라이트 생성 완료 (PPU: 200)");
    //    DebugLogger.LogToFile("기본 고양이 스프라이트 생성 완료 (PPU: 200)");
    //}

    //// 스프라이트가 변경될 때 원본 정보 업데이트
    //public void UpdateOriginalSpriteInfo()
    //{
    //    originalScale = transform.localScale;
    //    originalSprite = spriteRenderer.sprite;

    //    Debug.Log($"원본 스프라이트 정보 업데이트 - 스케일: {originalScale}, PPU: {(originalSprite != null ? originalSprite.pixelsPerUnit : 0)}");
    //    DebugLogger.LogToFile($"원본 스프라이트 정보 업데이트 - 스케일: {originalScale}, PPU: {(originalSprite != null ? originalSprite.pixelsPerUnit : 0)}");
    //}

    //// 외부에서 고양이를 특정 상태로 만들기 (예: 먹이 주기 후)
    //public void FeedCat()
    //{
    //    // 잠들어 있었다면 깨우기
    //    if (currentMovementState == MovementState.Sleeping)
    //    {
    //        SetMovementState(MovementState.Idle);
    //    }

    //    if (catAnimator != null)
    //    {
    //        // 먹이를 먹는 동안 잠깐 아이들 상태로
    //        catAnimator.PlayTemporaryAnimationSafe(CatPlayerAnimator.CatBaseState.Idle, 2f);
    //    }

    //    // 행복도 증가
    //    if (GameDataManager.Instance != null)
    //    {
    //        GameDataManager.Instance.FeedCat(1);
    //    }

    //    Debug.Log("고양이가 먹이를 먹었습니다!");
    //}

    //public void PetCat()
    //{
    //    // 잠들어 있었다면 깨우기
    //    if (currentMovementState == MovementState.Sleeping)
    //    {
    //        SetMovementState(MovementState.Idle);
    //    }

    //    if (catAnimator != null)
    //    {
    //        // 쓰다듬기 동안 아이들 상태로
    //        catAnimator.PlayTemporaryAnimationSafe(CatPlayerAnimator.CatBaseState.Idle, 1.5f);
    //    }

    //    // 행복도 약간 증가
    //    if (GameDataManager.Instance != null)
    //    {
    //        GameDataManager.Instance.happiness += 3f;
    //        GameDataManager.Instance.happiness = Mathf.Clamp(GameDataManager.Instance.happiness, 0f, 100f);
    //    }

    //    Debug.Log("고양이를 쓰다듬었습니다!");
    //}

    //// 강제로 잠들게 하기
    //public void MakeCatSleep()
    //{
    //    SetMovementState(MovementState.Sleeping);
    //}

    //// 강제로 깨우기
    //public void WakeCatUp()
    //{
    //    if (currentMovementState == MovementState.Sleeping)
    //    {
    //        SetMovementState(MovementState.Idle);
    //    }
    //}

    //// 방향 강제 설정 (외부에서 호출 가능)
    //public void SetFacingDirection(CatPlayerAnimator.CatDirection direction)
    //{
    //    if (currentFacingDirection != direction)
    //    {
    //        currentFacingDirection = direction;
    //        Debug.Log($"고양이 방향 강제 설정: {currentFacingDirection}");

    //        // 애니메이터에 즉시 반영
    //        if (catAnimator != null)
    //        {
    //            catAnimator.ForceStateWithDirection(catAnimator.CurrentBaseState, direction);
    //        }
    //    }
    //}

    //// 디버그용 - 움직임 영역 시각화
    //void OnDrawGizmos()
    //{
    //    // 상호작용 반경
    //    if (GetComponent<Collider2D>() != null)
    //    {
    //        Gizmos.color = Color.blue;
    //        Gizmos.DrawWireSphere(transform.position, GetComponent<Collider2D>().bounds.size.x / 2f);
    //    }

    //    // 움직임 영역 시각화
    //    if (Application.isPlaying)
    //    {
    //        Gizmos.color = Color.green;
    //        Vector3 center = new Vector3(
    //            (movementAreaMin.x + movementAreaMax.x) * 0.5f,
    //            (movementAreaMin.y + movementAreaMax.y) * 0.5f,
    //            transform.position.z
    //        );
    //        Vector3 size = new Vector3(
    //            movementAreaMax.x - movementAreaMin.x,
    //            movementAreaMax.y - movementAreaMin.y,
    //            0.1f
    //        );
    //        Gizmos.DrawWireCube(center, size);

    //        // 방향 표시 화살표
    //        Gizmos.color = currentFacingDirection == CatPlayerAnimator.CatDirection.Left ? Color.red : Color.cyan;
    //        Vector3 arrowStart = transform.position + Vector3.up * 0.5f;
    //        Vector3 arrowEnd = arrowStart + (currentFacingDirection == CatPlayerAnimator.CatDirection.Left ? Vector3.left : Vector3.right) * 0.5f;
    //        Gizmos.DrawLine(arrowStart, arrowEnd);
    //        Gizmos.DrawWireSphere(arrowEnd, 0.1f);
    //    }
    //}


    //// 외부에서 접근할 수 있는 프로퍼티들
    //public bool IsMoving => currentMovementState == MovementState.Walking;
    //public bool IsSleeping => currentMovementState == MovementState.Sleeping;
    //public bool IsIdle => currentMovementState == MovementState.Idle;
    //public MovementState CurrentMovementState => currentMovementState;
    //public CatPlayerAnimator.CatDirection CurrentFacingDirection => currentFacingDirection;
    //public CatPlayerAnimator.CatAnimationState CurrentAnimationState =>
    //    catAnimator != null ? catAnimator.currentState : CatPlayerAnimator.CatAnimationState.IdleRight;
}