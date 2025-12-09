using UnityEngine;

namespace Miris.Runtime
{
    /// <summary>
    /// Business logic to support the scene transition behaviors 
    /// we want in the Miris Player.
    /// 
    /// Currently supports only one Stream at a time. 
    /// TODO: Guardrails for when m_stream is null
    /// </summary>
    public class MirisPlayerSceneManager : MonoBehaviour
    {
        [SerializeField]
        MirisStreamController m_streamController;

        [SerializeField]
        MirisStream m_stream;

        public string GetAssetId()
        {
            return m_stream.m_assetId;
        }

        public void ClearScene()
        {
            ChangeScene("");
        }

        public void ChangeScene(string assetId, bool experimentalPath = false)
        {
            if (assetId == m_stream.m_assetId)
            {
                return;
            }

            if (m_stream.IsLoaded())
            {
                FadeOutScene(assetId);
            }
            else
            {
                LoadScene(assetId);
            }
        }

        private void LoadScene(string assetId)
        {
            m_stream.m_assetId = assetId;
        }

        private void FadeOutScene(string assetId)
        {
            m_stream.StopAllCoroutines();
            LoadScene(assetId);
        }
    }
}
