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
using System.Globalization;
using System.Linq;
using Polyhydra.Wythoff;


namespace TiltBrush
{

    public class PolyhydraPopUpWindowUniforms : PolyhydraPopUpWindowBase
    {

        private Uniform[] GetCurrentUniformList(PolyHydraEnums.PolyhedraCategory shapeCategory)
        {
            switch (shapeCategory)
            {
                case PolyHydraEnums.PolyhedraCategory.Platonic:
                    return Uniform.Platonic;
                case PolyHydraEnums.PolyhedraCategory.Archimedean:
                    return Uniform.Archimedean;
                case PolyHydraEnums.PolyhedraCategory.Prisms:
                    return Uniform.Prismatic;
                case PolyHydraEnums.PolyhedraCategory.KeplerPoinsot:
                    return Uniform.KeplerPoinsot;
                    // case ShapeCategories.UniformConvex:
                    //   return Uniform.Convex;
                    // case ShapeCategories.UniformStar:
                    //   return Uniform.Star;
            }

            return null;
        }
        protected override string[] GetButtonList()
        {
            return GetCurrentUniformList(ParentPanel.CurrentShapeCategory).Select(x => x.Name).ToArray();
        }

        protected override string GetButtonTexturePath(int i)
        {
            string name = GetCurrentUniformList(ParentPanel.CurrentShapeCategory)[i].Name;
            return $"ShapeButtons/poly_uniform_{name}".Replace(" ", "_");
        }

        public override void HandleButtonPress(int buttonIndex)
        {
            var enumName = GetCurrentUniformList(ParentPanel.CurrentShapeCategory)[buttonIndex].Name;
            enumName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(enumName.ToLower());
            enumName = enumName.Replace(" ", "_");
            UniformTypes polyType = (UniformTypes)Enum.Parse(typeof(UniformTypes), enumName);
            ParentPanel.PolyhydraModel.UniformPolyType = polyType;
            ParentPanel.ButtonUniformType.SetButtonTexture(GetButtonTexture(buttonIndex));
            ParentPanel.SetSliderConfiguration();
        }

    }
} // namespace TiltBrush
