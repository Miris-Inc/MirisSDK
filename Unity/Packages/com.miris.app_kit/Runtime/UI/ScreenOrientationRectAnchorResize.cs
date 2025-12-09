using UnityEngine;
using UnityEngine.UI;

public class ScreenOrientationRectAnchorResize : MonoBehaviour
{
    [SerializeField]
    private VerticalLayoutGroup m_layoutGroup;

    public RectTransform m_targetRect;
    public Vector2 m_portraitAnchorMin = new Vector2(0f, 0.15f);
    public Vector2 m_portraitAnchorMax = new Vector2(1f, 0.6f);

    public Vector2 m_landscapeAnchorMin = new Vector2(0f, 0.15f);
    public Vector2 m_landscapeAnchorMax = new Vector2(1f, 0.8f);

    private ScreenOrientation m_lastOrientation;

    protected void Start()
    {
        m_lastOrientation = Screen.orientation;
        UpdateAnchors(m_lastOrientation);
    }

    protected void Update()
    {
        if (Screen.orientation != m_lastOrientation)
        {
            m_lastOrientation = Screen.orientation;
            UpdateAnchors(m_lastOrientation);
            if(m_layoutGroup != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(this.gameObject.GetComponent<RectTransform>());
            }
        }
    }

    private void UpdateAnchors(ScreenOrientation orientation)
    {
        if (orientation == ScreenOrientation.LandscapeLeft || orientation == ScreenOrientation.LandscapeRight)
        {
            m_targetRect.anchorMin = m_landscapeAnchorMin;
            m_targetRect.anchorMax = m_landscapeAnchorMax;
        }
        else
        {
            m_targetRect.anchorMin = m_portraitAnchorMin;
            m_targetRect.anchorMax = m_portraitAnchorMax;
        }
    }
}
