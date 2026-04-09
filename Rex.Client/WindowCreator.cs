using Rex.Client.Graphics;
using Rex.Shared.Analyzers;
using Silk.NET.Windowing;
using Silk.NET.OpenGL;
using Silk.NET.Input;
using Silk.NET.Maths;
using System.Drawing;

namespace Rex.Client;

public sealed class WindowCreator : IGameWindow
{
    private IWindow? WindowHandle { get; set; }
    private GL _gl;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;
    private uint _program;
    public string Title { get; set; } = string.Empty;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public bool IsOpen { get; private set; }

    // TODO: Use these. They mirror what Silk.NET provides for Windowing.
    // TODO: Remove all of these disable stuff once we are actually using them.
#pragma warning disable CS0067 // Event is never used
    // ReSharper disable EventNeverSubscribedTo.Local
    // ReSharper disable EventNeverSubscribedTo.Global
    public event Action<Vector2D<int>>? OnResize;
    public event Action<Vector2D<int>>? OnFramebufferResize;
    public event Action? OnClosing;
    public event Action<bool>? OnFocusChanged;
    public event Action? OnLoad;
    public event Action<double>? OnUpdate;
    public event Action<double>? OnRender;
    // ReSharper restore EventNeverSubscribedTo.Global
    // ReSharper restore EventNeverSubscribedTo.Local
#pragma warning restore CS0067 // Event is never used

    public WindowCreator(string title, int width, int height)
    {

        Title = title;
        Width = width;
        Height = height;

        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(Width, Height),
            // ReSharper disable once ArrangeThisQualifier
            Title = this.Title
        };

        var window = Window.Create(options);
        WindowHandle = window;
        WindowHandle.Load += HandleLoad;
        WindowHandle.Update += HandleUpdate;
        WindowHandle.Render += HandleRender;
    }

    public void Dispose()
    {
        WindowHandle?.Dispose();
    }
    public void Open()
    {
        if (IsOpen)
        {
            return;
        }
        WindowHandle?.Run();
        IsOpen = true;
    }
    public void Close()
    {
        WindowHandle?.Close();
        IsOpen = false;
    }
    public void PollEvents()
    {
        throw new NotImplementedException();
    }

    public void SwapBuffers()
    {
        throw new NotImplementedException();
    }

    private unsafe void HandleLoad()
    {
        var window = WindowHandle ?? throw new InvalidOperationException("...");
        _gl = window.CreateOpenGL();

        // --- Input ---
        var input = window.CreateInput();
        foreach (var kb in input.Keyboards)
            kb.KeyDown += KeyDown;

        // --- Shaders (compile early, use later) ---
        const string vertexCode = @"
        #version 330 core
        layout (location = 0) in vec3 aPosition;
        void main() { gl_Position = vec4(aPosition, 1.0); }";

        const string fragmentCode = @"
        #version 330 core
        out vec4 out_color;
        void main() { out_color = vec4(1.0, 0.5, 0.2, 1.0); }";

        uint vertexShader = _gl.CreateShader(ShaderType.VertexShader);
        _gl.ShaderSource(vertexShader, vertexCode);
        _gl.CompileShader(vertexShader);
        _gl.GetShader(vertexShader, ShaderParameterName.CompileStatus, out int vStatus);
        if (vStatus != (int)GLEnum.True)
            throw new Exception("Vertex shader failed: " + _gl.GetShaderInfoLog(vertexShader));

        uint fragmentShader = _gl.CreateShader(ShaderType.FragmentShader);
        _gl.ShaderSource(fragmentShader, fragmentCode);
        _gl.CompileShader(fragmentShader);
        _gl.GetShader(fragmentShader, ShaderParameterName.CompileStatus, out int fStatus);
        if (fStatus != (int)GLEnum.True)
            throw new Exception("Fragment shader failed: " + _gl.GetShaderInfoLog(fragmentShader));

        // --- Geometry data ---
        float[] vertices = {
         0.5f,  0.5f, 0.0f,
         0.5f, -0.5f, 0.0f,
        -0.5f, -0.5f, 0.0f,
        -0.5f,  0.5f, 0.0f
    };
        uint[] indices = { 0u, 1u, 3u, 1u, 2u, 3u };

        // --- Create and bind VAO first ---
        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        // --- VBO: upload vertex data ---
        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* buf = vertices)
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), buf, BufferUsageARB.StaticDraw);

        // --- EBO: upload index data ---
        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* buf = indices)
            _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), buf, BufferUsageARB.StaticDraw);

        // --- Attrib pointers (VAO + VBO both bound) ---
        const int positionLoc = 0;
        _gl.EnableVertexAttribArray(positionLoc);
        _gl.VertexAttribPointer(positionLoc, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), (void*)0);

        // --- Unbind VAO (EBO stays recorded inside it) ---
        _gl.BindVertexArray(0);

        // --- Link program ---
        _program = _gl.CreateProgram();
        _gl.AttachShader(_program, vertexShader);
        _gl.AttachShader(_program, fragmentShader);
        _gl.LinkProgram(_program);
        _gl.GetProgram(_program, ProgramPropertyARB.LinkStatus, out int lStatus);
        if (lStatus != (int)GLEnum.True)
            throw new Exception("Program failed to link: " + _gl.GetProgramInfoLog(_program));

        _gl.DetachShader(_program, vertexShader);
        _gl.DetachShader(_program, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        _gl.ClearColor(Color.CornflowerBlue);

    }

    private void HandleUpdate(double deltaTime)
    {

    }

    private unsafe void HandleRender(double deltaTime)
    {
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        _gl.BindVertexArray(_vao);
        _gl.UseProgram(_program);
        _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (void*)0);
    }

    private void KeyDown(IKeyboard keyboard, Key key, int keyCode)
    {
        if (key == Key.Escape)
        {
            Close();
        }
    }
}
