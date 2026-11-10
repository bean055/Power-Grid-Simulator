using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PowerGridSim.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using static PowerGridSim.Models.Record;

namespace PowerGridSim.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public const double SecondsPerHour = 3.0;

    private readonly DispatcherTimer _timer;
    private long _lastTickTimestamp;

    [ObservableProperty]
    private PowerGridBlueprint? _blueprint;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClockText))]
    private SimulationTimeline? _timeline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnalyticsMode))]
    private bool _isLiveMapMode = true;
    public bool IsAnalyticsMode => !IsLiveMapMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(SelectedTypeText))]
    private MapNode? _selectedNode;
    [ObservableProperty]
    private NodeState? _selectedState;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClockText))]
    private int _currentHour = 0;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ClockText))]
    private double _secondsIntoHour;

 


    public string ClockText
    {
        get
        {
            if (Timeline == null || Timeline.Frames.Count == 0) return "--";
            int h = Math.Clamp(CurrentHour, 0, Timeline.Frames.Count - 1);
            return $"{Timeline.Frames[h].TimeLabel}   |   {SecondsIntoHour:F1}s / {SecondsPerHour:F1}s";
        }
    }

    public bool HasSelection => SelectedNode != null;
    public string SelectedTypeText => SelectedNode switch
    {
        GeneratorNode g => $"Generator: {g.GeneratorType}",
        SubstationNode s => $"Substation: {s.SubstationType}",
        ConsumerNode c => $"Consumer: {c.ConsumerType}",
        _ => ""
    };

    public MainViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnTimerTick;

        Blueprint = XmlMapLoader.Load(MapPaths.LoadPath);

        if (Blueprint != null)
        {
            var imp = LineParameters.Calculate(Blueprint);
            for (int i = 0; i < imp.Length; i++)
                Debug.WriteLine(
                    $"{Blueprint.Cables[i].Id}: {imp[i].LengthKm:F1} km | R={imp[i].RPu:F4} pu | X={imp[i].XPu:F4} pu");

            RunSimulation();
        }
    }
    void RunSimulation()
    {
        if (Blueprint == null) return;

        var events = new List<SimEvent> { new(30, SimEventType.BreakCable, "Cable1") };//to satifsy run
        Timeline = Simulator.Run(Blueprint, events, 168);

        var f = Timeline.Frames[0];
        Debug.WriteLine(
            $"{Blueprint.TownName}: {Blueprint.Nodes.Count} nodes, {Blueprint.Cables.Count} cables");
    }

    [RelayCommand]
    private void SetMode(string mode)
    {
        IsLiveMapMode = mode == "LiveMap";
    }
    

    [RelayCommand]
    public void SaveMap()
    {
        if (Blueprint is null) return;
        XmlMapLoader.Save(Blueprint, MapPaths.UserMap);
    }
    [RelayCommand]
    public void ResetMap()
    {
        Pause();
        SelectedNode = null;         

        try { Blueprint = XmlMapLoader.Load(MapPaths.LoadPath); }
        catch (Exception ex)
        {
            Debug.WriteLine($"load failed: {ex.Message}");
            Blueprint = XmlMapLoader.Load(MapPaths.DefaultMap);
        }

        CurrentHour = 0;
        SecondsIntoHour = 0;
        RunSimulation();             
    }

    // Time 

    [RelayCommand]
    public void TogglePlay()
    {
        if (IsPlaying) { Pause(); return; }
        if (Timeline == null || Timeline.Frames.Count == 0) return;

        if (CurrentHour >= Timeline.Frames.Count - 1)
        {
            CurrentHour = 0;
            SecondsIntoHour = 0;
        }
        Play();
    }

    [RelayCommand]
    public void StepForward()
    {
        Pause();
        if (Timeline == null) return;
        CurrentHour = Math.Min(CurrentHour + 1, Timeline.Frames.Count - 1);
        SecondsIntoHour = 0;
    }

    [RelayCommand]
    public void StepBack()
    {
        Pause();
        CurrentHour = Math.Max(CurrentHour - 1, 0);
        SecondsIntoHour = 0;
    }

    public void Play()
    {
        _lastTickTimestamp = Stopwatch.GetTimestamp();
        IsPlaying = true;
        _timer.Start();
    }

    public void Pause()
    {
        _timer.Stop();
        IsPlaying = false;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (Timeline == null || Timeline.Frames.Count == 0) { Pause(); return; }

        double dt = Stopwatch.GetElapsedTime(_lastTickTimestamp).TotalSeconds;
        _lastTickTimestamp = Stopwatch.GetTimestamp();

        double t = SecondsIntoHour + dt;
        int last = Timeline.Frames.Count - 1;

        while (t >= SecondsPerHour)
        {
            t -= SecondsPerHour;
            if (CurrentHour >= last)
            {
                SecondsIntoHour = 0;
                Pause();
                return;
            }
            CurrentHour++;
        }
        SecondsIntoHour = t;
    }

    partial void OnSelectedNodeChanged(MapNode? value) => RefreshSelectedState();
    partial void OnCurrentHourChanged(int value) => RefreshSelectedState();
    partial void OnTimelineChanged(SimulationTimeline? value) => RefreshSelectedState();

    void RefreshSelectedState()
    {
        SelectedState = null;
        if (SelectedNode == null || Timeline == null || Timeline.Frames.Count == 0) return;
        int h = Math.Clamp(CurrentHour, 0, Timeline.Frames.Count - 1);
        if (Timeline.Frames[h].NodeStates.TryGetValue(SelectedNode.Id, out var s))
            SelectedState = s;
    }
}