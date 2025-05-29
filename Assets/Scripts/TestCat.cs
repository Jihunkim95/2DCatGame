using UnityEngine;

public class TestCat : MonoBehaviour
{
    [Header("����� ����")]
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.cyan; // ����� �����ǵ��� ����
    public Color hoverColor = Color.yellow;
    public Color clickColor = Color.green;

    [Header("�̵� ����")]
    public float moveSpeed = 2f;
    public float changeDirectionTime = 3f;

    private Vector2 moveDirection;
    private float directionTimer;
    private Camera mainCamera;
    private Vector2 screenBounds;

    // ��ȣ�ۿ� ����
    private enum InteractionState
    {
        Normal,
        Hover,
        Clicked
    }
    private InteractionState currentState = InteractionState.Normal;

    void Start()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // ����� ��������Ʈ�� ������ �⺻ ��������Ʈ ����
        if (spriteRenderer.sprite == null)
        {
            // �⺻ ���� ��������Ʈ ����
            Texture2D texture = new Texture2D(64, 64);
            Color[] colors = new Color[64 * 64];

            // ���� ������� ��ĥ
            Vector2 center = new Vector2(32, 32);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    if (distance <= 30)
                    {
                        colors[y * 64 + x] = Color.cyan; // ����� ���� (��� ����)
                    }
                    else
                    {
                        colors[y * 64 + x] = Color.clear; // ����
                    }
                }
            }

            texture.SetPixels(colors);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            spriteRenderer.sprite = sprite;

            Debug.Log("�⺻ ����� ��������Ʈ ���� �Ϸ�");
            DebugLogger.LogToFile("�⺻ ����� ��������Ʈ ���� �Ϸ�");
        }

        // ���� ���� ����
        spriteRenderer.color = normalColor;

        mainCamera = Camera.main;

        // ȭ�� ��� ���
        screenBounds = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, mainCamera.transform.position.z));

        // �ʱ� �̵� ���� ����
        SetRandomDirection();

        // ����� ���̾� ���� (Layer 8 = Interactable)
        gameObject.layer = 8;

        // Collider2D�� ������ �߰�
        if (GetComponent<Collider2D>() == null)
        {
            CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
        }
    }

    void Update()
    {
        MoveCat();
        CheckInteraction();
    }

    void MoveCat()
    {
        // �̵�
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime);

        // ȭ�� ��� üũ �� �ݻ�
        Vector3 pos = transform.position;
        bool changedDirection = false;

        if (pos.x <= -screenBounds.x || pos.x >= screenBounds.x)
        {
            moveDirection.x = -moveDirection.x;
            changedDirection = true;
        }

        if (pos.y <= -screenBounds.y || pos.y >= screenBounds.y)
        {
            moveDirection.y = -moveDirection.y;
            changedDirection = true;
        }

        // ȭ�� ���η� ��ġ ����
        pos.x = Mathf.Clamp(pos.x, -screenBounds.x, screenBounds.x);
        pos.y = Mathf.Clamp(pos.y, -screenBounds.y, screenBounds.y);
        transform.position = pos;

        // �ֱ������� ���� ����
        directionTimer += Time.deltaTime;
        if (directionTimer >= changeDirectionTime && !changedDirection)
        {
            SetRandomDirection();
        }
    }

    void SetRandomDirection()
    {
        moveDirection = new Vector2(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)
        ).normalized;

        directionTimer = 0f;
    }

    void CheckInteraction()
    {
        if (CompatibilityWindowManager.Instance == null) return;

        // ���콺 ��ġ ��������
        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        // ����̿� ���콺 �Ÿ� ���
        float distance = Vector2.Distance(transform.position, mouseWorldPos);
        float interactionRadius = GetComponent<Collider2D>().bounds.size.x / 2f;

        // ���콺 Ŭ�� üũ
        if (Input.GetMouseButtonDown(0) && distance <= interactionRadius)
        {
            OnCatClicked();
        }
        // ���콺 ȣ�� üũ
        else if (distance <= interactionRadius)
        {
            if (currentState != InteractionState.Hover)
                OnCatHover();
        }
        // ���콺�� �־����� ��
        else
        {
            if (currentState != InteractionState.Normal)
                OnCatNormal();
        }
    }

    void OnCatClicked()
    {
        currentState = InteractionState.Clicked;
        spriteRenderer.color = clickColor;

        // Ŭ�� ȿ��
        transform.localScale = Vector3.one * 1.2f;

        Debug.Log("����̸� Ŭ���߽��ϴ�!");

        // 0.2�� �� �������
        Invoke(nameof(ResetClickEffect), 0.2f);
    }

    void OnCatHover()
    {
        currentState = InteractionState.Hover;
        spriteRenderer.color = hoverColor;

        Debug.Log("����̿� ���콺�� �÷Ƚ��ϴ�!");
    }

    void OnCatNormal()
    {
        currentState = InteractionState.Normal;
        spriteRenderer.color = normalColor;
    }

    void ResetClickEffect()
    {
        transform.localScale = Vector3.one;
        OnCatNormal();
    }

    // ����׿�
    void OnDrawGizmos()
    {
        if (GetComponent<Collider2D>() != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, GetComponent<Collider2D>().bounds.size.x / 2f);
        }
    }
}