using Silk.NET;
using Silk.NET.Core.Contexts;
using Silk.NET.SDL;

namespace Rex.Client;

internal static unsafe class Program {
    private static readonly Sdl _sdl = Sdl.GetApi();
    private static App app = new App();

    private const int SCREEN_WIDTH = 800;
    private const int SCREEN_HEIGHT = 600;

    static void InitSDL() {
        int rendererFlags = (int)RendererFlags.Accelerated;
        int windowFlags = 0;

        if (_sdl.Init(Sdl.InitVideo) < 0) {
            Console.WriteLine($"Couldn't initialize SDL: {_sdl.GetErrorS()}");
            Environment.Exit(1);
        }
        app.window = _sdl.CreateWindow(
            "Hello, World",
            Sdl.WindowposUndefined,
            Sdl.WindowposUndefined,
            SCREEN_WIDTH,
            SCREEN_HEIGHT,
            (uint)windowFlags);
        if (app.window == null) {
            Console.WriteLine($"Failed to open {SCREEN_WIDTH} x {SCREEN_HEIGHT} window: {_sdl.GetErrorS()}");
        }

        _sdl.SetHint(Sdl.HintRenderScaleQuality, "linear");
        app.renderer = _sdl.CreateRenderer(app.window, -1, (uint)rendererFlags);

        if (app.renderer == null) {
            Console.WriteLine($"Failed to create renderer: {_sdl.GetErrorS()}");
            Environment.Exit(1);
        }
    }

    static void GameLoop() {
        //TODO Later lol
    }
    private static void Main(string[] args) {
       InitSDL();
       while (Console.ReadLine() != "1") {
           
       }
       
       _sdl.DestroyRenderer(app.renderer);
       _sdl.DestroyWindow(app.window);
       _sdl.Quit();
    }
    
}

unsafe class App {
    internal Renderer* renderer;
    internal Window* window;
    
}