using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections;

public class CompatibilityWindowManager : MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    #region Windows API �Լ���
    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateSolidBrush(uint color);

    [DllImport("user32.dll")]
    private static extern IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);
    #endregion
#endif

    #region ������ �����
    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;  // �߰�: �Ϲ� ��Ÿ��
    private const int GCL_HBRBACKGROUND = -10;
    private const int WS_EX_LAYERED = 0x80000;
    private const int WS_EX_TRANSPARENT = 0x20;

    // ������ ��Ÿ�� ����� �߰�
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;

    private const uint LWA_COLORKEY = 0x1;
    private const uint LWA_ALPHA = 0x2;

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_FRAMECHANGED = 0x0020;
    #endregion

    #region ����ü
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }
    #endregion

    private IntPtr windowHandle;
    private bool isClickThrough = false;

    // �̱��� ����
    public static CompatibilityWindowManager Instance { get; private set; }

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
        StartCoroutine(SetupWindowDelayed());
    }

    IEnumerator SetupWindowDelayed()
    {
        yield return new WaitForSeconds(1f); // �� �� ����
        SetupWindow();
    }

    void SetupWindow()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            windowHandle = GetActiveWindow();
            
            if (windowHandle == IntPtr.Zero)
            {
                Debug.LogError("������ �ڵ��� ã�� �� �����ϴ�!");
                DebugLogger.LogToFile("������ �ڵ��� ã�� �� �����ϴ�!");
                return;
            }

            Debug.Log($"=== Full Screen Borderless ������ ���� ���� ===");
            Debug.Log($"������ �ڵ� ȹ��: {windowHandle}");
            Debug.Log($"���� ȭ�� �ػ�: {Screen.width}x{Screen.height}");
            DebugLogger.LogToFile($"=== Full Screen Borderless ������ ���� ���� ===");
            DebugLogger.LogToFile($"������ �ڵ� ȹ��: {windowHandle}");
            DebugLogger.LogToFile($"���� ȭ�� �ػ�: {Screen.width}x{Screen.height}");

            // 1. ���� ������ ��Ÿ�� Ȯ��
            int currentStyle = GetWindowLong(windowHandle, GWL_STYLE);
            int currentExStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
            
            Debug.Log($"���� �Ϲ� ��Ÿ��: 0x{currentStyle:X8}");
            Debug.Log($"���� Ȯ�� ��Ÿ��: 0x{currentExStyle:X8}");
            DebugLogger.LogToFile($"���� �Ϲ� ��Ÿ��: 0x{currentStyle:X8}");
            DebugLogger.LogToFile($"���� Ȯ�� ��Ÿ��: 0x{currentExStyle:X8}");

            // 2. ������ Borderless ��Ÿ�� ���� (�ٽ�!)
            int borderlessStyle = WS_POPUP | WS_VISIBLE;
            int styleResult = SetWindowLong(windowHandle, GWL_STYLE, borderlessStyle);
            
            Debug.Log($"���ο� �Ϲ� ��Ÿ�� ����: 0x{borderlessStyle:X8}");
            Debug.Log($"��Ÿ�� ���� ���: {styleResult}");
            DebugLogger.LogToFile($"���ο� �Ϲ� ��Ÿ�� ����: 0x{borderlessStyle:X8} (���� �׵θ� ����!)");
            DebugLogger.LogToFile($"��Ÿ�� ���� ���: {styleResult}");

            // 3. ������ ����� NULL�� ���� (GDI ���)
            SetClassLongPtr(windowHandle, GCL_HBRBACKGROUND, IntPtr.Zero);
            Debug.Log("������ ��� �귯�� ����");
            DebugLogger.LogToFile("������ ��� �귯�� ����");

            // 4. ���̾�� ������ ����
            int newExStyle = currentExStyle | WS_EX_LAYERED;
            int exStyleResult = SetWindowLong(windowHandle, GWL_EXSTYLE, newExStyle);
            Debug.Log($"���̾�� ������ ���� �Ϸ�, ���: {exStyleResult}");
            DebugLogger.LogToFile($"���̾�� ������ ���� �Ϸ�, ���: {exStyleResult}");

            // 5. ���� ���� Ű ���� (�������� ��������)
            bool transparentResult = SetLayeredWindowAttributes(windowHandle, 0x00000000, 0, LWA_COLORKEY);
            Debug.Log($"������ ����ȭ ���� ���: {transparentResult}");
            DebugLogger.LogToFile($"������ ����ȭ ���� ���: {transparentResult} (������ �� ����)");

            // 6. �׻� ���� ǥ�� + ������ ���� ������Ʈ
            bool posResult = SetWindowPos(windowHandle, HWND_TOPMOST, 0, 0, 0, 0, 
                SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED);
            Debug.Log($"������ ��ġ ���� ���: {posResult}");
            DebugLogger.LogToFile($"������ ��ġ ���� ���: {posResult}");

            // 7. ������ �ٽ� �׸��� ����
            bool invalidateResult = InvalidateRect(windowHandle, IntPtr.Zero, true);
            Debug.Log($"������ �ٽ� �׸��� ���: {invalidateResult}");
            DebugLogger.LogToFile($"������ �ٽ� �׸��� ���: {invalidateResult}");

            // 8. ���� ��Ÿ�� Ȯ��
            int finalStyle = GetWindowLong(windowHandle, GWL_STYLE);
            int finalExStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
            
            Debug.Log($"���� �Ϲ� ��Ÿ��: 0x{finalStyle:X8}");
            Debug.Log($"���� Ȯ�� ��Ÿ��: 0x{finalExStyle:X8}");
            DebugLogger.LogToFile($"���� �Ϲ� ��Ÿ��: 0x{finalStyle:X8}");
            DebugLogger.LogToFile($"���� Ȯ�� ��Ÿ��: 0x{finalExStyle:X8}");
            
            Debug.Log("=== ������ Borderless ������ ���� �Ϸ�! ===");
            DebugLogger.LogToFile("=== ������ Borderless ������ ���� �Ϸ�! (_, X ��ư ���� ����) ===");
        }
        catch (Exception e)
        {
            Debug.LogError($"Borderless ������ ���� ����: {e.Message}");
            Debug.LogError($"���� Ʈ���̽�: {e.StackTrace}");
            DebugLogger.LogToFile($"Borderless ������ ���� ����: {e.Message}");
            DebugLogger.LogToFile($"���� Ʈ���̽�: {e.StackTrace}");
        }
