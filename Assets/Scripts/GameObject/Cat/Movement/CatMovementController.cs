using UnityEngine;

/// <summary>
/// 고양이 움직임 제어를 담당하는 클래스
/// </summary>
public class CatMovementController : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 1.5f;
    public float changeDirectionTime = 2f;
    public float pauseTime = 5f;
    public float idleChance = 0.7f;

    [Header("움직임 영역 설정")]
    [Range(0.1f, 1f)]
    public float movementAreaHeight = 0.2f;
    public float bottomMargin = 0.5f;

    // 고양이 움직임 상태 enum
    public enum MovementState
    {
        Walking,
        Idle,
        Sleeping
    }

    // 상태 추적
    private MovementState currentMovementState = MovementState.Idle;
    private Vector2 moveDirection;
    private float directionTimer;
    private float pauseTimer;
    private float stateTimer;

    // 움직임 영역
    private Vector2 movementAreaMin;
    private Vector2 movementAreaMax;
    private Camera mainCamera;

    // 이벤트
    public System.Action<MovementState> OnMovementStateChanged;

    void Start()
    {
        mainCamera = Camera.main;
        CalculateMovementBounds();
        PlaceCatInMovementArea();
        SetMovementState(MovementState.Idle);
    }

    void Update()
    {
        UpdateMovement();
    }

    void CalculateMovementBounds()
    {
        Vector3 screenMin = mainCamera.ScreenToWorldPoint(new Vector3(0, 0, mainCamera.transform.position.z));
        Vector3 screenMax = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, mainCamera.transform.position.z));

        float screenHeight = screenMax.y - screenMin.y;

        movementAreaMin = new Vector2(
            screenMin.x + 1f,
            screenMin.y + bottomMargin
        );

        movementAreaMax = new Vector2(
            screenMax.x - 1f,
            screenMin.y + bottomMargin + (screenHeight * movementAreaHeight)
        );

        Debug.Log($"움직임 영역 설정: X({movementAreaMin.x:F2} ~ {movementAreaMax.x:F2}), Y({movementAreaMin.y:F2} ~ {movementAreaMax.y:F2})");
    }

    void PlaceCatInMovementArea()
    {
        Vector3 randomPosition = new Vector3(
            Random.Range(movementAreaMin.x, movementAreaMax.x),
            Random.Range(movementAreaMin.y, movementAreaMax.y),
            transform.position.z
        );

        transform.position = randomPosition;
        Debug.Log($"고양이 위치 설정: {randomPosition}");
    }

    void UpdateMovement()
    {
        switch (currentMovementState)
        {
            case MovementState.Walking:
                UpdateWalking();
                break;
            case MovementState.Idle:
                UpdateIdle();
                break;
            case MovementState.Sleeping:
                UpdateSleeping();
                break;
        }
    }

    void UpdateWalking()
    {
        Vector3 movement = moveDirection * moveSpeed * Time.deltaTime;
        Vector3 newPosition = transform.position + movement;

        bool hitBoundary = false;

        if (newPosition.x <= movementAreaMin.x || newPosition.x >= movementAreaMax.x)
        {
            moveDirection.x = -moveDirection.x;
            hitBoundary = true;
        }

        if (newPosition.y <= movementAreaMin.y || newPosition.y >= movementAreaMax.y)
        {
            moveDirection.y = -moveDirection.y;
            hitBoundary = true;
        }

        newPosition.x = Mathf.Clamp(newPosition.x, movementAreaMin.x, movementAreaMax.x);
        newPosition.y = Mathf.Clamp(newPosition.y, movementAreaMin.y, movementAreaMax.y);
        transform.position = newPosition;

        if (hitBoundary && Random.value < 0.3f)
        {
            SetMovementState(MovementState.Idle);
            return;
        }

        directionTimer += Time.deltaTime;

        if (directionTimer >= changeDirectionTime)
        {
            if (Random.value < idleChance)
            {
                SetMovementState(MovementState.Idle);
            }
            else if (Random.value < 0.2f)
            {
                SetMovementState(MovementState.Sleeping);
            }
            else
            {
                SetRandomDirection();
                changeDirectionTime = Random.Range(1.5f, 3f);
            }
        }
    }

    void UpdateIdle()
    {
        pauseTimer += Time.deltaTime;

        if (pauseTimer >= pauseTime)
        {
            float randomValue = Random.value;

            if (randomValue < 0.4f)
            {
                SetMovementState(MovementState.Walking);
            }
            else if (randomValue < 0.7f)
            {
                pauseTimer = 0f;
                pauseTime = Random.Range(3f, 7f);
            }
            else
            {
                SetMovementState(MovementState.Sleeping);
            }
        }
    }

    void UpdateSleeping()
    {
        stateTimer += Time.deltaTime;

        if (stateTimer > 10f)
        {
            float wakeUpChance = (stateTimer - 10f) / 30f;

            if (Random.value < wakeUpChance * 0.01f)
            {
                if (Random.value < 0.8f)
                {
                    SetMovementState(MovementState.Idle);
                }
                else
                {
                    SetMovementState(MovementState.Walking);
                }
            }
        }
    }

    void SetMovementState(MovementState newState)
    {
        if (currentMovementState == newState) return;

        Debug.Log($"🐱 고양이 움직임 상태 변경: {currentMovementState} → {newState}");

        currentMovementState = newState;
        directionTimer = 0f;
        pauseTimer = 0f;

        switch (newState)
        {
            case MovementState.Walking:
                SetRandomDirection();
                changeDirectionTime = Random.Range(1.5f, 3f);
                break;
            case MovementState.Idle:
                moveDirection = Vector2.zero;
                pauseTime = Random.Range(4f, 8f);
                break;
            case MovementState.Sleeping:
                moveDirection = Vector2.zero;
                stateTimer = 0f;
                break;
        }

        OnMovementStateChanged?.Invoke(newState);
    }

    void SetRandomDirection()
    {
        Vector2 currentPos = transform.position;
        Vector2 targetDirection;

        bool nearLeftEdge = currentPos.x - movementAreaMin.x < 1f;
        bool nearRightEdge = movementAreaMax.x - currentPos.x < 1f;
        bool nearBottomEdge = currentPos.y - movementAreaMin.y < 1f;
        bool nearTopEdge = movementAreaMax.y - currentPos.y < 1f;

        if (nearLeftEdge || nearRightEdge || nearBottomEdge || nearTopEdge)
        {
            Vector2 center = (movementAreaMin + movementAreaMax) * 0.5f;
            targetDirection = (center - currentPos).normalized;
        }
        else
        {
            targetDirection = new Vector2(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)
            ).normalized;
        }

        moveDirection = targetDirection;
        directionTimer = 0f;
    }

    // 외부에서 상태 제어
    public void ForceState(MovementState state)
    {
        SetMovementState(state);
    }

    public void MakeCatSleep()
    {
        SetMovementState(MovementState.Sleeping);
    }

    public void WakeCatUp()
    {
        if (currentMovementState == MovementState.Sleeping)
        {
            SetMovementState(MovementState.Idle);
        }
    }

    // Gizmos로 움직임 영역 표시
    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Vector3 center = new Vector3(
                (movementAreaMin.x + movementAreaMax.x) * 0.5f,
                (movementAreaMin.y + movementAreaMax.y) * 0.5f,
                transform.position.z
            );
            Vector3 size = new Vector3(
                movementAreaMax.x - movementAreaMin.x,
                movementAreaMax.y - movementAreaMin.y,
                0.1f
            );
            Gizmos.DrawWireCube(center, size);
        }
    }

    // 프로퍼티들
    public bool IsMoving => currentMovementState == MovementState.Walking;
    public bool IsSleeping => currentMovementState == MovementState.Sleeping;
    public bool IsIdle => currentMovementState == MovementState.Idle;
    public MovementState CurrentMovementState => currentMovementState;
    public Vector2 MoveDirection => moveDirection;
}