﻿// Copyright 2021 The Open Brush Authors
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
using System.IO;
using System.Linq;
using Unity.VectorGraphics;
using UnityEngine;
namespace TiltBrush
{
    public static class DrawStrokes
    {

        public static void TrTransformListToStroke(List<TrTransform> trList, Vector3 origin, float scale = 1f, float brushScale = 1f, bool rawStroke = false)
        {
            var tr = TrTransform.TRS(origin, Quaternion.identity, scale);
            MultiPositionPathsToStrokes(
                new List<List<Vector3>> { trList.Select(t => t.translation).ToList() },
                new List<List<Quaternion>> { trList.Select(t => t.rotation).ToList() },
                new List<List<float>> { trList.Select(t => t.scale).ToList() },
                tr, brushScale, rawStroke);
        }

        public static void SinglePath2dToStroke(List<Vector2> polyline2d, TrTransform tr)
        {
            var polylines2d = new List<List<Vector2>> { polyline2d };
            MultiPath2dToStrokes(polylines2d, tr);
        }

        public static void PositionPathsToStroke(List<TrTransform> path, Vector3 origin, float scale = 1f, float brushScale = 1f)
        {
            var positions = path.Select(x => x.translation).ToList();
            var rotations = path.Select(x => x.rotation).ToList();
            var pressures = path.Select(x => x.scale).ToList();
            var tr = TrTransform.TRS(origin, Quaternion.identity, scale);
            MultiPositionPathsToStrokes(
                new List<List<Vector3>> { positions },
                new List<List<Quaternion>> { rotations },
                new List<List<float>> { pressures },
                tr,
                brushScale
            );
        }

        public static void TrTransformListsToStroke(List<List<TrTransform>> path, Vector3 origin, float scale = 1f, float brushScale = 1f)
        {
            var positions = path.Select(x => x.Select(y => y.translation).ToList()).ToList();
            var rotations = path.Select(x => x.Select(y => y.rotation).ToList()).ToList();
            var pressures = path.Select(x => x.Select(y => y.scale).ToList()).ToList();
            var tr = TrTransform.TRS(origin, Quaternion.identity, scale);
            MultiPositionPathsToStrokes(
                positions,
                rotations,
                pressures,
                tr,
                brushScale
            );
        }

        public static void MultiPathsToStrokes(List<List<List<float>>> strokeData, Vector3 origin, Quaternion rotation = default, float scale = 1f, float brushScale = 1f, bool rawStroke = false)
        {
            MultiPathsToStrokes(strokeData, TrTransform.TRS(origin, rotation, scale), brushScale, rawStroke);
        }

        public static void MultiPathsToStrokes(List<List<List<float>>> strokeData, TrTransform tr, float brushScale = 1f, bool rawStroke = false)
        {
            var positions = new List<List<Vector3>>();
            var orientations = new List<List<Quaternion>>();
            var pressures = new List<List<float>>();

            // This assumes that the stroke data is consistent.
            // If we have orientation or pressure for the first point, we have it for all
            bool orientationsExist = strokeData[0][0].Count == 6 || strokeData[0][0].Count == 7;
            bool pressuresExist = strokeData[0][0].Count == 6 || strokeData[0][0].Count == 7;

            foreach (List<List<float>> positionList in strokeData)
            {
                var positionsPath = new List<Vector3>();
                var orientationsPath = new List<Quaternion>();
                var pressuresPath = new List<float>();

                foreach (List<float> controlPoint in positionList)
                {
                    if (controlPoint.Count < 3) { controlPoint.Add(0); }  // Handle 2D paths

                    positionsPath.Add(new Vector3(controlPoint[0], controlPoint[1], controlPoint[2]));
                    if (orientationsExist)
                    {
                        orientationsPath.Add(
                            Quaternion.Euler(
                                controlPoint[3],
                                controlPoint[4],
                                controlPoint[5]
                            ));
                    }
                    if (pressuresExist)
                    {
                        pressuresPath.Add(controlPoint.Last());
                    }
                }
                positions.Add(positionsPath);
                if (orientationsExist) orientations.Add(orientationsPath);
                if (pressuresExist) pressures.Add(pressuresPath);
            }
            MultiPositionPathsToStrokes(positions, orientations, pressures, tr, brushScale, rawStroke);
        }

        public static void MultiPath2dToStrokes(List<List<Vector2>> polylines2d, TrTransform tr,
                                                float brushScale = 1f, bool breakOnOrigin = false)
        {
            var positions = new List<List<Vector3>>();
            foreach (List<Vector2> positionList in polylines2d)
            {
                var path = new List<Vector3>();
                foreach (Vector2 position in positionList)
                {
                    path.Add(new Vector3(position.x, position.y, 0));
                }
                positions.Add(path);
            }
            MultiPositionPathsToStrokes(positions, null, null, tr, brushScale, breakOnOrigin);
        }

