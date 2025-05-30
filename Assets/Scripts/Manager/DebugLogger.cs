using UnityEngine;
using System.IO;
using System;

public class DebugLogger : MonoBehaviour
{
    private static string logFilePath;

    void Awake()
    {
        // �α� ���� ��� ���� (���� ���ϰ� ���� ����)
        logFilePath = Path.Combine(Application.dataPath, "../", "DebugLog.txt");

        // ���� �α� ���� ���� (�� ���� ����)
        if (File.Exists(logFilePath))
            File.Delete(logFilePath);

        // Unity �α׸� ���Ϸ� �����̷�Ʈ
        Application.logMessageReceived += OnLogMessageReceived;

        LogToFile("=== Debug Logger ���� ===");
        LogToFile($"Unity ����: {Application.unityVersion}");
        LogToFile($"�÷���: {Application.platform}");
        LogToFile($"�ػ�: {Screen.width}x{Screen.height}");
    }

    void OnLogMessageReceived(string logString, string stackTrace, LogType type)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string logEntry = $"[{timestamp}] [{type}] {logString}";

        if (type == LogType.Exception || type == LogType.Error)
        {
            logEntry += $"\nStackTrace: {stackTrace}";
        }

        LogToFile(logEntry);
    }

    public static void LogToFile(string message)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                writer.WriteLine(message);
                writer.Flush();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"�α� ���� ���� ����: {e.Message}");
        }
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessageReceived;
        LogToFile("=== Debug Logger ���� ===");
    }
}