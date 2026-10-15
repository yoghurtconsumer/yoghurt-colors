using Chromatic.Core.Validation;

namespace Chromatic.Core.Color;

// Numerical simulation results: Machado/Oliveira/Fernandes (2009), severity 1.0.
// Provenance and distribution scope are recorded in docs/COLOR-PROVENANCE.md.
// D is Chromatic's original encoded-SDR approximation, not the paper's simulation.
public static class DaltonizationBuilder
{
    public static Matrix5x5 Simulation(string mode)
    {
        double[] publishedRows = mode switch
        {
            "None" => [1, 0, 0, 0, 1, 0, 0, 0, 1],
            "RedWeak" => [.152286, 1.052583, -.204868, .114503, .786281, .099216, -.003882, -.048116, 1.051998],
            "GreenWeak" => [.367322, .860646, -.227968, .280085, .672501, .047413, -.011820, .042940, .968881],
            "BlueWeak" => [1.255528, -.076749, -.178779, -.078411, .930809, .147602, .004733, .691367, .303900],
            _ => throw new ArgumentException("Unknown CVD simulation mode.", nameof(mode))
        };
        var values = Matrix5x5.Identity.Values.ToArray();
        for (var input = 0; input < 3; input++)
            for (var output = 0; output < 3; output++) values[input * 5 + output] = publishedRows[output * 3 + input];
        return new(values);
    }
    public static Matrix5x5 Correction(string mode, double strength = 1)
    {
        SettingsValidator.Range(strength, 0, 1, nameof(strength));
        var simulation = Simulation(mode);
        if (mode == "None" || strength == 0) return Matrix5x5.Identity;
        double[,] redistribute = mode == "BlueWeak" ? new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { .7, .7, 0 } }
            : new double[,] { { 0, .7, .7 }, { 0, 1, 0 }, { 0, 0, 1 } };
        var result = Matrix5x5.Identity.Values.ToArray();
        for (var input = 0; input < 3; input++)
            for (var output = 0; output < 3; output++)
                for (var k = 0; k < 3; k++)
                    result[input * 5 + output] += strength * ((input == k ? 1 : 0) - simulation[input, k]) * redistribute[k, output];
        return new(result);
    }
}
