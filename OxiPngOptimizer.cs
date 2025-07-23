using System.Diagnostics;
using System.IO;
using System.Text;

namespace ICOforge
{
    public class OxiPngOptimizer
    {
        private readonly string _exePath;

        public OxiPngOptimizer()
        {
            _exePath = Path.Combine(AppContext.BaseDirectory, "oxipng.exe");
        }

        public bool IsAvailable() => File.Exists(_exePath);

        public async Task OptimizeAsync(string filePath, OxiPngOptions options)
        {
            if (!IsAvailable())
            {
                throw new FileNotFoundException($"OxiPNG executable not found at the expected path: {_exePath}", _exePath);
            }

            string arguments = BuildArguments(filePath, options);

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

        private string BuildArguments(string filePath, OxiPngOptions options)
        {
            var sb = new StringBuilder();

            string level = options.OptimizationLevel switch
            {
                OxiPngOptimizationLevel.Max => "6",
                _ => options.OptimizationLevel.ToString().ToLower().Replace("level", "")
            };
            sb.Append($"-o {level} ");

            if (options.StripMode != OxiPngStripMode.None)
            {
                string strip = options.StripMode.ToString().ToLower();
                sb.Append($"--strip {strip} ");
            }

            if (options.Timeout.HasValue)
            {
                sb.Append($"--timeout {options.Timeout.Value.TotalSeconds} ");
            }

            sb.Append("--threads 1 ");
            sb.Append("--quiet ");
            sb.Append($"\"{filePath}\"");

            return sb.ToString();
        }
    }
}