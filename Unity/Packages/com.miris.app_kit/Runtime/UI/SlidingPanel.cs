// Copyright © 2025 Miris, Inc. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlidingPanel : MonoBehaviour
{
    private enum SlideState
    {
        Closed,
        Sliding,
        Open,
    }

    private SlideState m_state = SlideState.Closed;

    private RectTransform m_rect;
    private Vector2 m_originalPosition;
    private Vector2 m_hiddenOffset;

    public void SlideClose()
    {
        if (m_state != SlideState.Open)
        {
            return;
        }
        StartCoroutine(Slide(false));
        m_state = SlideState.Closed;
    }

    public void SlideOpen()
    {
        if (m_state != SlideState.Closed)
        {
            return;
        }
        StartCoroutine(Slide(true));
        m_state = SlideState.Open;
    }

    public IEnumerator Slide(bool isOpening = true, float timeToMove = 0.5f)
    {
        m_state = SlideState.Sliding;
        Vector2 startPos = isOpening ?  m_originalPosition - m_hiddenOffset : m_originalPosition;
        Vector2 endPos = isOpening ? m_originalPosition : m_originalPosition - m_hiddenOffset;

        float currentTime = 0;
        while (currentTime <= timeToMove)
        {
            float timeRatio = currentTime / timeToMove;
            float easedTimeRatio = timeRatio * timeRatio * (3f - 2f * timeRatio);

            m_rect.anchoredPosition = Vector2.Lerp(startPos, endPos, easedTimeRatio);
            currentTime += Time.deltaTime;
            yield return null;
        }
        if (!isOpening)
        {
            gameObject.SetActive(false);
        }
        m_rect.anchoredPosition = endPos;
    }

    private void Awake()
    {
        if (m_rect == null)
        {
            m_rect = GetComponent<RectTransform>();
        }
        m_hiddenOffset = new Vector2(0f, m_rect.rect.height);
        m_originalPosition = m_rect.anchoredPosition;
    }
}
