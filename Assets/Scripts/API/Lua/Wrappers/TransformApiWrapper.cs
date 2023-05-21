using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [MoonSharpUserData]
    public class TransformApiWrapper
    {
        public TrTransform _TrTransform;

        public TransformApiWrapper(Vector3 translation, Quaternion rotation, float scale = 1)
        {
            _TrTransform = TrTransform.TRS(translation, rotation, scale);
        }

        public TransformApiWrapper(Vector3 translation, float scale = 1)
        {
            _TrTransform = TrTransform.TRS(translation, Quaternion.identity, scale);
        }

        public TrTransform inverse => _TrTransform.inverse;

        public Vector3 up => _TrTransform.up;
        public Vector3 down => -_TrTransform.up;
        public Vector3 right => _TrTransform.right;
        public Vector3 left => -_TrTransform.right;
        public Vector3 forward => _TrTransform.forward;
        public Vector3 back => -_TrTransform.forward;


        // Same as Multiply
        public TrTransform TransformBy(TrTransform transform) => _TrTransform * transform;
        public TrTransform TranslateBy(Vector3 translation) => _TrTransform * TrTransform.T(translation);
        public TrTransform RotateBy(Quaternion rotation) => _TrTransform * TrTransform.R(rotation);
        public TrTransform ScaleBy(float scale) => _TrTransform * TrTransform.S(scale);

        // Convenient shorthand
        public TransformApiWrapper(float x, float y, float z)
        {
            _TrTransform = TrTransform.T(new Vector3(x, y, z));
        }

        public TransformApiWrapper(TrTransform tr)
        {
            _TrTransform = tr;
        }

        public static TransformApiWrapper New(Vector3 translation, Quaternion rotation, float scale = 1)
        {
            var instance = new TransformApiWrapper(translation, rotation, scale);
            return instance;
        }

        public static TransformApiWrapper New(Vector3 translation, float scale = 1)
        {
            var instance = new TransformApiWrapper(translation, Quaternion.identity, scale);
            return instance;
        }

        public static TransformApiWrapper New(float scale = 1)
        {
            var instance = new TransformApiWrapper(Vector3.zero, Quaternion.identity, scale);
            return instance;
        }

        public static TransformApiWrapper New(float x, float y, float z)
        {
            var instance = new TransformApiWrapper(x, y, z);
            return instance;
        }

        public override string ToString()
        {
            return $"TrTransform({_TrTransform.translation}, {_TrTransform.rotation}, {_TrTransform.scale})";
        }

        public Vector3ApiWrapper position => new Vector3ApiWrapper(_TrTransform.translation);
        public RotationApiWrapper rotation => new RotationApiWrapper(_TrTransform.rotation);
        public float scale => _TrTransform.scale;

        public static TrTransform zero => TrTransform.identity;

        // Operators
        public TrTransform Multiply(TrTransform b) => _TrTransform * b;
        public bool Equals(TrTransform b) => _TrTransform == b;

        // Static Operators
        public static TrTransform Multiply(TrTransform a, TrTransform b) => a * b;
        public static bool Equals(TrTransform a, TrTransform b) => a == b;
    }
}
