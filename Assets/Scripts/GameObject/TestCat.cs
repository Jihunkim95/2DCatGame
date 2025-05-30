using UnityEngine;
using System.Collections;

public class TestCat : MonoBehaviour
{
    [Header("����� ����")]
    public SpriteRenderer spriteRenderer;
    public Color normalColor = Color.cyan;
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

    // �̱���
    public static TestCat Instance { get; private set; }

    void Start()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer.sprite == null)
        {
            CreateCatSprite();
        }

        spriteRenderer.color = normalColor;
        mainCamera = Camera.main;
        screenBounds = mainCamera.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, mainCamera.transform.position.z));
        SetRandomDirection();

        gameObject.layer = 8; // Interactable layer

        if (GetComponent<Collider2D>() == null)
        {
            CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
        }
    }

    void CreateCatSprite()
    {
        Texture2D texture = new Texture2D(64, 64);
        Color[] colors = new Color[64 * 64];

        Vector2 center = new Vector2(32, 32);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= 30)
                {
                    colors[y * 64 + x] = Color.cyan;
                }
                else
                {
                    colors[y * 64 + x] = Color.clear;
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

    void Update()
    {
        MoveCat();
        CheckInteraction();
    }

    void SetRandomDirection()
    {
        // x�� �Ǵ� y�� �� �ϳ��� �����ϵ��� ���� ����
        if (Random.value > 0.5f)
        {
            moveDirection = new Vector2(Random.Range(-1f, 1f), 0).normalized; // x�����θ� �̵�
        }
        else
        {
            moveDirection = new Vector2(0, Random.Range(-1f, 1f)).normalized; // y�����θ� �̵�
        }

        directionTimer = 0f;
    }

    void MoveCat()
    {
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime);

        Vector3 pos = transform.position;
        bool changedDirection = false;

        // ȭ�� ��迡 �������� �� x�� �Ǵ� y�� ���⸸ ����
        if (pos.x <= -screenBounds.x || pos.x >= screenBounds.x)
        {
            moveDirection = new Vector2(-moveDirection.x, 0); // x�� ����, y���� 0���� ����
            changedDirection = true;
        }

        if (pos.y <= -screenBounds.y || pos.y >= screenBounds.y - 8.0f )
        {
            moveDirection = new Vector2(0, -moveDirection.y); // y�� ����, x���� 0���� ����
            changedDirection = true;
        }
        pos.x = Mathf.Clamp(pos.x, -screenBounds.x, screenBounds.x);
        pos.y = Mathf.Clamp(pos.y, -screenBounds.y, screenBounds.y);
        transform.position = pos;

        directionTimer += Time.deltaTime;
        if (directionTimer >= changeDirectionTime && !changedDirection)
        {
            SetRandomDirection();
        }
    }

    void CheckInteraction()
    {
        if (CompatibilityWindowManager.Instance == null) return;

        Vector2 mousePos = CompatibilityWindowManager.Instance.GetMousePositionInWindow();
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mainCamera.nearClipPlane));

        float distance = Vector2.Distance(transform.position, mouseWorldPos);
        float interactionRadius = GetComponent<Collider2D>().bounds.size.x / 2f;

        // ��Ŭ�� üũ - Modern UI Context Menu ǥ��
        if (Input.GetMouseButtonDown(1) && distance <= interactionRadius)
        {
            OnCatRightClicked(mouseWorldPos);
        }
        // ��Ŭ�� üũ
        else if (Input.GetMouseButtonDown(0) && distance <= interactionRadius)
        {
            OnCatClicked();
        }
        // ���콺 ȣ�� üũ
        else if (distance <= interactionRadius)
        {
            if (currentState != InteractionState.Hover)
                OnCatHover();
        }
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
        transform.localScale = Vector3.one * 1.2f;

        Debug.Log("����̸� Ŭ���߽��ϴ�! (���ٵ��)");
        DebugLogger.LogToFile("����̸� Ŭ���߽��ϴ�! (���ٵ��)");

        Invoke(nameof(ResetClickEffect), 0.2f);
    }

    void OnCatRightClicked(Vector3 mousePosition)
    {
        currentState = InteractionState.Clicked;
        spriteRenderer.color = clickColor;
        transform.localScale = Vector3.one * 1.1f;

        // ContextMenuManager ȣ��
        if (ContextMenuManager.Instance != null)
        {
            ContextMenuManager.Instance.ShowCatMenu(transform.position);
        }

        Debug.Log("����� ��Ŭ��! ���ؽ�Ʈ �޴� ǥ��");
        DebugLogger.LogToFile("����� ��Ŭ��! ���ؽ�Ʈ �޴� ǥ��");

        Invoke(nameof(ResetClickEffect), 0.2f);
    }

    void OnCatHover()
    {
        currentState = InteractionState.Hover;
        spriteRenderer.color = hoverColor;
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

    // ���ٵ�� ȿ�� �ڷ�ƾ (Modern UI Context Menu���� ���)
    public IEnumerator PetEffect()
    {
        // ���� ��ȭ ȿ��
        Color originalColor = spriteRenderer.color;
        spriteRenderer.color = Color.magenta; // ���ٵ�� ����

        // ũ�� ��ȭ ȿ��
        Vector3 originalScale = transform.localScale;
        transform.localScale = originalScale * 1.3f;

        // 0.5�� ���� ȿ�� ����
        yield return new WaitForSeconds(0.5f);

        // ���� ���·� ����
        spriteRenderer.color = originalColor;
        transform.localScale = originalScale;

        Debug.Log("���ٵ�� ȿ�� �Ϸ�!");
    }

    void OnDrawGizmos()
    {
        if (GetComponent<Collider2D>() != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, GetComponent<Collider2D>().bounds.size.x / 2f);
        }
    }
}