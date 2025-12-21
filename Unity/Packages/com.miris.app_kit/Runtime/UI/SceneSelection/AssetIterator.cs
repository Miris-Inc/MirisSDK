// Copyright © 2025 Miris, Inc. All rights reserved.

using UnityEngine;

using System.Threading.Tasks;

namespace Miris.Runtime
{
    public class AssetIterator
    {
        private int m_currentAssetIndex = 0;
        private AssetInfo[] m_currentAssets;

        public async Task<AssetInfo[]> LoadAssets(AssetManager assetManager)
        {
            m_currentAssets = await assetManager.GetAssets();
            return m_currentAssets;
        }

        private bool HasValidAssets()
        {
            return m_currentAssets != null && m_currentAssets.Length > 0;
        }

        private int ValidateAssetIndex(int index)
        {
            int assetCount = m_currentAssets.Length;
            return (index % assetCount + assetCount) % assetCount;
        }

        public string PreviousAsset()
        {
            if(!HasValidAssets())
            {
                return string.Empty;
            }
            m_currentAssetIndex = ValidateAssetIndex(m_currentAssetIndex - 1);
            return m_currentAssets[m_currentAssetIndex].m_uuid;
        }

        public string NextAsset()
        {
            if(!HasValidAssets())
            {
                return string.Empty;
            }
            m_currentAssetIndex = ValidateAssetIndex(m_currentAssetIndex + 1);
            return m_currentAssets[m_currentAssetIndex].m_uuid;
        }

        public string GetAsset(int index = 0)
        {
            if(!HasValidAssets())
            {
                return string.Empty;
            }
            m_currentAssetIndex = ValidateAssetIndex(index);
            return m_currentAssets[index].m_uuid;
        }
    }
}
