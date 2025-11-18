using System.Diagnostics;
using System.IO;
using System.Text;
using ICOforge.Models;

namespace ICOforge.Services
{
    public class OxiPngOptimizer
    {
        private readonly string _exePath;

        public OxiPngOptimizer()
        {
            _exePath = Path.Combine(AppContext.BaseDirectory, "oxipng.exe");
        }

        public bool IsAvailable() => File.Exists(_exePath);

        public async Task OptimizeAsync(IEnumerable<string> filePaths, OxiPngOptions options)
        {
            if (!IsAvailable())
            {
                throw new FileNotFoundException($"OxiPNG executable not found at the expected path: {_exePath}", _exePath);
            }

            string arguments = BuildArguments(filePaths, options);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = _exePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                throw new System.Exception("Failed to start OxiPNG process.");
            }

            string errorOutput = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new System.Exception($"OxiPNG failed with exit code {process.ExitCode}: {errorOutput}");
            }
        }

        private string BuildArguments(IEnumerable<string> filePaths, OxiPngOptions options)
        {
            var sb = new StringBuilder();

            string level = options.OptimizationLevel switch
            {
                OxiPngOptimizationLevel.Level0 => "0",
                OxiPngOptimizationLevel.Level1 => "1",
                OxiPngOptimizationLevel.Level2 => "2",
                OxiPngOptimizationLevel.Level3 => "3",
                OxiPngOptimizationLevel.Level4 => "4",
                OxiPngOptimizationLevel.Level5 => "5",
                OxiPngOptimizationLevel.Level6 or OxiPngOptimizationLevel.Max => "6",
                _ => "2"
            };
            sb.Append($"-o {level} ");

            if (options.NoColorTypeReduction)
            {
                sb.Append("--nc ");
            }

            if (options.NoBitDepthReduction)
            {
                sb.Append("--nb ");
            }

            if (options.StripMode != OxiPngStripMode.None)
            {
                string strip = options.StripMode.ToString().ToLower();
                sb.Append($"--strip {strip} ");
            }

            if (options.Timeout.HasValue)
            {
                sb.Append($"--timeout {options.Timeout.Value.TotalSeconds} ");
            }

            sb.Append("--quiet ");

            foreach (var filePath in filePaths)
            {
                sb.Append($"\"{filePath}\" ");
            }

            return sb.ToString();
        }
    }
}