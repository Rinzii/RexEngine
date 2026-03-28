using Rex.Client.Graphics;
using Rex.Shared.Analyzers;
using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace Rex.Client;

public sealed class WindowCreator : IGameWindow {
    private IWindow WINDOW { get; set; }
    public string Title { get; set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool IsOpen { get; private set; }

   
    public void Dispose() {
        WINDOW.Dispose();
    }
    public void Open(string title, int width, int height) {
        Title = title;
        Width = width;
        Height = height;
        
         WindowOptions options = WindowOptions.Default with {
            Size = new Vector2D<int>(Width, Height),
            // ReSharper disable once ArrangeThisQualifier
            Title = this.Title
        };
         
        IWindow window = Window.Create(options);
        WINDOW = window;
        WINDOW.Load += OnLoad;
        WINDOW.Update += OnUpdate;
        WINDOW.Render += OnRender; 
        window.Run();
        IsOpen = true;
    }
    public void Close() {
        
        WINDOW.Close();
        IsOpen = false;
    }

    public void PollEvents() {
        throw new NotImplementedException();
    }

    public void SwapBuffers() {
        throw new NotImplementedException();
    }

    private void OnLoad() {
        IInputContext input = WINDOW.CreateInput();
        for (int i = 0; i < input.Keyboards.Count; i++) {
            input.Keyboards[i].KeyDown += KeyDown;
        }
    }

    private void OnUpdate(double deltaTime) {
        
    }

    private void OnRender(double deltaTime) {
        
    }

    private void KeyDown(IKeyboard keyboard, Key key, int keyCode) {
        if (key == Key.Escape) {
            Close();
        }
    }
}



