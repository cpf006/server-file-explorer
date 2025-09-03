namespace TestProject {
    public class Program {
        public static void Main(string[] args) {
            var builder = WebApplication.CreateBuilder(args);

            // Set up MVC and load file explorer settings (like the root folder)
            builder.Services.AddControllers();
            builder.Services.Configure<FileExplorerOptions>(builder.Configuration.GetSection("FileExplorer"));

            var app = builder.Build();

            // Serve controllers and the SPA from wwwroot
            app.UseHttpsRedirection();
            app.UseDefaultFiles(); // look for index.html automatically
            app.UseStaticFiles();

            app.MapControllers();
            app.MapFallbackToFile("/index.html");

            app.Run();
        }
    }
}