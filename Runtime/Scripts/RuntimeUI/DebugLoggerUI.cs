#if UNITY_ANDROID
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Android;

namespace UnityCTVisualizer
{
  public class DebugLogger : MonoBehaviour
  {

    [Header("Visual Feedback")]
    [Tooltip("If you want to see everything being sent to the console.")]
    public bool m_ignoreLogLevel = true;

    [Tooltip("Show only the specific Log entries.")]
    public LogType m_logLevel = LogType.Error;


    [Tooltip("Check this if you want the stack trace printed after the message.")]
    public bool m_logStackTrace = false;

    [Tooltip("Log stack trace for log levels. Setting this to true adds a lot of text!")]
    public bool m_ignoreStackTraceLogLevel = false;

    [Tooltip("Which log level to include stack tracing for.")]
    public LogType m_stackTraceLogLevel = LogType.Error;



    [Header("Visual Feedback")]
    [Tooltip("Whether to write the logs to a log file")]
    public bool m_writeLogFile;

    [Tooltip("Log file path. Make sure to set this to a valid path on the target before building!")]
    public string m_logFilePath;


    [Header("Performance")]
    [Tooltip("Maximum number of messages in the text UI before deleting the older messages.")]
    public int m_maxNbrMessagesUI = 15;

    [Tooltip("Maximum number of messages to handle per frame. If exceeded, remaining messages will be handled in the next frame.")]
    public int m_maxNbrMessagesPerFrame = 10;

    private bool m_dirty = false;

    private FileStream m_fs = null;
    private StreamWriter m_sw = null;

    [SerializeField] private TMP_Text m_debugText;

    private ConcurrentQueue<string> m_messageQueue;


    void OnEnable()
    {
      if (m_debugText == null)
      {
        throw new NullReferenceException("TMP_Text component has to be provided!");
      }
      m_messageQueue = new ConcurrentQueue<string>();
      // request write permissions
      if (m_writeLogFile && !Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite)) {
          Permission.RequestUserPermission(Permission.ExternalStorageWrite);
      }
      // Triggered regardless of whether the message comes in on the main thread or not; handler is thread safe
      Application.logMessageReceivedThreaded += Application_logMessageReceivedThreaded;
      if (m_writeLogFile)
      {
        if (File.Exists(m_logFilePath))
          m_fs = File.Open(m_logFilePath, FileMode.Append, FileAccess.Write, FileShare.None);
        else if (Directory.Exists(Path.GetDirectoryName(m_logFilePath)))
          m_fs = File.Open(m_logFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        m_sw = new StreamWriter(m_fs, Encoding.ASCII);
      }
    }

    void OnDisable()
    {
      Application.logMessageReceivedThreaded -= Application_logMessageReceivedThreaded;
      m_sw?.Close();
      m_fs?.Close();
    }


    private void Application_logMessageReceivedThreaded(string log_string, string stack_trace, LogType log_type)
    {
      DateTime timestamp = System.DateTime.Now;
      if (m_ignoreLogLevel || log_type == m_logLevel)
      {
        m_dirty = true;

        StringBuilder stringBuilder = new();
        switch (log_type)
        {
          case LogType.Log:
            stringBuilder.Append("[INF] ");
            break;
          case LogType.Warning:
            stringBuilder.Append("[WAR] ");
            break;
          case LogType.Error:
            stringBuilder.Append("[ERR] ");
            break;
          case LogType.Exception:
            stringBuilder.Append("[EXC] ");
            break;
          case LogType.Assert:
            stringBuilder.Append("[ASS] ");
            break;
        }
        stringBuilder.AppendFormat("{0} {1}", timestamp, log_string);
        if (m_logStackTrace && (m_ignoreStackTraceLogLevel || (log_type == m_stackTraceLogLevel)))
          stringBuilder.AppendFormat(" {0}", stack_trace);
        stringBuilder.Append("\n");

        m_messageQueue.Enqueue(stringBuilder.ToString());
      }
    }

    void Update()
    {
      if (m_dirty)
      {
        StringBuilder string_build = new();
        int counter = 0;
        while (counter <= m_maxNbrMessagesPerFrame && m_messageQueue.TryDequeue(out string msg))
        {
          string_build.Append(msg);
          ++counter;
        }
        string all_messages = string_build.ToString();
        m_sw?.Write(all_messages);
        m_debugText.text += all_messages;
        m_dirty = false;
      }
    }
  }
}
#endif
