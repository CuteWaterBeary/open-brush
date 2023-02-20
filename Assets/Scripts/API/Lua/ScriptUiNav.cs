﻿// Copyright 2022 The Tilt Brush Authors
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

using System.Collections;
using System.Collections.Generic;
using TiltBrush;
using TMPro;
using UnityEngine;

public class ScriptUiNav : MonoBehaviour
{

    private TextMeshPro textMesh;
    public LuaManager.ApiCategory ApiCategory;

    void Start()
    {
        Init();
    }

    public void Init()
    {
        textMesh = GetComponentInChildren<TextMeshPro>();
        var names = LuaManager.Instance.GetScriptNames(ApiCategory);
        if (names.Count > 0) textMesh.text = names[0];
    }

    public void ChangeScript(int increment)
    {
        LuaManager.Instance.ChangeCurrentScript(ApiCategory, increment);
        var index = LuaManager.Instance.ActiveScripts[ApiCategory];
        var scriptName = LuaManager.Instance.GetScriptNames(ApiCategory)[index];
        textMesh.text = scriptName;
    }
}