        public static void MultiPositionPathsToStrokes(
            List<List<Vector3>> positions,
            List<List<Quaternion>> orientations,
            List<List<float>> pressures,
            TrTransform tr,
            float brushScale = 1f,
            bool breakOnOrigin = false,
            bool rawStrokes = false)
        {
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            uint time = 0;
            float minPressure = PointerManager.m_Instance.MainPointer.CurrentBrush.PressureSizeMin(false);
            float defaultPressure = Mathf.Lerp(minPressure, 1f, 0.5f);
            var group = App.GroupManager.NewUnusedGroup();
            for (var pathIndex = 0; pathIndex < positions.Count; pathIndex++)
            {
                // Single joined paths
                var positionList = positions[pathIndex];
                if (positionList.Count < 2) continue;
                var controlPoints = new List<PointerManager.ControlPoint>();
                for (var vertexIndex = 0; vertexIndex < positionList.Count - 1; vertexIndex++)
                {
                    var position = positionList[vertexIndex];
                    Quaternion orientation = orientations?.Any() == true ?
                        orientations[pathIndex][vertexIndex] :
                        Quaternion.identity;
                    float pressure = pressures?.Any() == true ?
                        pressures[pathIndex][vertexIndex] :
                        defaultPressure;
                    var nextPosition = positionList[(vertexIndex + 1) % positionList.Count];

                    if (rawStrokes)
                    {
                        controlPoints.Add(new PointerManager.ControlPoint
                        {
                            m_Pos = position,
                            m_Orient = orientation,
                            m_Pressure = pressure,
                            m_TimestampMs = time++
                        });
                    }
                    else
                    {
                        // Create extra control points if needed
                        // Procedural strokes need to have extra control points added to avoid being smoothed out.
                        for (float step = 0; step <= 1f; step += 0.25f)
                        {
                            controlPoints.Add(new PointerManager.ControlPoint
                            {
                                m_Pos = position + (nextPosition - position) * step,
                                m_Orient = orientation,
                                m_Pressure = pressure,
                                m_TimestampMs = time++
                            });
                        }
                    }

                }
                var stroke = new Stroke
                {
                    m_Type = Stroke.Type.NotCreated,
                    m_IntendedCanvas = App.Scene.ActiveCanvas,
                    m_BrushGuid = brush.m_Guid,
                    m_BrushScale = brushScale,
                    m_BrushSize = PointerManager.m_Instance.MainPointer.BrushSizeAbsolute,
                    m_Color = App.BrushColor.CurrentColor,
                    m_Seed = 0,
                    m_ControlPoints = controlPoints.ToArray(),
                };
                stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
                stroke.Group = group;
                stroke.Recreate(tr, App.Scene.ActiveCanvas);
                if (pathIndex != 0) stroke.m_Flags = SketchMemoryScript.StrokeFlags.IsGroupContinue;
                SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
                var undoParent = ApiManager.Instance.ActiveUndo;
                var cmd = new BrushStrokeCommand(
                    stroke, WidgetManager.m_Instance.ActiveStencil, 123, undoParent
                );
                if (undoParent == null)
                {
                    // No active undo. So actually perform the command
                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
                }
            }
        }

        public static void Polygon(int sides, TrTransform tr = default)
        {
            var path = new List<Vector3>();
            for (float i = 0; i <= sides; i++)
            {
                var theta = Mathf.PI * (i / sides) * 2f;
                theta += Mathf.Deg2Rad;
                var point = new Vector3(
                    Mathf.Cos(theta),
                    Mathf.Sin(theta),
                    0
                );
                point = ApiManager.Instance.BrushRotation * point;
                path.Add(point);
            }
            MultiPositionPathsToStrokes(new List<List<Vector3>> { path }, null, null, tr);
        }

        public static void Text(string text, TrTransform tr)
        {
            var textToStroke = new TextToStrokes(ApiManager.Instance.TextFont);
            var polyline2d = textToStroke.Build(text);
            MultiPositionPathsToStrokes(polyline2d, null, null, tr);
        }

        public static void SvgPath(string svgPathString, TrTransform tr)
        {
            MultiPath2dToStrokes(ParseSvgPath(svgPathString), tr, 1f, true);
        }

        public static List<List<Vector2>> ParseSvgPath(string svgPathString)
        {
            var svgText = $"<svg xmlns=\"http: //www.w3.org/2000/svg\"><path d=\"{svgPathString}\"/></svg>";
            TextReader stringReader = new StringReader(svgText);
            var sceneInfo = SVGParser.ImportSVG(stringReader);
            VectorUtils.TessellationOptions tessellationOptions = new VectorUtils.TessellationOptions
            {
                StepDistance = 100.0f,
                MaxCordDeviation = 0.5f,
                MaxTanAngleDeviation = 0.1f,
                SamplingStepSize = 0.01f,
                AllowConcavePaths = true
            };
            var geoms = VectorUtils.TessellateScene(sceneInfo.Scene, tessellationOptions);
            var svgPolyline = new List<List<Vector2>>();
            foreach (var geom in geoms)
            {
                var verts = geom.Vertices.Skip(1); // Skip the centroid vertex added for tessellation
                verts = verts.Select(v => new Vector2(v.x, -v.y)); // SVG is Y down, Unity is Y up
                svgPolyline.Add(verts.ToList());
            }
            return svgPolyline;
        }

        public static void CameraPath(CameraPath path, TrTransform tr = default)
        {
            var positions = new List<Vector3>();
            var rotations = new List<Quaternion>();
            for (float t = 0; t < path.Segments.Count; t += .1f)
            {
                positions.Add(path.GetPosition(new PathT(t)));
                rotations.Add(path.GetRotation(new PathT(t)));
            }
            MultiPositionPathsToStrokes(
                new List<List<Vector3>> { positions },
                new List<List<Quaternion>> { rotations },
                null,
                tr
            );

        }
    }
}
