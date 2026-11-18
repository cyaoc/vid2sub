namespace Vid2Sub.CLI;

/// <summary>One-line console progress bar; draws nothing when output is redirected.</summary>
public sealed class ConsoleProgressBar(string label) : IProgress<double>
{
    private const int Width = 30;
    private int _lastPermille = -1;

    public void Report(double value)
    {
        var permille = (int)(Math.Clamp(value, 0, 1) * 1000);
        if (permille == _lastPermille || Console.IsOutputRedirected)
        {
            return;
        }

        _lastPermille = permille;
        var filled = permille * Width / 1000;
        Console.Write($"\r{label}: [{new string('█', filled)}{new string('░', Width - filled)}] {permille / 10.0:0.0}%");
        if (permille == 1000)
        {
            Console.WriteLine();
        }
    }
}
