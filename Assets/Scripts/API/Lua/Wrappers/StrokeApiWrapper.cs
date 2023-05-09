﻿using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [MoonSharpUserData]
    public class StrokeApiWrapper
    {
        public Stroke _Stroke;
        private PathApiWrapper m_Path;
        public PathApiWrapper path
        {
            get
            {
                if (m_Path == null)
                {
                    var origin = _Stroke.m_ControlPoints[0].m_Pos;
                    m_Path = new PathApiWrapper(
                        _Stroke.m_ControlPoints.Select(cp => cp.m_Pos).ToList()
                    );
                    m_Path.Translate(-origin);
                }
                return m_Path;
            }
            set
            {
                _Stroke.m_ControlPoints = value._Path.Select(tr => new PointerManager.ControlPoint
                {
                    m_Pos = tr.translation,
                    m_Orient = tr.rotation,
                    m_Pressure = tr.scale
                }).ToArray();
                _Stroke.Recreate();
            }
        }

        public StrokeApiWrapper(Stroke stroke)
        {
            _Stroke = stroke;
        }

        public StrokeApiWrapper(StrokeApiWrapper stroke)
        {
            _Stroke = stroke._Stroke;
        }

        public override string ToString()
        {
            return $"{_Stroke.m_BatchSubset.m_ParentBatch.Brush.m_Description} stroke on {_Stroke.Canvas.name})";
        }


        // public Transform this[int index] => SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
        // public Stroke last => this[count - 1];
        // public Stroke this[int index] => SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
        // public Stroke last => this[count - 1];
        // public  Stroke main => this[0];
        // public int index => SketchMemoryScript.m_Instance.GetAllActiveStrokes().IndexOf(this._Stroke);

        // public static StrokesApiWrapper New(StrokesApiWrapper stroke)
        // {
        //     var instance = new StrokesApiWrapper(stroke);
        //     return instance;
        // }
        // public void add(int index) => ApiMethods.AddPointToStroke(index);
        // public void quantize() => ApiMethods.QuantizeSelection(index);
        // public void addNoise(Vector3 a) => ApiMethods.PerlinNoiseSelection(a);


        // Highly experimental
        public void ChangeMaterial(string brushName)
        {
            var brush = ApiMethods.LookupBrushDescriptor(brushName);
            _Stroke.m_BatchSubset.m_ParentBatch.ReplaceMaterial(brush.Material);
        }

        public TrTransform this[int index]
        {
            get => path._Path[index];
            set
            {
                var newPath = path._Path.ToList();
                newPath[index] = value;
                path = new PathApiWrapper(newPath);
            }
        }

        public int count => _Stroke.m_ControlPoints.Length;

        public void Delete()
        {
            SketchMemoryScript.m_Instance.RemoveMemoryObject(_Stroke);
            _Stroke.Uncreate();
            _Stroke = null;
        }
        public void Select()
        {
            SelectionManager.m_Instance.SelectStrokes(new List<Stroke> { _Stroke });
        }
        public void SelectMultiple(int from, int to) => ApiMethods.SelectStrokes(from, to);
        public void Join(int from, int to) => ApiMethods.JoinStrokes(from, to);
        public void JoinPrevious() => ApiMethods.JoinStroke();
        public void Import(string name) => ApiMethods.MergeNamedFile(name);

    }

}
