using System.IO;
using TestProject.Services;
using Microsoft.AspNetCore.Http.Features;

namespace TestProject {
    public class Program {
        public static void Main(string[] args) {
            var builder = WebApplication.CreateBuilder(args);

            var defaultRoot = Path.Combine(AppContext.BaseDirectory, "DefaultDirectory");
            if (!Directory.Exists(defaultRoot)) {
                Directory.CreateDirectory(defaultRoot);
                File.WriteAllText(Path.Combine(defaultRoot, "readme.txt"), "Drop files here.");
            }

            builder.Services.AddControllers();
            builder.Services.AddHttpsRedirection(options => options.HttpsPort = 5001);
            builder.Services.Configure<FileExplorerOptions>(builder.Configuration.GetSection("FileExplorer"));
            builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = long.MaxValue);
            builder.Services.AddSingleton<PathResolver>();
            builder.Services.AddSingleton<FileService>();
            builder.Services.AddSingleton<PreviewService>();

            builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = long.MaxValue);

            var app = builder.Build();

            app.UseHttpsRedirection();
            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.MapControllers();
            app.MapFallbackToFile("/index.html");

            app.Run();
        }
    }
}
