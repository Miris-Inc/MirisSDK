// Copyright (c) 2024 Miris. All rights reserved.

// Standard library
using System;

// Unity
using UnityEngine;
using UnityEngine.UI;

// Text mesh pro
using TMPro;


namespace Miris.Runtime
{
    public abstract class DeveloperBaseController : MonoBehaviour
    {
        void Start()
        {
            InitializeUI();
        }

        void OnEnable()
        {

        }

        void OnDisable()
        {

        }

        void OnDestroy()
        {
            TeardownUI();
        }

        // --------------------------------------------------------------------
        // UI Initialization
        // --------------------------------------------------------------------
        // Initialization of UI elements by populating dropdowns based on enums, etc
        public abstract void InitializeUI();

        // --------------------------------------------------------------------
        // UI Teardown
        // --------------------------------------------------------------------

        public abstract void TeardownUI();

        // --------------------------------------------------------------------
        // Synchronization of model data to UI
        // --------------------------------------------------------------------

        public abstract void SyncUI();

    }
}
