using System.Drawing;
using Silk.NET;
using Silk.NET.Core.Contexts;
using Silk.NET.SDL;
namespace Rex.Client;

internal static class Program {
    
    private static void Main(string[] args) {
       Windows.InitSDL();
       Windows.GameLoop();
       
       
    }
    
}

unsafe class App {
    internal Renderer* renderer;
    internal Window* window;
    
}

internal unsafe class Windows {
    
    private static readonly Sdl _sdl = Sdl.GetApi();
    private static App app = new App();

    private const int SCREEN_WIDTH = 800;
    private const int SCREEN_HEIGHT = 600;

    public static void InitSDL() {
        int rendererFlags = (int)RendererFlags.Accelerated;
        int windowFlags = 0;

        if (_sdl.Init(Sdl.InitVideo) < 0) {
            Kill($"Couldn't initialize SDL: {_sdl.GetErrorS()}");
            
        }
        app.window = _sdl.CreateWindow(
            "Hello, World",
            Sdl.WindowposUndefined,
            Sdl.WindowposUndefined,
            SCREEN_WIDTH,
            SCREEN_HEIGHT,
            (uint)windowFlags);
        if (app.window == null) {
            Kill($"Failed to open {SCREEN_WIDTH} x {SCREEN_HEIGHT} window: {_sdl.GetErrorS()}");
        }

        _sdl.SetHint(Sdl.HintRenderScaleQuality, "linear");
        app.renderer = _sdl.CreateRenderer(app.window, -1, (uint)rendererFlags);

        if (app.renderer == null) {
            Kill($"Failed to create renderer: {_sdl.GetErrorS()}");
        }
    }

    static  void Draw() {
        // ---- filled rectangle (red) ----
        _sdl.SetRenderDrawColor(app.renderer, 255, 0, 0, 255);
        FRect redBox = new FRect { X = 50, Y = 50, H = 300, W = 400 };
        _sdl.RenderFillRectF(app.renderer, in redBox);
    }

    static void Kill(string msg) {
        Console.WriteLine(msg);
        Environment.Exit(1);
    }

    public static void GameLoop() {
        bool running = true;

        Event e;
        
        while (running) {
            while (_sdl.PollEvent(&e) != 0) {
                if (e.Type == (uint)EventType.Quit) {
                    running = false;
                }
            }
            _sdl.SetRenderDrawColor(app.renderer, 30, 30, 30, 255);
            _sdl.RenderClear(app.renderer);
            
            Draw();
            _sdl.RenderPresent(app.renderer);
        }
        _sdl.DestroyRenderer(app.renderer);
        _sdl.DestroyWindow(app.window);
        _sdl.Quit();
    }
}