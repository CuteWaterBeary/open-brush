﻿// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using UnityEngine;

namespace TiltBrush
{
    public class BrushEditorTexturePickerButton : OptionButton
    {
        [NonSerialized] public EditBrushPanel ParentPanel;
        [NonSerialized] public string TexturePropertyName;
        public int TextureIndex;
        [SerializeField] private GameObject[] m_ObjectsToHideBehindPopups;
        [NonSerialized] public BrushEditorTexturePopUpWindow popup;
        
        protected override void OnButtonPressed()
        {
            for (int i = 0; i < m_ObjectsToHideBehindPopups.Length; ++i)
            {
                m_ObjectsToHideBehindPopups[i].SetActive(false);
            }
            base.OnButtonPressed();
            
            popup = (BrushEditorTexturePopUpWindow) ParentPanel.PanelPopUp;
            popup.ActiveTextureIndex = TextureIndex;
            popup.OpenerButton = this;
            popup.SetActiveButtonSelected(TextureIndex);

        }
        
        public void SetPreset(Texture tex, string texName)
        {
            SetButtonTexture((Texture2D)tex);
            SetDescriptionText(texName==null?"None" : texName);
        }
        
        void OnTextureChanged()
        {
            BasePanel panel = m_Manager.GetPanelForPopUps();
            if (panel != null)
            {
                SetColor(panel.GetGazeColorFromActiveGazePercent());
            }
        }
        
        override public void GazeRatioChanged(float gazeRatio)
        {
            GetComponent<Renderer>().material.SetFloat("_Distance", gazeRatio);
        }

        void OnPopUpClose()
        {
            for (int i = 0; i < m_ObjectsToHideBehindPopups.Length; ++i)
            {
                m_ObjectsToHideBehindPopups[i].SetActive(true);
            }
            popup = null;
        }
        
        void OnTexturePickedAsFinal(Texture2D tex)
        {
            //ParentPanel.TextureChanged(ShaderPropertyName, tex);
        }
        
        public void UpdateValue(Texture2D tex)
        {
            this.m_ButtonTexture = tex;
        }
    }
} // namespace TiltBrush