#else
        Debug.Log("�����Ϳ����� ������ ������ ��Ȱ��ȭ�˴ϴ�.");
        DebugLogger.LogToFile("�����Ϳ����� ������ ������ ��Ȱ��ȭ�˴ϴ�.");
#endif
    }

    public void EnableClickThrough()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (windowHandle == IntPtr.Zero) return;
        
        try
        {
            int currentStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
            SetWindowLong(windowHandle, GWL_EXSTYLE, currentStyle | WS_EX_TRANSPARENT);
            isClickThrough = true;
            Debug.Log("Ŭ�� ��� Ȱ��ȭ");
        }
        catch (Exception e)
        {
            Debug.LogError($"Ŭ�� ��� Ȱ��ȭ ����: {e.Message}");
        }
#endif
    }

    public void DisableClickThrough()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (windowHandle == IntPtr.Zero) return;
        
        try
        {
            int currentStyle = GetWindowLong(windowHandle, GWL_EXSTYLE);
            SetWindowLong(windowHandle, GWL_EXSTYLE, currentStyle & ~WS_EX_TRANSPARENT);
            isClickThrough = false;
            Debug.Log("Ŭ�� ��� ��Ȱ��ȭ");
        }
        catch (Exception e)
        {
            Debug.LogError($"Ŭ�� ��� ��Ȱ��ȭ ����: {e.Message}");
        }
#endif
    }

    public Vector2 GetMousePosition()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        try
        {
            POINT point;
            GetCursorPos(out point);
            return new Vector2(point.x, point.y);
        }
        catch (Exception e)
        {
            Debug.LogError($"���콺 ��ġ ȹ�� ����: {e.Message}");
            return Input.mousePosition;
        }
#else
        return Input.mousePosition;
#endif
    }

    public Vector2 GetMousePositionInWindow()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (windowHandle == IntPtr.Zero) return Input.mousePosition;
        
        try
        {
            POINT point;
            GetCursorPos(out point);
            ScreenToClient(windowHandle, ref point);
            return new Vector2(point.x, Screen.height - point.y);
        }
        catch (Exception e)
        {
            Debug.LogError($"������ �� ���콺 ��ġ ȹ�� ����: {e.Message}");
            return Input.mousePosition;
        }
#else
        return Input.mousePosition;
#endif
    }

    public bool IsClickThrough => isClickThrough;
}