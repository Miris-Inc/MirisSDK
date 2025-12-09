// Copyright © 2025 Miris, Inc. All rights reserved.

using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using TMPro;

namespace Miris.Runtime
{
    public class LogConsoleUIController : MonoBehaviour
    {
        [SerializeField]
        private TMP_Dropdown m_logLevelDropdown;

        [SerializeField]
        private Toggle m_logEnabledToggle;

        [SerializeField]
        private TMP_InputField m_logConsoleTextField;

        [SerializeField]
        private Scrollbar m_logConsoleScrollbar;

        [SerializeField]
        private int m_maxLines = 500; // Adjust as needed

        [SerializeField]
        private float m_updateFrequencySeconds = 1.0f;

        // Buffer of the last N log messages (N determined by m_maxLines)
        private Queue<string> m_logQueue = new Queue<string>();

        // Timer for when we want to update the InputField UI element with
        // our queue of logs.
        private float m_updateTimer = 0.0f;

        // Dirty flag for when we receive new logs.  This is so we don't
        // need to waste cycles updating the UI when there are no new log messages.
        private bool m_logDirty = false;

        private void Start()
        {
            // TODO: move event registration / de-registration to OnEnabled() & OnDisabled()
            UiUtils.InitializeEnumDropdown(m_logLevelDropdown, typeof(LogLevel), OnLogLevelDropdownValueChanged);
            m_logEnabledToggle.onValueChanged.AddListener(OnLogEnabledToggleValueChanged);
        }

        private void OnDestroy()
        {
            m_logLevelDropdown.onValueChanged.RemoveListener(OnLogLevelDropdownValueChanged);
            m_logEnabledToggle.onValueChanged.AddListener(OnLogEnabledToggleValueChanged);
        }

        private void OnEnable()
        {
            Application.logMessageReceived += OnLogMessageReceived;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
        }

        // --------------------------------------------------------------------
        // Sync data model -> UI
        // --------------------------------------------------------------------

        private void Update()
        {
            SyncLogEnabledToggle();
            SyncLogLevelDropdown();
            SyncLogConsole();
        }

        private void SyncLogEnabledToggle()
        {
            m_logEnabledToggle.SetIsOnWithoutNotify(Debug.unityLogger.logEnabled);
        }

        private void SyncLogLevelDropdown()
        {
            m_logLevelDropdown.SetValueWithoutNotify((int)MirisApi.GetLogLevel());
        }

        private void SyncLogConsole()
        {
            m_updateTimer += Time.deltaTime;
            if (m_updateTimer > m_updateFrequencySeconds && m_logDirty)
            {
                // Update the text view.
                m_logConsoleTextField.text = string.Join("\n", m_logQueue.ToArray());

                // Reset timer
                m_updateTimer = 0;
            }
        }

        // --------------------------------------------------------------------
        // UI event handling
        // --------------------------------------------------------------------

        private void OnLogEnabledToggleValueChanged(bool value)
        {
            Debug.unityLogger.logEnabled = value;
        }

        private void OnLogLevelDropdownValueChanged(int selectedIndex)
        {
            MirisApi.SetLogLevel((LogLevel)selectedIndex);
        }

        // Keep a queue of the logs
        void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            string message = $"[{type.ToString()}] {logString}";
            if (type == LogType.Exception || type == LogType.Error)
            {
                message += $"\n{stackTrace}";
            }

            m_logQueue.Enqueue(message);

            if (m_logQueue.Count > m_maxLines)
            {
                m_logQueue.Dequeue();
            }

            m_logDirty = true;
        }
    }
}
