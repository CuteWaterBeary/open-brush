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
using System.Linq;
using UnityEngine;

namespace TiltBrush
{

    public class BrushEditorTexturePopUpWindow : PagingPopUpWindow
    {

        [NonSerialized] public int ActiveTextureIndex;
        
        protected EditBrushPanel ParentPanel
        {
            get
            {
                return m_ParentPanel as EditBrushPanel;
            }
        }
        protected override int m_DataCount
        {
            get
            {
                return ParentPanel.AvailableTextures.Count();
            }
        }
        
        [NonSerialized] public BrushEditorTexturePickerButton OpenerButton;

        protected override void InitIcon(ImageIcon icon)
        {
            icon.m_Valid = true;
        }

        protected override void RefreshIcon(ImageIcon icon, int index)
        {
            EditBrushEditorTextureButton iconButton = icon.m_IconScript as EditBrushEditorTextureButton;
            Texture2D thisTexture = ParentPanel.AvailableTextures[index];
            iconButton.SetPreset(thisTexture, ParentPanel.TextureNames[index], index);
            // TODO this doesn't work because OpenerButton isn't set until after Init has created all the buttons
            if (OpenerButton != null)
            {
                Texture2D currentTexture = (Texture2D)ParentPanel.PreviewMaterial.GetTexture(OpenerButton.TexturePropertyName);
                iconButton.SetButtonSelected(thisTexture.imageContentsHash==currentTexture.imageContentsHash);
            }
        }

        public override void Init(GameObject rParent, string sText)
        {
            // Set this early so that Init can access parent to get m_DataCount
            m_ParentPanel = rParent.GetComponent<BasePanel>();
            
            if (ActiveTextureIndex >= 0)
            {
                m_RequestedPageIndex = ActiveTextureIndex / m_IconCountNavPage;
            }

            base.Init(rParent, sText);
        }
        public void SetActiveButtonSelected(int index)
        {
            var iconButton = m_Icons[index].m_IconScript;
            iconButton.SetButtonSelected(true);
        }
    }
} // namespace TiltBrush