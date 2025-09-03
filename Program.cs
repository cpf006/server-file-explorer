using System.IO;

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
            builder.Services.Configure<FileExplorerOptions>(builder.Configuration.GetSection("FileExplorer"));

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