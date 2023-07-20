// Copyright 2020 The Tilt Brush Authors
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

Shader "Brush/Special/Intersection" {

  Properties { }

  SubShader{

    // -------------------------------------------------------------------------------------- //
    // Intersection Test Pass
    // -------------------------------------------------------------------------------------- //

    Pass{
      Tags{ "Queue" = "Opaque" "IgnoreProjector" = "True" "RenderType" = "Opaque" "DisableBatching" = "True" }
      Lighting Off
      Cull Off
      Blend Off

      CGPROGRAM

#pragma vertex vert
#pragma fragment frag

#include "Assets/Shaders/Include/Brush.cginc"
#include "Assets/Shaders/Include/PackInt.cginc"

      struct appdata_t {
        float4 vertex : POSITION;
        float4 triangleids : TEXCOORD3;
      };

      struct v2f {
        float4 vertex : POSITION;
        float4 triangleids : TEXCOORD3;
        half4 color : COLOR;
      };

      v2f vert(appdata_t v)
      {
        v2f o;
        o.vertex = UnityObjectToClipPos(v.vertex);
        o.color = half4(0, 0, 0, 0);
        o.triangleids = v.triangleids;
        return o;
      }

      half4 frag(v2f i) : COLOR
      {
        uint triangleIndex = uint(i.triangleids.x * 65535) * 65535 + uint(i.triangleids.y * 65535);
        return PackUint16x2ToRgba8(uint2(_BatchID, triangleIndex));
      }
        ENDCG
    }
  }

  Fallback "Unlit/Diffuse"

}
