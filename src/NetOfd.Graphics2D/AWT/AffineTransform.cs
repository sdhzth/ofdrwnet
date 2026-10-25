using System;
using System.IO;

namespace NetOfd.Graphics2D.AWT;

/// <summary>
/// 二维仿射变换矩阵（替代 Java AWT AffineTransform）
/// </summary>
public class AffineTransform
{
    private double _m00, _m10, _m01, _m11, _m02, _m12;

    public AffineTransform()
    { _m00 = _m11 = 1; }

    public AffineTransform(double m00, double m10, double m01, double m11, double m02, double m12)
    { _m00 = m00; _m10 = m10; _m01 = m01; _m11 = m11; _m02 = m02; _m12 = m12; }

    public double ScaleX => _m00;
    public double ScaleY => _m11;
    public double ShearX => _m01;
    public double ShearY => _m10;
    public double TranslateX => _m02;
    public double TranslateY => _m12;

    public bool IsIdentity
    {
        get
        {
            return Math.Abs(_m00 - 1) < 1e-10 && Math.Abs(_m11 - 1) < 1e-10
                && Math.Abs(_m01) < 1e-10 && Math.Abs(_m10) < 1e-10
                && Math.Abs(_m02) < 1e-10 && Math.Abs(_m12) < 1e-10;
        }
    }

    public void SetToIdentity() { _m00 = _m11 = 1; _m01 = _m10 = _m02 = _m12 = 0; }

    public void SetToTranslation(double tx, double ty)
    { _m00 = _m11 = 1; _m01 = _m10 = 0; _m02 = tx; _m12 = ty; }

    public void SetToScale(double sx, double sy)
    { _m00 = sx; _m11 = sy; _m01 = _m10 = _m02 = _m12 = 0; }

    public void SetToRotation(double theta)
    {
        double c = Math.Cos(theta), s = Math.Sin(theta);
        _m00 = _m11 = c; _m01 = -s; _m10 = s; _m02 = _m12 = 0;
    }

    public void SetToRotation(double theta, double px, double py)
    {
        SetToRotation(theta);
        _m02 = px - _m00 * px - _m01 * py;
        _m12 = py - _m10 * px - _m11 * py;
    }

    public void Translate(double tx, double ty)
    {
        _m02 += _m00 * tx + _m01 * ty;
        _m12 += _m10 * tx + _m11 * ty;
    }

    public void Translate(double tx, double ty, AffineTransform result)
    {
        result._m00 = _m00; result._m01 = _m01; result._m02 = _m00 * tx + _m01 * ty + _m02;
        result._m10 = _m10; result._m11 = _m11; result._m12 = _m10 * tx + _m11 * ty + _m12;
    }

    public void Scale(double sx, double sy)
    {
        // AWT 语义：this = this · S（后续坐标先缩放再经原矩阵变换，平移分量不受影响）
        _m00 *= sx; _m01 *= sy;
        _m10 *= sx; _m11 *= sy;
    }

    public void Rotate(double theta)
    {
        // AWT 语义：this = this · R（用户空间旋转），R = [[c, s], [-s, c]]
        double c = Math.Cos(theta), s = Math.Sin(theta);
        double m00 = _m00, m01 = _m01;
        double m10 = _m10, m11 = _m11;
        _m00 = m00 * c - m01 * s; _m01 = m00 * s + m01 * c;
        _m10 = m10 * c - m11 * s; _m11 = m10 * s + m11 * c;
    }

    public void Concatenate(AffineTransform? tx)
    {
        if (tx == null) return;
        double tm00 = tx._m00, tm01 = tx._m01, tm02 = tx._m02;
        double tm10 = tx._m10, tm11 = tx._m11, tm12 = tx._m12;

        double m00 = _m00, m01 = _m01, m02 = _m02;
        double m10 = _m10, m11 = _m11, m12 = _m12;

        _m00 = m00 * tm00 + m01 * tm10;
        _m01 = m00 * tm01 + m01 * tm11;
        _m02 = m00 * tm02 + m01 * tm12 + m02;
        _m10 = m10 * tm00 + m11 * tm10;
        _m11 = m10 * tm01 + m11 * tm11;
        _m12 = m10 * tm02 + m11 * tm12 + m12;
    }

    public AffineTransform CreateInverse()
    {
        double det = _m00 * _m11 - _m01 * _m10;
        if (Math.Abs(det) < 1e-10) throw new InvalidOperationException("Matrix is not invertible");
        double invDet = 1 / det;
        return new AffineTransform(
            _m11 * invDet, -_m10 * invDet,
            -_m01 * invDet, _m00 * invDet,
            (_m01 * _m12 - _m11 * _m02) * invDet,
            (_m10 * _m02 - _m00 * _m12) * invDet);
    }

    public IShape CreateTransformedShape(IShape shape)
    {
        // 简化为返回原形状（完整实现需要变换所有点）
        return shape;
    }

    public (double X, double Y) Transform(double x, double y)
    {
        return (_m00 * x + _m01 * y + _m02, _m10 * x + _m11 * y + _m12);
    }

    public double[] GetMatrix()
    {
        return new double[] { _m00, _m10, _m01, _m11, _m02, _m12 };
    }

    public void GetMatrix(double[] varArray)
    {
        if (varArray.Length >= 6) { varArray[0] = _m00; varArray[1] = _m10; varArray[2] = _m01; varArray[3] = _m11; varArray[4] = _m02; varArray[5] = _m12; }
    }

    public static AffineTransform GetTranslateInstance(double tx, double ty)
    {
        var t = new AffineTransform(); t.SetToTranslation(tx, ty); return t;
    }

    public static AffineTransform GetScaleInstance(double sx, double sy)
    {
        var t = new AffineTransform(); t.SetToScale(sx, sy); return t;
    }

    public static AffineTransform GetRotateInstance(double theta)
    {
        var t = new AffineTransform(); t.SetToRotation(theta); return t;
    }

    public static AffineTransform GetRotateInstance(double theta, double px, double py)
    {
        var t = new AffineTransform(); t.SetToRotation(theta, px, py); return t;
    }

    public AffineTransform Clone() => new(_m00, _m10, _m01, _m11, _m02, _m12);

    public override string ToString() => $"AffineTransform[[{_m00:F3}, {_m01:F3}, {_m02:F3}], [{_m10:F3}, {_m11:F3}, {_m12:F3}]]";
}

/// <summary>
/// 非可逆变换异常
/// </summary>
public class NoninvertibleTransformException : Exception
{
    public NoninvertibleTransformException() { }
    public NoninvertibleTransformException(string msg) : base(msg) { }
}