namespace FishPaper;

using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using StbImageSharp;

public enum ImageFitMode
{
    Fit,     // Letterbox: whole image visible, borders where aspect differs
    Fill,    // Crop: image covers the window, edges cut off
    Stretch  // Distort to fill the window
}

public sealed class UWindow : IDisposable
{
    private const string VertexShaderSource = @"#version 330 core
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aUv;

uniform vec2 uScale;   // Scales the quad (used for Fit letterboxing)
uniform vec4 uUvRect;  // Source UV range (used for Fill cropping)

out vec2 vUv;

void main()
{
    gl_Position = vec4(aPos * uScale, 0.0, 1.0);
    vUv = mix(uUvRect.xy, uUvRect.zw, aUv);
}";

    private const string FragmentShaderSource = @"#version 330 core
in vec2 vUv;
uniform sampler2D uTex;
out vec4 FragColor;

void main()
{
    FragColor = texture(uTex, vUv);
}";

    private IWindow _window;
    private WindowOptions _options;
    private bool _isMainWindow;

    private IInputContext? _input;
    private ImGuiController? _imgui;
    private GL? _gl;

    private uint _vao, _vbo, _ebo, _program, _texture;
    private int _scaleLoc, _uvRectLoc, _texLoc;

    private string? _imagePath;
    private int _imageWidth, _imageHeight;
    private bool _hasTexture;
    
    private string? _pendingPath;
    private int _targetIndex;
    private string[] _targetNames = Array.Empty<string>();

    private string _pathInput = "";
    private string? _error;

    public ImageFitMode FitMode { get; set; } = ImageFitMode.Fill;
    
    public Action<int, string>? OnSetImage;

    public void SetTargets(string[] names) => _targetNames = names;

    public UWindow(bool isMainWindow = false)
    {
        _isMainWindow = isMainWindow;
    }
    
    public void SetOptions(WindowOptions options)
    {
        _options = options;
        _window = Window.Create(_options);
        
        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.FramebufferResize += size => _gl?.Viewport(size);
        _window.Closing += OnClosing;
    }

    /// <summary>Sets the wallpaper image. Safe to call before or after the window loads.</summary>
    public void SetImage(string path)
    {
        _imagePath = path;
        _pathInput = path;
        if (_gl is not null)
            LoadTexture(path);
    }

    public void Run() => _window.Run();

    // ---------- Lifecycle ----------

    private void OnLoad()
    {
        _gl = GL.GetApi(_window);
        _input = _window.CreateInput();
        if (_isMainWindow)
            _imgui = new ImGuiController(_gl, _window, _input);

        CreateQuad();
        CreateProgram();

        if (_imagePath is not null)
            LoadTexture(_imagePath);
    }

    private void OnUpdate(double delta)
    {
        _imgui?.Update((float)delta);
    }

    private void OnRender(double delta)
    {
        if (_gl is null) return;

        _gl.ClearColor(0.05f, 0.05f, 0.07f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        if (_hasTexture)
            DrawWallpaper();
        
        if (_isMainWindow)
            DrawControls();

        _imgui?.Render();
    }

    private void OnClosing()
    {
        Dispose();
    }

    // ---------- Geometry and shaders ----------
    private unsafe void CreateQuad()
    {
        // Positions (x, y) and UVs (u, v). UV (0,0) is top-left so the image is not flipped.
        float[] vertices =
        {
            -1f, 1f, 0f, 0f, // top-left
            1f, 1f, 1f, 0f, // top-right
            1f, -1f, 1f, 1f, // bottom-right
            -1f, -1f, 0f, 1f, // bottom-left
        };
        uint[] indices = { 0, 1, 2, 2, 3, 0 };

        _vao = _gl!.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        _gl.BufferData<float>(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.StaticDraw);

        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        _gl.BufferData<uint>(BufferTargetARB.ElementArrayBuffer, indices, BufferUsageARB.StaticDraw);

        const uint stride = 4 * sizeof(float);
        

            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);
        

        _gl.BindVertexArray(0);
    }

    private void CreateProgram()
    {
        uint vs = CompileShader(ShaderType.VertexShader, VertexShaderSource);
        uint fs = CompileShader(ShaderType.FragmentShader, FragmentShaderSource);

        _program = _gl!.CreateProgram();
        _gl.AttachShader(_program, vs);
        _gl.AttachShader(_program, fs);
        _gl.LinkProgram(_program);
        _gl.GetProgram(_program, GLEnum.LinkStatus, out int linked);
        if (linked == 0)
            throw new InvalidOperationException($"Program link failed: {_gl.GetProgramInfoLog(_program)}");

        _gl.DetachShader(_program, vs);
        _gl.DetachShader(_program, fs);
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);

