using System.Collections.Immutable;

namespace Chromatic.Core.Color;

public readonly record struct Rgba(double R, double G, double B, double A = 1);

// Row-major homogeneous color matrix. [R G B A 1] multiplies on the left.
public sealed class Matrix5x5
{
    public ImmutableArray<double> Values { get; }
    public double this[int row, int column] => row is >= 0 and < 5 && column is >= 0 and < 5
        ? Values[row * 5 + column] : throw new ArgumentOutOfRangeException(nameof(row));
    public static Matrix5x5 Identity { get; } = new(Enumerable.Range(0, 25).Select(i => i % 6 == 0 ? 1d : 0d));
    public Matrix5x5(IEnumerable<double> values)
    {
        Values = values.ToImmutableArray();
        if (Values.Length != 25 || Values.Any(v => !double.IsFinite(v)))
            throw new ArgumentException("A matrix requires exactly 25 finite coefficients.", nameof(values));
    }
    public Rgba Transform(Rgba value)
    {
        if (!double.IsFinite(value.R) || !double.IsFinite(value.G) || !double.IsFinite(value.B) || !double.IsFinite(value.A))
            throw new ArgumentException("Color channels must be finite.", nameof(value));
        double Output(int channel) => value.R * this[0, channel] + value.G * this[1, channel]
            + value.B * this[2, channel] + value.A * this[3, channel] + this[4, channel];
        return new(Output(0), Output(1), Output(2), Output(3));
    }
    public static Matrix5x5 operator *(Matrix5x5 first, Matrix5x5 second)
    {
        var values = new double[25];
        for (var row = 0; row < 5; row++)
            for (var column = 0; column < 5; column++)
                for (var k = 0; k < 5; k++) values[row * 5 + column] += first[row, k] * second[k, column];
        return new(values);
    }
    public ImmutableArray<float> ToSinglePrecision()
    {
        if (Values.Any(v => Math.Abs(v) > float.MaxValue)) throw new ArgumentException("Matrix exceeds native float range.");
        return Values.Select(v => (float)v).ToImmutableArray();
    }
}
