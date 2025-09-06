using System.IO;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using TestProject.Services;

namespace TestProject {
    public class Program {
        public static void Main(string[] args) {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddHttpsRedirection(options => options.HttpsPort = 5001);
            builder.Services.Configure<FileExplorerOptions>(builder.Configuration.GetSection("FileExplorer"));
            builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = long.MaxValue);
            builder.Services.AddSingleton<PathResolver>();

            builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = long.MaxValue);

            var app = builder.Build();

            var options = app.Services.GetRequiredService<IOptions<FileExplorerOptions>>().Value;
            var rootPath = Path.GetFullPath(options.RootPath);
            if (!Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);

            app.UseHttpsRedirection();
            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.MapControllers();
            app.MapFallbackToFile("/index.html");

            app.Run();
        }
    }
}
