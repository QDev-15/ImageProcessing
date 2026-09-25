using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ImageOptimizerTool;

/// <summary>
/// Image viewer with zoom (mouse wheel anchored at the cursor, or ZoomIn / ZoomOut) and
/// pan (drag with the left / middle mouse button). Starts in "fit to window" mode, which
/// is kept across resizes until the user zooms. Only the visible part of the image is
/// drawn, so panning a very large scan stays responsive. The control does not own
/// <see cref="Image"/>: the caller disposes it.
/// </summary>
public sealed class ZoomPanView : Control
{
    private const float MinZoom = 0.01f;
    private const float MaxZoom = 32f;
    private const float WheelStep = 1.25f;

    private Image? _image;
    private float _zoom = 1f;
    private bool _fit = true;
    private PointF _offset; // top-left of the image in client coordinates
    private bool _dragging;
    private Point _dragLast;
    private Control? _prevFocus;

    /// <summary>Raised when the zoom factor changes (including fit-to-window recalculation).</summary>
    public event EventHandler? ZoomChanged;

    public ZoomPanView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                 | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque | ControlStyles.Selectable, true);
        DoubleBuffered = true;
        BackColor = Color.DimGray;
        TabStop = false;
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? Image
    {
        get => _image;
        set
        {
            _image = value;
            FitToWindow();
        }
    }

    /// <summary>Current zoom factor (1 = 100%, one image pixel per screen pixel).</summary>
    [Browsable(false)]
    public float Zoom => _zoom;

    /// <summary>
    /// Swaps in a different rendering of the SAME page (e.g. the full-resolution image replacing
    /// its low-resolution proxy) without disturbing the view: the same point of the page stays
    /// under the view centre at the same on-screen scale. In fit mode it simply stays fitted.
    /// </summary>
    public void ReplaceImage(Image? next)
    {
        Image? prev = _image;
        if (prev == null || next == null || _fit)
        {
            Image = next;
            return;
        }

        // Keep the page point under the centre, and the on-screen size of the page.
        float cx = ClientSize.Width / 2f, cy = ClientSize.Height / 2f;
        float rx = (cx - _offset.X) / (prev.Width * _zoom), ry = (cy - _offset.Y) / (prev.Height * _zoom);
        float pageScale = prev.Width * _zoom / next.Width;
        _image = next;
        _zoom = Math.Clamp(pageScale, MinZoom, MaxZoom);
        _offset = new PointF(cx - rx * next.Width * _zoom, cy - ry * next.Height * _zoom);
        ClampOffset();
        UpdateCursor();
        Invalidate();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    public void FitToWindow()
    {
        _fit = true;
        ApplyFit();
        Invalidate();
    }

    public void ActualSize() => SetZoom(1f, new PointF(ClientSize.Width / 2f, ClientSize.Height / 2f));

    public void ZoomIn() => SetZoom(_zoom * WheelStep, ViewCenter());

    public void ZoomOut() => SetZoom(_zoom / WheelStep, ViewCenter());

    private PointF ViewCenter() => new(ClientSize.Width / 2f, ClientSize.Height / 2f);

    private void ApplyFit()
    {
        if (_image == null || ClientSize.Width < 1 || ClientSize.Height < 1)
        {
            _zoom = 1f;
            _offset = PointF.Empty;
        }
        else
        {
            _zoom = Math.Clamp(Math.Min((float)ClientSize.Width / _image.Width, (float)ClientSize.Height / _image.Height), MinZoom, MaxZoom);
            _offset = PointF.Empty;
            ClampOffset();
        }
        UpdateCursor();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Change zoom keeping the image point under <paramref name="anchor"/> fixed.</summary>
    private void SetZoom(float zoom, PointF anchor)
    {
        if (_image == null) return;
        zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        if (Math.Abs(zoom - _zoom) < 1e-6f && !_fit) return;

        float ix = (anchor.X - _offset.X) / _zoom, iy = (anchor.Y - _offset.Y) / _zoom;
        _fit = false;
        _zoom = zoom;
        _offset = new PointF(anchor.X - ix * zoom, anchor.Y - iy * zoom);
        ClampOffset();
        UpdateCursor();
        Invalidate();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Center the image on an axis where it is smaller than the view; otherwise
    /// keep it from being dragged past the edges.</summary>
    private void ClampOffset()
    {
        if (_image == null) return;
        float w = _image.Width * _zoom, h = _image.Height * _zoom;
        float x = w <= ClientSize.Width ? (ClientSize.Width - w) / 2f : Math.Clamp(_offset.X, ClientSize.Width - w, 0f);
        float y = h <= ClientSize.Height ? (ClientSize.Height - h) / 2f : Math.Clamp(_offset.Y, ClientSize.Height - h, 0f);
        _offset = new PointF(x, y);
    }

    private bool CanPan => _image != null
        && (_image.Width * _zoom > ClientSize.Width + 0.5f || _image.Height * _zoom > ClientSize.Height + 0.5f);

    private void UpdateCursor() => Cursor = _dragging ? Cursors.SizeAll : CanPan ? Cursors.Hand : Cursors.Default;

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(BackColor);
        if (_image == null) return;

        float w = _image.Width * _zoom, h = _image.Height * _zoom;
        var dest = new RectangleF(_offset.X, _offset.Y, w, h);
        var visible = RectangleF.Intersect(dest, new RectangleF(0, 0, ClientSize.Width, ClientSize.Height));
        if (visible.IsEmpty) return;

        // Source rect of the visible part, snapped outward to whole pixels so magnified
        // pixels keep crisp, aligned edges; the destination is recomputed from it.
        float sx0 = MathF.Floor((visible.Left - _offset.X) / _zoom), sy0 = MathF.Floor((visible.Top - _offset.Y) / _zoom);
        float sx1 = MathF.Ceiling((visible.Right - _offset.X) / _zoom), sy1 = MathF.Ceiling((visible.Bottom - _offset.Y) / _zoom);
        sx0 = Math.Max(sx0, 0); sy0 = Math.Max(sy0, 0);
        sx1 = Math.Min(sx1, _image.Width); sy1 = Math.Min(sy1, _image.Height);
        var src = new RectangleF(sx0, sy0, sx1 - sx0, sy1 - sy0);
        var dst = new RectangleF(_offset.X + sx0 * _zoom, _offset.Y + sy0 * _zoom, src.Width * _zoom, src.Height * _zoom);

        g.InterpolationMode = _zoom >= 2f ? InterpolationMode.NearestNeighbor : InterpolationMode.Bilinear;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(_image, dst, src, GraphicsUnit.Pixel);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_fit) ApplyFit();
        else { ClampOffset(); UpdateCursor(); }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_image == null || e.Delta == 0) return;
        SetZoom(_zoom * MathF.Pow(WheelStep, e.Delta / 120f), e.Location);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button is MouseButtons.Left or MouseButtons.Middle && CanPan)
        {
            _dragging = true;
            _dragLast = e.Location;
            Capture = true;
            UpdateCursor();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging) return;
        _offset = new PointF(_offset.X + e.X - _dragLast.X, _offset.Y + e.Y - _dragLast.Y);
        _dragLast = e.Location;
        ClampOffset();
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging) return;
        _dragging = false;
        Capture = false;
        UpdateCursor();
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left || _image == null) return;
        if (_fit) SetZoom(1f, e.Location); else FitToWindow();
    }

    // The wheel goes to the focused control, so take focus while the mouse is over the
    // view and hand it back afterwards (keeps Delete / arrow keys working in the page list).
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Control? active = FindForm()?.ActiveControl;
        if (active != null && active != this && Form.ActiveForm == FindForm())
        {
            _prevFocus = active;
            Focus();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_dragging) return;
        if (Focused && _prevFocus is { IsDisposed: false }) _prevFocus.Focus();
        _prevFocus = null;
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        const float pan = 60f;
        switch (e.KeyCode)
        {
            case Keys.Add or Keys.Oemplus: ZoomIn(); break;
            case Keys.Subtract or Keys.OemMinus: ZoomOut(); break;
            case Keys.D0 or Keys.NumPad0: FitToWindow(); break;
            case Keys.D1 or Keys.NumPad1: ActualSize(); break;
            case Keys.Left: PanBy(pan, 0); break;
            case Keys.Right: PanBy(-pan, 0); break;
            case Keys.Up: PanBy(0, pan); break;
            case Keys.Down: PanBy(0, -pan); break;
            default: return;
        }
        e.Handled = true;
    }

    private void PanBy(float dx, float dy)
    {
        _offset = new PointF(_offset.X + dx, _offset.Y + dy);
        ClampOffset();
        Invalidate();
    }
}
