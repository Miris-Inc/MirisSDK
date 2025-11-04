using UnityEngine;

namespace Aqua.Runtime
{
    public class AquaAssetRoot : MonoBehaviour
    {
        [SerializeField]
        public AssetMetadata m_assetMetadata = new AssetMetadata
        {
            m_version = "0.0.1"
        };
    }
}