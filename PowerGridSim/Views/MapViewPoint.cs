using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PowerGridSim.Models;
using System;
using System.Globalization;

namespace PowerGridSim.Views;

public class MapViewpoint : Control //ADD MOVEMENT ?+ CHANGE WORLD RATIO?
{
    // 16:9 world. 400 px = 1 mile
    public const double WorldW = 9600, WorldH = 5400;
    public const double PxPerMile = 400;
    static readonly double[] RingMiles = { 1, 2, 5, 10 };
    const double MaxZoom = 6.0;

    static readonly IBrush WorldBrush = new SolidColorBrush(Color.Parse("#282A36"));
    static readonly IPen GridPenBase = new Pen(new SolidColorBrush(Color.Parse("#323644")), 1);

    double _zoom = 0.25;
    Vector _pan;
    bool _panning, _initialised;
    Point _panStartMouse;
    Vector _panStart;

    double MinZoom => Math.Max(0.01, Math.Min(Bounds.Width / WorldW, Bounds.Height / WorldH));

    public static readonly StyledProperty<PowerGridBlueprint?> BlueprintProperty =
    AvaloniaProperty.Register<MapViewpoint, PowerGridBlueprint?>(nameof(Blueprint));

    public static readonly StyledProperty<MapNode?> SelectedNodeProperty =
    AvaloniaProperty.Register<MapViewpoint, MapNode?>(nameof(SelectedNode),
        defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public MapNode? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public PowerGridBlueprint? Blueprint
    {
        get => GetValue(BlueprintProperty);
        set => SetValue(BlueprintProperty, value);
    }

    static MapViewpoint()
    {
        AffectsRender<MapViewpoint>(BlueprintProperty, SelectedNodeProperty);
        ClipToBoundsProperty.OverrideDefaultValue<MapViewpoint>(true);
    }

    public Point ScreenToWorld(Point s) => new((s.X - _pan.X) / _zoom, (s.Y - _pan.Y) / _zoom);
    public Point WorldToScreen(Point w) => new(w.X * _zoom + _pan.X, w.Y * _zoom + _pan.Y);

    //  input 
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsMiddleButtonPressed)
        {
            _panning = true;
            _panStartMouse = e.GetPosition(this);
            _panStart = _pan;
            e.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Handled = true;
        }
        else if (props.IsLeftButtonPressed)
        {
            SelectedNode = HitTest(ScreenToWorld(e.GetPosition(this)));
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_panning) return;
        _pan = _panStart + (e.GetPosition(this) - _panStartMouse);
        ClampPan();
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_panning) return;
        _panning = false;
        e.Pointer.Capture(null);
        Cursor = Cursor.Default;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var mouse = e.GetPosition(this);
        double newZoom = Math.Clamp(_zoom * Math.Pow(1.1, e.Delta.Y), MinZoom, MaxZoom);
        double k = newZoom / _zoom;
        _pan = new Vector(mouse.X - (mouse.X - _pan.X) * k, mouse.Y - (mouse.Y - _pan.Y) * k);
        _zoom = newZoom;
        ClampPan();
        InvalidateVisual();
        e.Handled = true;
    }

    MapNode? HitTest(Point world)
    {
        if (Blueprint == null) return null;
        double reach = Math.Max(15, 10 / _zoom);
        MapNode? best = null;
        double bestSq = reach * reach;
        foreach (var n in Blueprint.Nodes)
        {
            double dx = n.X - world.X, dy = n.Y - world.Y;
            double sq = dx * dx + dy * dy;
            if (sq <= bestSq) { bestSq = sq; best = n; }
        }
        return best;
    }

    void ClampPan()
    {
        double sw = WorldW * _zoom, sh = WorldH * _zoom;
        double x = sw <= Bounds.Width ? (Bounds.Width - sw) / 2 : Math.Clamp(_pan.X, Bounds.Width - sw, 0);
        double y = sh <= Bounds.Height ? (Bounds.Height - sh) / 2 : Math.Clamp(_pan.Y, Bounds.Height - sh, 0);
        _pan = new Vector(x, y);
    }

    void Initialise()
    {
        _zoom = Math.Clamp(0.25, MinZoom, MaxZoom);
        _pan = new Vector(Bounds.Width / 2 - WorldW / 2 * _zoom, Bounds.Height / 2 - WorldH / 2 * _zoom);
        _initialised = true;
    }

    public override void Render(DrawingContext ctx)
    {
        if (Bounds.Width < 1 || Bounds.Height < 1) return;

        ctx.DrawRectangle(WorldBrush, null, new Rect(Bounds.Size));

        if (!_initialised) Initialise();
        _zoom = Math.Max(_zoom, MinZoom);
        ClampPan();

        double cx = WorldW / 2, cy = WorldH / 2;

        using (ctx.PushTransform(Matrix.CreateScale(_zoom, _zoom) * Matrix.CreateTranslation(_pan.X, _pan.Y)))
        using (ctx.PushClip(new Rect(0, 0, WorldW, WorldH)))
        {
            var grid = new Pen(new SolidColorBrush(Color.Parse("#323644")), 1 / _zoom);
            for (double x = 0; x <= WorldW; x += 200) ctx.DrawLine(grid, new Point(x, 0), new Point(x, WorldH));
            for (double y = 0; y <= WorldH; y += 200) ctx.DrawLine(grid, new Point(0, y), new Point(WorldW, y));

            var ringPen = new Pen(new SolidColorBrush(Color.Parse("#6B7089")), 1.5 / _zoom,
                                  new DashStyle(new[] { 6.0, 4.0 }, 0));
            var typeface = new Typeface("Biome, monospace");
            foreach (double miles in RingMiles)
            {
                double r = miles * PxPerMile;
                ctx.DrawEllipse(null, ringPen, new Point(cx, cy), r, r);

                var label = new FormattedText($"{miles:0} mi", CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, typeface, 12 / _zoom,
                    new SolidColorBrush(Color.Parse("#9AA0B8")));
                ctx.DrawText(label, new Point(cx + r + 4 / _zoom, cy - 14 / _zoom));
            }

            //CABLES
            foreach(var cable in Blueprint.Cables)
            {
                var fromNode = Blueprint.Nodes.Find(n => n.Id == cable.FromNodeId);
                var toNode = Blueprint.Nodes.Find(n => n.Id == cable.ToNodeId);
                if (fromNode != null && toNode!= null)
                {
                    ctx.DrawLine(new Pen(Brushes.Gray, 2 / _zoom), new Point(fromNode.X, fromNode.Y), new Point(toNode.X, toNode.Y));
                }
            }
            // NODES
            if (Blueprint != null)
                foreach (var n in Blueprint.Nodes)
                {
                    IBrush backgroundBrush = n switch
                    {
                        GeneratorNode => new SolidColorBrush(Color.Parse("#FFC40C")),
                        SubstationNode => new SolidColorBrush(Color.Parse("#03C03C")),
                        ConsumerNode => new SolidColorBrush(Color.Parse("#B53389")),
                        _ => new SolidColorBrush(Color.Parse("#FFFFFF")),
                    };
                    ctx.DrawRectangle(
                        backgroundBrush,
                        new Pen(Brushes.White, 1),
                        new Rect(n.X - 10, n.Y - 10, 20, 20)
                    );
                    var icon = GetIcon(GetNodeIconName(n));
                    if (icon != null) ctx.DrawImage(icon, new Rect(n.X - 15, n.Y - 15, 30, 30));

                }
                if (SelectedNode != null)
                            ctx.DrawEllipse(null, new Pen(Brushes.Yellow, 2 / _zoom),
                                            new Point(SelectedNode.X, SelectedNode.Y), 22, 22);
        }
        
    }
    static readonly Dictionary<string, Bitmap?> IconCache = new();

    static Bitmap? GetIcon(string name)
    {
        if (IconCache.TryGetValue(name, out var cached)) return cached;
        var uri = new Uri($"avares://PowerGridSim/Assets/Images/{name}");
        var bmp = AssetLoader.Exists(uri) ? new Bitmap(AssetLoader.Open(uri)) : null;
        return IconCache[name] = bmp;
    }
    private string GetNodeIconName(MapNode node)
    {
        return node switch
        {
            GeneratorNode genNode => genNode.GeneratorType switch
            {
                GeneratorType.PowerPlant => "PowerPlant-icon.png",
                GeneratorType.AlternativeGeneration => "Solar-icon.png",
                GeneratorType.HVDC_Link => "HVDC-icon.png",
                _ => "PowerPlant-icon.png"
            },

            SubstationNode subNode => subNode.SubstationType switch
            {
                SubstationType.TransmissionSubstation => "TransmissionSS-icon.png",
                SubstationType.GridSupplyPoint => "GridSupplyPoint-icon.png",
                SubstationType.DistributionSubstation => "DistributionSS-icon.png",
                _ => "DistributionSS-icon.png"
            },

            ConsumerNode conNode => conNode.ConsumerType switch
            {
                ConsumerType.Industrial => "Industrial-icon.png",
                ConsumerType.Commercial => "Commercial-icon.png",
                ConsumerType.Residential => "Residential-icon.png",
                _ => "Residential-icon.png"
            }

        };
    }
}
