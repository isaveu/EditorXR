﻿#if UNITY_EDITOR
using UnityEngine;
using UnityEditor.Experimental.EditorVR.Handles;

namespace UnityEditor.Experimental.EditorVR.Workspaces
{
    sealed class InspectorUI : MonoBehaviour
    {
#pragma warning disable 649
        [SerializeField]
        InspectorListViewController m_ListView;

        [SerializeField]
        LinearHandle m_ScrollHandle;

        [SerializeField]
        RectTransform m_ListPanel;
#pragma warning restore 649

        public RectTransform listPanel
        {
            get { return m_ListPanel; }
        }

        public LinearHandle scrollHandle
        {
            get { return m_ScrollHandle; }
        }

        public InspectorListViewController listView
        {
            get { return m_ListView; }
        }
    }
}
#endif