        _scaleLoc = _gl.GetUniformLocation(_program, "uScale");
        _uvRectLoc = _gl.GetUniformLocation(_program, "uUvRect");
        _texLoc = _gl.GetUniformLocation(_program, "uTex");
    }

    private uint CompileShader(ShaderType type, string source)
    {
        uint shader = _gl!.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0)
            throw new InvalidOperationException($"{type} compile failed: {_gl.GetShaderInfoLog(shader)}");
        return shader;
    }

    // ---------- Texture ----------

    // ---------- Texture ----------

    private void LoadTexture(string path)
    {
        using var stream = File.OpenRead(path);
        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

        if (_hasTexture)
            _gl!.DeleteTexture(_texture);

        _texture = _gl!.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _texture);
        _gl.TexImage2D<byte>(
            TextureTarget.Texture2D, 0, InternalFormat.Rgba,
            (uint)image.Width, (uint)image.Height, 0,
            PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);

        _gl.TexParameter(TextureTarget.Texture2D,
            TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D,
            TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D,
            TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D,
            TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

        _imageWidth = image.Width;
        _imageHeight = image.Height;
        _hasTexture = true;
        _error = null;
    }

    // ---------- Drawing ----------


    private unsafe void DrawWallpaper()
    {
        var size = _window.FramebufferSize;
        float winAspect = (float)size.X / Math.Max(1, size.Y);
        float imgAspect = (float)_imageWidth / _imageHeight;

        Vector2 scale = Vector2.One;
        Vector4 uvRect = new(0f, 0f, 1f, 1f);

        switch (FitMode)
        {
            case ImageFitMode.Fit:
                // Shrink the quad on one axis so the whole image fits with borders.
                scale = imgAspect > winAspect
                    ? new Vector2(1f, winAspect / imgAspect)
                    : new Vector2(imgAspect / winAspect, 1f);
                break;

            case ImageFitMode.Fill:
                // Crop the texture's UV range so it covers the window.
                if (imgAspect > winAspect)
                {
                    float f = winAspect / imgAspect;
                    uvRect = new Vector4((1f - f) / 2f, 0f, (1f + f) / 2f, 1f);
                }
                else
                {
                    float f = imgAspect / winAspect;
                    uvRect = new Vector4(0f, (1f - f) / 2f, 1f, (1f + f) / 2f);
                }
                break;

            case ImageFitMode.Stretch:
                break; // Defaults: full quad, full UVs
        }

        _gl!.UseProgram(_program);
        _gl.Uniform2(_scaleLoc, scale);
        _gl.Uniform4(_uvRectLoc, uvRect);
        _gl.Uniform1(_texLoc, 0);

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _texture);

        _gl.BindVertexArray(_vao);
        _gl.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, null);
        _gl.BindVertexArray(0);
    }

    private void DrawControls()
    {
        ImGui.SetNextWindowSize(new Vector2(420, 0), ImGuiCond.FirstUseEver);
        ImGui.Begin("FishPaper");

        ImGui.Text("Wallpaper settings");
        ImGui.Separator();

        ImGui.InputText("Image path", ref _pathInput, 1024);
        ImGui.SameLine();
        if (ImGui.Button("Load"))
        {
            try
            {
                SetImage(_pathInput);
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
        }

        if (_error is not null)
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), _error);

        ImGui.Spacing();
        ImGui.Text("Display mode");

        if (ImGui.RadioButton("Fit", FitMode == ImageFitMode.Fit))
            FitMode = ImageFitMode.Fit;
        ImGui.SameLine();
        if (ImGui.RadioButton("Fill", FitMode == ImageFitMode.Fill))
            FitMode = ImageFitMode.Fill;
        ImGui.SameLine();
        if (ImGui.RadioButton("Stretch", FitMode == ImageFitMode.Stretch))
            FitMode = ImageFitMode.Stretch;

        if (_hasTexture)
            ImGui.Text($"Image: {_imageWidth} x {_imageHeight}");

        ImGui.End();
    }

    // ---------- Cleanup ----------

    public void Dispose()
    {
        if (_gl is null) return;

        if (_hasTexture) _gl.DeleteTexture(_texture);
        if (_program != 0) _gl.DeleteProgram(_program);
        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
        if (_ebo != 0) _gl.DeleteBuffer(_ebo);
        if (_vao != 0) _gl.DeleteVertexArray(_vao);

        _imgui?.Dispose();
        _input?.Dispose();
        _gl.Dispose();
        _gl = null;
    }
    
    public bool IsClosing => _window?.IsClosing ?? true;

    public void Initialize()
    {
        _window!.Initialize();   // Fires Load; creates the GL context
    }

    public void Tick()
    {
        if (_window is null || _window.IsClosing) return;

        _window.MakeCurrent();   // Each window has its own GL context
        _window.DoEvents();
        _window.DoUpdate();
        _window.DoRender();
    }

}
