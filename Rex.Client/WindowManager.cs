using System.Runtime.InteropServices.Marshalling;
using Silk.NET.SDL;

namespace Rex.Client;

internal sealed unsafe class WindowManager : IDisposable {
    
    private static readonly Sdl _sdl = Sdl.GetApi();
    private static Renderer* _renderer;
    private static Window* _window;

    private readonly int _screenWidth;
    private readonly int _screenHeight;

    public WindowManager( int screenWidth, int screenHeight) {
        _screenWidth = screenWidth;
        _screenHeight = screenHeight;
    }
    public void InitSDL() {
        int rendererFlags = (int)RendererFlags.Accelerated;
        int windowFlags = 0;
         
        if (_sdl.Init(Sdl.InitVideo) < 0) {
            Kill($"Couldn't initialize SDL: {_sdl.GetErrorS()}");
            
        }
        _window = CreateWindow(
            "Hello, World",
            Sdl.WindowposUndefined,
            Sdl.WindowposUndefined,
            _screenWidth,
            _screenHeight,
            (uint)windowFlags);
        if (_window == null) {
            Kill($"Failed to open {_screenWidth} x {_screenHeight} window: {_sdl.GetErrorS()}");
        }

        _sdl.SetHint(Sdl.HintRenderScaleQuality, "linear");
        _renderer = _sdl.CreateRenderer(_window, -1, (uint)rendererFlags);

        if (_renderer == null) {
            Kill($"Failed to create renderer: {_sdl.GetErrorS()}");
        }
    }

    static void Draw() {
        _sdl.SetRenderDrawColor(_renderer, 30, 30, 30, 255);
        _sdl.RenderClear(_renderer);
        // ---- filled rectangle (red) begin ----
        _sdl.SetRenderDrawColor(_renderer, 255, 0, 0, 255);
        FRect redBox = new FRect { X = 50, Y = 50, H = 300, W = 400 };
        _sdl.RenderFillRectF(_renderer, in redBox);
        // ---- filled rectangle (red) end ----
        _sdl.RenderPresent(_renderer);
    }

    static void Kill(string msg) {
        Console.WriteLine(msg);
        _sdl.Quit();
    }

    public void GameLoop() {
        bool running = true;

        Event e;
        
        while (running) {
            while (_sdl.PollEvent(&e) != 0) {
                if (e.Type == (uint)EventType.Quit) {
                    running = false;
                }
            }
            Draw();
            
        }
        
    }

    Window* CreateWindow(string title, int x, int y, int w, int h, uint flags) {
        return _sdl.CreateWindow(
            title,
            x,
            y,
            w,
            h,
            flags);
    }
    public void Dispose() {
        _sdl.DestroyRenderer(_renderer);
        _sdl.DestroyWindow(_window);
        _sdl.Quit();
    }
}